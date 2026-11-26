namespace PontoFacial.Dominio.Jornada;

/// <summary>Como ficou um dia de trabalho.</summary>
public enum SituacaoDoDia
{
    /// <summary>Houve trabalho, com as marcações em pares.</summary>
    Trabalhado = 1,

    /// <summary>Dia de horário sem marcação nenhuma.</summary>
    Falta = 2,

    /// <summary>Dia sem horário — o sábado compensado, por exemplo — e sem trabalho.</summary>
    Folga = 3,

    /// <summary>O descanso semanal, sem trabalho.</summary>
    DescansoSemanal = 4,

    /// <summary>Feriado sem trabalho.</summary>
    Feriado = 5,

    /// <summary>Número ímpar de marcações: falta uma entrada ou uma saída, e o dia espera tratamento.</summary>
    Pendente = 6,

    /// <summary>Dia de horário sem marcação, com a ausência justificada no tratamento.</summary>
    Abonado = 7,
}

/// <summary>
/// A apuração de um dia de trabalho pelas regras de jornada da CLT, a partir
/// das marcações e do horário contratual.
/// </summary>
/// <remarks>
/// <para>
/// A ordem das contas é a da lei:
/// </para>
/// <list type="number">
/// <item>O intervalo pré-assinalado (art. 74, § 2º) entra quando a pessoa marcou só a entrada e a saída.</item>
/// <item>A tolerância (art. 58, § 1º): cada marcação até 5 minutos longe do horário conta como se estivesse no horário, desde que a soma dessas variações no dia não passe de 10 minutos. Passou de 10, nenhuma é desconsiderada; a de mais de 5 minutos nunca é.</item>
/// <item>A hora noturna (art. 73): o que cai entre 22h e 5h vale 8/7, e a prorrogação de quem começou de noite também.</item>
/// <item>O trabalhado fora do horário é extra; no descanso semanal e no feriado, extra em descanso, que a Lei 605/1949 manda pagar em dobro. Mais de 2 horas extras no dia passam do limite do art. 59.</item>
/// <item>O horário sem trabalho é falta: atraso, saída antecipada ou o dia inteiro. O abono do tratamento — o atestado, a ausência justificada — o cobre, e ele fica à parte, como abonado.</item>
/// <item>O intervalo do art. 71 e o descanso de 11 horas entre jornadas do art. 66: o que faltou neles é suprimido.</item>
/// </list>
/// <para>
/// Cada valor é arredondado ao minuto: a hora noturna dá frações, e o
/// espelho mostra horas e minutos.
/// </para>
/// </remarks>
public sealed class ApuracaoDoDia
{
    public static readonly TimeSpan Tolerancia = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan ToleranciaDiaria = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan ExtrasPorDia = TimeSpan.FromHours(2);

    public static readonly TimeSpan DescansoEntreJornadas = TimeSpan.FromHours(11);

    private ApuracaoDoDia(DateOnly data, SituacaoDoDia situacao, string? feriado, bool diaDeDescanso, DateTimeOffset[] marcacoes, DateTimeOffset[] preAssinaladas)
    {
        Data = data;
        Situacao = situacao;
        Feriado = feriado;
        DiaDeDescanso = diaDeDescanso;
        Marcacoes = marcacoes.AsReadOnly();
        PreAssinaladas = preAssinaladas.AsReadOnly();
    }

    /// <summary>O dia de trabalho: o da entrada, mesmo que a saída seja depois da meia-noite.</summary>
    public DateOnly Data { get; }

    public SituacaoDoDia Situacao { get; }

    /// <summary>O nome do feriado, se o dia é feriado.</summary>
    public string? Feriado { get; }

    /// <summary>Feriado ou descanso semanal: o trabalho nele é extra em descanso.</summary>
    public bool DiaDeDescanso { get; }

    /// <summary>As marcações consideradas, em ordem, com as pré-assinaladas.</summary>
    public IReadOnlyList<DateTimeOffset> Marcacoes { get; }

    /// <summary>As marcações do intervalo lançadas pelo horário, sem a pessoa marcar.</summary>
    public IReadOnlyList<DateTimeOffset> PreAssinaladas { get; }

    /// <summary>As horas do horário contratual no dia, com a hora noturna reduzida.</summary>
    public TimeSpan Prevista { get; private init; }

    /// <summary>A duração da jornada realizada, com a hora noturna reduzida (art. 84, VI, da Portaria 671).</summary>
    public TimeSpan Realizada { get; private init; }

    /// <summary>As horas noturnas, já reduzidas: a base do adicional noturno.</summary>
    public TimeSpan Noturna { get; private init; }

