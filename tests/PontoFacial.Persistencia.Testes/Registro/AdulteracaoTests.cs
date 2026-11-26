using Npgsql;
using PontoFacial.Dominio.Testes;
using PontoFacial.Dominio.Testes.Registro;

namespace PontoFacial.Persistencia.Testes.Registro;

/*
 * O superusuário do banco passa por cima de tudo o que o banco garante: com
 * session_replication_role = replica, nem os gatilhos nem as chaves
 * estrangeiras disparam. Ele consegue alterar e apagar. O que o sistema faz
 * é não deixar passar: a leitura refaz o hash de cada marcação e confere a
 * numeração e a cadeia (ADR-0013).
 *
 * No RegistroVariado, o João marca no NSR 5, a Maria no 6, o ajuste do
 * relógio é o 7 e o João marca de novo no 8.
 */
public sealed class AdulteracaoTests(BancoDeTestes banco)
{
    [Fact]
    public async Task Quem_troca_a_hora_de_uma_marcacao_por_fora_e_pego_na_leitura()
    {
        var (empresa, obra) = await RegistroNoBanco();

        await PorForaDoSistema("update ponto.operacoes set marcada_em = marcada_em - interval '8 minutes' where estabelecimento_id = $1 and nsr = 5", obra);

        var erro = await Assert.ThrowsAsync<ConteudoAdulterado>(() => empresa.Registros.Operacoes(obra, cancelamento: TestContext.Current.CancellationToken));
        Assert.Equal("A marcação do NSR 5 no banco não confere com o hash dela: a hora, o CPF ou o coletor foram trocados por fora do sistema.", erro.Message);

        await Assert.ThrowsAsync<ConteudoAdulterado>(() => empresa.Registros.Comprovante(obra, 5, RegistroDeExemplo.RegistroNoInpi, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Quem_refaz_o_hash_de_uma_marcacao_e_pego_na_cadeia_que_se_rompe_na_seguinte()
    {
        var (empresa, obra) = await RegistroNoBanco();

        // A entrada do João passa para 7h50, com os campos e o hash refeitos: a linha confere sozinha.
        await PorForaDoSistema(
            """
            update ponto.operacoes
            set marcada_em = marcada_em - interval '8 minutes',
                campos = overlay(campos placing '07:50' from 22 for 5),
                hash = encode(sha256(convert_to(overlay(campos placing '07:50' from 22 for 5) || coalesce(hash_anterior, ''), 'UTF8')), 'hex')
            where estabelecimento_id = $1 and nsr = 5
            """,
            obra);

        var erro = await Assert.ThrowsAsync<ConteudoAdulterado>(() => empresa.Registros.Operacoes(obra, cancelamento: TestContext.Current.CancellationToken));
        Assert.Equal(
            "A cadeia se rompe no NSR 6: o hash anterior gravado nele não é o da marcação de antes, que foi alterada, incluída ou removida.",
            erro.Message);

        // A linha refeita, lida sozinha, confere: é a seguinte que a denuncia.
        Assert.Single(await empresa.Registros.Operacoes(obra, 5, 1, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Quem_apaga_uma_operacao_por_fora_e_pego_no_buraco_da_numeracao()
    {
        var (empresa, obra) = await RegistroNoBanco();

        await PorForaDoSistema("delete from ponto.operacoes where estabelecimento_id = $1 and nsr = 7", obra);

        var erro = await Assert.ThrowsAsync<ConteudoAdulterado>(() => empresa.Registros.Operacoes(obra, cancelamento: TestContext.Current.CancellationToken));
        Assert.Equal("Falta o NSR 7 no banco: a numeração do registro tem um buraco.", erro.Message);
    }

    [Fact]
    public async Task Quem_troca_o_cpf_de_uma_mudanca_por_um_que_nao_confere_e_pego_na_leitura()
    {
        var (empresa, obra) = await RegistroNoBanco();

        await PorForaDoSistema("update ponto.operacoes set cpf = '52998224700' where estabelecimento_id = $1 and nsr = 10", obra);

        var erro = await Assert.ThrowsAsync<ConteudoAdulterado>(() => empresa.Registros.Operacoes(obra, cancelamento: TestContext.Current.CancellationToken));
        Assert.StartsWith("A operação do NSR 10 no banco não passa pelas regras do domínio: O CPF 52998224700 não confere", erro.Message, StringComparison.Ordinal);
    }

    private async Task<(EmpresaNoBanco Empresa, Guid Obra)> RegistroNoBanco()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await Copia.DoRegistro(empresa, RegistroVariado.Montar(empregador), Exemplos.Joao, Exemplos.Maria);

        return (empresa, obra);
    }

    /// <summary>O SQL como o superusuário o rodaria, com os gatilhos e as chaves estrangeiras desligados só nesta transação.</summary>
    private async Task PorForaDoSistema(string sql, params object?[] valores)
    {
        await using var conexao = await banco.Dono.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var transacao = await conexao.BeginTransactionAsync(TestContext.Current.CancellationToken);

        await using (var replica = new NpgsqlCommand("set local session_replication_role = replica", conexao, transacao))
        {
            await replica.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using (var comando = new NpgsqlCommand(sql, conexao, transacao))
        {
            foreach (var valor in valores)
            {
                comando.Parameters.Add(new NpgsqlParameter { Value = valor ?? DBNull.Value });
            }

            Assert.Equal(1, await comando.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        }

        await transacao.CommitAsync(TestContext.Current.CancellationToken);
    }
}
