using System.Globalization;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Jornada;

/// <summary>
/// O relatório Espelho de Ponto Eletrônico do art. 84 da Portaria 671: o
/// que o trabalhador vê do próprio ponto, no mínimo todo mês.
/// </summary>
/// <remarks>
/// <para>
/// O art. 84 pede, no mínimo: o empregador (nome, CNPJ ou CPF, e CEI, CAEPF
/// ou CNO, se houver); o trabalhador (nome, CPF, admissão e cargo); a data
/// de emissão e o período; o horário e a jornada do contrato; as marcações
/// feitas no registro e as tratadas — incluídas, desconsideradas e
/// pré-assinaladas —; e a duração das jornadas, com a hora noturna reduzida.
/// Tudo isso está aqui, na ordem dos incisos.
/// </para>
/// <para>
/// Além do mínimo, o espelho traz a apuração de cada dia — extras,
/// noturnas, faltas, intervalos — e a lista dos tratamentos com o motivo, o
/// responsável e a data: quem lê vê o que mudou no próprio ponto e por quê.
/// </para>
/// </remarks>
public sealed class EspelhoDePonto
{
    public const string Titulo = "Espelho de Ponto Eletrônico";

    public const string Legenda = "Marcações: P pré-assinalada; I incluída no tratamento; D desconsiderada no tratamento, não conta.";

    private const int LarguraDasMarcacoes = 40;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private EspelhoDePonto(TratamentoDoPonto tratamento, Trabalhador trabalhador, HorarioContratual horario, ApuracaoDoPeriodo apuracao, DateTimeOffset emitidoEm)
    {
        Tratamento = tratamento;
        Trabalhador = trabalhador;
        Horario = horario;
        Apuracao = apuracao;
        EmitidoEm = emitidoEm;
    }

    public Empregador Empregador => Tratamento.Estabelecimento.Empregador;

    public Trabalhador Trabalhador { get; }

    public HorarioContratual Horario { get; }

    public ApuracaoDoPeriodo Apuracao { get; }

    public TratamentoDoPonto Tratamento { get; }

    public DateTimeOffset EmitidoEm { get; }

    public static EspelhoDePonto Gerar(
        TratamentoDoPonto tratamento,
        Trabalhador trabalhador,
        HorarioContratual horario,
        CalendarioDeFeriados calendario,
        DateOnly de,
        DateOnly ate,
        DateTimeOffset emitidoEm)
    {
        ArgumentNullException.ThrowIfNull(tratamento);
        ArgumentNullException.ThrowIfNull(trabalhador);
        ArgumentNullException.ThrowIfNull(horario);
        ArgumentNullException.ThrowIfNull(calendario);

        if (trabalhador.Cpf != tratamento.Trabalhador)
        {
            throw new ExcecaoDeDominio("O tratamento é de outro trabalhador.");
        }

        var fuso = tratamento.Estabelecimento.Fuso;
        var consideradas = tratamento.Consideradas();
        var antes = consideradas.Where(marcacao => DiaDe(marcacao, horario, fuso) < de).ToList();
        var abonos = tratamento.Abonos.ToDictionary(par => par.Key, par => par.Value.Motivo);
        var apuracao = ApuracaoDoPeriodo.Apurar(de, ate, consideradas, horario, calendario, fuso, antes.Count > 0 ? antes[^1] : null, abonos);

        return new EspelhoDePonto(tratamento, trabalhador, horario, apuracao, emitidoEm);
    }