    /// <summary>O trabalhado fora do horário, num dia comum.</summary>
    public TimeSpan Extras { get; private init; }

    /// <summary>O trabalhado no descanso semanal ou no feriado.</summary>
    public TimeSpan ExtrasEmDescanso { get; private init; }

    /// <summary>O horário que ficou sem trabalho: atraso, saída antecipada, falta.</summary>
    public TimeSpan Faltantes { get; private init; }

    /// <summary>O motivo da ausência justificada no dia, se houve abono.</summary>
    public string? Abono { get; private init; }

    /// <summary>O horário sem trabalho coberto pelo abono: não entra nas faltantes.</summary>
    public TimeSpan FaltantesAbonadas { get; private init; }

    /// <summary>O que faltou no intervalo do art. 71.</summary>
    public TimeSpan IntervaloSuprimido { get; private init; }

    /// <summary>O que faltou nas 11 horas de descanso entre a jornada anterior e esta (art. 66).</summary>
    public TimeSpan DescansoSuprimido { get; private init; }

    /// <summary>Alguma variação de até 5 minutos foi desconsiderada.</summary>
    public bool ToleranciaAplicada { get; private init; }

    /// <summary>Mais de 2 horas extras no dia: passou do limite do art. 59.</summary>
    public bool AcimaDoLimiteDeExtras => Extras > ExtrasPorDia;

    /// <summary>A última marcação do dia: daqui conta o descanso até a próxima jornada.</summary>
    public DateTimeOffset? FimDaJornada => Marcacoes.Count > 0 ? Marcacoes[^1] : null;

    /// <param name="data">O dia de trabalho.</param>
    /// <param name="marcacoes">As marcações do dia, já tratadas, na hora do estabelecimento.</param>
    /// <param name="fimDaJornadaAnterior">A última marcação da jornada anterior, para o descanso entre jornadas.</param>
    /// <param name="abono">O motivo da ausência justificada no dia — atestado, por exemplo —, que cobre as horas faltantes.</param>
    public static ApuracaoDoDia Apurar(
        DateOnly data,
        IReadOnlyList<DateTimeOffset> marcacoes,
        HorarioContratual horario,
        CalendarioDeFeriados calendario,
        TimeZoneInfo fuso,
        DateTimeOffset? fimDaJornadaAnterior = null,
        string? abono = null)
    {
        ArgumentNullException.ThrowIfNull(marcacoes);
        ArgumentNullException.ThrowIfNull(horario);
        ArgumentNullException.ThrowIfNull(calendario);
        ArgumentNullException.ThrowIfNull(fuso);

        var feriado = calendario.FeriadoEm(data);
        var descansoSemanal = data.DayOfWeek == horario.DescansoSemanal;
        var noHorario = feriado is null ? horario.TrechosEm(data, fuso) : Trechos.Nenhum;
        var prevista = TrabalhoNoturno.Computar(noHorario, TrabalhoNoturno.Da(noHorario, fuso));
        var ordenadas = marcacoes.Order().ToList();
        var preAssinaladas = PreAssinalar(ordenadas, noHorario, horario);
        ordenadas = [.. ordenadas.Concat(preAssinaladas).Order()];

        if (ordenadas.Count == 0)
        {
            var situacao = prevista > TimeSpan.Zero ? (abono is null ? SituacaoDoDia.Falta : SituacaoDoDia.Abonado)
                : feriado is not null ? SituacaoDoDia.Feriado
                : descansoSemanal ? SituacaoDoDia.DescansoSemanal
                : SituacaoDoDia.Folga;

            return new ApuracaoDoDia(data, situacao, feriado, feriado is not null || descansoSemanal, [], [])
            {
                Prevista = prevista,
                Faltantes = abono is null ? prevista : TimeSpan.Zero,
                Abono = abono,
                FaltantesAbonadas = abono is null ? TimeSpan.Zero : prevista,
            };
        }

        if (ordenadas.Count % 2 == 1)
        {
            return new ApuracaoDoDia(data, SituacaoDoDia.Pendente, feriado, feriado is not null || descansoSemanal, [.. ordenadas], [.. preAssinaladas]) { Prevista = prevista };
        }

        var (ajustadas, toleranciaAplicada) = AplicarTolerancia(ordenadas, noHorario);
        var trabalhado = Trechos.De(Enumerable.Range(0, ajustadas.Count / 2).Select(i => new Trecho(ajustadas[2 * i], ajustadas[(2 * i) + 1])));
        var noturnos = TrabalhoNoturno.Da(trabalhado, fuso);
        var realizada = TrabalhoNoturno.Computar(trabalhado, noturnos);
        var fora = TrabalhoNoturno.Computar(trabalhado.Menos(noHorario), noturnos);
        var diaDeDescanso = feriado is not null || descansoSemanal;
        var exigido = realizada > TimeSpan.FromHours(6) ? horario.IntervaloMinimo
            : realizada > TimeSpan.FromHours(4) ? HorarioContratual.IntervaloCurto
            : TimeSpan.Zero;
        var intervalo = trabalhado.MaiorIntervalo();
        var descanso = fimDaJornadaAnterior is { } fim ? ordenadas[0] - fim : DescansoEntreJornadas;
        var faltantes = TrabalhoNoturno.Computar(noHorario.Menos(trabalhado), TrabalhoNoturno.Da(noHorario, fuso));

        return new ApuracaoDoDia(data, SituacaoDoDia.Trabalhado, feriado, diaDeDescanso, [.. ordenadas], [.. preAssinaladas])
        {
            Prevista = prevista,
            Realizada = realizada,
            Noturna = TrabalhoNoturno.AoMinuto(TrabalhoNoturno.Reduzida(trabalhado.Intersecao(noturnos).Duracao)),
            Extras = diaDeDescanso ? TimeSpan.Zero : fora,
            ExtrasEmDescanso = diaDeDescanso ? fora : TimeSpan.Zero,
            Faltantes = abono is null ? faltantes : TimeSpan.Zero,
            Abono = abono,
            FaltantesAbonadas = abono is null ? TimeSpan.Zero : faltantes,
            IntervaloSuprimido = intervalo < exigido ? exigido - intervalo : TimeSpan.Zero,
            DescansoSuprimido = descanso < DescansoEntreJornadas ? TrabalhoNoturno.AoMinuto(DescansoEntreJornadas - descanso) : TimeSpan.Zero,
            ToleranciaAplicada = toleranciaAplicada,
        };
    }

