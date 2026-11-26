using DotNet.Testcontainers.Builders;
using Npgsql;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Persistencia.Migracoes;
using PontoFacial.Persistencia.Testes;
using Testcontainers.PostgreSql;

// Um banco para o assembly de testes inteiro: sobe uma vez, antes do
// primeiro teste que o pede.
[assembly: AssemblyFixture(typeof(BancoDeTestes))]

namespace PontoFacial.Persistencia.Testes;

/// <summary>
/// Um PostgreSQL de verdade, num contêiner, para o assembly de testes
/// inteiro: sobe uma vez, recebe as migrações, e cada teste trabalha com uma
/// empresa própria, sem precisar limpar o banco.
/// </summary>
/// <remarks>
/// <para>
/// PostgreSQL de verdade, e não um banco em memória: o que se testa aqui é o
/// que só o PostgreSQL faz — as restrições que refazem o hash, a chave
/// estrangeira que não deixa buraco no NSR, a segurança por linha, os
/// gatilhos que recusam o update (ADR-0012 e ADR-0013).
/// </para>
/// <para>
/// Duas fontes de conexão. A do dono do banco, superusuário, aplica as
/// migrações e serve aos testes que examinam o banco por dentro — e que
/// tentam fazer o que a aplicação não pode. A da aplicação entra com um
/// usuário membro de <c>ponto_aplicacao</c>, como em produção: é ela que a
/// segurança por linha e as permissões alcançam.
/// </para>
/// <para>
/// Sem Docker na máquina de quem desenvolve, os testes que precisam do banco
/// são pulados, com o motivo. No CI não: lá, Docker indisponível é falha —
/// teste de integração que some sem ninguém ver é pior do que teste que
/// falha.
/// </para>
/// </remarks>
public sealed class BancoDeTestes : IAsyncLifetime
{
    /// <summary>Versão fixa: o banco dos testes não muda sozinho de uma execução para outra.</summary>
    public const string Imagem = "postgres:17.6-alpine";

    /// <summary>O usuário da aplicação nos testes: entra no banco, e tem só o que o papel ponto_aplicacao tem.</summary>
    public const string UsuarioDaAplicacao = "aplicacao_nos_testes";

    public const string SenhaDaAplicacao = "aplicacao-nos-testes";

    private static int _documentos;

    private PostgreSqlContainer? _conteiner;

    private NpgsqlDataSource? _dono;

    private NpgsqlDataSource? _aplicacao;

    private string? _indisponivel;

    private static bool EmIntegracaoContinua =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>A fonte do dono do banco — superusuário, que passa por cima da segurança por linha. Pula o teste se não houver banco.</summary>
    public NpgsqlDataSource Dono
    {
        get
        {
            Assert.SkipWhen(_dono is null, _indisponivel ?? "O banco de testes não subiu.");

            return _dono!;
        }
    }

    /// <summary>A fonte da aplicação, com o usuário que a segurança por linha e as permissões alcançam.</summary>
    public NpgsqlDataSource Aplicacao
    {
        get
        {
            Assert.SkipWhen(_aplicacao is null, _indisponivel ?? "O banco de testes não subiu.");

            return _aplicacao!;
        }
    }

    /// <summary>O banco do ponto como a aplicação o usa.</summary>
    public BancoDoPonto Banco => new(Aplicacao);

    public async ValueTask InitializeAsync()
    {
        try
        {
            _conteiner = new PostgreSqlBuilder(Imagem).Build();
            await _conteiner.StartAsync();
        }
        catch (DockerUnavailableException erro) when (!EmIntegracaoContinua)
        {
            _indisponivel = $"Docker indisponível nesta máquina; os testes com PostgreSQL foram pulados. ({erro.Message})";

            return;
        }

        var conexao = _conteiner.GetConnectionString();
        _dono = NpgsqlDataSource.Create(conexao);

        await Migrador.Aplicar(_dono);
        await CriarUsuarioDaAplicacao(_dono);

        _aplicacao = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(conexao)
        {
            Username = UsuarioDaAplicacao,
            Password = SenhaDaAplicacao,
        }.ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        if (_aplicacao is not null)
        {
            await _aplicacao.DisposeAsync();
        }

        if (_dono is not null)
        {
            await _dono.DisposeAsync();
        }

        if (_conteiner is not null)
        {
            await _conteiner.DisposeAsync();
        }
    }

