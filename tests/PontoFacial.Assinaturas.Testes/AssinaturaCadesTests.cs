using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;

namespace PontoFacial.Assinaturas.Testes;

/*
 * O .p7s que vai ao lado do AFD e do AEJ: CMS destacado com os atributos do
 * CAdES, conferido pelo .NET e pelo openssl.
 */
public sealed class AssinaturaCadesTests
{
    private static readonly byte[] Afd = Encoding.Latin1.GetBytes("0000000001...\r\n999999999...\r\nASSINATURA_DIGITAL_EM_ARQUIVO_P7S\r\n");

    private static readonly DateTimeOffset Em = new(2026, 11, 23, 18, 0, 0, TimeSpan.FromHours(-3));

    [Fact]
    public void A_assinatura_destacada_confere_com_o_conteudo_e_traz_quem_assinou()
    {
        var p7s = AssinaturaCades.Destacada(Afd, Certificados.DoDesenvolvedor, Em);

        var assinante = AssinaturaCades.Conferir(Afd, p7s);

        Assert.Equal(Certificados.DoDesenvolvedor.Thumbprint, assinante.Thumbprint);
    }

    [Fact]
    public void O_p7s_nao_leva_o_conteudo_dentro()
    {
        var p7s = AssinaturaCades.Destacada(Afd, Certificados.DoDesenvolvedor, Em);
        var cms = new SignedCms();
        cms.Decode(p7s);

        Assert.Empty(cms.ContentInfo.Content);
        Assert.Equal(-1, p7s.AsSpan().IndexOf(Afd.AsSpan(0, 20)));
    }

    [Fact]
    public void Um_byte_trocado_no_arquivo_e_a_assinatura_nao_confere()
    {
        var p7s = AssinaturaCades.Destacada(Afd, Certificados.DoDesenvolvedor, Em);
        var mexido = (byte[])Afd.Clone();
        mexido[5] ^= 0x01;

        Assert.ThrowsAny<CryptographicException>(() => AssinaturaCades.Conferir(mexido, p7s));
    }

    [Fact]
    public void Os_atributos_do_cades_vao_assinados_com_o_hash_do_certificado()
    {
        var cms = new SignedCms(new ContentInfo(Afd), detached: true);
        cms.Decode(AssinaturaCades.Destacada(Afd, Certificados.DoDesenvolvedor, Em));
        var assinante = cms.SignerInfos[0];
        var atributos = assinante.SignedAttributes.OfType<CryptographicAttributeObject>().ToDictionary(atributo => atributo.Oid.Value!);

        Assert.Equal("2.16.840.1.101.3.4.2.1", assinante.DigestAlgorithm.Value);
        Assert.Contains("1.2.840.113549.1.9.3", atributos.Keys); // content-type
        Assert.Contains("1.2.840.113549.1.9.4", atributos.Keys); // message-digest
        Assert.Equal(Em.UtcDateTime, new Pkcs9SigningTime(atributos["1.2.840.113549.1.9.5"].Values[0].RawData).SigningTime);

        // signing-certificate-v2: o SHA-256 do certificado, o emissor e o número de série.
        var leitor = new AsnReader(atributos["1.2.840.113549.1.9.16.2.47"].Values[0].RawData, AsnEncodingRules.DER);
        var essCertId = leitor.ReadSequence().ReadSequence().ReadSequence();
        Assert.Equal(SHA256.HashData(Certificados.DoDesenvolvedor.RawData), essCertId.ReadOctetString());
        var emissorESerie = essCertId.ReadSequence();
        emissorESerie.ReadSequence();
        Assert.Equal(Certificados.DoDesenvolvedor.SerialNumberBytes.ToArray(), emissorESerie.ReadIntegerBytes().ToArray());
    }

    [Fact]
    public void A_politica_de_assinatura_configurada_vai_nos_atributos()
    {
        var politica = new PoliticaDeAssinatura("2.16.76.1.7.1.1.2.3", "2.16.840.1.101.3.4.2.1", SHA256.HashData("documento da política"u8), new Uri("http://politicas.exemplo/pa_ad_rb.der"));
        var cms = new SignedCms(new ContentInfo(Afd), detached: true);
        cms.Decode(AssinaturaCades.Destacada(Afd, Certificados.DoDesenvolvedor, Em, politica));

        var atributo = cms.SignerInfos[0].SignedAttributes.OfType<CryptographicAttributeObject>().Single(atributo => atributo.Oid.Value == "1.2.840.113549.1.9.16.2.15");
        var leitor = new AsnReader(atributo.Values[0].RawData, AsnEncodingRules.DER).ReadSequence();

        Assert.Equal("2.16.76.1.7.1.1.2.3", leitor.ReadObjectIdentifier());
        var hash = leitor.ReadSequence();
        Assert.Equal("2.16.840.1.101.3.4.2.1", hash.ReadSequence().ReadObjectIdentifier());
        Assert.Equal(politica.Hash.ToArray(), hash.ReadOctetString());
        var qualificador = leitor.ReadSequence().ReadSequence();
        Assert.Equal("1.2.840.113549.1.9.16.5.1", qualificador.ReadObjectIdentifier());
        Assert.Equal("http://politicas.exemplo/pa_ad_rb.der", qualificador.ReadCharacterString(UniversalTagNumber.IA5String));
        cms.CheckSignature(verifySignatureOnly: true);
    }

    [Fact]
    public void O_openssl_confere_a_assinatura_e_recusa_o_arquivo_mexido()
    {
        var p7s = AssinaturaCades.Destacada(Afd, Certificados.DoDesenvolvedor, Em);
        var mexido = (byte[])Afd.Clone();
        mexido[^5] ^= 0x01;

        var (certo, mensagem) = OpenSsl.ConferirCms(Afd, p7s);
        var (errado, _) = OpenSsl.ConferirCms(mexido, p7s);

        Assert.True(certo == 0, mensagem);
        Assert.NotEqual(0, errado);
    }

    [Fact]
    public void O_certificado_sem_a_chave_privada_nao_assina()
    {
        using var semChave = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(Certificados.DoDesenvolvedor.RawData);

        Assert.Throws<ArgumentException>(() => AssinaturaCades.Destacada(Afd, semChave, Em));
    }
}
