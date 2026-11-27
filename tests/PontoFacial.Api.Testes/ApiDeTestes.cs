using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PontoFacial.Api.Configuracao;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Persistencia.Testes;

namespace PontoFacial.Api.Testes;

/// <summary>
/// A API de verdade, em memória, sobre o PostgreSQL do contêiner, com um
/// relógio que o teste acerta e um certificado de teste no lugar do
/// ICP-Brasil.
/// </summary>
/// <remarks>
/// O fixture sobe a aplicação já no começo (<see cref="InitializeAsync"/>):
/// com os testes em paralelo, subir na primeira requisição criaria dois hosts
/// (lição do crm-obras).
/// </remarks>
public sealed class ApiDeTestes : IAsyncLifetime
{
    public const string Emissor = "ponto-facial";

    public const string Publico = "ponto-facial-api";

    /// <summary>A chave dos tokens dos testes: a API a recebe pela configuração.</summary>
    private static readonly byte[] ChaveDosTokens = RandomNumberGenerator.GetBytes(32);

    private readonly BancoDeTestes _banco;

    private readonly Fabrica _fabrica;

    public ApiDeTestes(BancoDeTestes banco)
    {
        _banco = banco;
        _fabrica = new Fabrica(this);
    }

    public RelogioDeTeste Relogio { get; } = new(new DateTimeOffset(2026, 11, 23, 6, 0, 0, TimeSpan.FromHours(-3)));

    public X509Certificate2 Certificado { get; } = CertificadoDeTeste();

    public BancoDeTestes Banco => _banco;

    public IServiceProvider Servicos => _fabrica.Services;

    public static JsonSerializerOptions Json { get; } = OpcoesDoJson();

    public ValueTask InitializeAsync()
    {
        if (_banco.Disponivel)
        {
            _ = _fabrica.Services;
        }

        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync() => await _fabrica.DisposeAsync();

    /// <summary>Um cliente com o token do papel, da empresa e do CPF dados.</summary>
    public HttpClient Cliente(string papel, Guid empresa, Cpf cpf) => ComToken(Token(papel, empresa.ToString(), cpf.Numero));

    /// <summary>Um cliente sem token nenhum.</summary>
    public HttpClient SemToken()
    {
        _ = _banco.Aplicacao;

        return _fabrica.CreateClient();
    }

    /// <summary>Um cliente com o token escrito à mão, para os testes do que o token traz errado.</summary>
    public HttpClient ComToken(string token)
    {
        _ = _banco.Aplicacao;
        var cliente = _fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return cliente;
    }

    public static string Token(string papel, string empresa, string cpf, byte[]? chave = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Emissor,
            Audience = Publico,
            Subject = new ClaimsIdentity([new Claim("empresa", empresa), new Claim("cpf", cpf), new Claim("role", papel)]),
            Expires = DateTime.UtcNow.AddMinutes(10),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(chave ?? ChaveDosTokens), SecurityAlgorithms.HmacSha256),
        });

    private static X509Certificate2 CertificadoDeTeste()
    {
        using var chave = RSA.Create(2048);

        return new CertificateRequest("CN=Desenvolvedora de Exemplo Ltda.:11444777000161, C=BR", chave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 12, 31, 0, 0, 0, TimeSpan.Zero));
    }

    private static JsonSerializerOptions OpcoesDoJson()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        opcoes.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());

        return opcoes;
    }

    private sealed class Fabrica(ApiDeTestes api) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            var conexao = new NpgsqlConnectionStringBuilder(api._banco.Aplicacao.ConnectionString) { Password = BancoDeTestes.SenhaDaAplicacao };

            builder.UseEnvironment("Development");
            builder.UseSetting("Banco:Conexao", conexao.ConnectionString);
            builder.UseSetting("Autenticacao:Chave", Convert.ToBase64String(ChaveDosTokens));
            builder.ConfigureTestServices(servicos =>
            {
                // A hora acertada pelo teste chega à API pelo contexto de execução.
                servicos.Configure<TestServerOptions>(opcoes => opcoes.PreserveExecutionContext = true);
                servicos.AddSingleton<TimeProvider>(api.Relogio);
                servicos.AddSingleton(api.Certificado);
            });
        }
    }
}

/// <summary>
/// O relógio que o teste acerta: a API lê a hora dele, e não a do sistema.
/// Cada teste tem a sua hora — ela segue o fluxo assíncrono do teste até a
/// API, que roda no mesmo processo —, e os testes em paralelo não mexem no
/// relógio um do outro.
/// </summary>
public sealed class RelogioDeTeste(DateTimeOffset padrao) : TimeProvider
{
    private static readonly AsyncLocal<DateTimeOffset?> Agora = new();

    /// <summary>Acerta a hora do teste. Chame no próprio método do teste, para a hora seguir com ele.</summary>
    public static void Acertar(DateTimeOffset agora) => Agora.Value = agora;

    public override DateTimeOffset GetUtcNow() => (Agora.Value ?? padrao).ToUniversalTime();
}

/// <summary>O que os testes repetem nas respostas.</summary>
internal static class Respostas
{
    public static async Task<T> Ler<T>(this HttpResponseMessage resposta)
    {
        Assert.True(resposta.IsSuccessStatusCode, $"{(int)resposta.StatusCode}: {await resposta.Content.ReadAsStringAsync()}");

        return (await resposta.Content.ReadFromJsonAsync<T>(ApiDeTestes.Json))!;
    }

    public static async Task<JsonElement> Problema(this HttpResponseMessage resposta, int status)
    {
        Assert.Equal(status, (int)resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);

        return await resposta.Content.ReadFromJsonAsync<JsonElement>();
    }
}
