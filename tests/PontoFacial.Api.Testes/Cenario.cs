using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Testes;
using PontoFacial.Persistencia;

namespace PontoFacial.Api.Testes;

/// <summary>
/// A obra montada pela API, como o RH a montaria: o estabelecimento, o João e
/// a Maria com o horário da obra, e o registro aberto às 6h de 23/11/2026
/// com os dois incluídos (NSR 1 a 3).
/// </summary>
internal sealed class Cenario
{
    public static readonly Cpf Rh = Cpf.De("123.456.789-09");

    private Cenario(EmpresaNoBanco empresa, Guid obra, HttpClient rh, HttpClient joao, HttpClient maria, HttpClient gestor)
    {
        Empresa = empresa;
        Obra = obra;
        RhCliente = rh;
        Joao = joao;
        Maria = maria;
        Gestor = gestor;
    }

    public EmpresaNoBanco Empresa { get; }

    public Guid Obra { get; }

    public HttpClient RhCliente { get; }

    public HttpClient Joao { get; }

    public HttpClient Maria { get; }

    public HttpClient Gestor { get; }

    /// <summary>O horário da obra no corpo do pedido: segunda a sexta 8h–12h e 13h–17h, sábado 8h–12h, intervalo pré-assinalado.</summary>
    public static object HorarioDaObra { get; } = new
    {
        semana = new Dictionary<string, string[][]>
        {
            ["1"] = [["08:00", "12:00"], ["13:00", "17:00"]],
            ["2"] = [["08:00", "12:00"], ["13:00", "17:00"]],
            ["3"] = [["08:00", "12:00"], ["13:00", "17:00"]],
            ["4"] = [["08:00", "12:00"], ["13:00", "17:00"]],
            ["5"] = [["08:00", "12:00"], ["13:00", "17:00"]],
            ["6"] = [["08:00", "12:00"]],
        },
        descansoSemanal = 7,
        intervaloPreAssinalado = true,
    };

    /// <summary>Monta a obra. As horas são as do relógio do teste, acertado por quem chama a cada passo.</summary>
    /// <param name="acertar">Acerta o relógio do teste: precisa ser chamado no próprio método do teste.</param>
    public static async Task<Cenario> Montar(ApiDeTestes api, Action<DateTimeOffset> acertar)
    {
        var (empresa, _) = await api.Banco.NovaEmpresa();
        var rh = api.Cliente("rh", empresa.Id, Rh);

        var obra = (await (await rh.PostAsJsonAsync("/api/estabelecimentos", new { local = "Rua das Palmeiras, 100 - Santos/SP", fuso = "America/Sao_Paulo" })).Ler<JsonElement>())
            .GetProperty("id").GetGuid();

        foreach (var (cpf, nome, admissao, cargo) in new[] { ("529.982.247-25", "João da Silva", "2026-03-02", "Pedreiro"), ("111.444.777-35", "Maria Souza", "2026-05-04", "Mestre de obras") })
        {
            Esperar(HttpStatusCode.NoContent, await rh.PutAsJsonAsync($"/api/trabalhadores/{cpf}", new { nome, admissao, cargo }));
            Esperar(HttpStatusCode.NoContent, await rh.PutAsJsonAsync($"/api/trabalhadores/{cpf}/horarios/2026-03-02", HorarioDaObra));
        }

        acertar(Exemplos.Em(23, 6, 0));
        Esperar(HttpStatusCode.Created, await rh.PostAsync($"/api/estabelecimentos/{obra}/registro", null));
        acertar(Exemplos.Em(23, 6, 1));
        Esperar(HttpStatusCode.Created, await rh.PostAsJsonAsync($"/api/estabelecimentos/{obra}/registro/inclusoes", new { cpf = "52998224725" }));
        acertar(Exemplos.Em(23, 6, 2));
        Esperar(HttpStatusCode.Created, await rh.PostAsJsonAsync($"/api/estabelecimentos/{obra}/registro/inclusoes", new { cpf = "11144477735" }));

        return new Cenario(
            empresa,
            obra,
            rh,
            api.Cliente("trabalhador", empresa.Id, Exemplos.Joao.Cpf),
            api.Cliente("trabalhador", empresa.Id, Exemplos.Maria.Cpf),
            api.Cliente("gestor", empresa.Id, Cpf.De("390.533.447-05")));
    }

    public static void Esperar(HttpStatusCode status, HttpResponseMessage resposta) =>
        Assert.True(resposta.StatusCode == status, $"Esperava {(int)status}, veio {(int)resposta.StatusCode}: {resposta.Content.ReadAsStringAsync().Result}");
}
