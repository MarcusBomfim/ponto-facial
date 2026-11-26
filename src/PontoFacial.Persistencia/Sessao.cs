using Npgsql;

namespace PontoFacial.Persistencia;

/// <summary>
/// Uma transação aberta em nome de uma empresa: os comandos, as leituras e a
/// fila do estabelecimento. Só existe dentro de <see cref="EmpresaNoBanco"/>.
/// </summary>
/// <remarks>
/// Os parâmetros vão por posição — <c>$1</c>, <c>$2</c> —, e o texto do SQL é
/// sempre constante: nada do que vem de fora entra no comando como texto.
/// </remarks>
internal sealed class Sessao(NpgsqlConnection conexao, NpgsqlTransaction transacao, CancellationToken cancelamento)
{
    private const string FilaDoEstabelecimento = "select pg_advisory_xact_lock(hashtextextended('ponto.estabelecimento:' || $1::text, 0))";

    public CancellationToken Cancelamento => cancelamento;

    public async Task<int> Executar(string sql, params object?[] valores)
    {
        await using var comando = Comando(sql, valores);

        return await comando.ExecuteNonQueryAsync(cancelamento);
    }

    public async Task<IReadOnlyList<T>> Ler<T>(string sql, Func<NpgsqlDataReader, T> linha, params object?[] valores)
    {
        await using var comando = Comando(sql, valores);
        await using var leitor = await comando.ExecuteReaderAsync(cancelamento);

        var linhas = new List<T>();

        while (await leitor.ReadAsync(cancelamento))
        {
            linhas.Add(linha(leitor));
        }

        return linhas;
    }

    public async Task<T?> LerUma<T>(string sql, Func<NpgsqlDataReader, T> linha, params object?[] valores)
        where T : class
    {
        var linhas = await Ler(sql, linha, valores);

        return linhas.Count == 0 ? null : linhas[0];
    }

    /// <summary>
    /// Entra na fila do estabelecimento: até o fim da transação, ninguém mais
    /// grava no registro nem no tratamento dele. É o que mantém a numeração e
    /// o relógio em ordem com dois coletores marcando ao mesmo tempo.
    /// </summary>
    public Task<int> EntrarNaFila(Guid estabelecimento) => Executar(FilaDoEstabelecimento, estabelecimento);

    private NpgsqlCommand Comando(string sql, object?[] valores)
    {
        var comando = new NpgsqlCommand(sql, conexao, transacao);

        foreach (var valor in valores)
        {
            comando.Parameters.Add(new NpgsqlParameter { Value = valor ?? DBNull.Value });
        }

        return comando;
    }
}
