using System.Text.Json;
using System.Text.Json.Nodes;

namespace PontoFacial.Api.Testes;

/*
 * O contrato da API fica publicado em docs/api/openapi.json: o aplicativo da
 * Parte 7 é feito contra ele. Se a API mudar e o arquivo não, o teste falha.
 * Para atualizar o arquivo depois de uma mudança de propósito:
 * ATUALIZAR_CONTRATO=1 dotnet test --project tests/PontoFacial.Api.Testes
 */
public sealed class ContratoTests(ApiDeTestes api) : IClassFixture<ApiDeTestes>
{
    private static readonly JsonSerializerOptions Indentado = new() { WriteIndented = true, IndentSize = 2 };

    [Fact]
    public async Task O_contrato_publicado_e_o_da_api()
    {
        var gerado = Normalizar(await api.SemToken().GetStringAsync("/openapi/v1.json", TestContext.Current.CancellationToken));
        var arquivo = Path.Combine(Raiz(), "docs", "api", "openapi.json");

        if (Environment.GetEnvironmentVariable("ATUALIZAR_CONTRATO") == "1")
        {
            await File.WriteAllTextAsync(arquivo, gerado, TestContext.Current.CancellationToken);
        }

        Assert.Equal((await File.ReadAllTextAsync(arquivo, TestContext.Current.CancellationToken)).ReplaceLineEndings("\n"), gerado);
    }

    /// <summary>O documento sem o endereço do servidor de testes, indentado como o arquivo, com LF.</summary>
    private static string Normalizar(string json)
    {
        var documento = JsonNode.Parse(json)!.AsObject();
        documento.Remove("servers");

        return documento.ToJsonString(Indentado).ReplaceLineEndings("\n") + "\n";
    }

    private static string Raiz()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);

        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "PontoFacial.slnx")))
        {
            pasta = pasta.Parent;
        }

        return pasta?.FullName ?? throw new InvalidOperationException("A raiz do repositório, com o PontoFacial.slnx, não foi encontrada.");
    }
}