    /// <summary>O espelho em texto, uma informação por linha, com a tabela dos dias.</summary>
    public IReadOnlyList<string> Linhas()
    {
        var linhas = new List<string>
        {
            Titulo,
            $"Empregador: {Empregador.Nome}",
            $"{(Empregador.Documento is Cnpj ? "CNPJ" : "CPF")}: {Empregador.Documento.Formatado}",
        };

        if (Empregador.CeiCaepfOuCno is { } cei)
        {
            linhas.Add($"CEI/CAEPF/CNO: {cei}");
        }

        var fuso = Tratamento.Estabelecimento.Fuso;
        var emitido = TimeZoneInfo.ConvertTime(EmitidoEm, fuso);

        linhas.AddRange(
        [
            $"Local: {Tratamento.Estabelecimento.Local}",
            $"Trabalhador: {Trabalhador.Nome}",
            $"CPF: {Trabalhador.Cpf.Formatado}",
            string.Create(PtBr, $"Admissão: {Trabalhador.Admissao:dd/MM/yyyy}"),
            $"Cargo: {Trabalhador.Cargo}",
            string.Create(PtBr, $"Emitido em: {emitido:dd/MM/yyyy HH:mm} (UTC{emitido.ToString("zzz", CultureInfo.InvariantCulture)})"),
            string.Create(PtBr, $"Período: {Apuracao.De:dd/MM/yyyy} a {Apuracao.Ate:dd/MM/yyyy}"),
            $"Horário contratual: {Horario.Descricao()}",
            string.Empty,
            Coluna("Dia", 9) + "  " + Coluna("Marcações", LarguraDasMarcacoes) + "  Jornada  Noturna  Extras  Em descanso  Faltas  Ocorrências",
        ]);

        foreach (var dia in Apuracao.Dias)
        {
            var valores = string.Join(
                "  ",
                Numero(dia.Realizada, 7),
                Numero(dia.Noturna, 7),
                Numero(dia.Extras, 6),
                Numero(dia.ExtrasEmDescanso, 11),
                Numero(dia.Faltantes, 6));
            var ocorrencias = Ocorrencias(dia);

            linhas.Add((Coluna(Dia(dia.Data), 9) + "  " + Coluna(Marcacoes(dia, fuso), LarguraDasMarcacoes) + "  " + valores + (ocorrencias.Length > 0 ? "  " + ocorrencias : string.Empty)).TrimEnd());
        }

        linhas.AddRange(
        [
            string.Empty,
            $"Horas do horário: {Duracoes.Formatar(Apuracao.Prevista)}",
            $"Jornada realizada, com a hora noturna reduzida: {Duracoes.Formatar(Apuracao.Realizada)}",
            $"Noturnas: {Duracoes.Formatar(Apuracao.Noturna)}",
            $"Extras: {Duracoes.Formatar(Apuracao.Extras)}",
            $"Extras em descanso ou feriado: {Duracoes.Formatar(Apuracao.ExtrasEmDescanso)}",
            $"Faltantes: {Duracoes.Formatar(Apuracao.Faltantes)}",
            $"Abonadas: {Duracoes.Formatar(Apuracao.FaltantesAbonadas)}",
            $"Intervalo suprimido: {Duracoes.Formatar(Apuracao.IntervaloSuprimido)}",
            $"Descanso entre jornadas suprimido: {Duracoes.Formatar(Apuracao.DescansoSuprimido)}",
            string.Create(PtBr, $"Dias com falta: {Apuracao.Faltas}; abonados: {Apuracao.Abonos}; pendentes de tratamento: {Apuracao.Pendentes}; acima do limite de extras: {Apuracao.DiasAcimaDoLimiteDeExtras}"),
            Legenda,
        ]);

        var tratamentos = Tratamentos(fuso);

        if (tratamentos.Count > 0)
        {
            linhas.Add("Tratamentos:");
            linhas.AddRange(tratamentos.Select(tratamento => "  " + tratamento));
        }

        return linhas.AsReadOnly();
    }

    private static DateOnly DiaDe(DateTimeOffset marcacao, HorarioContratual horario, TimeZoneInfo fuso) =>
        horario.DiaDeTrabalho(TimeZoneInfo.ConvertTime(marcacao, fuso).DateTime);

    /// <summary>As marcações do dia como o espelho as mostra: as do registro, as tratadas e as pré-assinaladas, cada uma com a letra.</summary>
    private string Marcacoes(ApuracaoDoDia dia, TimeZoneInfo fuso)
    {
        var desconsideradas = Tratamento.Marcacoes
            .Where(tratada => tratada.Tipo == TipoDeTratamento.Desconsiderada)
            .Select(tratada => tratada.Nsr)
            .ToHashSet();

        var doDia = Tratamento.Registradas()
            .Select(marcacao => (Em: marcacao.MarcadaEm, Letra: desconsideradas.Contains(marcacao.Nsr) ? "D" : string.Empty))
            .Concat(Tratamento.Marcacoes.Where(tratada => tratada.Tipo == TipoDeTratamento.Incluida).Select(tratada => (Em: tratada.MarcadaEm, Letra: "I")))
            .Where(marcacao => DiaDe(marcacao.Em, Horario, fuso) == dia.Data)
            .Concat(dia.PreAssinaladas.Select(marcacao => (Em: marcacao, Letra: "P")))
            .OrderBy(marcacao => marcacao.Em)
            .Select(marcacao => string.Create(PtBr, $"{TimeZoneInfo.ConvertTime(marcacao.Em, fuso):HH:mm}{marcacao.Letra}"));

        return string.Join(" ", doDia);
    }

