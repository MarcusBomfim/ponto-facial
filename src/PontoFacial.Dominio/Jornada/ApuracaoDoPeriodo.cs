using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Jornada;

/// <summary>
/// A apuração de um período — em geral, o mês — dia a dia, com os totais. As
/// marcações vão para o dia de trabalho pela virada do horário, e o
/// descanso entre jornadas é contado de uma jornada para a seguinte.
/// </summary>
public sealed class ApuracaoDoPeriodo
{
    /// <summary>Um período apurado de uma vez vai até um ano.</summary>
    public const int DiasNoMaximo = 366;

    private ApuracaoDoPeriodo(DateOnly de, DateOnly ate, ApuracaoDoDia[] dias)
    {
        De = de;
        Ate = ate;
        Dias = dias.AsReadOnly();
    }

    public DateOnly De { get; }

    public DateOnly Ate { get; }

    public IReadOnlyList<ApuracaoDoDia> Dias { get; }

    public TimeSpan Prevista => Somar(dia => dia.Prevista);

    public TimeSpan Realizada => Somar(dia => dia.Realizada);

    public TimeSpan Noturna => Somar(dia => dia.Noturna);

    public TimeSpan Extras => Somar(dia => dia.Extras);

    public TimeSpan ExtrasEmDescanso => Somar(dia => dia.ExtrasEmDescanso);

    public TimeSpan Faltantes => Somar(dia => dia.Faltantes);

    public TimeSpan FaltantesAbonadas => Somar(dia => dia.FaltantesAbonadas);

    public TimeSpan IntervaloSuprimido => Somar(dia => dia.IntervaloSuprimido);

    public TimeSpan DescansoSuprimido => Somar(dia => dia.DescansoSuprimido);

    public int Faltas => Dias.Count(dia => dia.Situacao == SituacaoDoDia.Falta);

    public int Abonos => Dias.Count(dia => dia.Abono is not null);

    /// <summary>Os dias com marcação ímpar, que esperam tratamento: as contas deles ainda não entram.</summary>
    public int Pendentes => Dias.Count(dia => dia.Situacao == SituacaoDoDia.Pendente);

    public int DiasAcimaDoLimiteDeExtras => Dias.Count(dia => dia.AcimaDoLimiteDeExtras);

    /// <param name="marcacoes">As marcações do trabalhador, já tratadas; as que caem fora do período não contam.</param>
    /// <param name="fimDaJornadaAnterior">A última marcação antes do período, para o descanso entre jornadas no primeiro dia.</param>
    /// <param name="abonos">O motivo de cada dia com ausência justificada.</param>
    public static ApuracaoDoPeriodo Apurar(
        DateOnly de,
        DateOnly ate,
        IEnumerable<DateTimeOffset> marcacoes,
        HorarioContratual horario,
        CalendarioDeFeriados calendario,
        TimeZoneInfo fuso,
        DateTimeOffset? fimDaJornadaAnterior = null,
        IReadOnlyDictionary<DateOnly, string>? abonos = null)
    {
        ArgumentNullException.ThrowIfNull(marcacoes);
        ArgumentNullException.ThrowIfNull(horario);
        ArgumentNullException.ThrowIfNull(calendario);
        ArgumentNullException.ThrowIfNull(fuso);

        if (ate < de || ate.DayNumber - de.DayNumber >= DiasNoMaximo)
        {
            throw new ExcecaoDeDominio("O período vai de um dia a um ano, e o fim não vem antes do começo.");
        }

        var porDia = marcacoes
            .GroupBy(marcacao => horario.DiaDeTrabalho(TimeZoneInfo.ConvertTime(marcacao, fuso).DateTime))
            .ToDictionary(grupo => grupo.Key, grupo => (IReadOnlyList<DateTimeOffset>)[.. grupo]);

        var dias = new List<ApuracaoDoDia>();
        var fimAnterior = fimDaJornadaAnterior;

        for (var dia = de; dia <= ate; dia = dia.AddDays(1))
        {
            var doDia = porDia.TryGetValue(dia, out var lista) ? lista : [];
            var abono = abonos is not null && abonos.TryGetValue(dia, out var motivo) ? motivo : null;
            var apuracao = ApuracaoDoDia.Apurar(dia, doDia, horario, calendario, fuso, fimAnterior, abono);
            dias.Add(apuracao);
            fimAnterior = apuracao.FimDaJornada ?? fimAnterior;
        }

        return new ApuracaoDoPeriodo(de, ate, [.. dias]);
    }

    private TimeSpan Somar(Func<ApuracaoDoDia, TimeSpan> valor) => Dias.Aggregate(TimeSpan.Zero, (soma, dia) => soma + valor(dia));
}
