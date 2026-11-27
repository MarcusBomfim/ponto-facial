using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Jornada;

namespace PontoFacial.Persistencia.Testes.Jornada;

public sealed class FeriadosNoBancoTests(BancoDeTestes banco)
{
    /// <summary>Aniversário de Santos, feriado municipal.</summary>
    private static readonly DateOnly AniversarioDeSantos = new(2026, 1, 26);

    [Fact]
    public async Task O_feriado_local_entra_no_calendario_com_os_nacionais()
    {
        var (empresa, obra) = await EmpresaComAObra();

        await empresa.Feriados.Cadastrar(obra, AniversarioDeSantos, "  Aniversário   de Santos ", TestContext.Current.CancellationToken);
        await empresa.Feriados.Cadastrar(obra, CalendarioDeFeriados.SextaFeiraDaPaixao(2026), "Sexta-feira da Paixão", TestContext.Current.CancellationToken);
        var calendario = await empresa.Feriados.Calendario(obra, TestContext.Current.CancellationToken);

        Assert.Equal("Aniversário de Santos", calendario.FeriadoEm(AniversarioDeSantos));
        Assert.Equal("Sexta-feira da Paixão", calendario.FeriadoEm(new DateOnly(2026, 4, 3)));
        Assert.Equal("Dia Nacional de Zumbi e da Consciência Negra", calendario.FeriadoEm(new DateOnly(2026, 11, 20)));
    }

    [Fact]
    public async Task O_feriado_nacional_e_o_repetido_sao_recusados_pelas_regras_do_calendario()
    {
        var (empresa, obra) = await EmpresaComAObra();
        await empresa.Feriados.Cadastrar(obra, AniversarioDeSantos, "Aniversário de Santos", TestContext.Current.CancellationToken);

        var nacional = await Assert.ThrowsAsync<ExcecaoDeDominio>(() => empresa.Feriados.Cadastrar(obra, new DateOnly(2026, 11, 15), "República", TestContext.Current.CancellationToken));
        var repetido = await Assert.ThrowsAsync<ExcecaoDeDominio>(() => empresa.Feriados.Cadastrar(obra, AniversarioDeSantos, "De novo", TestContext.Current.CancellationToken));

        Assert.Equal("15/11/2026 já é feriado nacional: Proclamação da República.", nacional.Message);
        Assert.Equal("26/01/2026 já está no calendário como Aniversário de Santos.", repetido.Message);
    }

    [Fact]
    public async Task O_feriado_cadastrado_por_engano_sai()
    {
        var (empresa, obra) = await EmpresaComAObra();
        await empresa.Feriados.Cadastrar(obra, AniversarioDeSantos, "Aniversário de Santos", TestContext.Current.CancellationToken);

        Assert.True(await empresa.Feriados.Remover(obra, AniversarioDeSantos, TestContext.Current.CancellationToken));
        Assert.False(await empresa.Feriados.Remover(obra, AniversarioDeSantos, TestContext.Current.CancellationToken));
        Assert.Null((await empresa.Feriados.Calendario(obra, TestContext.Current.CancellationToken)).FeriadoEm(AniversarioDeSantos));
    }

    [Fact]
    public async Task Cada_estabelecimento_tem_os_seus_e_outra_empresa_nao_os_ve()
    {
        var (empresa, obra) = await EmpresaComAObra();
        var sede = await empresa.Estabelecimentos.Cadastrar(Obras.De(await empresa.Empregador(TestContext.Current.CancellationToken)), TestContext.Current.CancellationToken);
        var (outra, _) = await banco.NovaEmpresa("Construtora Aurora Ltda.");
        await empresa.Feriados.Cadastrar(obra, AniversarioDeSantos, "Aniversário de Santos", TestContext.Current.CancellationToken);

        Assert.Null((await empresa.Feriados.Calendario(sede, TestContext.Current.CancellationToken)).FeriadoEm(AniversarioDeSantos));
        await Assert.ThrowsAsync<NaoEncontrado>(() => outra.Feriados.Calendario(obra, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<NaoEncontrado>(() => outra.Feriados.Cadastrar(obra, new DateOnly(2026, 9, 8), "Outro", TestContext.Current.CancellationToken));
    }

    private async Task<(EmpresaNoBanco Empresa, Guid Obra)> EmpresaComAObra()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();

        return (empresa, await empresa.Estabelecimentos.Cadastrar(Obras.De(empregador), TestContext.Current.CancellationToken));
    }
}
