using System.Globalization;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Jornada;

/// <summary>
/// O horário de trabalho do contrato, dia a dia da semana, com o descanso
/// semanal: o "horário e jornada contratual" que o espelho de ponto mostra
/// (art. 84, IV, da Portaria 671).
/// </summary>
/// <remarks>
/// <para>
/// O horário respeita os limites da lei ao ser criado: até 44 horas por
/// semana (Constituição, art. 7º, XIII), até 10 por dia — o máximo com
/// compensação (art. 59, § 2º, da CLT) —, e o intervalo do art. 71 em cada
/// dia: uma hora, ou o que a norma coletiva fixar a partir de 30 minutos
/// (art. 611-A, III), quando o dia passa de 6 horas; 15 minutos quando passa
/// de 4. As horas são computadas com a hora noturna reduzida.
/// </para>
/// <para>
/// A virada do dia é a hora em que um dia de trabalho termina e o seguinte
/// começa. Para quem trabalha de dia, é a meia-noite; para quem entra às 22h
/// e sai às 5h, tem de ser uma hora fora do turno — o meio-dia, por exemplo
/// —, para a entrada e a saída caírem no mesmo dia de trabalho.
/// </para>
/// </remarks>
public sealed class HorarioContratual
{
    public static readonly TimeSpan JornadaSemanalMaxima = TimeSpan.FromHours(44);

    public static readonly TimeSpan JornadaDiariaMaxima = TimeSpan.FromHours(10);

    public static readonly TimeSpan IntervaloDaLei = TimeSpan.FromHours(1);

