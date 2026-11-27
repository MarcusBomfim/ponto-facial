using System.Globalization;
using PontoFacial.Dominio.Arquivos;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Registro;
using PontoFacial.Persistencia.Jornada;
using PontoFacial.Persistencia.Registro;

namespace PontoFacial.Persistencia.Arquivos;

/// <summary>
/// Os arquivos que o Auditor-Fiscal do Trabalho pede, gerados do que está
/// no banco: o AFD do registro de um estabelecimento (art. 81) e o AEJ do
/// tratamento (art. 83, I), cada um num período (ADR-0015).
/// </summary>
/// <remarks>
/// As operações e os tratamentos são lidos pelos caminhos de sempre, que
/// conferem o hash de cada marcação, a numeração e a cadeia: o arquivo não sai
/// de um banco adulterado.
/// </remarks>
public sealed class ArquivosDaPortaria
{
    private const int Pagina = 1000;

    private const string FaixaDoPeriodo = """
        select min(nsr), max(nsr)
        from ponto.operacoes
        where estabelecimento_id = $1 and gravada_em >= $2 and gravada_em < $3
        """;

    private const string MudancasNosTrabalhadores = """
        select cpf, mudanca, gravada_em, gravada_em_deslocamento
        from ponto.operacoes
        where estabelecimento_id = $1 and tipo = 5
        order by cpf, nsr
        """;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly EmpresaNoBanco _empresa;

    internal ArquivosDaPortaria(EmpresaNoBanco empresa) => _empresa = empresa;

    /// <summary>O AFD das operações gravadas no período, pela data da gravação na hora do estabelecimento.</summary>
    public Task<ArquivoFonteDeDados> Afd(Guid estabelecimento, DateOnly de, DateOnly ate, ProgramaDoPonto programa, DateTimeOffset geradoEm, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(programa);

        return _empresa.Transacao(
            async sessao =>
            {
                var local = await _empresa.Estabelecimentos.Obter(sessao, estabelecimento);
                var inicio = Meianoite(de, local.Fuso);
                var fim = Meianoite(ate.AddDays(1), local.Fuso);
                var faixa = (await sessao.Ler(
                    FaixaDoPeriodo,
                    linha => linha.IsDBNull(0) ? null : new Faixa(linha.GetInt64(0), linha.GetInt64(1)),
                    estabelecimento,
                    inicio,
                    fim))[0];

                // Depois de um ajuste do relógio, uma operação de fora do
                // período pode cair no meio dele: lê-se a faixa inteira de
                // NSRs, em ordem e conferida, e o arquivo escolhe pela data.
                var operacoes = new List<Operacao>();

                for (var nsr = faixa?.De ?? 1; faixa is not null && nsr <= faixa.Ate; nsr += Pagina)
                {
                    operacoes.AddRange((await RegistrosDePonto.Pagina(sessao, estabelecimento, nsr, Pagina)).Where(operacao => operacao.Nsr <= faixa.Ate));
                }

                return ArquivoFonteDeDados.Gerar(local, programa, operacoes, de, ate, geradoEm);
            },
            cancelamento);
    }

