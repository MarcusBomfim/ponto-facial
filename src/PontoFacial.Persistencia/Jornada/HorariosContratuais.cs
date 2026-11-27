using System.Globalization;
using System.Text.Json;
using Npgsql;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Jornada;

namespace PontoFacial.Persistencia.Jornada;

/// <summary>
/// O horário contratual de cada trabalhador, cada um a partir da data em que
/// passou a valer. O espelho e o AEJ de um período usam o horário que valia
/// nele (ADR-0015).
/// </summary>
public sealed class HorariosContratuais
{
    private const string Gravacao = """
        insert into ponto.horarios_contratuais (empregador_id, cpf, vigente_desde, horario)
        values ($1, $2, $3, $4::jsonb)
        on conflict (empregador_id, cpf, vigente_desde) do update set horario = excluded.horario, gravado_em = now()
        """;

    private const string LerVigente = """
        select vigente_desde, horario::text
        from ponto.horarios_contratuais
        where cpf = $1 and vigente_desde <= $2
        order by vigente_desde desc
        limit 1
        """;

    private const string LerMudancaNoPeriodo = """
        select min(vigente_desde)
        from ponto.horarios_contratuais
        where cpf = $1 and vigente_desde > $2 and vigente_desde <= $3
        """;

    private const string LerTodos = "select vigente_desde, horario::text from ponto.horarios_contratuais where cpf = $1 order by vigente_desde";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly EmpresaNoBanco _empresa;

    internal HorariosContratuais(EmpresaNoBanco empresa) => _empresa = empresa;

    /// <summary>Grava o horário que vale a partir da data; o da mesma data, se havia, é corrigido.</summary>
    public Task Gravar(Cpf trabalhador, DateOnly vigenteDesde, HorarioContratual horario, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);
        ArgumentNullException.ThrowIfNull(horario);

        return _empresa.Transacao(
            async sessao =>
            {
                try
                {
                    return await sessao.Executar(Gravacao, _empresa.Id, trabalhador.Numero, vigenteDesde, DocumentoDoHorario.Json(horario));
                }
                catch (PostgresException erro) when (erro is { SqlState: PostgresErrorCodes.ForeignKeyViolation })
                {
                    throw new NaoEncontrado($"O trabalhador {trabalhador} não está cadastrado nesta empresa.", erro);
                }
            },
            cancelamento);
    }

    /// <summary>O horário que vale no dia; nulo se o trabalhador ainda não tinha horário.</summary>
    public Task<HorarioContratual?> Vigente(Cpf trabalhador, DateOnly dia, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);

        return _empresa.Transacao(async sessao => (await Vigente(sessao, trabalhador, dia))?.Horario, cancelamento);
    }

    /// <summary>Todos os horários do trabalhador, cada um com a data em que passou a valer.</summary>
    public Task<IReadOnlyList<HorarioVigente>> DoTrabalhador(Cpf trabalhador, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);

        return _empresa.Transacao(sessao => sessao.Ler(LerTodos, linha => Ler(linha, trabalhador), trabalhador.Numero), cancelamento);
    }

    /// <summary>
    /// O horário de um período inteiro, para o espelho de ponto; nulo se o
    /// trabalhador não tinha horário no começo dele. Se o horário muda no meio
    /// do período, o pedido é recusado, com as duas partes em que pedir.
    /// </summary>
    public Task<HorarioContratual?> NoPeriodo(Cpf trabalhador, DateOnly de, DateOnly ate, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);

        return _empresa.Transacao(sessao => NoPeriodo(sessao, trabalhador, de, ate), cancelamento);
    }

    /// <summary>
    /// O horário de um período inteiro. Se o horário muda no meio do período,
    /// não há um só horário para ele, e o período tem de ser pedido em duas
    /// partes.
    /// </summary>
    internal static async Task<HorarioContratual?> NoPeriodo(Sessao sessao, Cpf trabalhador, DateOnly de, DateOnly ate)
    {
        var mudancas = await sessao.Ler(LerMudancaNoPeriodo, linha => linha.IsDBNull(0) ? (DateOnly?)null : linha.GetFieldValue<DateOnly>(0), trabalhador.Numero, de, ate);

        if (mudancas[0] is { } mudou)
        {
            throw new ExcecaoDeDominio(string.Create(
                PtBr,
                $"O horário de {trabalhador} mudou em {mudou:dd/MM/yyyy}: peça o período em duas partes, até {mudou.AddDays(-1):dd/MM/yyyy} e a partir de {mudou:dd/MM/yyyy}."));
        }

        return (await Vigente(sessao, trabalhador, de))?.Horario;
    }

    private static async Task<HorarioVigente?> Vigente(Sessao sessao, Cpf trabalhador, DateOnly dia) =>
        await sessao.LerUma(LerVigente, linha => Ler(linha, trabalhador), trabalhador.Numero, dia);

    private static HorarioVigente Ler(NpgsqlDataReader linha, Cpf trabalhador)
    {
        var desde = linha.GetFieldValue<DateOnly>(0);

        try
        {
            return new HorarioVigente(desde, DocumentoDoHorario.Horario(linha.GetString(1)));
        }
        catch (Exception erro) when (erro is JsonException or FormatException or ExcecaoDeDominio)
        {
            throw new ConteudoAdulterado(
                string.Create(PtBr, $"O horário de {trabalhador} a partir de {desde:dd/MM/yyyy} no banco não se lê: {erro.Message}"),
                erro);
        }
    }
}

/// <summary>Um horário contratual e a data em que ele passou a valer.</summary>
public sealed record HorarioVigente(DateOnly VigenteDesde, HorarioContratual Horario);
