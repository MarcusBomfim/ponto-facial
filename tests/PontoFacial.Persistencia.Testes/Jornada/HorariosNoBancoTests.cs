using Npgsql;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Dominio.Testes;
using PontoFacial.Dominio.Testes.Jornada;

namespace PontoFacial.Persistencia.Testes.Jornada;

public sealed class HorariosNoBancoTests(BancoDeTestes banco)
{
    private static readonly DateOnly Outubro = new(2026, 10, 1);

    private static readonly DateOnly Novembro = new(2026, 11, 1);

    [Fact]
    public async Task O_horario_volta_do_banco_com_tudo_o_que_o_contrato_diz()
    {
        var empresa = await EmpresaComOJoao();

        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, Outubro, Horarios.DaObraComIntervaloPreAssinalado, TestContext.Current.CancellationToken);
        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, Novembro, Horarios.Noturno, TestContext.Current.CancellationToken);

        var deOutubro = await empresa.Horarios.Vigente(Exemplos.Joao.Cpf, new DateOnly(2026, 10, 31), TestContext.Current.CancellationToken);
        var deNovembro = await empresa.Horarios.Vigente(Exemplos.Joao.Cpf, new DateOnly(2026, 11, 1), TestContext.Current.CancellationToken);

        Assert.Equal(Horarios.DaObraComIntervaloPreAssinalado.Descricao(), deOutubro!.Descricao());
        Assert.Equal(Horarios.Noturno.Descricao(), deNovembro!.Descricao());
        Assert.Equal(Horarios.Noturno.ViradaDoDia, deNovembro.ViradaDoDia);
        Assert.Null(await empresa.Horarios.Vigente(Exemplos.Joao.Cpf, new DateOnly(2026, 9, 30), TestContext.Current.CancellationToken));
        Assert.Equal([Outubro, Novembro], (await empresa.Horarios.DoTrabalhador(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken)).Select(horario => horario.VigenteDesde));
    }

    [Fact]
    public async Task O_intervalo_da_norma_coletiva_e_o_descanso_em_outro_dia_vao_e_voltam()
    {
        var empresa = await EmpresaComOJoao();
        IReadOnlyList<Periodo> util = [new(new(7, 0), new(11, 0)), new(new(11, 30), new(15, 20))];
        var comercio = HorarioContratual.De(
            new Dictionary<DayOfWeek, IReadOnlyList<Periodo>> { [DayOfWeek.Sunday] = util, [DayOfWeek.Tuesday] = util, [DayOfWeek.Wednesday] = util, [DayOfWeek.Thursday] = util, [DayOfWeek.Friday] = util },
            DayOfWeek.Monday,
            intervaloNegociado: TimeSpan.FromMinutes(30));

        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, Outubro, comercio, TestContext.Current.CancellationToken);
        var lido = await empresa.Horarios.Vigente(Exemplos.Joao.Cpf, Outubro, TestContext.Current.CancellationToken);

        Assert.Equal(comercio.Descricao(), lido!.Descricao());
        Assert.Equal(DayOfWeek.Monday, lido.DescansoSemanal);
        Assert.Equal(TimeSpan.FromMinutes(30), lido.IntervaloMinimo);
    }

    [Fact]
    public async Task Gravar_de_novo_na_mesma_data_corrige_o_horario()
    {
        var empresa = await EmpresaComOJoao();
        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, Outubro, Horarios.DaObra, TestContext.Current.CancellationToken);

        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, Outubro, Horarios.DaObraComIntervaloPreAssinalado, TestContext.Current.CancellationToken);

        var horario = Assert.Single(await empresa.Horarios.DoTrabalhador(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        Assert.True(horario.Horario.IntervaloPreAssinalado);
    }

    [Fact]
    public async Task O_periodo_em_que_o_horario_muda_e_pedido_em_duas_partes()
    {
        var empresa = await EmpresaComOJoao();
        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, Outubro, Horarios.DaObra, TestContext.Current.CancellationToken);
        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, Novembro, Horarios.Noturno, TestContext.Current.CancellationToken);

        var erro = await Assert.ThrowsAsync<ExcecaoDeDominio>(() => empresa.Horarios.NoPeriodo(Exemplos.Joao.Cpf, new DateOnly(2026, 10, 25), new DateOnly(2026, 11, 7), TestContext.Current.CancellationToken));
        var inteiro = await empresa.Horarios.NoPeriodo(Exemplos.Joao.Cpf, Novembro, new DateOnly(2026, 11, 30), TestContext.Current.CancellationToken);

        Assert.Equal("O horário de 529.982.247-25 mudou em 01/11/2026: peça o período em duas partes, até 31/10/2026 e a partir de 01/11/2026.", erro.Message);
        Assert.Equal(Horarios.Noturno.Descricao(), inteiro!.Descricao());
    }

    [Fact]
    public async Task O_horario_mexido_por_fora_que_as_regras_recusam_nao_volta()
    {
        var empresa = await EmpresaComOJoao();
        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, Outubro, Horarios.DaObra, TestContext.Current.CancellationToken);

        // Doze horas na segunda: passa do limite diário de 10.
        await banco.ComoDono(
            """update ponto.horarios_contratuais set horario = jsonb_set(horario, '{semana,1}', '[["06:00","12:00"],["13:00","19:00"]]') where empregador_id = $1""",
            empresa.Id);

        var erro = await Assert.ThrowsAsync<ConteudoAdulterado>(() => empresa.Horarios.Vigente(Exemplos.Joao.Cpf, Outubro, TestContext.Current.CancellationToken));

        Assert.StartsWith("O horário de 529.982.247-25 a partir de 01/10/2026 no banco não se lê: ", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_documento_com_campo_que_o_formato_nao_tem_nao_volta()
    {
        var empresa = await EmpresaComOJoao();
        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, Outubro, Horarios.DaObra, TestContext.Current.CancellationToken);
        await banco.ComoDono("""update ponto.horarios_contratuais set horario = horario || '{"bancoDeHoras": true}' where empregador_id = $1""", empresa.Id);

        await Assert.ThrowsAsync<ConteudoAdulterado>(() => empresa.Horarios.Vigente(Exemplos.Joao.Cpf, Outubro, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task O_horario_e_de_um_trabalhador_da_empresa_e_outra_empresa_nao_o_ve()
    {
        var empresa = await EmpresaComOJoao();
        var (outra, _) = await banco.NovaEmpresa("Construtora Aurora Ltda.");
        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, Outubro, Horarios.DaObra, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<NaoEncontrado>(() => outra.Horarios.Gravar(Exemplos.Joao.Cpf, Outubro, Horarios.DaObra, TestContext.Current.CancellationToken));
        Assert.Null(await outra.Horarios.Vigente(Exemplos.Joao.Cpf, Outubro, TestContext.Current.CancellationToken));
        Assert.Equal(0L, await banco.ComoAplicacao(outra.Id, "select count(*) from ponto.horarios_contratuais"));
    }

    [Fact]
    public async Task A_aplicacao_nao_apaga_horario()
    {
        var empresa = await EmpresaComOJoao();
        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, Outubro, Horarios.DaObra, TestContext.Current.CancellationToken);

        var erro = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoAplicacao(empresa.Id, "delete from ponto.horarios_contratuais"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, erro.SqlState);
    }

    private async Task<EmpresaNoBanco> EmpresaComOJoao()
    {
        var (empresa, _) = await banco.NovaEmpresa();
        await empresa.Trabalhadores.Gravar(Exemplos.Joao, TestContext.Current.CancellationToken);

        return empresa;
    }
}