    /// <summary>
    /// O AEJ do estabelecimento no período: cada trabalhador que esteve
    /// incluído no registro em algum dia dele, com o tratamento, o horário que
    /// valia e os feriados do estabelecimento.
    /// </summary>
    public Task<ArquivoEletronicoDeJornada> Aej(Guid estabelecimento, DateOnly de, DateOnly ate, ProgramaDoPonto programa, DateTimeOffset geradoEm, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(programa);

        if (ate < de)
        {
            throw new ExcecaoDeDominio("O período do arquivo termina antes de começar.");
        }

        return _empresa.Transacao(
            async sessao =>
            {
                await _empresa.Estabelecimentos.Obter(sessao, estabelecimento);
                var calendario = await _empresa.Feriados.Calendario(sessao, estabelecimento);
                var mudancas = await sessao.Ler(
                    MudancasNosTrabalhadores,
                    linha => (Cpf: linha.GetString(0), Mudanca: linha.GetString(1)[0], Dia: DateOnly.FromDateTime(Instantes.Ler(linha, 2, 3).DateTime)),
                    estabelecimento);

                var vinculos = new List<JornadaDoVinculo>();

                foreach (var doTrabalhador in mudancas.GroupBy(mudanca => mudanca.Cpf))
                {
                    if (Vinculo(doTrabalhador.Select(mudanca => (mudanca.Mudanca, mudanca.Dia)), de, ate) is not { } vinculo)
                    {
                        continue;
                    }

                    var cpf = Leitura.Validada(() => Cpf.De(doTrabalhador.Key), "O CPF de um trabalhador do registro");
                    var trabalhador = await Cadastro.Trabalhadores.Obter(sessao, cpf);
                    var primeiro = vinculo.Desde > de ? vinculo.Desde : de;
                    var ultimo = vinculo.Ate is { } termino && termino < ate ? termino : ate;
                    var horario = await HorariosContratuais.NoPeriodo(sessao, cpf, primeiro, ultimo)
                        ?? throw new ExcecaoDeDominio(string.Create(
                            PtBr,
                            $"{trabalhador.Nome} ({cpf}) não tem horário contratual em {primeiro:dd/MM/yyyy}: cadastre o horário antes de gerar o AEJ."));
                    var (tratamento, _) = await _empresa.Tratamentos.Refazer(sessao, estabelecimento, cpf);

                    vinculos.Add(new JornadaDoVinculo(trabalhador, tratamento, horario, calendario, primeiro, ultimo));
                }

                var empregador = await _empresa.Empregador(sessao);

                return ArquivoEletronicoDeJornada.Gerar(
                    empregador,
                    programa,
                    [.. vinculos.OrderBy(vinculo => vinculo.Trabalhador.Nome, StringComparer.Ordinal).ThenBy(vinculo => vinculo.Trabalhador.Cpf.Numero, StringComparer.Ordinal)],
                    de,
                    ate,
                    geradoEm);
            },
            cancelamento);
    }

    /// <summary>
    /// Do primeiro dia em que o trabalhador esteve incluído no período ao
    /// último; nulo se não esteve em nenhum. Quem saiu e voltou no meio do
    /// período tem um vínculo só, do começo ao fim, e os dias de fora
    /// aparecem como falta: o AEJ não tem como dizer que ele não estava lá.
    /// </summary>
    internal static (DateOnly Desde, DateOnly? Ate)? Vinculo(IEnumerable<(char Mudanca, DateOnly Dia)> mudancas, DateOnly de, DateOnly ate)
    {
        var intervalos = new List<(DateOnly Desde, DateOnly? Ate)>();
        DateOnly? aberto = null;

        foreach (var (mudanca, dia) in mudancas)
        {
            if (mudanca == (char)TipoDeMudanca.Inclusao)
            {
                aberto = dia;
            }
            else if (mudanca == (char)TipoDeMudanca.Exclusao && aberto is { } desde)
            {
                intervalos.Add((desde, dia));
                aberto = null;
            }
        }

        if (aberto is { } semFim)
        {
            intervalos.Add((semFim, null));
        }

        var noPeriodo = intervalos.Where(intervalo => intervalo.Desde <= ate && (intervalo.Ate ?? DateOnly.MaxValue) >= de).ToList();

        return noPeriodo.Count == 0
            ? null
            : (noPeriodo.Min(intervalo => intervalo.Desde), noPeriodo.Any(intervalo => intervalo.Ate is null) ? null : noPeriodo.Max(intervalo => intervalo.Ate));
    }

    private static DateTimeOffset Meianoite(DateOnly dia, TimeZoneInfo fuso) =>
        new(TimeZoneInfo.ConvertTimeToUtc(dia.ToDateTime(TimeOnly.MinValue), fuso), TimeSpan.Zero);

    private sealed record Faixa(long De, long Ate);
}
