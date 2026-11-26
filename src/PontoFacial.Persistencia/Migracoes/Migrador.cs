using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace PontoFacial.Persistencia.Migracoes;

/// <summary>
/// Leva o banco à versão deste código: aplica, em ordem, as migrações que
/// ainda não foram aplicadas, todas numa transação só.
/// </summary>
/// <remarks>
/// <para>
/// Cada migração é um arquivo <c>.sql</c> embutido no assembly, com o número
/// na frente do nome: <c>0001-cadastro-e-registro-de-ponto.sql</c>. O banco
/// guarda, de cada uma aplicada, o número, o nome e o SHA-256 do texto. Uma
/// migração já aplicada que mudou depois é recusada: o banco foi feito com
/// outro texto, e o que está no repositório já não diz o que ele tem.
/// </para>
/// <para>
/// Roda com o dono do esquema, e não com o papel da aplicação, que não cria
/// tabela nenhuma. Duas instâncias subindo ao mesmo tempo fazem fila no
/// mesmo bloqueio, e a segunda não encontra nada a aplicar.
/// </para>
/// </remarks>
public static class Migrador
{
    private const string Prefixo = "Migracoes.";

    private const string Preparar = """
        select pg_advisory_xact_lock(hashtextextended('ponto.migracoes', 0));
        create schema if not exists ponto;
        create table if not exists ponto.migracoes (
            numero integer not null,
            nome text not null,
            sha256 char(64) not null,
            aplicada_em timestamptz not null default now(),
            constraint pk_migracoes primary key (numero)
        );
        """;

    private const string Aplicadas = "select numero, nome, sha256 from ponto.migracoes order by numero";

    private const string Registrar = "insert into ponto.migracoes (numero, nome, sha256) values ($1, $2, $3)";

    /// <summary>As migrações deste código, em ordem.</summary>
    public static IReadOnlyList<Migracao> Todas { get; } = Carregar();

    /// <summary>Aplica as que faltam e devolve as que aplicou; nenhuma, se o banco já estava em dia.</summary>
    public static async Task<IReadOnlyList<Migracao>> Aplicar(NpgsqlDataSource fonte, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(fonte);

        await using var conexao = await fonte.OpenConnectionAsync(cancelamento);
        await using var transacao = await conexao.BeginTransactionAsync(cancelamento);

        await using (var preparar = new NpgsqlCommand(Preparar, conexao, transacao))
        {
            await preparar.ExecuteNonQueryAsync(cancelamento);
        }

        var aplicadas = new Dictionary<int, (string Nome, string Sha256)>();

        await using (var consulta = new NpgsqlCommand(Aplicadas, conexao, transacao))
        await using (var leitor = await consulta.ExecuteReaderAsync(cancelamento))
        {
            while (await leitor.ReadAsync(cancelamento))
            {
                aplicadas.Add(leitor.GetInt32(0), (leitor.GetString(1), leitor.GetString(2)));
            }
        }

        var novas = new List<Migracao>();

        foreach (var migracao in Todas)
        {
            if (aplicadas.TryGetValue(migracao.Numero, out var aplicada))
            {
                if (aplicada.Sha256 != migracao.Sha256)
                {
                    throw new InvalidOperationException(string.Create(
                        CultureInfo.InvariantCulture,
                        $"A migração {migracao.Numero:D4} ({aplicada.Nome}) mudou depois de aplicada: o banco tem o SHA-256 {aplicada.Sha256}, e o código, {migracao.Sha256}. Migração aplicada não se edita; a correção vai numa migração nova."));
                }

                continue;
            }

            await using (var aplicar = new NpgsqlCommand(migracao.Texto, conexao, transacao))
            {
                await aplicar.ExecuteNonQueryAsync(cancelamento);
            }

            await using (var registrar = new NpgsqlCommand(Registrar, conexao, transacao))
            {
                registrar.Parameters.Add(new NpgsqlParameter { Value = migracao.Numero });
                registrar.Parameters.Add(new NpgsqlParameter { Value = migracao.Nome });
                registrar.Parameters.Add(new NpgsqlParameter { Value = migracao.Sha256 });
                await registrar.ExecuteNonQueryAsync(cancelamento);
            }

            novas.Add(migracao);
        }

        await transacao.CommitAsync(cancelamento);

        return novas;
    }

    private static List<Migracao> Carregar()
    {
        var assembly = typeof(Migrador).Assembly;

        return
        [
            .. assembly
                .GetManifestResourceNames()
                .Where(recurso => recurso.StartsWith(Prefixo, StringComparison.Ordinal) && recurso.EndsWith(".sql", StringComparison.Ordinal))
                .Select(recurso =>
                {
                    using var fluxo = assembly.GetManifestResourceStream(recurso)!;
                    using var leitor = new StreamReader(fluxo, Encoding.UTF8);

                    return Migracao.De(recurso[Prefixo.Length..^".sql".Length], leitor.ReadToEnd());
                })
                .OrderBy(migracao => migracao.Numero),
        ];
    }
}

/// <summary>Uma migração: o número, o nome do arquivo, o texto e o SHA-256 dele.</summary>
public sealed class Migracao
{
    private Migracao(int numero, string nome, string texto, string sha256)
    {
        Numero = numero;
        Nome = nome;
        Texto = texto;
        Sha256 = sha256;
    }

    public int Numero { get; }

    /// <summary>O nome do arquivo, sem a extensão: <c>0001-cadastro-e-registro-de-ponto</c>.</summary>
    public string Nome { get; }

    public string Texto { get; }

    /// <summary>
    /// O SHA-256 do texto em UTF-8, com as quebras de linha como LF: o mesmo
    /// arquivo dá o mesmo hash num checkout do Windows e no do Linux.
    /// </summary>
    public string Sha256 { get; }

    internal static Migracao De(string nome, string texto)
    {
        if (nome.Length < 5 || nome[4] != '-' || !int.TryParse(nome.AsSpan(0, 4), NumberStyles.None, CultureInfo.InvariantCulture, out var numero) || numero < 1)
        {
            throw new InvalidOperationException($"A migração \"{nome}\" não começa com o número de quatro dígitos e um traço: 0001-o-que-ela-faz.sql.");
        }

        var normalizado = texto.Replace("\r\n", "\n", StringComparison.Ordinal);

        return new Migracao(numero, nome, normalizado, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalizado))));
    }
}
