using Npgsql;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Testes;
using PontoFacial.Dominio.Testes.Jornada;
using PontoFacial.Dominio.Testes.Registro;
using PontoFacial.Persistencia.Testes.Registro;

namespace PontoFacial.Persistencia.Testes.Jornada;

public sealed class TratamentoNoBancoTests(BancoDeTestes banco)
{
    private const string InserirDesconsiderada = """
        insert into ponto.tratamentos (empregador_id, estabelecimento_id, cpf, sequencia, tipo, nsr, motivo, responsavel, tratado_em, tratado_em_deslocamento)
        values ($1, $2, '52998224725', $3, 'D', $4, 'Engano.', '12345678909', '2026-11-24 12:00Z', -180)
        """;

    [Fact]
    public async Task A_semana_do_joao_volta_do_banco_com_o_mesmo_espelho_de_ponto()
    {
        var (empresa, _) = await banco.NovaEmpresa();
        var (registro, tratamento, _) = SemanaDoJoao.Montar();
        var obra = await Copia.DoRegistro(empresa, registro, Exemplos.Joao);
        await Copia.DoTratamento(empresa, obra, tratamento);

        // Um abono, para os três tipos de tratamento irem e voltarem.
        tratamento.Abonar(new DateOnly(2026, 11, 17), "Atestado médico de 10 minutos.", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0));
        await empresa.Tratamentos.Tratar(
            obra,
            Exemplos.Joao.Cpf,
            lido => lido.Abonar(new DateOnly(2026, 11, 17), "Atestado médico de 10 minutos.", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0)),
            TestContext.Current.CancellationToken);

        var doBanco = await empresa.Tratamentos.Obter(obra, Exemplos.Joao.Cpf, TestContext.Current.CancellationToken);

        // O CNPJ é o da empresa do teste; o resto do espelho, linha por linha, é o mesmo.
        Assert.Equal(SemCnpj(SemanaDoJoao.Espelho(tratamento).Linhas()), SemCnpj(SemanaDoJoao.Espelho(doBanco).Linhas()));
        Assert.Equal(3, doBanco.Marcacoes.Count);
        Assert.Single(doBanco.Abonos);
    }

    [Fact]
    public async Task O_tratamento_que_o_dominio_recusa_nao_vai_para_o_banco_nem_o_que_veio_junto()
    {
        var (empresa, _) = await banco.NovaEmpresa();
        var (registro, tratamento, repetida) = SemanaDoJoao.Montar();
        var obra = await Copia.DoRegistro(empresa, registro, Exemplos.Joao);
        await Copia.DoTratamento(empresa, obra, tratamento);

        var erro = await Assert.ThrowsAsync<ExcecaoDeDominio>(() => empresa.Tratamentos.Tratar(
            obra,
            Exemplos.Joao.Cpf,
            lido =>
            {
                lido.Abonar(new DateOnly(2026, 11, 17), "Atestado médico.", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0));

                return lido.Desconsiderar(repetida, "De novo.", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 1));
            },
            TestContext.Current.CancellationToken));

        Assert.Equal($"A marcação do NSR {repetida} já foi desconsiderada.", erro.Message);
        Assert.Equal(3L, await banco.ComoAplicacao(empresa.Id, "select count(*) from ponto.tratamentos"));
    }

    [Fact]
    public async Task O_banco_recusa_desconsiderar_a_marcacao_de_outro_trabalhador()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await Copia.DoRegistro(empresa, RegistroVariado.Montar(empregador), Exemplos.Joao, Exemplos.Maria);

        // O NSR 6 é da Maria; o 5, do João.
        var erro = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoAplicacao(empresa.Id, InserirDesconsiderada, empresa.Id, obra, 1, 6L));
        await banco.ComoAplicacao(empresa.Id, InserirDesconsiderada, empresa.Id, obra, 1, 5L);

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_tratamentos_operacoes"), (erro.SqlState, erro.ConstraintName));
    }

    [Fact]
    public async Task O_banco_recusa_buraco_na_sequencia_do_tratamento()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await Copia.DoRegistro(empresa, RegistroVariado.Montar(empregador), Exemplos.Joao, Exemplos.Maria);

        var erro = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoAplicacao(empresa.Id, InserirDesconsiderada, empresa.Id, obra, 2, 5L));

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_tratamentos_sequencia_anterior"), (erro.SqlState, erro.ConstraintName));
    }

    [Fact]
    public async Task O_tratamento_gravado_nao_se_altera_nem_se_apaga()
    {
        var (empresa, _) = await banco.NovaEmpresa();
        var (registro, tratamento, _) = SemanaDoJoao.Montar();
        var obra = await Copia.DoRegistro(empresa, registro, Exemplos.Joao);
        await Copia.DoTratamento(empresa, obra, tratamento);

        var peloDono = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoDono("update ponto.tratamentos set motivo = 'Outro.' where estabelecimento_id = $1", obra));
        var pelaAplicacao = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoAplicacao(empresa.Id, "delete from ponto.tratamentos"));

        Assert.Equal("PF001", peloDono.SqlState);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, pelaAplicacao.SqlState);
    }

    [Fact]
    public async Task O_tratamento_gravado_que_as_regras_recusariam_nao_volta_do_banco()
    {
        var (empresa, _) = await banco.NovaEmpresa();
        var (registro, tratamento, _) = SemanaDoJoao.Montar();
        var obra = await Copia.DoRegistro(empresa, registro, Exemplos.Joao);
        await Copia.DoTratamento(empresa, obra, tratamento);

        // Incluída por fora uma marcação às 7h58 de segunda, quando o João já tinha marcado.
        await banco.ComoDono(
            """
            insert into ponto.tratamentos (empregador_id, estabelecimento_id, cpf, sequencia, tipo, marcada_em, marcada_em_deslocamento, motivo, responsavel, tratado_em, tratado_em_deslocamento)
            values ($1, $2, '52998224725', 4, 'I', '2026-11-16 10:58Z', -180, 'Por fora.', '12345678909', '2026-11-24 12:00Z', -180)
            """,
            empresa.Id,
            obra);

        var erro = await Assert.ThrowsAsync<ConteudoAdulterado>(() => empresa.Tratamentos.Obter(obra, Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));

        Assert.Equal(
            "O tratamento 4 do trabalhador 529.982.247-25 no banco não passa pelas regras do domínio: Já há marcação às 07:58 de 16/11/2026.",
            erro.Message);
    }

    [Fact]
    public async Task Quem_nunca_foi_incluido_no_registro_nao_tem_tratamento()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await Copia.DoRegistro(empresa, RegistroVariado.Montar(empregador), Exemplos.Joao, Exemplos.Maria);
        var pedro = Cpf.De("390.533.447-05");

        var erro = await Assert.ThrowsAsync<ExcecaoDeDominio>(() => empresa.Tratamentos.Obter(obra, pedro, TestContext.Current.CancellationToken));

        Assert.Equal("O trabalhador não foi incluído neste registro de ponto.", erro.Message);
    }

    private static string[] SemCnpj(IEnumerable<string> linhas) => [.. linhas.Where(linha => !linha.StartsWith("CNPJ:", StringComparison.Ordinal))];
}
