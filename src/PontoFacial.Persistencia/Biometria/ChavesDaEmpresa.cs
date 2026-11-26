using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace PontoFacial.Persistencia.Biometria;

/// <summary>
/// A chave de cifra da empresa, em versões: a mais nova cifra os modelos do
/// rosto; as outras só decifram o que ainda não foi cifrado de novo. Cada
/// versão fica no banco embrulhada pela chave mestra (ADR-0014).
/// </summary>
/// <remarks>
/// A primeira versão nasce sozinha, no primeiro modelo cifrado. Girar a
/// chave, cifrar de novo e descartar a antiga são operações da manutenção:
/// trocam a chave de uma empresa sem mexer nas outras.
/// </remarks>
public sealed class ChavesDaEmpresa
{
    private const string FilaDasChaves = "select pg_advisory_xact_lock(hashtextextended('ponto.chaves:' || $1::text, 0))";

    private const string LerAtual = "select versao, chave_mestra, chave_embrulhada from ponto.chaves_das_empresas order by versao desc limit 1";

    private const string LerVersao = "select versao, chave_mestra, chave_embrulhada from ponto.chaves_das_empresas where versao = $1";

    private const string LerDeOutraChaveMestra = "select versao, chave_mestra, chave_embrulhada from ponto.chaves_das_empresas where chave_mestra <> $1 order by versao";

    private const string Inserir = """
        insert into ponto.chaves_das_empresas (empregador_id, versao, chave_mestra, chave_embrulhada)
        values ($1, $2, $3, $4)
        on conflict (empregador_id, versao) do nothing
        """;

    private const string TrocarOEmbrulho = "update ponto.chaves_das_empresas set chave_mestra = $2, chave_embrulhada = $3 where versao = $1";

    private const string Descartar = """
        delete from ponto.chaves_das_empresas chave
        where chave.versao < (select max(versao) from ponto.chaves_das_empresas)
          and not exists (select from ponto.cadastros_faciais cadastro where cadastro.versao_da_chave = chave.versao)
        """;

    private readonly EmpresaNoBanco _empresa;

    private readonly IChaveMestra _mestra;

    private readonly IReadOnlyList<IChaveMestra> _anteriores;

    internal ChavesDaEmpresa(EmpresaNoBanco empresa, IChaveMestra mestra, IReadOnlyList<IChaveMestra> anteriores)
    {
        _empresa = empresa;
        _mestra = mestra;
        _anteriores = anteriores;
    }

    /// <summary>Cria a versão seguinte da chave, que passa a cifrar os modelos novos. Devolve o número dela.</summary>
    public Task<int> Girar(CancellationToken cancelamento = default) =>
        _empresa.Transacao(
            async sessao =>
            {
                await sessao.Executar(FilaDasChaves, _empresa.Id);
                var atual = await sessao.LerUma(LerAtual, Linha.De);
                var versao = (atual?.Versao ?? 0) + 1;
                await Criar(sessao, versao);

                return versao;
            },
            cancelamento);

    /// <summary>
    /// Embrulha de novo, com a chave mestra atual, as versões embrulhadas por
    /// uma anterior: é o que se faz ao trocar a chave mestra. Os modelos do
    /// rosto não mudam. Devolve quantas versões foram embrulhadas de novo.
    /// </summary>
    public Task<int> EmbrulharDeNovo(CancellationToken cancelamento = default) =>
        _empresa.Transacao(
            async sessao =>
            {
                await sessao.Executar(FilaDasChaves, _empresa.Id);
                var antigas = await sessao.Ler(LerDeOutraChaveMestra, Linha.De, _mestra.Id);

                foreach (var antiga in antigas)
                {
                    var chave = await Desembrulhar(antiga, sessao.Cancelamento);

                    try
                    {
                        var embrulhada = await _mestra.Embrulhar(chave, Contexto(antiga.Versao), sessao.Cancelamento);
                        await sessao.Executar(TrocarOEmbrulho, antiga.Versao, _mestra.Id, embrulhada);
                    }
                    finally
                    {
                        CryptographicOperations.ZeroMemory(chave);
                    }
                }

                return antigas.Count;
            },
            cancelamento);

