using Npgsql;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Persistencia.Biometria;

/// <summary>
/// As autorizações biométricas dos trabalhadores da empresa, com o termo que
/// cada um leu. A encerrada fica: é a prova do consentimento que valeu
/// (LGPD, art. 8º, § 2º).
/// </summary>
/// <remarks>
/// Encerrar a autorização e apagar o modelo do rosto acontecem na mesma
/// transação: o domínio pede a eliminação (<see cref="ModeloFacialDeveSerEliminado"/>),
/// e o pedido é atendido antes do commit. Não há um instante em que a
/// autorização acabou e o vetor ainda está no banco — e o banco recusa a
/// autorização que acaba com o vetor ainda lá (ADR-0014).
/// </remarks>
public sealed class AutorizacoesBiometricas
{
    private const string InserirTermo = """
        insert into ponto.termos_de_consentimento (empregador_id, hash, versao, texto)
        values ($1, $2, $3, $4)
        on conflict (empregador_id, hash) do nothing
        """;

    private const string Inserir = """
        insert into ponto.autorizacoes_biometricas (id, empregador_id, cpf, termo_hash, concedida_em, concedida_em_deslocamento)
        values ($1, $2, $3, $4, $5, $6)
        """;

    private const string Colunas = """
        autorizacao.id, autorizacao.cpf, autorizacao.concedida_em, autorizacao.concedida_em_deslocamento,
        autorizacao.encerrada_em, autorizacao.encerrada_em_deslocamento, autorizacao.motivo,
        termo.versao, termo.texto, termo.hash
        """;

    private const string DaAutorizacao = """
        from ponto.autorizacoes_biometricas autorizacao
        join ponto.termos_de_consentimento termo on termo.empregador_id = autorizacao.empregador_id and termo.hash = autorizacao.termo_hash
        """;

    private const string LerVigente = $"select {Colunas} {DaAutorizacao} where autorizacao.cpf = $1 and autorizacao.encerrada_em is null";

    private const string LerVigenteParaEncerrar = $"select {Colunas} {DaAutorizacao} where autorizacao.cpf = $1 and autorizacao.encerrada_em is null for update of autorizacao";

    private const string LerDoTrabalhador = $"select {Colunas} {DaAutorizacao} where autorizacao.cpf = $1 order by autorizacao.concedida_em, autorizacao.id";

    private const string Encerramento = """
        update ponto.autorizacoes_biometricas
        set encerrada_em = $2, encerrada_em_deslocamento = $3, motivo = $4
        where id = $1
        """;

    private readonly EmpresaNoBanco _empresa;

    internal AutorizacoesBiometricas(EmpresaNoBanco empresa) => _empresa = empresa;

    /// <summary>Grava a autorização recém-concedida, com o termo. O trabalhador só tem uma vigente de cada vez.</summary>
    public Task Conceder(AutorizacaoBiometrica autorizacao, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(autorizacao);

        if (autorizacao.EncerradaEm is not null)
        {
            throw new ArgumentException("Só se grava como concedida a autorização que ainda vale.", nameof(autorizacao));
        }

        return _empresa.Transacao(
            async sessao =>
            {
                var empregador = await _empresa.Empregador(sessao);

                if (empregador.Documento != autorizacao.Empregador)
                {
                    throw new InvalidOperationException($"A autorização é para o empregador {autorizacao.Empregador}, e esta empresa é o {empregador.Documento}.");
                }

                await sessao.Executar(InserirTermo, _empresa.Id, autorizacao.Termo.Hash, autorizacao.Termo.Versao, autorizacao.Termo.Texto);

                try
                {
                    return await sessao.Executar(
                        Inserir,
                        Guid.CreateVersion7(),
                        _empresa.Id,
                        autorizacao.Trabalhador.Numero,
                        autorizacao.Termo.Hash,
                        Instantes.Utc(autorizacao.ConcedidaEm),
                        Instantes.Deslocamento(autorizacao.ConcedidaEm));
                }
                catch (PostgresException erro) when (erro is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_autorizacoes_biometricas_vigente" })
                {
                    throw new JaExiste($"O trabalhador {autorizacao.Trabalhador} já tem uma autorização biométrica vigente.", erro);
                }
                catch (PostgresException erro) when (erro is { SqlState: PostgresErrorCodes.ForeignKeyViolation, ConstraintName: "fk_autorizacoes_biometricas_trabalhadores" })
                {
                    throw new NaoEncontrado($"O trabalhador {autorizacao.Trabalhador} não está cadastrado nesta empresa.", erro);
                }
            },
            cancelamento);
    }

    /// <summary>A autorização que vale agora para o trabalhador; nula se ele não tem.</summary>
    public Task<AutorizacaoBiometrica?> Vigente(Cpf trabalhador, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);

        return _empresa.Transacao(
            async sessao =>
            {
                var empregador = await _empresa.Empregador(sessao);

                return await sessao.LerUma(LerVigente, linha => Ler(linha, empregador.Documento).Autorizacao, trabalhador.Numero);
            },
            cancelamento);
    }

