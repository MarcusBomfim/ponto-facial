using Npgsql;
using PontoFacial.Persistencia.Migracoes;

namespace PontoFacial.Persistencia.Testes;

public sealed class MigracoesTests(BancoDeTestes banco)
{
    [Fact]
    public void As_migracoes_tem_numeros_seguidos_a_partir_do_1()
    {
        Assert.Equal(Enumerable.Range(1, Migrador.Todas.Count), Migrador.Todas.Select(migracao => migracao.Numero));
        Assert.Equal("0001-cadastro-e-registro-de-ponto", Migrador.Todas[0].Nome);
    }

    [Fact]
    public void O_hash_da_migracao_nao_depende_da_quebra_de_linha_do_checkout()
    {
        var comLf = Migracao.De("0001-teste", "create table x ();\nselect 1;\n");
        var comCrLf = Migracao.De("0001-teste", "create table x ();\r\nselect 1;\r\n");

        Assert.Equal(comLf.Sha256, comCrLf.Sha256);
    }

    [Fact]
    public void A_migracao_sem_numero_na_frente_e_recusada()
    {
        var erro = Assert.Throws<InvalidOperationException>(() => Migracao.De("cadastro", "select 1;"));

        Assert.Equal("A migração \"cadastro\" não começa com o número de quatro dígitos e um traço: 0001-o-que-ela-faz.sql.", erro.Message);
    }

    [Fact]
    public async Task O_banco_guarda_o_hash_de_cada_migracao_e_aplicar_de_novo_nao_faz_nada()
    {
        Assert.Empty(await Migrador.Aplicar(banco.Dono, TestContext.Current.CancellationToken));

        await using var comando = banco.Dono.CreateCommand("select numero, sha256 from ponto.migracoes order by numero");
        await using var leitor = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var gravadas = new List<(int, string)>();

        while (await leitor.ReadAsync(TestContext.Current.CancellationToken))
        {
            gravadas.Add((leitor.GetInt32(0), leitor.GetString(1)));
        }

        Assert.Equal(Migrador.Todas.Select(migracao => (migracao.Numero, migracao.Sha256)), gravadas);
    }

    [Fact]
    public async Task Num_banco_vazio_aplica_todas_e_recusa_a_migracao_que_mudou_depois_de_aplicada()
    {
        // Um banco à parte, no mesmo servidor: este teste mexe no histórico das migrações.
        var nome = $"migracoes_{Guid.NewGuid():N}";
        await banco.ComoDono($"create database {nome}");

        try
        {
            await using var outro = NpgsqlDataSource.Create(new NpgsqlConnectionStringBuilder(banco.Dono.ConnectionString)
            {
                Database = nome,
                Password = "postgres",
            }.ConnectionString);

            Assert.Equal(Migrador.Todas.Count, (await Migrador.Aplicar(outro, TestContext.Current.CancellationToken)).Count);

            await using (var mexer = outro.CreateCommand("update ponto.migracoes set sha256 = repeat('0', 64) where numero = 1"))
            {
                await mexer.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            }

            var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => Migrador.Aplicar(outro, TestContext.Current.CancellationToken));

            Assert.StartsWith("A migração 0001 (0001-cadastro-e-registro-de-ponto) mudou depois de aplicada", erro.Message, StringComparison.Ordinal);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await banco.ComoDono($"drop database {nome} with (force)");
        }
    }
}
