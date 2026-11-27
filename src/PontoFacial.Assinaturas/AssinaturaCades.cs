using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace PontoFacial.Assinaturas;

/// <summary>
/// A assinatura CAdES destacada, que a Portaria 671 manda guardar num
/// arquivo .p7s ao lado do AFD e do AEJ (art. 88, § 2º).
/// </summary>
/// <remarks>
/// <para>
/// É uma estrutura CMS SignedData sem o conteúdo dentro, com SHA-256 e os
/// atributos assinados do CAdES: o tipo do conteúdo, o hash dele, a hora da
/// assinatura e o certificado de quem assina, pelo hash. Com a política de
/// assinatura configurada, ela vai junto (ADR-0016).
/// </para>
/// <para>
/// O certificado tem de ser ICP-Brasil, da empresa ou do desenvolvedor, e ter
/// a chave privada (art. 88). Quem confere a cadeia até a raiz da ICP-Brasil é
/// quem recebe o arquivo; aqui se confere a assinatura.
/// </para>
/// </remarks>
public static class AssinaturaCades
{
    /// <summary>O SHA-256.</summary>
    internal const string Sha256 = "2.16.840.1.101.3.4.2.1";

    /// <summary>O .p7s do conteúdo: a assinatura destacada, em DER.</summary>
    public static byte[] Destacada(ReadOnlyMemory<byte> conteudo, X509Certificate2 certificado, DateTimeOffset em, PoliticaDeAssinatura? politica = null) =>
        Assinar(conteudo, certificado, em, politica, comHoraNosAtributos: true);

    /// <summary>
    /// Confere a assinatura destacada contra o conteúdo e devolve o
    /// certificado de quem assinou. A assinatura que não confere lança
    /// <see cref="CryptographicException"/>.
    /// </summary>
    public static X509Certificate2 Conferir(ReadOnlyMemory<byte> conteudo, ReadOnlySpan<byte> p7s)
    {
        var cms = new SignedCms(new ContentInfo(conteudo.ToArray()), detached: true);
        cms.Decode(p7s);
        cms.CheckSignature(verifySignatureOnly: true);

        var assinante = cms.SignerInfos[0];

        if (assinante.SignedAttributes.OfType<CryptographicAttributeObject>().All(atributo => atributo.Oid.Value != AtributosDaAssinatura.CertificadoDeQuemAssina))
        {
            throw new CryptographicException("A assinatura não tem o atributo signing-certificate-v2: não é CAdES.");
        }

        return assinante.Certificate ?? throw new CryptographicException("A assinatura não traz o certificado de quem assinou.");
    }

    /// <param name="comHoraNosAtributos">
    /// No CAdES, a hora vai como atributo assinado; no PAdES, não vai: ela
    /// fica no dicionário da assinatura do PDF (ETSI EN 319 142-1).
    /// </param>
    internal static byte[] Assinar(ReadOnlyMemory<byte> conteudo, X509Certificate2 certificado, DateTimeOffset em, PoliticaDeAssinatura? politica, bool comHoraNosAtributos)
    {
        ArgumentNullException.ThrowIfNull(certificado);

        if (!certificado.HasPrivateKey)
        {
            throw new ArgumentException("O certificado de assinatura não tem a chave privada.", nameof(certificado));
        }

        var cms = new SignedCms(new ContentInfo(conteudo.ToArray()), detached: true);
        var assinante = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificado)
        {
            DigestAlgorithm = new Oid(Sha256),
            IncludeOption = X509IncludeOption.EndCertOnly,
        };

        if (comHoraNosAtributos)
        {
            assinante.SignedAttributes.Add(new Pkcs9SigningTime(em.UtcDateTime));
        }

        assinante.SignedAttributes.Add(AtributosDaAssinatura.Certificado(certificado));

        if (politica is not null)
        {
            assinante.SignedAttributes.Add(AtributosDaAssinatura.Politica(politica));
        }

        cms.ComputeSignature(assinante, silent: true);

        return cms.Encode();
    }
}
