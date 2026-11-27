using System.Globalization;
using Npgsql;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Registro;

namespace PontoFacial.Persistencia.Registro;

/// <summary>
/// O registro de ponto de cada estabelecimento da empresa, no banco: abrir,
/// acrescentar e ler. Não há como alterar nem apagar — nem aqui, nem no
/// banco (ADR-0013).
/// </summary>
/// <remarks>
/// <para>
/// Acrescentar não lê o registro inteiro. O banco devolve a ponta — o último
/// NSR, o relógio, o hash da última marcação, quem está incluído —, o domínio
/// retoma o registro por ela e faz a operação com as regras de sempre, e só
/// o que ela acrescentou vai para o banco. Marcar num registro com um milhão
/// de marcações não custa mais do que num com dez.
/// </para>
/// <para>
/// Quem grava num estabelecimento entra na fila dele: dois tablets marcando
/// no mesmo segundo gravam um depois do outro, cada um com a ponta que o
/// anterior deixou, e nenhum recebe o mesmo NSR.
/// </para>
/// </remarks>
public sealed class RegistrosDePonto
{
    private const string UltimaOperacao = """
        select nsr, tipo, gravada_em, gravada_em_deslocamento, ajustado_para, ajustado_para_deslocamento
        from ponto.operacoes
        where estabelecimento_id = $1
        order by nsr desc
        limit 1
        """;

    private const string UltimoHash = """
        select hash
        from ponto.operacoes
        where estabelecimento_id = $1 and tipo = 7
        order by nsr desc
        limit 1
        """;

    /// <summary>
    /// A última mudança de cada trabalhador diz se ele está incluído e com que
    /// nome; a última inclusão, desde quando.
    /// </summary>
    private const string Incluidos = """
        select ultima.cpf, ultima.nome, inclusao.gravada_em, inclusao.gravada_em_deslocamento
        from (
            select distinct on (cpf) cpf, mudanca, nome
            from ponto.operacoes
            where estabelecimento_id = $1 and tipo = 5
            order by cpf, nsr desc
        ) ultima
        cross join lateral (
            select gravada_em, gravada_em_deslocamento
            from ponto.operacoes
            where estabelecimento_id = $1 and tipo = 5 and cpf = ultima.cpf and mudanca = 'I'
            order by nsr desc
            limit 1
        ) inclusao
        where ultima.mudanca <> 'E'
        order by ultima.cpf
        """;

    private const string LerOperacoes = $"""
        select {OperacoesNoBanco.Colunas}
        from ponto.operacoes
        where estabelecimento_id = $1 and nsr >= $2
        order by nsr
        limit $3
        """;

    private const string LerOperacao = $"""
        select {OperacoesNoBanco.Colunas}
        from ponto.operacoes
        where estabelecimento_id = $1 and nsr = $2
        """;

    private const string LerMarcacoesDoTrabalhador = $"""
        select {OperacoesNoBanco.Colunas}
        from ponto.operacoes
        where estabelecimento_id = $1 and cpf = $2 and tipo = 7
        order by nsr
        """;

    private const string FoiIncluido = """
        select exists (
            select from ponto.operacoes
            where estabelecimento_id = $1 and tipo = 5 and cpf = $2 and mudanca = 'I'
        )
        """;

    private const string NomeAntesDe = """
        select nome
        from ponto.operacoes
        where estabelecimento_id = $1 and tipo = 5 and cpf = $2 and nsr < $3
        order by nsr desc
        limit 1
        """;

    private readonly EmpresaNoBanco _empresa;

    internal RegistrosDePonto(EmpresaNoBanco empresa) => _empresa = empresa;

    /// <summary>Abre o registro do estabelecimento, com a inclusão do empregador no NSR 1.</summary>
    public Task<InclusaoDoEmpregador> Abrir(Guid estabelecimento, Cpf responsavel, DateTimeOffset em, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(responsavel);

        return _empresa.Transacao(
            async sessao =>
            {
                await sessao.EntrarNaFila(estabelecimento);
                var local = await _empresa.Estabelecimentos.Obter(sessao, estabelecimento);

                if (await Ponta(sessao, estabelecimento) is not null)
                {
                    throw new JaExiste($"O registro de ponto do estabelecimento {estabelecimento} já foi aberto.");
                }

                var registro = RegistroDePonto.Abrir(local, responsavel, em);
                await Inserir(sessao, estabelecimento, registro.Operacoes);

                return (InclusaoDoEmpregador)registro.Operacoes[0];
            },
            cancelamento);
    }