    /// <summary>
    /// Uma empresa nova, com um CNPJ que nenhum outro teste usa — os testes
    /// rodam em paralelo no mesmo banco, e o CNPJ não se repete.
    /// </summary>
    public async Task<(EmpresaNoBanco Empresa, Empregador Empregador)> NovaEmpresa(string nome = "Construtora Horizonte Ltda.")
    {
        var empregador = Empregador.Novo(CnpjNovo(), nome);

        return (await Banco.Cadastrar(empregador), empregador);
    }

    /// <summary>Um CNPJ válido e novo a cada chamada: a raiz sai de um contador, e os verificadores, da conta do módulo 11.</summary>
    public static Cnpj CnpjNovo()
    {
        var raiz = string.Concat(
            (Environment.ProcessId % 1000).ToString("D3", System.Globalization.CultureInfo.InvariantCulture),
            Interlocked.Increment(ref _documentos).ToString("D5", System.Globalization.CultureInfo.InvariantCulture),
            "0001");

        var primeiro = Verificador(raiz);
        var segundo = Verificador(raiz + primeiro);

        return Cnpj.De(string.Concat(raiz, primeiro, segundo));
    }

    /// <summary>Executa o SQL como o dono do banco, e devolve as linhas afetadas.</summary>
    public async Task<int> ComoDono(string sql, params object?[] valores)
    {
        await using var comando = Dono.CreateCommand(sql);

        foreach (var valor in valores)
        {
            comando.Parameters.Add(new NpgsqlParameter { Value = valor ?? DBNull.Value });
        }

        return await comando.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Executa o SQL como a aplicação, numa transação em nome da empresa
    /// dada — ou de nenhuma, com <see langword="null"/> —, e devolve a
    /// primeira coluna da primeira linha.
    /// </summary>
    public async Task<object?> ComoAplicacao(Guid? empresa, string sql, params object?[] valores)
    {
        await using var conexao = await Aplicacao.OpenConnectionAsync();
        await using var transacao = await conexao.BeginTransactionAsync();

        if (empresa is { } id)
        {
            await using var definir = new NpgsqlCommand("select set_config('ponto.empregador_id', $1, true)", conexao, transacao);
            definir.Parameters.Add(new NpgsqlParameter { Value = id.ToString() });
            await definir.ExecuteNonQueryAsync();
        }

        await using var comando = new NpgsqlCommand(sql, conexao, transacao);

        foreach (var valor in valores)
        {
            comando.Parameters.Add(new NpgsqlParameter { Value = valor ?? DBNull.Value });
        }

        var resultado = await comando.ExecuteScalarAsync();
        await transacao.CommitAsync();

        return resultado;
    }

    private static char Verificador(string caracteres)
    {
        var soma = 0;
        var peso = 2;

        for (var posicao = caracteres.Length - 1; posicao >= 0; posicao--)
        {
            soma += (caracteres[posicao] - '0') * peso;
            peso = peso == 9 ? 2 : peso + 1;
        }

        var resto = soma % 11;

        return resto < 2 ? '0' : (char)('0' + (11 - resto));
    }

    /// <summary>O usuário como quem implanta o criaria: com login, e só com o que o papel da aplicação tem.</summary>
    private static async Task CriarUsuarioDaAplicacao(NpgsqlDataSource dono)
    {
        await using var comando = dono.CreateCommand(
            $"""
            create role {UsuarioDaAplicacao} login password '{SenhaDaAplicacao}';
            grant ponto_aplicacao to {UsuarioDaAplicacao};
            """);
        await comando.ExecuteNonQueryAsync();
    }
}
