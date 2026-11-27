using System.Net.Http.Json;
using System.Text;
using PontoFacial.Api.Configuracao;
using PontoFacial.Dominio.Testes;

namespace PontoFacial.Api.Testes;

/*
 * O que dá errado volta como ProblemDetails, com o tipo do catálogo
 * (docs/api/problemas.md) e a mensagem do domínio, escrita para quem usa.
 */
public sealed class ProblemasTests(ApiDeTestes api) : IClassFixture<ApiDeTestes>
{
    [Fact]
    public async Task A_regra_do_ponto_volta_como_422_com_a_mensagem_do_dominio()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 0));
        Assert.True((await cenario.RhCliente.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/registro/exclusoes", new { cpf = "52998224725" }, TestContext.Current.CancellationToken)).IsSuccessStatusCode);

        var resposta = await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "DispositivoEletronico" }, TestContext.Current.CancellationToken);
        var problema = await resposta.Problema(422);

        Assert.Equal(TratadorDeErros.Prefixo + "regra-do-ponto", problema.GetProperty("type").GetString());
        Assert.Equal("O trabalhador não está incluído neste registro de ponto.", problema.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task O_registro_aberto_duas_vezes_e_409_e_o_que_nao_existe_e_404()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        var outraObra = Guid.NewGuid();

        var deNovo = await cenario.RhCliente.PostAsync($"/api/estabelecimentos/{cenario.Obra}/registro", null, TestContext.Current.CancellationToken);
        var semObra = await cenario.RhCliente.PostAsync($"/api/estabelecimentos/{outraObra}/registro", null, TestContext.Current.CancellationToken);

        Assert.Equal(TratadorDeErros.Prefixo + "ja-existe", (await deNovo.Problema(409)).GetProperty("type").GetString());
        Assert.Equal(TratadorDeErros.Prefixo + "nao-encontrado", (await semObra.Problema(404)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task O_cliente_nao_escolhe_a_hora_da_marcacao()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);

        // O campo a mais é recusado: a hora é a do relógio do servidor, e só ela.
        var resposta = await cenario.Joao.PostAsync(
            $"/api/estabelecimentos/{cenario.Obra}/marcacoes",
            new StringContent("""{"coletor":"DispositivoEletronico","marcadaEm":"2026-11-23T07:00:00-03:00"}""", Encoding.UTF8, "application/json"),
            TestContext.Current.CancellationToken);

        Assert.Equal(TratadorDeErros.Prefixo + "pedido-invalido", (await resposta.Problema(400)).GetProperty("type").GetString());
    }

    [Fact]
    public async Task O_coletor_que_nao_existe_e_o_cpf_que_nao_confere_sao_recusados()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);

        var coletor = await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "Relogio" }, TestContext.Current.CancellationToken);
        var cpf = await cenario.RhCliente.GetAsync("/api/trabalhadores/52998224700", TestContext.Current.CancellationToken);

        await coletor.Problema(400);
        Assert.Equal("O CPF 52998224700 não confere: os dígitos verificadores não batem com o número.", (await cpf.Problema(422)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task O_horario_que_a_clt_nao_deixa_e_recusado_com_o_artigo()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        var doze = new
        {
            semana = new Dictionary<string, string[][]> { ["1"] = [["06:00", "12:00"], ["13:00", "19:00"]] },
            descansoSemanal = 7,
            intervaloPreAssinalado = false,
        };

        var resposta = await cenario.RhCliente.PutAsJsonAsync("/api/trabalhadores/52998224725/horarios/2026-12-01", doze, TestContext.Current.CancellationToken);

        Assert.EndsWith("o máximo é 10:00 por dia, mesmo com compensação (art. 59, § 2º, da CLT).", (await resposta.Problema(422)).GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_espelho_de_um_periodo_em_que_o_horario_muda_e_pedido_em_duas_partes()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        Assert.True((await cenario.RhCliente.PutAsJsonAsync("/api/trabalhadores/52998224725/horarios/2026-11-25", Cenario.HorarioDaObra, TestContext.Current.CancellationToken)).IsSuccessStatusCode);

        var resposta = await cenario.RhCliente.GetAsync($"/api/estabelecimentos/{cenario.Obra}/trabalhadores/52998224725/espelho?de=2026-11-23&ate=2026-11-27", TestContext.Current.CancellationToken);

        Assert.Equal(
            "O horário de 529.982.247-25 mudou em 25/11/2026: peça o período em duas partes, até 24/11/2026 e a partir de 25/11/2026.",
            (await resposta.Problema(422)).GetProperty("detail").GetString());
    }
}