    /// <summary>
    /// O intervalo do horário, quando ele é pré-assinalado e a pessoa marcou
    /// só a entrada e a saída, com o intervalo entre elas.
    /// </summary>
    private static List<DateTimeOffset> PreAssinalar(List<DateTimeOffset> marcacoes, Trechos noHorario, HorarioContratual horario)
    {
        if (!horario.IntervaloPreAssinalado || marcacoes.Count != 2 || noHorario.Todos.Count < 2)
        {
            return [];
        }

        var lancadas = new List<DateTimeOffset>();

        for (var i = 1; i < noHorario.Todos.Count; i++)
        {
            lancadas.Add(noHorario.Todos[i - 1].Ate);
            lancadas.Add(noHorario.Todos[i].De);
        }

        return lancadas.All(lancada => lancada > marcacoes[0] && lancada < marcacoes[1]) ? lancadas : [];
    }

    /// <summary>
    /// O art. 58, § 1º: as variações de até 5 minutos, somadas até 10 no dia,
    /// não contam. Cada marcação é comparada com o limite do horário na mesma
    /// posição; se o número de marcações não é o do horário, só a primeira e a
    /// última têm com o que se comparar.
    /// </summary>
    private static (List<DateTimeOffset> Ajustadas, bool Aplicada) AplicarTolerancia(List<DateTimeOffset> marcacoes, Trechos noHorario)
    {
        var limites = noHorario.Todos.SelectMany(trecho => new[] { trecho.De, trecho.Ate }).ToList();
        var ajustadas = new List<DateTimeOffset>(marcacoes);

        if (limites.Count == 0)
        {
            return (ajustadas, false);
        }

        var pares = marcacoes.Count == limites.Count
            ? Enumerable.Range(0, marcacoes.Count).Select(i => (Marcacao: i, Limite: i)).ToList()
            : [(0, 0), (marcacoes.Count - 1, limites.Count - 1)];

        var pequenas = pares
            .Where(par => (marcacoes[par.Marcacao] - limites[par.Limite]).Duration() <= Tolerancia)
            .ToList();

        var soma = pequenas.Aggregate(TimeSpan.Zero, (total, par) => total + (marcacoes[par.Marcacao] - limites[par.Limite]).Duration());

        if (soma > ToleranciaDiaria)
        {
            return (ajustadas, false);
        }

        foreach (var (marcacao, limite) in pequenas)
        {
            ajustadas[marcacao] = limites[limite];
        }

        return (ajustadas, pequenas.Any(par => marcacoes[par.Marcacao] != limites[par.Limite]));
    }
}