    /// <summary>
    /// Apaga as versões antigas que nenhum modelo usa mais. Depois de cifrar
    /// tudo de novo com a atual, nenhuma usa, e a chave antiga deixa de
    /// existir. Devolve quantas foram apagadas.
    /// </summary>
    public Task<int> DescartarAsAntigas(CancellationToken cancelamento = default) =>
        _empresa.Transacao(
            async sessao =>
            {
                await sessao.Executar(FilaDasChaves, _empresa.Id);

                return await sessao.Executar(Descartar);
            },
            cancelamento);

    /// <summary>A versão mais nova, aberta; cria a primeira se a empresa ainda não tem chave.</summary>
    internal async Task<ChaveAberta> Atual(Sessao sessao)
    {
        var atual = await sessao.LerUma(LerAtual, Linha.De);

        if (atual is null)
        {
            // Duas transações podem criar a primeira ao mesmo tempo: uma
            // grava, a outra não grava nada, e as duas leem a que ficou.
            await Criar(sessao, 1);
            atual = await sessao.LerUma(LerAtual, Linha.De);
        }

        return new ChaveAberta(atual!.Versao, await Desembrulhar(atual, sessao.Cancelamento));
    }

    /// <summary>A versão dada, aberta, para decifrar o que ela cifrou.</summary>
    internal async Task<ChaveAberta> DaVersao(Sessao sessao, int versao)
    {
        var linha = await sessao.LerUma(LerVersao, Linha.De, versao)
            ?? throw new ConteudoAdulterado(string.Create(CultureInfo.InvariantCulture, $"A versão {versao} da chave da empresa não está no banco."));

        return new ChaveAberta(linha.Versao, await Desembrulhar(linha, sessao.Cancelamento));
    }

    private async Task Criar(Sessao sessao, int versao)
    {
        var chave = RandomNumberGenerator.GetBytes(Cifra.TamanhoDaChave);

        try
        {
            var embrulhada = await _mestra.Embrulhar(chave, Contexto(versao), sessao.Cancelamento);
            await sessao.Executar(Inserir, _empresa.Id, versao, _mestra.Id, embrulhada);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(chave);
        }
    }

    private async Task<byte[]> Desembrulhar(Linha linha, CancellationToken cancelamento)
    {
        var mestra = (linha.ChaveMestra == _mestra.Id ? _mestra : _anteriores.FirstOrDefault(anterior => anterior.Id == linha.ChaveMestra))
            ?? throw new InvalidOperationException(string.Create(
                CultureInfo.InvariantCulture,
                $"A versão {linha.Versao} da chave da empresa foi embrulhada pela chave mestra \"{linha.ChaveMestra}\", que esta instalação não tem."));

        try
        {
            return await mestra.Desembrulhar(linha.Embrulhada, Contexto(linha.Versao), cancelamento);
        }
        catch (AuthenticationTagMismatchException erro)
        {
            throw new ConteudoAdulterado(
                string.Create(CultureInfo.InvariantCulture, $"A versão {linha.Versao} da chave da empresa não desembrulha: o embrulho foi mexido, ou é de outra empresa."),
                erro);
        }
    }

    /// <summary>O contexto do embrulho: a chave só desembrulha como a desta empresa, nesta versão.</summary>
    private byte[] Contexto(int versao) =>
        Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"ponto-facial/chave-da-empresa/v1|{_empresa.Id:D}|{versao}"));

    private sealed record Linha(int Versao, string ChaveMestra, byte[] Embrulhada)
    {
        public static Linha De(NpgsqlDataReader linha) => new(linha.GetInt32(0), linha.GetString(1), linha.GetFieldValue<byte[]>(2));
    }
}

/// <summary>Uma versão da chave da empresa, aberta, só pelo tempo de cifrar ou decifrar: ao descartar, os bytes são zerados.</summary>
internal sealed class ChaveAberta(int versao, byte[] chave) : IDisposable
{
    public int Versao { get; } = versao;

    public ReadOnlySpan<byte> Bytes => chave;

    public void Dispose() => CryptographicOperations.ZeroMemory(chave);
}
