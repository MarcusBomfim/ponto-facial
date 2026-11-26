using System.Globalization;

namespace PontoFacial.Dominio.Jornada;

/// <summary>Durações como o espelho de ponto as escreve: horas e minutos, as horas podendo passar de 24.</summary>
public static class Duracoes
{
    /// <summary>"08:00", "45:30", "-00:58".</summary>
    public static string Formatar(TimeSpan duracao)
    {
        var minutos = (long)Math.Round(Math.Abs(duracao.TotalMinutes), MidpointRounding.AwayFromZero);
        var sinal = duracao < TimeSpan.Zero && minutos > 0 ? "-" : string.Empty;

        return string.Create(CultureInfo.InvariantCulture, $"{sinal}{minutos / 60:00}:{minutos % 60:00}");
    }
}
