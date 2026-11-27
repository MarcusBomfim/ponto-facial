using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.RegularExpressions;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes;
using PontoFacial.Dominio.Testes.Registro;

namespace PontoFacial.Assinaturas.Testes;

/*
 * O comprovante em PDF assinado com PAdES: o arquivo é um PDF válido, de uma
 * página, com as linhas do art. 79, e a assinatura cobre tudo menos o espaço
 * dela — conferida pelo .NET e pelo openssl, como um leitor de PDF a confere.
 */
public sealed partial class ComprovanteEmPdfTests
{
    private static readonly DateTimeOffset Em = new(2026, 11, 23, 17, 2, 5, TimeSpan.FromHours(-3));

    [Fact]
    public void O_pdf_tem_as_linhas_do_comprovante_em_winansi()
    {
        var comprovante = Comprovante();
        var texto = Encoding.Latin1.GetString(ComprovanteEmPdf.Assinado(comprovante, Certificados.DoDesenvolvedor, Em));

        Assert.StartsWith("%PDF-1.7\n", texto, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", texto, StringComparison.Ordinal);

        foreach (var linha in comprovante.Linhas())
        {
            Assert.Contains($"({linha.Replace("(", @"\(", StringComparison.Ordinal).Replace(")", @"\)", StringComparison.Ordinal)}) Tj", texto, StringComparison.Ordinal);
        }

        Assert.Contains("(Trabalhador: João da Silva) Tj", texto, StringComparison.Ordinal);
    }

    [Fact]
    public void A_tabela_de_referencias_aponta_para_cada_objeto()
    {
        var pdf = ComprovanteEmPdf.Assinado(Comprovante(), Certificados.DoDesenvolvedor, Em);
        var texto = Encoding.Latin1.GetString(pdf);
        var inicioDaTabela = int.Parse(Regex.Match(texto, @"startxref\n(\d+)\n%%EOF").Groups[1].Value, CultureInfo.InvariantCulture);
        var entradas = Regex.Matches(texto[inicioDaTabela..], @"(\d{10}) 00000 n ").Select(entrada => int.Parse(entrada.Groups[1].Value, CultureInfo.InvariantCulture)).ToList();

        Assert.StartsWith("xref\n0 9\n", texto[inicioDaTabela..], StringComparison.Ordinal);
        Assert.Equal(8, entradas.Count);

        for (var i = 0; i < entradas.Count; i++)
        {
            Assert.StartsWith($"{i + 1} 0 obj\n", texto[entradas[i]..], StringComparison.Ordinal);
        }
    }

    [Fact]
    public void A_assinatura_cobre_o_arquivo_inteiro_menos_o_espaco_dela_e_confere()
    {
        var pdf = ComprovanteEmPdf.Assinado(Comprovante(), Certificados.DoDesenvolvedor, Em);
        var (assinado, cms, faixa) = Separar(pdf);

        Assert.Equal(0, faixa[0]);
        Assert.Equal(pdf.Length, faixa[2] + faixa[3]);
        Assert.Equal('<', (char)pdf[faixa[1]]);
        Assert.Equal('>', (char)pdf[faixa[2] - 1]);

        var assinatura = new SignedCms(new ContentInfo(assinado), detached: true);
        assinatura.Decode(cms);
        assinatura.CheckSignature(verifySignatureOnly: true);
        Assert.Equal(Certificados.DoDesenvolvedor.Thumbprint, assinatura.SignerInfos[0].Certificate!.Thumbprint);
    }

    [Fact]
    public void E_pades_a_hora_vai_no_dicionario_e_nao_nos_atributos()
    {
        var pdf = ComprovanteEmPdf.Assinado(Comprovante(), Certificados.DoDesenvolvedor, Em);
        var texto = Encoding.Latin1.GetString(pdf);
        var (_, cms, _) = Separar(pdf);
        var assinatura = new SignedCms();
        assinatura.Decode(cms);
        var atributos = assinatura.SignerInfos[0].SignedAttributes.OfType<CryptographicAttributeObject>().Select(atributo => atributo.Oid.Value).ToList();

        Assert.Contains("/SubFilter /ETSI.CAdES.detached", texto, StringComparison.Ordinal);
        Assert.Contains("/M (D:20261123170205-03'00')", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("1.2.840.113549.1.9.5", atributos);
        Assert.Contains("1.2.840.113549.1.9.16.2.47", atributos);
    }

    [Fact]
    public void Um_caractere_trocado_no_texto_e_a_assinatura_nao_confere()
    {
        var pdf = ComprovanteEmPdf.Assinado(Comprovante(), Certificados.DoDesenvolvedor, Em);
        var texto = Encoding.Latin1.GetString(pdf);
        pdf[texto.IndexOf("17:02", StringComparison.Ordinal) + 1] = (byte)'6';
        var (assinado, cms, _) = Separar(pdf);

        var assinatura = new SignedCms(new ContentInfo(assinado), detached: true);
        assinatura.Decode(cms);

        Assert.ThrowsAny<CryptographicException>(() => assinatura.CheckSignature(verifySignatureOnly: true));
    }

    [Fact]
    public void O_openssl_confere_a_assinatura_do_pdf()
    {
        var (assinado, cms, _) = Separar(ComprovanteEmPdf.Assinado(Comprovante(), Certificados.DoDesenvolvedor, Em));

        var (saida, mensagem) = OpenSsl.ConferirCms(assinado, cms);

        Assert.True(saida == 0, mensagem);
    }

    [Fact]
    public void O_texto_com_parenteses_e_barra_vai_escapado()
    {
        var obra = PontoFacial.Dominio.Cadastro.Estabelecimento.Novo(Exemplos.Construtora, @"Galpão (fundos) \ Bloco B - Santos/SP", "America/Sao_Paulo");
        var registro = RegistroDePonto.Abrir(obra, RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 1));
        var marcacao = registro.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 7, 58)), Coletor.DispositivoEletronico);

        var texto = Encoding.Latin1.GetString(ComprovanteEmPdf.Assinado(registro.Comprovante(marcacao.Nsr, RegistroDeExemplo.RegistroNoInpi), Certificados.DoDesenvolvedor, Em));

        Assert.Contains(@"(Local: Galpão \(fundos\) \\ Bloco B - Santos/SP) Tj", texto, StringComparison.Ordinal);
    }

    /// <summary>O comprovante da saída do João, às 17h02 de 23/11, no NSR 5.</summary>
    private static ComprovanteDeRegistro Comprovante()
    {
        var registro = RegistroDeExemplo.ComJoaoEMaria();
        registro.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 7, 58)), Coletor.DispositivoEletronico);
        var saida = registro.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 17, 2)), Coletor.DispositivoEletronico);

        return registro.Comprovante(saida.Nsr, RegistroDeExemplo.RegistroNoInpi);
    }

    /// <summary>Os bytes que a assinatura cobre, a assinatura e o /ByteRange, como um leitor de PDF os separa.</summary>
    private static (byte[] Assinado, byte[] Cms, int[] Faixa) Separar(byte[] pdf)
    {
        var texto = Encoding.Latin1.GetString(pdf);
        var faixa = FaixaDaAssinatura().Match(texto).Groups.Values.Skip(1).Select(grupo => int.Parse(grupo.Value, CultureInfo.InvariantCulture)).ToArray();
        var assinado = pdf[faixa[0]..(faixa[0] + faixa[1])].Concat(pdf[faixa[2]..(faixa[2] + faixa[3])]).ToArray();

        // O espaço da assinatura: o CMS em hexadecimal, completado com zeros.
        return (assinado, Recortar(Convert.FromHexString(texto[(faixa[1] + 1)..(faixa[2] - 1)])), faixa);
    }

    /// <summary>O CMS sem os zeros que sobraram do espaço: ele mesmo diz o tamanho no começo do DER.</summary>
    private static byte[] Recortar(byte[] der)
    {
        var leitor = new System.Formats.Asn1.AsnReader(der, System.Formats.Asn1.AsnEncodingRules.BER, new System.Formats.Asn1.AsnReaderOptions());

        return leitor.ReadEncodedValue().ToArray();
    }

    [GeneratedRegex(@"/ByteRange \[(\d+) (\d+) (\d+) (\d+)\]")]
    private static partial Regex FaixaDaAssinatura();
}
