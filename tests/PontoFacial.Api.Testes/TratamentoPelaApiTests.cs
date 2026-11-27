using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PontoFacial.Dominio.Testes;

namespace PontoFacial.Api.Testes;

public sealed class TratamentoPelaApiTests(ApiDeTestes api) : IClassFixture<ApiDeTestes>
{
    [Fact]
    public async Task O_rh_inclui_desconsidera_e_abona_e_o_espelho_mostra_quem_tratou()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 58));
        Cenario.Esperar(HttpStatusCode.Created, await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "DispositivoEletronico" }, TestContext.Current.CancellationToken));
        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 59));
        var repetida = await (await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "DispositivoEletronico" }, TestContext.Current.CancellationToken)).Ler<JsonElement>();
        var tratamento = $"/api/estabelecimentos/{cenario.Obra}/trabalhadores/52998224725";

        RelogioDeTeste.Acertar(Exemplos.Em(24, 9, 0));
        var incluida = await (await cenario.RhCliente.PostAsJsonAsync($"{tratamento}/inclusoes", new { marcadaEm = "2026-11-23T17:00:00-03:00", motivo = "Esqueceu de marcar a saída." }, TestContext.Current.CancellationToken)).Ler<JsonElement>();
        var desconsiderada = await (await cenario.RhCliente.PostAsJsonAsync($"{tratamento}/desconsideracoes", new { nsr = repetida.GetProperty("nsr").GetInt64(), motivo = "Marcou duas vezes." }, TestContext.Current.CancellationToken)).Ler<JsonElement>();
        var abono = await (await cenario.RhCliente.PostAsJsonAsync("/api/estabelecimentos/" + cenario.Obra + "/trabalhadores/11144477735/abonos", new { dia = "2026-11-23", motivo = "Atestado médico." }, TestContext.Current.CancellationToken)).Ler<JsonElement>();

        var espelho = await cenario.RhCliente.GetStringAsync($"{tratamento}/espelho?de=2026-11-23&ate=2026-11-23", TestContext.Current.CancellationToken);

        Assert.Equal("I", incluida.GetProperty("tipo").GetString());
        Assert.Equal("123.456.789-09", incluida.GetProperty("responsavel").GetString());
        Assert.Equal("D", desconsiderada.GetProperty("tipo").GetString());
        Assert.Equal("A", abono.GetProperty("tipo").GetString());
        Assert.Contains("\nseg 23/11  07:58 07:59D 12:00P 13:00P 17:00I ", espelho, StringComparison.Ordinal);
        Assert.Contains("17:00 de 23/11/2026 incluída por 123.456.789-09 em 24/11/2026 09:00: Esqueceu de marcar a saída.", espelho, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_que_o_tratamento_recusa_volta_como_422_e_nada_se_grava()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        RelogioDeTeste.Acertar(Exemplos.Em(23, 9, 0));

        var noFuturo = await cenario.RhCliente.PostAsJsonAsync(
            $"/api/estabelecimentos/{cenario.Obra}/trabalhadores/52998224725/inclusoes",
            new { marcadaEm = "2026-11-23T17:00:00-03:00", motivo = "Adiantado." },
            TestContext.Current.CancellationToken);

        Assert.Equal("Não se inclui marcação que ainda não aconteceu.", (await noFuturo.Problema(422)).GetProperty("detail").GetString());
        Assert.Equal(0L, await api.Banco.ComoAplicacao(cenario.Empresa.Id, "select count(*) from ponto.tratamentos"));
    }
}