    public static readonly TimeSpan IntervaloNegociadoMinimo = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan IntervaloCurto = TimeSpan.FromMinutes(15);

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly DayOfWeek[] Semana =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday,
    ];

    private readonly Dictionary<DayOfWeek, Periodo[]> _semana;

    private HorarioContratual(Dictionary<DayOfWeek, Periodo[]> semana, DayOfWeek descansoSemanal, TimeSpan intervaloMinimo, bool intervaloPreAssinalado, TimeOnly viradaDoDia)
    {
        _semana = semana;
        DescansoSemanal = descansoSemanal;
        IntervaloMinimo = intervaloMinimo;
        IntervaloPreAssinalado = intervaloPreAssinalado;
        ViradaDoDia = viradaDoDia;
        DuracaoSemanal = Semana.Aggregate(TimeSpan.Zero, (soma, dia) => soma + DuracaoDo(dia));
    }

    /// <summary>O dia do descanso semanal remunerado: o trabalho nele, sem folga em outro dia, é pago em dobro.</summary>
    public DayOfWeek DescansoSemanal { get; }

    /// <summary>O intervalo mínimo nos dias de mais de 6 horas: o da lei, ou o da norma coletiva.</summary>
    public TimeSpan IntervaloMinimo { get; }

    /// <summary>
    /// O intervalo é pré-assinalado (art. 74, § 2º, da CLT): quem marca só a
    /// entrada e a saída tem o intervalo do horário lançado no espelho.
    /// </summary>
    public bool IntervaloPreAssinalado { get; }

    public TimeOnly ViradaDoDia { get; }

    /// <summary>As horas da semana, com a hora noturna reduzida.</summary>
    public TimeSpan DuracaoSemanal { get; }

    /// <param name="semana">Os períodos de cada dia de trabalho; o dia que falta é folga.</param>
    /// <param name="descansoSemanal">O dia do descanso semanal; o domingo, se nada for dito.</param>
    /// <param name="intervaloPreAssinalado">Se o intervalo do horário vale sem ser marcado.</param>
    /// <param name="intervaloNegociado">O intervalo mínimo fixado em norma coletiva, de 30 minutos a uma hora.</param>
    /// <param name="viradaDoDia">A hora em que o dia de trabalho vira; a meia-noite, se nada for dito.</param>
    public static HorarioContratual De(
        IReadOnlyDictionary<DayOfWeek, IReadOnlyList<Periodo>> semana,
        DayOfWeek descansoSemanal = DayOfWeek.Sunday,
        bool intervaloPreAssinalado = false,
        TimeSpan? intervaloNegociado = null,
        TimeOnly viradaDoDia = default)
    {
        ArgumentNullException.ThrowIfNull(semana);

        var intervaloMinimo = intervaloNegociado ?? IntervaloDaLei;

        if (intervaloMinimo < IntervaloNegociadoMinimo || intervaloMinimo > IntervaloDaLei)
        {
            throw new ExcecaoDeDominio("O intervalo fixado em norma coletiva vai de 30 minutos a uma hora (art. 611-A, III, da CLT).");
        }

        if (semana.TryGetValue(descansoSemanal, out var noDescanso) && noDescanso.Count > 0)
        {
            throw new ExcecaoDeDominio($"{Nome(descansoSemanal)} é o descanso semanal: não tem horário de trabalho.");
        }

        var dias = new Dictionary<DayOfWeek, Periodo[]>();

        foreach (var (dia, periodos) in semana.Where(par => par.Value.Count > 0))
        {
            dias[dia] = Ordenar(dia, periodos, viradaDoDia);
        }

        var horario = new HorarioContratual(dias, descansoSemanal, intervaloMinimo, intervaloPreAssinalado, viradaDoDia);

        foreach (var dia in dias.Keys)
        {
            horario.ConferirODia(dia);
        }

        if (horario.DuracaoSemanal > JornadaSemanalMaxima)
        {
            throw new ExcecaoDeDominio(string.Create(
                PtBr,
                $"O horário soma {Duracoes.Formatar(horario.DuracaoSemanal)} por semana; o máximo é 44:00 (Constituição, art. 7º, XIII)."));
        }

        return horario;
    }

    /// <summary>Os períodos de trabalho do dia da semana, a partir da virada; nenhum na folga e no descanso.</summary>
    public IReadOnlyList<Periodo> Do(DayOfWeek dia) => _semana.TryGetValue(dia, out var periodos) ? periodos.AsReadOnly() : [];

    /// <summary>As horas do dia da semana, com a hora noturna reduzida.</summary>
    public TimeSpan DuracaoDo(DayOfWeek dia)
    {
        var trechos = TrechosEm(Referencia(dia), TimeZoneInfo.Utc);

        return TrabalhoNoturno.Computar(trechos, TrabalhoNoturno.Da(trechos, TimeZoneInfo.Utc));
    }

    /// <summary>Como o horário aparece no espelho de ponto.</summary>
    public string Descricao()
    {
        var partes = new List<string>();
        var i = 0;

        while (i < Semana.Length)
        {
            var periodos = Do(Semana[i]);
            var j = i;

            while (j + 1 < Semana.Length && Do(Semana[j + 1]).SequenceEqual(periodos))
            {
                j++;
            }

            if (periodos.Count > 0)
            {
                var dias = i == j ? Abreviado(Semana[i]) : $"{Abreviado(Semana[i])} a {Abreviado(Semana[j])}";
                partes.Add($"{dias} {string.Join(" e ", periodos)}");
            }

            i = j + 1;
        }

        partes.Add($"{Duracoes.Formatar(DuracaoSemanal)} por semana");
        partes.Add($"descanso {(DescansoSemanal is DayOfWeek.Saturday or DayOfWeek.Sunday ? "aos" : "às")} {Plural(DescansoSemanal)}");

        if (IntervaloPreAssinalado)
        {
            partes.Add("intervalo pré-assinalado");
        }

        return string.Join("; ", partes);
    }

    /// <summary>Os trechos do horário no dia dado, como instantes no fuso do estabelecimento.</summary>
    internal Trechos TrechosEm(DateOnly dia, TimeZoneInfo fuso) => Trechos.De(Do(dia.DayOfWeek).Select(periodo =>
    {
        var entrada = DiaDe(dia, periodo.Entrada);
        var saida = DiaDe(dia, periodo.Saida);

        // A saída à meia-noite, ou antes da entrada no mesmo dia do relógio, é no dia seguinte.
        if (saida.ToDateTime(periodo.Saida) <= entrada.ToDateTime(periodo.Entrada))
        {
            saida = saida.AddDays(1);
        }

        return new Trecho(TrabalhoNoturno.Local(entrada, periodo.Entrada, fuso), TrabalhoNoturno.Local(saida, periodo.Saida, fuso));
    }));

    /// <summary>O dia de trabalho a que pertence uma hora do relógio local: antes da virada, é o dia anterior.</summary>
    internal DateOnly DiaDeTrabalho(DateTime relogio) => DateOnly.FromDateTime(relogio - ViradaDoDia.ToTimeSpan());

    internal static string Abreviado(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Monday => "seg",
        DayOfWeek.Tuesday => "ter",
        DayOfWeek.Wednesday => "qua",
        DayOfWeek.Thursday => "qui",
        DayOfWeek.Friday => "sex",
        DayOfWeek.Saturday => "sáb",
        _ => "dom",
    };

    /// <summary>Num dia de trabalho que vira depois da meia-noite, a hora antes da virada é do dia seguinte do relógio.</summary>
    private DateOnly DiaDe(DateOnly dia, TimeOnly hora) => hora < ViradaDoDia ? dia.AddDays(1) : dia;

    private static Periodo[] Ordenar(DayOfWeek dia, IReadOnlyList<Periodo> periodos, TimeOnly virada)
    {
        TimeSpan DesdeAVirada(TimeOnly hora) => hora - virada;

        var ordenados = periodos.OrderBy(periodo => DesdeAVirada(periodo.Entrada)).ToArray();

        for (var i = 0; i < ordenados.Length; i++)
        {
            var periodo = ordenados[i];

            if (periodo.Entrada == periodo.Saida)
            {
                throw new ExcecaoDeDominio($"{Nome(dia)}: o período {periodo} não tem duração.");
            }

            // O período não pode atravessar a virada: o fim dele, contado da
            // virada, tem de vir depois do começo.
            var fim = DesdeAVirada(periodo.Saida) == TimeSpan.Zero ? TimeSpan.FromDays(1) : DesdeAVirada(periodo.Saida);

            if (fim <= DesdeAVirada(periodo.Entrada))
            {
                throw new ExcecaoDeDominio(string.Create(
                    PtBr,
                    $"{Nome(dia)}: o período {periodo} atravessa a virada do dia, às {virada:HH\\:mm}. Escolha uma virada fora do turno."));
            }

            if (i > 0)
            {
                var anterior = ordenados[i - 1];
                var fimAnterior = DesdeAVirada(anterior.Saida) == TimeSpan.Zero ? TimeSpan.FromDays(1) : DesdeAVirada(anterior.Saida);

                if (DesdeAVirada(periodo.Entrada) < fimAnterior)
                {
                    throw new ExcecaoDeDominio($"{Nome(dia)}: os períodos {anterior} e {periodo} se sobrepõem.");
                }
            }
        }

        return ordenados;
    }

    private void ConferirODia(DayOfWeek dia)
    {
        var duracao = DuracaoDo(dia);

        if (duracao > JornadaDiariaMaxima)
        {
            throw new ExcecaoDeDominio(string.Create(
                PtBr,
                $"{Nome(dia)}: o horário soma {Duracoes.Formatar(duracao)}; o máximo é 10:00 por dia, mesmo com compensação (art. 59, § 2º, da CLT)."));
        }

        var intervalo = TrechosEm(Referencia(dia), TimeZoneInfo.Utc).MaiorIntervalo();
        var exigido = duracao > TimeSpan.FromHours(6) ? IntervaloMinimo : duracao > TimeSpan.FromHours(4) ? IntervaloCurto : TimeSpan.Zero;

        if (intervalo < exigido)
        {
            throw new ExcecaoDeDominio(string.Create(
                PtBr,
                $"{Nome(dia)}: um dia de {Duracoes.Formatar(duracao)} pede intervalo de pelo menos {Duracoes.Formatar(exigido)} (art. 71 da CLT); o maior do horário é de {Duracoes.Formatar(intervalo)}."));
        }
    }

    /// <summary>Um dia qualquer com o dia da semana dado, para as contas do horário.</summary>
    private static DateOnly Referencia(DayOfWeek dia) => new DateOnly(2001, 1, 1).AddDays(((int)dia + 6) % 7);

    private static string Nome(DayOfWeek dia)
    {
        var nome = PtBr.DateTimeFormat.GetDayName(dia);

        return char.ToUpperInvariant(nome[0]) + nome[1..];
    }

    private static string Plural(DayOfWeek dia) => dia switch
    {
        DayOfWeek.Saturday => "sábados",
        DayOfWeek.Sunday => "domingos",
        _ => PtBr.DateTimeFormat.GetDayName(dia).Replace("-feira", "s-feiras", StringComparison.Ordinal),
    };
}
