using PontoFacial.Dominio.Jornada;

namespace PontoFacial.Dominio.Testes.Jornada;

/// <summary>Os horários de teste e as marcações na hora da obra, em novembro de 2026.</summary>
internal static class Horarios
{
    public static readonly TimeZoneInfo Fuso = Exemplos.Obra.Fuso;

    /// <summary>Segunda a sexta das 8h às 12h e das 13h às 17h; sábado das 8h às 12h: 44 horas, descanso no domingo.</summary>
    public static readonly HorarioContratual DaObra = HorarioContratual.De(Semana(true));

    /// <summary>O mesmo horário, com o intervalo pré-assinalado.</summary>
    public static readonly HorarioContratual DaObraComIntervaloPreAssinalado = HorarioContratual.De(Semana(true), intervaloPreAssinalado: true);

    /// <summary>
    /// Segunda a sexta das 22h às 2h e das 3h às 5h, com a virada ao meio-dia:
    /// 6 horas de relógio, todas noturnas, que valem 6:51.
    /// </summary>
    public static readonly HorarioContratual Noturno = HorarioContratual.De(
        new Dictionary<DayOfWeek, IReadOnlyList<Periodo>>
        {
            [DayOfWeek.Monday] = Turno,
            [DayOfWeek.Tuesday] = Turno,
            [DayOfWeek.Wednesday] = Turno,
            [DayOfWeek.Thursday] = Turno,
            [DayOfWeek.Friday] = Turno,
        },
        viradaDoDia: new TimeOnly(12, 0));

    private static IReadOnlyList<Periodo> Turno => [new(new(22, 0), new(2, 0)), new(new(3, 0), new(5, 0))];

    public static Dictionary<DayOfWeek, IReadOnlyList<Periodo>> Semana(bool comSabado)
    {
        IReadOnlyList<Periodo> util = [new(new(8, 0), new(12, 0)), new(new(13, 0), new(17, 0))];
        var semana = new Dictionary<DayOfWeek, IReadOnlyList<Periodo>>
        {
            [DayOfWeek.Monday] = util,
            [DayOfWeek.Tuesday] = util,
            [DayOfWeek.Wednesday] = util,
            [DayOfWeek.Thursday] = util,
            [DayOfWeek.Friday] = util,
        };

        if (comSabado)
        {
            semana[DayOfWeek.Saturday] = [new(new(8, 0), new(12, 0))];
        }

        return semana;
    }

    /// <summary>Uma marcação em novembro de 2026, na hora de Brasília: "07:58" no dia 23.</summary>
    public static DateTimeOffset Em(int dia, string hora) =>
        Exemplos.Em(dia, int.Parse(hora[..2], System.Globalization.CultureInfo.InvariantCulture), int.Parse(hora[3..], System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>As marcações do dia: Marcacoes(23, "07:58", "12:01").</summary>
    public static DateTimeOffset[] Marcacoes(int dia, params string[] horas) => [.. horas.Select(hora => Em(dia, hora))];

    public static ApuracaoDoDia Apurar(int dia, HorarioContratual horario, params string[] horas) =>
        ApuracaoDoDia.Apurar(new DateOnly(2026, 11, dia), Marcacoes(dia, horas), horario, CalendarioDeFeriados.Nacionais, Fuso);

    public static TimeSpan Hm(string hora) => TimeSpan.ParseExact(hora, @"hh\:mm", System.Globalization.CultureInfo.InvariantCulture);
}
