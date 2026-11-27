using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using PontoFacial.Dominio.Registro;

namespace PontoFacial.Assinaturas;

/// <summary>
/// O Comprovante de Registro de Ponto do Trabalhador em PDF, assinado com
/// PAdES (art. 80, parágrafo único, I, e art. 88, § 1º, da Portaria 671).
/// </summary>
/// <remarks>
/// <para>
/// O PDF tem uma página, com as linhas do comprovante do art. 79 em
/// Helvetica, e um campo de assinatura invisível. A assinatura cobre o
/// arquivo inteiro, menos o espaço reservado para ela mesma: é o que o
/// <c>/ByteRange</c> diz, e é como qualquer leitor de PDF a confere.
/// </para>
/// <para>
/// O arquivo é montado em duas passadas. Na primeira, a assinatura é um
/// espaço reservado de zeros, de tamanho fixo, e as posições de cada objeto
/// ficam conhecidas. Na segunda, o espaço recebe a assinatura CAdES dos bytes
/// de fora dele. Como nada muda de tamanho, a tabela de referências da
/// primeira passada continua certa (ADR-0016).
/// </para>
/// </remarks>
public static class ComprovanteEmPdf
{
    /// <summary>Os bytes reservados para a assinatura: o certificado e a cadeia cabem com folga.</summary>
    internal const int EspacoDaAssinatura = 24 * 1024;

    /// <summary>A largura fixa dos números do /ByteRange, para o arquivo não mudar de tamanho ao preenchê-los.</summary>
    private const int LarguraDoNumero = 10;

    private static readonly Encoding WinAnsi = Encoding.Latin1;

    /// <summary>O comprovante em PDF, assinado pelo certificado do REP-P.</summary>
    public static byte[] Assinado(ComprovanteDeRegistro comprovante, X509Certificate2 certificado, DateTimeOffset em, PoliticaDeAssinatura? politica = null)
    {
        ArgumentNullException.ThrowIfNull(comprovante);
        ArgumentNullException.ThrowIfNull(certificado);

        var pdf = Montar(comprovante.Linhas(), em);
        var conteudo = Encoding.Latin1.GetString(pdf);
        var inicio = conteudo.IndexOf("/Contents <", StringComparison.Ordinal) + "/Contents ".Length;
        var fim = inicio + 2 + (2 * EspacoDaAssinatura);

        // O /ByteRange: tudo antes do "<" e tudo depois do ">" da assinatura.
        var faixa = string.Join(' ', new[] { 0L, inicio, fim, pdf.Length - fim }.Select(numero => numero.ToString(CultureInfo.InvariantCulture).PadLeft(LarguraDoNumero, '0')));
        var marcador = conteudo.IndexOf(Faixa(PreencherDepois), StringComparison.Ordinal);
        WinAnsi.GetBytes(Faixa(faixa)).CopyTo(pdf, marcador);

        var assinado = new byte[inicio + (pdf.Length - fim)];
        pdf.AsSpan(0, inicio).CopyTo(assinado);
        pdf.AsSpan(fim).CopyTo(assinado.AsSpan(inicio));

        var cms = AssinaturaCades.Assinar(assinado, certificado, em, politica, comHoraNosAtributos: false);

        if (cms.Length > EspacoDaAssinatura)
        {
            throw new InvalidOperationException($"A assinatura tem {cms.Length} bytes e o espaço reservado no PDF, {EspacoDaAssinatura}.");
        }

        WinAnsi.GetBytes(Convert.ToHexString(cms)).CopyTo(pdf, inicio + 1);

        return pdf;
    }

    /// <summary>O marcador do /ByteRange antes de preenchido: quatro números de dez dígitos.</summary>
    private static readonly string PreencherDepois = string.Join(' ', Enumerable.Repeat(new string('0', LarguraDoNumero), 4));

    private static string Faixa(string numeros) => $"/ByteRange [{numeros}]";

    /// <summary>O PDF com o espaço da assinatura cheio de zeros e o /ByteRange por preencher.</summary>
    private static byte[] Montar(IReadOnlyList<string> linhas, DateTimeOffset em)
    {
        var texto = new StringBuilder();
        texto.Append("BT\n/F2 13 Tf\n50 790 Td\n18 TL\n");
        texto.Append('(').Append(Escapar(linhas[0])).Append(") Tj\n/F1 11 Tf\n");

        foreach (var linha in linhas.Skip(1))
        {
            texto.Append("T* (").Append(Escapar(linha)).Append(") Tj\n");
        }

        texto.Append("ET\n");
        var pagina = WinAnsi.GetBytes(texto.ToString());
        var momento = string.Create(
            CultureInfo.InvariantCulture,
            $"D:{em:yyyyMMddHHmmss}{(em.Offset < TimeSpan.Zero ? '-' : '+')}{em.Offset.Duration():hh}'{em.Offset.Duration():mm}'");

        string[] objetos =
        [
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] /SigFlags 3 >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R /F2 8 0 R >> >> /Contents 6 0 R /Annots [5 0 R] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            "<< /Type /Annot /Subtype /Widget /FT /Sig /Rect [0 0 0 0] /F 132 /T (Assinatura do REP-P) /V 7 0 R /P 3 0 R >>",
            string.Create(CultureInfo.InvariantCulture, $"<< /Length {pagina.Length} >>\nstream\n") + WinAnsi.GetString(pagina) + "\nendstream",
            $"<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /ETSI.CAdES.detached {Faixa(PreencherDepois)} /Contents <{new string('0', 2 * EspacoDaAssinatura)}> /M ({momento}) >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>",
        ];

        var pdf = new StringBuilder("%PDF-1.7\n%âãÏÓ\n");
        var posicoes = new List<int>();

        for (var i = 0; i < objetos.Length; i++)
        {
            posicoes.Add(WinAnsi.GetByteCount(pdf.ToString()));
            pdf.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objetos[i]}\nendobj\n");
        }

        var xref = WinAnsi.GetByteCount(pdf.ToString());
        pdf.Append(CultureInfo.InvariantCulture, $"xref\n0 {objetos.Length + 1}\n0000000000 65535 f \n");

        foreach (var posicao in posicoes)
        {
            pdf.Append(CultureInfo.InvariantCulture, $"{posicao:D10} 00000 n \n");
        }

        pdf.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objetos.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");

        return WinAnsi.GetBytes(pdf.ToString());
    }

    /// <summary>O texto dentro de um literal do PDF: a barra invertida e os parênteses vão escapados.</summary>
    private static string Escapar(string texto) => texto.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
}
