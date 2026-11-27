using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Testes;
using PontoFacial.Persistencia.Biometria;

namespace PontoFacial.Api.Testes;

/*
 * O trabalhador lê o termo, autoriza e revoga sozinho, e o RH encerra no
 * desligamento. O modelo do rosto sai do banco com a autorização.
 */
public sealed class BiometriaPelaApiTests(ApiDeTestes api) : IClassFixture<ApiDeTestes>
{
    [Fact]
    public async Task O_trabalhador_le_o_termo_autoriza_e_revoga_e_o_modelo_sai_junto()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 0));
        var termo = await (await cenario.Joao.GetAsync("/api/biometria/termo", TestContext.Current.CancellationToken)).Ler<JsonElement>();

        var concedida = await cenario.Joao.PostAsJsonAsync("/api/biometria/autorizacao", new { hashDoTermo = termo.GetProperty("hash").GetString() }, TestContext.Current.CancellationToken);
        Cenario.Esperar(HttpStatusCode.Created, concedida);

        // O cadastro do rosto vem pela câmera, na Parte 7; aqui, direto pela persistência, com a chave mestra da API.
        var biometria = cenario.Empresa.Biometria(api.Servicos.GetRequiredService<IChaveMestra>());
        var autorizacao = (await biometria.Autorizacoes.Vigente(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken))!;
        await biometria.Cadastros.Gravar(CadastroFacial.Cadastrar(autorizacao, Capturas(), LimiarDeAceitacao.De("teste-3d", 0.5, "teste"), Exemplos.Em(23, 7, 5)), TestContext.Current.CancellationToken);

        RelogioDeTeste.Acertar(Exemplos.Em(23, 12, 30));
        var revogada = await (await cenario.Joao.DeleteAsync("/api/biometria/autorizacao", TestContext.Current.CancellationToken)).Ler<JsonElement>();
        var depois = await cenario.Joao.GetAsync("/api/biometria/autorizacao", TestContext.Current.CancellationToken);

        Assert.Equal("2026.1", termo.GetProperty("versao").GetString());
        Assert.Equal(AutorizacaoBiometrica.Finalidade, termo.GetProperty("finalidade").GetString());
        Assert.Equal(1, revogada.GetProperty("modelosApagados").GetInt32());
        Assert.Null(await biometria.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        await depois.Problema(404);
    }

    [Fact]
    public async Task Quem_autoriza_diz_qual_termo_leu_e_o_termo_que_mudou_e_recusado()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);

        var resposta = await cenario.Joao.PostAsJsonAsync("/api/biometria/autorizacao", new { hashDoTermo = new string('0', 64) }, TestContext.Current.CancellationToken);

        Assert.StartsWith("O termo de consentimento mudou: a versão em vigor é a 2026.1.", (await resposta.Problema(422)).GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Uma_autorizacao_vigente_por_vez_e_o_rh_encerra_no_desligamento()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 0));
        var termo = await (await cenario.Joao.GetAsync("/api/biometria/termo", TestContext.Current.CancellationToken)).Ler<JsonElement>();
        var pedido = new { hashDoTermo = termo.GetProperty("hash").GetString() };
        Cenario.Esperar(HttpStatusCode.Created, await cenario.Joao.PostAsJsonAsync("/api/biometria/autorizacao", pedido, TestContext.Current.CancellationToken));

        var deNovo = await cenario.Joao.PostAsJsonAsync("/api/biometria/autorizacao", pedido, TestContext.Current.CancellationToken);
        RelogioDeTeste.Acertar(Exemplos.Em(30, 17, 0));
        var desligamento = await cenario.RhCliente.DeleteAsync("/api/trabalhadores/52998224725/biometria", TestContext.Current.CancellationToken);
        var historico = await cenario.Empresa.Biometria(api.Servicos.GetRequiredService<IChaveMestra>()).Autorizacoes.DoTrabalhador(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken);

        await deNovo.Problema(409);
        Assert.Equal(HttpStatusCode.OK, desligamento.StatusCode);
        Assert.Equal(MotivoDoEncerramento.Desligamento, Assert.Single(historico).Motivo);
    }

    private static ModeloFacial[] Capturas() =>
    [
        ModeloFacial.De("teste-3d", [1f, 0.1f, 0f]),
        ModeloFacial.De("teste-3d", [1f, 0f, 0.1f]),
        ModeloFacial.De("teste-3d", [1f, 0.05f, 0.05f]),
    ];
}