    /// <summary>
    /// Faz a operação no registro retomado pela ponta e grava o que ela
    /// acrescentou, tudo na fila do estabelecimento. Se a operação recusa —
    /// trabalhador não incluído, relógio voltando —, nada se grava.
    /// </summary>
    /// <example>
    /// <code>
    /// var entrada = await empresa.Registros.Gravar(obra, registro => registro.MarcarOnLine(identificacao, Coletor.DispositivoEletronico));
    /// </code>
    /// </example>
    public Task<T> Gravar<T>(Guid estabelecimento, Func<RegistroDePonto, T> operacao, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(operacao);

        return _empresa.Transacao(
            async sessao =>
            {
                await sessao.EntrarNaFila(estabelecimento);
                var registro = await Retomar(sessao, estabelecimento);
                var resultado = operacao(registro);
                await Inserir(sessao, estabelecimento, registro.Operacoes);

                return resultado;
            },
            cancelamento);
    }

    /// <summary>As operações a partir do NSR dado, em ordem, conferidas: para o Arquivo Fonte de Dados e para quem audita.</summary>
    public Task<IReadOnlyList<Operacao>> Operacoes(Guid estabelecimento, long aPartirDoNsr = 1, int quantas = 1000, CancellationToken cancelamento = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(aPartirDoNsr, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(quantas, 1);

        return _empresa.Transacao(
            async sessao =>
            {
                await _empresa.Estabelecimentos.Obter(sessao, estabelecimento);

                return await Pagina(sessao, estabelecimento, aPartirDoNsr, quantas);
            },
            cancelamento);
    }

    /// <summary>As operações a partir do NSR, em ordem e conferidas, dentro de uma transação já aberta.</summary>
    internal static async Task<IReadOnlyList<Operacao>> Pagina(Sessao sessao, Guid estabelecimento, long aPartirDoNsr, int quantas)
    {
        var operacoes = await sessao.Ler(LerOperacoes, OperacoesNoBanco.Ler, estabelecimento, aPartirDoNsr, quantas);
        ConferirASequencia(operacoes, aPartirDoNsr);

        return operacoes;
    }

    /// <summary>O comprovante de uma marcação gravada (art. 79), com o nome que o trabalhador tinha quando marcou.</summary>
    public Task<ComprovanteDeRegistro> Comprovante(Guid estabelecimento, long nsr, string registroNoInpi, CancellationToken cancelamento = default) =>
        _empresa.Transacao(
            async sessao =>
            {
                var local = await _empresa.Estabelecimentos.Obter(sessao, estabelecimento);
                var marcacao = await sessao.LerUma(LerOperacao, OperacoesNoBanco.Ler, estabelecimento, nsr) as MarcacaoDePonto
                    ?? throw new NaoEncontrado(string.Create(CultureInfo.InvariantCulture, $"O NSR {nsr} não é de uma marcação deste registro de ponto."));

                var nome = await sessao.LerUma(NomeAntesDe, linha => linha.GetString(0), estabelecimento, marcacao.Cpf.Numero, nsr)
                    ?? throw new ConteudoAdulterado(string.Create(CultureInfo.InvariantCulture, $"A marcação do NSR {nsr} é de um trabalhador que nunca foi incluído no registro."));

                return new ComprovanteDeRegistro(local, nome, marcacao, registroNoInpi);
            },
            cancelamento);

    /// <summary>O registro retomado pela ponta, para acrescentar; recusado se não foi aberto.</summary>
    internal async Task<RegistroDePonto> Retomar(Sessao sessao, Guid estabelecimento)
    {
        var local = await _empresa.Estabelecimentos.Obter(sessao, estabelecimento);
        var ponta = await Ponta(sessao, estabelecimento)
            ?? throw new NaoEncontrado($"O registro de ponto do estabelecimento {estabelecimento} ainda não foi aberto.");

        return Leitura.Validada(() => RegistroDePonto.Retomar(local, ponta), "A ponta do registro de ponto");
    }

    /// <summary>As marcações do trabalhador no registro, em ordem de NSR, e se ele foi incluído alguma vez.</summary>
    internal static async Task<(bool Incluido, IReadOnlyList<MarcacaoDePonto> Marcacoes)> DoTrabalhador(Sessao sessao, Guid estabelecimento, Cpf trabalhador)
    {
        var incluido = await sessao.Ler(FoiIncluido, linha => linha.GetBoolean(0), estabelecimento, trabalhador.Numero);
        var marcacoes = await sessao.Ler(LerMarcacoesDoTrabalhador, linha => (MarcacaoDePonto)OperacoesNoBanco.Ler(linha), estabelecimento, trabalhador.Numero);

        return (incluido[0], marcacoes);
    }

    /// <summary>A ponta do registro, lida do que está gravado; nula se o registro não foi aberto.</summary>
    internal static async Task<PontaDoRegistro?> Ponta(Sessao sessao, Guid estabelecimento)
    {
        var ultima = await sessao.LerUma(
            UltimaOperacao,
            linha => new UltimaGravada(
                linha.GetInt64(0),
                linha.GetInt16(1) == 4 ? Instantes.Ler(linha, 4, 5) : Instantes.Ler(linha, 2, 3)),
            estabelecimento);

        if (ultima is null)
        {
            return null;
        }

        var hash = await sessao.LerUma(UltimoHash, linha => linha.GetString(0), estabelecimento);
        var incluidos = await sessao.Ler(
            Incluidos,
            linha => Leitura.Validada(
                () => new VinculoNaPonta(Cpf.De(linha.GetString(0)), linha.GetString(1), Instantes.Ler(linha, 2, 3)),
                "Um trabalhador incluído no registro"),
            estabelecimento);

        return new PontaDoRegistro(ultima.Nsr, ultima.Relogio, hash, incluidos);
    }

    /// <summary>
    /// A sequência lida tem de ser a que as restrições do banco garantem: os
    /// NSRs seguidos, e cada marcação amarrada à de antes. Quem passa por cima
    /// delas — o superusuário, com os gatilhos e as chaves estrangeiras
    /// desligados — ainda é pego aqui.
    /// </summary>
    private static void ConferirASequencia(IReadOnlyList<Operacao> operacoes, long aPartirDoNsr)
    {
        // A partir do NSR 1, a primeira marcação não tem anterior; no meio, a
        // anterior da primeira lida ficou antes da página.
        var anteriorConhecida = aPartirDoNsr == 1;
        string? anterior = null;

        for (var i = 0; i < operacoes.Count; i++)
        {
            if (operacoes[i].Nsr != aPartirDoNsr + i)
            {
                throw new ConteudoAdulterado(string.Create(
                    CultureInfo.InvariantCulture,
                    $"Falta o NSR {aPartirDoNsr + i} no banco: a numeração do registro tem um buraco."));
            }

            if (operacoes[i] is not MarcacaoDePonto marcacao)
            {
                continue;
            }

            if (anteriorConhecida && marcacao.HashAnterior != anterior)
            {
                throw new ConteudoAdulterado(string.Create(
                    CultureInfo.InvariantCulture,
                    $"A cadeia se rompe no NSR {marcacao.Nsr}: o hash anterior gravado nele não é o da marcação de antes, que foi alterada, incluída ou removida."));
            }

            anterior = marcacao.Hash;
            anteriorConhecida = true;
        }
    }

    private async Task Inserir(Sessao sessao, Guid estabelecimento, IReadOnlyList<Operacao> operacoes)
    {
        foreach (var operacao in operacoes)
        {
            try
            {
                await sessao.Executar(OperacoesNoBanco.Inserir, OperacoesNoBanco.Valores(_empresa.Id, estabelecimento, operacao));
            }
            catch (PostgresException erro) when (erro is { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "fk_operacoes_trabalhadores" })
            {
                var cpf = operacao switch
                {
                    MudancaNoTrabalhador mudanca => mudanca.Cpf,
                    MarcacaoDePonto marcacao => marcacao.Cpf,
                    _ => null,
                };

                throw new NaoEncontrado($"O trabalhador {cpf} não está cadastrado nesta empresa: cadastre-o antes de incluí-lo no registro de ponto.", erro);
            }
        }
    }

    private sealed record UltimaGravada(long Nsr, DateTimeOffset Relogio);
}