    private static string Ocorrencias(ApuracaoDoDia dia)
    {
        var ocorrencias = new List<string>();

        if (dia.Feriado is { } feriado)
        {
            ocorrencias.Add($"Feriado: {feriado}");
        }

        ocorrencias.AddRange(dia.Situacao switch
        {
            SituacaoDoDia.Falta => ["Falta"],
            SituacaoDoDia.Folga => ["Folga"],
            SituacaoDoDia.DescansoSemanal => ["Descanso semanal"],
            SituacaoDoDia.Pendente => ["Pendente: marcação ímpar"],
            _ => [],
        });

        if (dia.Abono is { } abono)
        {
            ocorrencias.Add($"Abono: {abono}");
        }

        if (dia.ToleranciaAplicada)
        {
            ocorrencias.Add("Tolerância do art. 58");
        }

        if (dia.IntervaloSuprimido > TimeSpan.Zero)
        {
            ocorrencias.Add($"Intervalo suprimido {Duracoes.Formatar(dia.IntervaloSuprimido)}");
        }

        if (dia.DescansoSuprimido > TimeSpan.Zero)
        {
            ocorrencias.Add($"Descanso entre jornadas suprimido {Duracoes.Formatar(dia.DescansoSuprimido)}");
        }

        if (dia.AcimaDoLimiteDeExtras)
        {
            ocorrencias.Add("Mais de 2 horas extras (art. 59)");
        }

        return string.Join("; ", ocorrencias);
    }

    private List<string> Tratamentos(TimeZoneInfo fuso)
    {
        var tratamentos = new List<string>();

        foreach (var tratada in Tratamento.Marcacoes.Where(tratada => Dentro(DiaDe(tratada.MarcadaEm, Horario, fuso))).OrderBy(tratada => tratada.MarcadaEm))
        {
            var marcada = TimeZoneInfo.ConvertTime(tratada.MarcadaEm, fuso);
            var em = TimeZoneInfo.ConvertTime(tratada.Em, fuso);
            var acao = tratada.Tipo == TipoDeTratamento.Incluida
                ? $"{marcada:HH:mm} de {marcada:dd/MM/yyyy} incluída"
                : $"{marcada:HH:mm} de {marcada:dd/MM/yyyy} (NSR {tratada.Nsr}) desconsiderada";

            tratamentos.Add(string.Create(PtBr, $"{acao} por {tratada.Responsavel.Formatado} em {em:dd/MM/yyyy HH:mm}: {tratada.Motivo}"));
        }

        foreach (var abono in Tratamento.Abonos.Values.Where(abono => Dentro(abono.Dia)).OrderBy(abono => abono.Dia))
        {
            var em = TimeZoneInfo.ConvertTime(abono.Em, fuso);
            tratamentos.Add(string.Create(PtBr, $"{abono.Dia:dd/MM/yyyy} abonado por {abono.Responsavel.Formatado} em {em:dd/MM/yyyy HH:mm}: {abono.Motivo}"));
        }

        return tratamentos;
    }

    private bool Dentro(DateOnly dia) => dia >= Apuracao.De && dia <= Apuracao.Ate;

    private static string Dia(DateOnly data) => string.Create(PtBr, $"{HorarioContratual.Abreviado(data.DayOfWeek)} {data:dd/MM}");

    private static string Coluna(string texto, int largura) => texto.Length >= largura ? texto : texto.PadRight(largura);

    private static string Numero(TimeSpan valor, int largura) => Duracoes.Formatar(valor).PadLeft(largura);
}
