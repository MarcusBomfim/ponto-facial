using Npgsql;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes;
using PontoFacial.Persistencia.Testes.Registro;

namespace PontoFacial.Persistencia.Testes;

/*
 * Cada empresa só vê o que é dela. Os testes passam pelos repositórios e
 * também por SQL escrito à mão, com o usuário da aplicação: a segurança por
 * linha não depende de quem escreve o SQL lembrar do where (ADR-0012).
 */
public sealed class IsolamentoEntreEmpresasTests(BancoDeTestes banco)
{
    [Fact]
    public async Task Uma_empresa_nao_encontra_o_estabelecimento_nem_o_registro_de_outra()
    {
        var (horizonte, empregadorDaHorizonte) = await banco.NovaEmpresa();
        var (aurora, _) = await banco.NovaEmpresa("Construtora Aurora Ltda.");
        var obra = await Copia.DoRegistro(horizonte, RegistroVariado.Montar(empregadorDaHorizonte), Exemplos.Joao, Exemplos.Maria);

        var estabelecimento = await Assert.ThrowsAsync<NaoEncontrado>(() => aurora.Estabelecimentos.Obter(obra, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NaoEncontrado>(() => aurora.Registros.Operacoes(obra, cancelamento: TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NaoEncontrado>(() => aurora.Registros.Gravar(
            obra,
            registro => registro.RegistrarEvento(EventoSensivel.RetornoDeEnergia, Exemplos.Em(23, 19, 0)),
            TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NaoEncontrado>(() => aurora.Tratamentos.Obter(obra, Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NaoEncontrado>(() => aurora.Trabalhadores.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));

        Assert.Equal($"O estabelecimento {obra} não está cadastrado nesta empresa.", estabelecimento.Message);
        Assert.Equal(12, (await horizonte.Registros.Operacoes(obra, cancelamento: TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task O_sql_sem_where_so_ve_a_empresa_da_transacao()
    {
        var (horizonte, empregadorDaHorizonte) = await banco.NovaEmpresa();
        var (aurora, empregadorDaAurora) = await banco.NovaEmpresa("Construtora Aurora Ltda.");
        await Copia.DoRegistro(horizonte, RegistroVariado.Montar(empregadorDaHorizonte), Exemplos.Joao, Exemplos.Maria);
        await Copia.DoRegistro(aurora, RegistroVariado.Montar(empregadorDaAurora), Exemplos.Joao, Exemplos.Maria);

        const string Empresas = "select string_agg(distinct empregador_id::text, ',') from ponto.operacoes";

        Assert.Equal(horizonte.Id.ToString(), await banco.ComoAplicacao(horizonte.Id, Empresas));
        Assert.Equal(aurora.Id.ToString(), await banco.ComoAplicacao(aurora.Id, Empresas));
        Assert.Equal(2L, await banco.ComoAplicacao(horizonte.Id, "select count(*) from ponto.trabalhadores"));
        Assert.Equal(1L, await banco.ComoAplicacao(aurora.Id, "select count(*) from ponto.empregadores"));
    }

    [Fact]
    public async Task Sem_empresa_definida_a_aplicacao_nao_ve_nada()
    {
        var (horizonte, empregador) = await banco.NovaEmpresa();
        await Copia.DoRegistro(horizonte, RegistroVariado.Montar(empregador), Exemplos.Joao, Exemplos.Maria);

        foreach (var tabela in new[] { "empregadores", "estabelecimentos", "trabalhadores", "operacoes", "tratamentos" })
        {
            Assert.Equal(0L, await banco.ComoAplicacao(null, $"select count(*) from ponto.{tabela}"));
        }
    }

    [Fact]
    public async Task A_aplicacao_nao_grava_em_nome_de_outra_empresa()
    {
        var (horizonte, _) = await banco.NovaEmpresa();
        var (aurora, _) = await banco.NovaEmpresa("Construtora Aurora Ltda.");

        // Em nome da Aurora, um trabalhador para a Horizonte.
        var erro = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoAplicacao(
            aurora.Id,
            "insert into ponto.trabalhadores (empregador_id, cpf, nome, admissao, cargo) values ($1, '52998224725', 'João da Silva', '2026-03-02', 'Pedreiro')",
            horizonte.Id));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, erro.SqlState);
        Assert.Contains("row-level security", erro.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_empresa_nao_passa_de_um_pedido_para_o_seguinte_na_mesma_conexao()
    {
        // Sem o reset que o Npgsql faz ao devolver a conexão ao pool, que muita
        // gente desliga por desempenho: o que protege é o parâmetro ser local à
        // transação, e não o reset.
        var conexao = new NpgsqlConnectionStringBuilder(banco.Aplicacao.ConnectionString)
        {
            Password = BancoDeTestes.SenhaDaAplicacao,
            MaxPoolSize = 1,
            NoResetOnClose = true,
        };
        await using var fonte = NpgsqlDataSource.Create(conexao.ConnectionString);
        var (horizonte, _) = await banco.NovaEmpresa();

        // A mesma conexão física — o pool só tem uma —, antes em nome da Horizonte.
        var empregador = await new BancoDoPonto(fonte).Empresa(horizonte.Id).Empregador(TestContext.Current.CancellationToken);
        await using var depois = await fonte.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var consulta = new NpgsqlCommand("select coalesce(current_setting('ponto.empregador_id', true), ''), (select count(*) from ponto.empregadores)", depois);
        await using var leitor = await consulta.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        await leitor.ReadAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(empregador);
        Assert.Equal(string.Empty, leitor.GetString(0));
        Assert.Equal(0L, leitor.GetInt64(1));
    }
}
