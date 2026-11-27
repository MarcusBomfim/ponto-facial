using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using PontoFacial.Dominio.Testes;

namespace PontoFacial.Api.Testes;

/*
 * Os papéis do token: o RH cadastra e trata, o gestor lê, o trabalhador
 * marca e vê só o que é dele. A empresa também vem no token, e nenhum papel
 * enxerga outra.
 */
public sealed class PapeisTests(ApiDeTestes api) : IClassFixture<ApiDeTestes>
{
    [Fact]
    public async Task Sem_token_ou_com_token_de_outra_chave_a_api_nao_atende()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        var forjado = api.ComToken(ApiDeTestes.Token("rh", cenario.Empresa.Id.ToString(), "12345678909", RandomNumberGenerator.GetBytes(32)));

        Assert.Equal(HttpStatusCode.Unauthorized, (await api.SemToken().GetAsync($"/api/estabelecimentos/{cenario.Obra}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await forjado.GetAsync($"/api/estabelecimentos/{cenario.Obra}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task O_token_sem_empresa_ou_com_cpf_que_nao_confere_e_recusado()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);

        var semEmpresa = api.ComToken(ApiDeTestes.Token("rh", "nenhuma", "12345678909"));
        var cpfErrado = api.ComToken(ApiDeTestes.Token("rh", cenario.Empresa.Id.ToString(), "12345678900"));

        Assert.Equal(HttpStatusCode.Forbidden, (await semEmpresa.GetAsync($"/api/estabelecimentos/{cenario.Obra}", TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cpfErrado.GetAsync($"/api/estabelecimentos/{cenario.Obra}", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task O_trabalhador_nao_cadastra_nem_trata_nem_gera_arquivo()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);

        Assert.Equal(HttpStatusCode.Forbidden, (await cenario.Joao.PostAsJsonAsync("/api/estabelecimentos", new { local = "Outro", fuso = "America/Sao_Paulo" }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/trabalhadores/52998224725/abonos", new { dia = "2026-11-23", motivo = "Eu mesmo." }, TestContext.Current.CancellationToken)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await cenario.Joao.GetAsync($"/api/estabelecimentos/{cenario.Obra}/afd?de=2026-11-23&ate=2026-11-23", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task O_gestor_le_o_espelho_de_todos_mas_nao_trata_nem_marca()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);

        var espelho = await cenario.Gestor.GetAsync($"/api/estabelecimentos/{cenario.Obra}/trabalhadores/11144477735/espelho?de=2026-11-23&ate=2026-11-23", TestContext.Current.CancellationToken);
        var tratar = await cenario.Gestor.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/trabalhadores/11144477735/abonos", new { dia = "2026-11-23", motivo = "Atestado." }, TestContext.Current.CancellationToken);
        var marcar = await cenario.Gestor.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "Navegador" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, espelho.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, tratar.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, marcar.StatusCode);
    }

    [Fact]
    public async Task O_rh_nao_marca_pelo_trabalhador()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);

        var resposta = await cenario.RhCliente.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "Navegador" }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, resposta.StatusCode);
    }

    [Fact]
    public async Task O_trabalhador_so_ve_o_proprio_espelho_cadastro_e_comprovante()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 58));
        var daMaria = await (await cenario.Maria.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "AplicativoMovel" }, TestContext.Current.CancellationToken)).Ler<System.Text.Json.JsonElement>();

        var espelho = await cenario.Joao.GetAsync($"/api/estabelecimentos/{cenario.Obra}/trabalhadores/11144477735/espelho?de=2026-11-23&ate=2026-11-23", TestContext.Current.CancellationToken);
        var cadastro = await cenario.Joao.GetAsync("/api/trabalhadores/111.444.777-35", TestContext.Current.CancellationToken);
        var comprovante = await cenario.Joao.GetAsync(daMaria.GetProperty("comprovante").GetString(), TestContext.Current.CancellationToken);
        var proprio = await cenario.Joao.GetAsync("/api/trabalhadores/529.982.247-25", TestContext.Current.CancellationToken);

        Assert.Equal("O trabalhador só vê o que é dele.", (await espelho.Problema(403)).GetProperty("title").GetString());
        await cadastro.Problema(403);
        await comprovante.Problema(403);
        Assert.Equal("João da Silva", (await proprio.Ler<System.Text.Json.JsonElement>()).GetProperty("nome").GetString());
    }

    [Fact]
    public async Task O_token_de_outra_empresa_nao_encontra_a_obra()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        var (outra, _) = await api.Banco.NovaEmpresa("Construtora Aurora Ltda.");
        var rhDaOutra = api.Cliente("rh", outra.Id, Cenario.Rh);

        var obra = await rhDaOutra.GetAsync($"/api/estabelecimentos/{cenario.Obra}", TestContext.Current.CancellationToken);
        var afd = await rhDaOutra.GetAsync($"/api/estabelecimentos/{cenario.Obra}/afd?de=2026-11-23&ate=2026-11-23", TestContext.Current.CancellationToken);

        Assert.Equal($"O estabelecimento {cenario.Obra} não está cadastrado nesta empresa.", (await obra.Problema(404)).GetProperty("detail").GetString());
        await afd.Problema(404);
    }
}