    /// <summary>Todas as autorizações do trabalhador, vigente e encerradas, na ordem em que foram dadas.</summary>
    public Task<IReadOnlyList<AutorizacaoBiometrica>> DoTrabalhador(Cpf trabalhador, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);

        return _empresa.Transacao(
            async sessao =>
            {
                var empregador = await _empresa.Empregador(sessao);

                return await sessao.Ler(LerDoTrabalhador, linha => Ler(linha, empregador.Documento).Autorizacao, trabalhador.Numero);
            },
            cancelamento);
    }

    /// <summary>
    /// Encerra a autorização vigente do trabalhador — pela revogação ou pelo
    /// desligamento — e atende, na mesma transação, o pedido de eliminação do
    /// modelo do rosto. Devolve os pedidos atendidos.
    /// </summary>
    /// <example>
    /// <code>
    /// await biometria.Autorizacoes.Encerrar(joao.Cpf, autorizacao => autorizacao.Revogar(agora));
    /// </code>
    /// </example>
    public Task<IReadOnlyList<ModeloFacialDeveSerEliminado>> Encerrar(Cpf trabalhador, Action<AutorizacaoBiometrica> encerrar, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);
        ArgumentNullException.ThrowIfNull(encerrar);

        return _empresa.Transacao<IReadOnlyList<ModeloFacialDeveSerEliminado>>(
            async sessao =>
            {
                var empregador = await _empresa.Empregador(sessao);
                var (id, autorizacao) = await sessao.LerUma(LerVigenteParaEncerrar, linha => Ler(linha, empregador.Documento), trabalhador.Numero)
                    ?? throw new NaoEncontrado($"O trabalhador {trabalhador} não tem autorização biométrica vigente.");

                encerrar(autorizacao);

                if (autorizacao.EncerradaEm is not { } encerradaEm)
                {
                    return [];
                }

                // Primeiro o modelo sai; depois a autorização acaba. Na ordem
                // inversa, o banco recusa: a autorização não acaba com o
                // modelo ainda apontando para ela.
                var eliminacoes = autorizacao.Eventos.OfType<ModeloFacialDeveSerEliminado>().ToList();

                foreach (var eliminacao in eliminacoes)
                {
                    await CadastrosFaciais.Apagar(sessao, eliminacao.Trabalhador);
                }

                await sessao.Executar(
                    Encerramento,
                    id,
                    Instantes.Utc(encerradaEm),
                    Instantes.Deslocamento(encerradaEm),
                    (short)autorizacao.Motivo!.Value);

                return eliminacoes;
            },
            cancelamento);
    }

    private static Lida Ler(NpgsqlDataReader linha, Documento empregador) => Leitura.Validada(
        () =>
        {
            var termo = TermoDeConsentimento.Novo(linha.GetString(7), linha.GetString(8));

            if (termo.Hash != linha.GetString(9))
            {
                throw new ArgumentException("O texto do termo não tem o hash gravado.");
            }

            return new Lida(
                linha.GetGuid(0),
                AutorizacaoBiometrica.Reconstituir(
                    empregador,
                    Cpf.De(linha.GetString(1)),
                    termo,
                    Instantes.Ler(linha, 2, 3),
                    Instantes.LerOuNulo(linha, 4, 5),
                    linha.IsDBNull(6) ? null : (MotivoDoEncerramento)linha.GetInt16(6)));
        },
        "A autorização biométrica");

    private sealed record Lida(Guid Id, AutorizacaoBiometrica Autorizacao);
}
