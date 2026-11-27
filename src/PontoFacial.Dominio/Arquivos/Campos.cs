using System.Globalization;
using System.Text;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Arquivos;

/// <summary>
/// Como os campos dos arquivos da Portaria são escritos: o texto em ISO
/// 8859-1, os números com zeros à esquerda, o texto alinhado à esquerda com
/// espaços, e cada linha terminada por CR e LF (ADR-0015).
/// </summary>
internal static class Campos
{
    /// <summary>A codificação dos dois arquivos: ISO 8859-1. O que não cabe nela vira "?".</summary>
    public static readonly Encoding Latin1 = Encoding.Latin1;

    /// <summary>O texto que o leiaute manda pôr na última linha: a assinatura fica no arquivo .p7s ao lado.</summary>
    public const string AssinaturaEmArquivoP7s = "ASSINATURA_DIGITAL_EM_ARQUIVO_P7S";

    /// <summary>
    /// O texto alinhado à esquerda, completado com espaços. Texto maior que o
    /// campo é defeito de quem chamou: o domínio já limita cada nome ao campo
    /// do arquivo.
    /// </summary>
    public static string Alfa(string? texto, int tamanho)
    {
        var limpo = Seguro(texto ?? string.Empty);

        return limpo.Length <= tamanho
            ? limpo.PadRight(tamanho)
            : throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"\"{limpo}\" não cabe nos {tamanho} caracteres do campo."));
    }

    /// <summary>Os dígitos com zeros à esquerda; o campo vazio, sem valor, fica com espaços.</summary>
    public static string Numero(string? digitos, int tamanho)
    {
        if (string.IsNullOrEmpty(digitos))
        {
            return new string(' ', tamanho);
        }

        return digitos.Length <= tamanho && digitos.All(char.IsAsciiDigit)
            ? digitos.PadLeft(tamanho, '0')
            : throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"\"{digitos}\" não é um número de até {tamanho} dígitos."));
    }

    /// <summary>"1" para o CNPJ, "2" para o CPF.</summary>
    public static string TipoDoDocumento(Documento documento) => documento is Cnpj ? "1" : "2";

    /// <summary>
    /// O texto sem o que quebraria o arquivo: as quebras de linha e o "|", que
    /// separa os campos do AEJ, viram espaço.
    /// </summary>
    public static string Seguro(string texto) =>
        string.Create(texto.Length, texto, (destino, origem) =>
        {
            for (var i = 0; i < origem.Length; i++)
            {
                destino[i] = origem[i] is '\r' or '\n' or '|' ? ' ' : origem[i];
            }
        });

    public static string Data(DateOnly data) => data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>As linhas em ISO 8859-1, cada uma terminada por CR e LF, inclusive a última.</summary>
    public static byte[] Conteudo(IEnumerable<string> linhas)
    {
        var texto = new StringBuilder();

        foreach (var linha in linhas)
        {
            texto.Append(linha).Append("\r\n");
        }

        return Latin1.GetBytes(texto.ToString());
    }
}
