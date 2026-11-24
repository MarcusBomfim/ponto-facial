using System.Globalization;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Registro;

/// <summary>
/// Os campos como o leiaute do Arquivo Fonte de Dados os escreve. O hash da
/// marcação é calculado sobre eles, e não sobre os valores: dois sistemas que
/// escrevam a mesma marcação do mesmo jeito chegam ao mesmo hash.
/// </summary>
internal static class CamposDoAfd
{
    /// <summary>O NSR tem 9 posições no arquivo.</summary>
    public const long MaiorNsr = 999_999_999;

    public static string Nsr(long nsr) => nsr.ToString("D9", CultureInfo.InvariantCulture);

    /// <summary>
    /// <c>AAAA-MM-ddThh:mm:00ZZZZZ</c>: os segundos são sempre 00, e o fuso vem
    /// com sinal e quatro dígitos, sem dois-pontos — <c>2026-11-23T07:58:00-0300</c>.
    /// </summary>
    public static string DataHora(DateTimeOffset instante)
    {
        var sinal = instante.Offset < TimeSpan.Zero ? '-' : '+';
        var fuso = instante.Offset.Duration();

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{instante:yyyy-MM-dd}T{instante:HH:mm}:00{sinal}{fuso.Hours:D2}{fuso.Minutes:D2}");
    }

    /// <summary>O CPF ocupa 12 posições numéricas: os 11 dígitos com um zero à esquerda.</summary>
    public static string Cpf(Cpf cpf) => cpf.Numero.PadLeft(12, '0');

    public static string Coletor(Coletor coletor) => ((int)coletor).ToString("D2", CultureInfo.InvariantCulture);

    /// <summary>"0" para a marcação on-line, "1" para a off-line.</summary>
    public static string OnLine(bool online) => online ? "0" : "1";
}
