namespace PontoFacial.Dominio.Jornada;

/// <summary>Um trecho de tempo, do início, incluído, ao fim, não incluído.</summary>
internal readonly record struct Trecho(DateTimeOffset De, DateTimeOffset Ate)
{
    public TimeSpan Duracao => Ate - De;
}

/// <summary>
/// Um conjunto de trechos sem sobreposição, em ordem: o que foi trabalhado,
/// o que estava no horário, o que é noturno. As contas de jornada são
/// contas de conjunto — o trabalhado fora do horário, o horário que ficou
/// sem trabalho.
/// </summary>
internal sealed class Trechos
{
    public static readonly Trechos Nenhum = new([]);

    private readonly Trecho[] _trechos;

    private Trechos(Trecho[] trechos) => _trechos = trechos;

    public IReadOnlyList<Trecho> Todos => _trechos;

    public TimeSpan Duracao => _trechos.Aggregate(TimeSpan.Zero, (soma, trecho) => soma + trecho.Duracao);

    /// <summary>Os trechos dados, juntos onde se tocam ou se sobrepõem.</summary>
    public static Trechos De(IEnumerable<Trecho> trechos)
    {
        var juntos = new List<Trecho>();

        foreach (var trecho in trechos.Where(trecho => trecho.Ate > trecho.De).OrderBy(trecho => trecho.De))
        {
            if (juntos.Count > 0 && trecho.De <= juntos[^1].Ate)
            {
                juntos[^1] = juntos[^1] with { Ate = trecho.Ate > juntos[^1].Ate ? trecho.Ate : juntos[^1].Ate };
            }
            else
            {
                juntos.Add(trecho);
            }
        }

        return new Trechos([.. juntos]);
    }

    public Trechos Intersecao(Trechos outros) =>
        De(_trechos.SelectMany(a => outros._trechos
            .Select(b => new Trecho(a.De > b.De ? a.De : b.De, a.Ate < b.Ate ? a.Ate : b.Ate))));

    public Trechos Menos(Trechos outros)
    {
        var resto = new List<Trecho>();

        foreach (var trecho in _trechos)
        {
            var de = trecho.De;

            foreach (var corte in outros._trechos.Where(corte => corte.Ate > trecho.De && corte.De < trecho.Ate))
            {
                if (corte.De > de)
                {
                    resto.Add(new Trecho(de, corte.De));
                }

                de = corte.Ate > de ? corte.Ate : de;
            }

            if (trecho.Ate > de)
            {
                resto.Add(new Trecho(de, trecho.Ate));
            }
        }

        return De(resto);
    }

    /// <summary>O maior intervalo entre dois trechos seguidos; zero se há um trecho só.</summary>
    public TimeSpan MaiorIntervalo()
    {
        var maior = TimeSpan.Zero;

        for (var i = 1; i < _trechos.Length; i++)
        {
            var intervalo = _trechos[i].De - _trechos[i - 1].Ate;
            maior = intervalo > maior ? intervalo : maior;
        }

        return maior;
    }
}

/// <summary>
/// O trabalho noturno do art. 73 da CLT: o das 22h às 5h, com a hora de 52
/// minutos e 30 segundos — 7 horas de relógio valem 8.
/// </summary>
/// <remarks>
/// A prorrogação segue o § 5º do art. 73 ("às prorrogações do trabalho
/// noturno aplica-se o disposto neste capítulo") e a Súmula 60, II, do TST:
/// a jornada que começou no período noturno e continuou depois das 5h tem o
/// que passou das 5h também como noturno, com a hora reduzida.
/// </remarks>
internal static class TrabalhoNoturno
{
    public static readonly TimeOnly Inicio = new(22, 0);

    public static readonly TimeOnly Fim = new(5, 0);

    /// <summary>
    /// Os trechos noturnos de uma jornada: as noites que ela atravessa e, se
    /// ela começou de noite e continuava às 5h, a prorrogação até a noite
    /// seguinte.
    /// </summary>
    public static Trechos Da(Trechos jornada, TimeZoneInfo fuso)
    {
        if (jornada.Todos.Count == 0)
        {
            return Trechos.Nenhum;
        }

        var inicio = jornada.Todos[0].De;
        var fim = jornada.Todos[^1].Ate;
        var noites = new List<Trecho>();
        var primeiroDia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(inicio, fuso).DateTime).AddDays(-1);
        var ultimoDia = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(fim, fuso).DateTime);

        for (var dia = primeiroDia; dia <= ultimoDia; dia = dia.AddDays(1))
        {
            var noite = new Trecho(Local(dia, Inicio, fuso), Local(dia.AddDays(1), Fim, fuso));
            noites.Add(noite);

            var comecouNela = inicio >= noite.De && inicio < noite.Ate;
            var trabalhavaAs5 = jornada.Todos.Any(trecho => trecho.De < noite.Ate && trecho.Ate > noite.Ate);

            if (comecouNela && trabalhavaAs5)
            {
                noites.Add(new Trecho(noite.Ate, Local(dia.AddDays(1), Inicio, fuso)));
            }
        }

        return jornada.Intersecao(Trechos.De(noites));
    }

    /// <summary>
    /// A duração computada: o diurno como é, o noturno multiplicado por 8/7,
    /// arredondado ao minuto.
    /// </summary>
    public static TimeSpan Computar(Trechos trechos, Trechos noturnos)
    {
        var noturno = trechos.Intersecao(noturnos).Duracao;
        var diurno = trechos.Duracao - noturno;

        return AoMinuto(diurno + Reduzida(noturno));
    }

    /// <summary>O noturno de relógio em horas de 52 min 30 s.</summary>
    public static TimeSpan Reduzida(TimeSpan noturno) => TimeSpan.FromTicks(noturno.Ticks * 8 / 7);

    public static TimeSpan AoMinuto(TimeSpan duracao) =>
        TimeSpan.FromMinutes(Math.Round(duracao.TotalMinutes, MidpointRounding.AwayFromZero));

    /// <summary>A data e a hora do relógio local como instante, com o fuso daquele dia.</summary>
    public static DateTimeOffset Local(DateOnly dia, TimeOnly hora, TimeZoneInfo fuso)
    {
        var relogio = dia.ToDateTime(hora);

        return new DateTimeOffset(relogio, fuso.GetUtcOffset(relogio));
    }
}
