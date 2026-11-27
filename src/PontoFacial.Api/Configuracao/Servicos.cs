using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Npgsql;
using PontoFacial.Assinaturas;
using PontoFacial.Dominio.Arquivos;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Persistencia;
using PontoFacial.Persistencia.Biometria;

namespace PontoFacial.Api.Configuracao;

/// <summary>O que a API precisa, montado pela configuração (ADR-0017).</summary>
internal static class Servicos
{
    /// <summary>
    /// A semente das chaves de desenvolvimento: só vale no ambiente
    /// Development, e ninguém deve tratá-la como segredo.
    /// </summary>
    private const string SementeDeDesenvolvimento = "ponto-facial: só para desenvolvimento, nunca em produção";

    public static IServiceCollection AdicionarPontoFacial(this IServiceCollection servicos, IConfiguration configuracao, IHostEnvironment ambiente)
    {
        servicos.ConfigureHttpJsonOptions(opcoes => Json(opcoes.SerializerOptions));

        // O corpo que não se lê vira exceção, e o tratador de erros a responde como os outros problemas.
        servicos.Configure<RouteHandlerOptions>(opcoes => opcoes.ThrowOnBadRequest = true);
        servicos.AddProblemDetails();
        servicos.AddExceptionHandler<TratadorDeErros>();
        servicos.AddOpenApi();
        servicos.AdicionarAutenticacao(configuracao, ambiente);

        servicos.AddSingleton(TimeProvider.System);
        servicos.AddSingleton(_ => NpgsqlDataSource.Create(Obrigatorio(configuracao, "Banco:Conexao")));
        servicos.AddSingleton(provedor => new BancoDoPonto(provedor.GetRequiredService<NpgsqlDataSource>()));
        servicos.AddSingleton(_ => Programa(configuracao));
        servicos.AddSingleton(_ => Termo(configuracao));
        servicos.AddSingleton(provedor => Certificado(configuracao, ambiente, provedor.GetRequiredService<ILoggerFactory>().CreateLogger("PontoFacial.Api.Assinatura")));
        servicos.AddSingleton<IChaveMestra>(_ => ChaveMestra(configuracao, ambiente));
        servicos.AddSingleton(_ => PoliticaConfigurada(configuracao));

        return servicos;
    }

    /// <summary>
    /// O JSON da API: estrito na entrada — campo desconhecido, obrigatório
    /// faltando ou nulo onde não cabe é recusado — e os enums por nome.
    /// </summary>
    public static void Json(JsonSerializerOptions opcoes)
    {
        opcoes.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        opcoes.RespectNullableAnnotations = true;
        opcoes.RespectRequiredConstructorParameters = true;
        opcoes.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
    }

    /// <summary>A chave de desenvolvimento para um uso: derivada da semente, a mesma a cada execução.</summary>
    internal static byte[] ChaveDeDesenvolvimento(string uso) => SHA256.HashData(Encoding.UTF8.GetBytes($"{SementeDeDesenvolvimento}|{uso}"));

    internal static string Obrigatorio(IConfiguration configuracao, string chave) =>
        configuracao[chave] is { Length: > 0 } valor ? valor : throw new InvalidOperationException($"Falta {chave} na configuração.");

    private static ProgramaDoPonto Programa(IConfiguration configuracao) => ProgramaDoPonto.De(
        Obrigatorio(configuracao, "Programa:Nome"),
        Obrigatorio(configuracao, "Programa:Versao"),
        Obrigatorio(configuracao, "Programa:RegistroNoInpi"),
        Documento.De(Obrigatorio(configuracao, "Programa:Desenvolvedor")),
        Obrigatorio(configuracao, "Programa:RazaoSocialDoDesenvolvedor"),
        Obrigatorio(configuracao, "Programa:EmailDoDesenvolvedor"));

    private static TermoDeConsentimento Termo(IConfiguration configuracao) =>
        TermoDeConsentimento.Novo(Obrigatorio(configuracao, "Biometria:Termo:Versao"), Obrigatorio(configuracao, "Biometria:Termo:Texto"));

    /// <summary>
    /// O certificado ICP-Brasil que assina o comprovante, o AFD e o AEJ. Em
    /// desenvolvimento, sem um configurado, a API faz um assinado por ele
    /// mesmo — que não vale para a fiscalização, e o registro do log diz isso.
    /// </summary>
    private static X509Certificate2 Certificado(IConfiguration configuracao, IHostEnvironment ambiente, ILogger log)
    {
        if (configuracao["Assinatura:Certificado"] is { Length: > 0 } arquivo)
        {
            return X509CertificateLoader.LoadPkcs12FromFile(arquivo, configuracao["Assinatura:Senha"], X509KeyStorageFlags.EphemeralKeySet);
        }

        if (!ambiente.IsDevelopment())
        {
            throw new InvalidOperationException("Falta Assinatura:Certificado: o certificado ICP-Brasil que assina o comprovante, o AFD e o AEJ (art. 88 da Portaria 671).");
        }

        log.CertificadoDeDesenvolvimento();

        using var chave = RSA.Create(2048);
        var pedido = new CertificateRequest("CN=Ponto Facial - desenvolvimento, O=Sem validade, C=BR", chave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return pedido.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(1));
    }

    private static ChaveMestraLocal ChaveMestra(IConfiguration configuracao, IHostEnvironment ambiente)
    {
        if (configuracao["ChaveMestra:Base64"] is { Length: > 0 } base64)
        {
            return ChaveMestraLocal.DeBase64(Obrigatorio(configuracao, "ChaveMestra:Id"), base64);
        }

        return ambiente.IsDevelopment()
            ? new ChaveMestraLocal("desenvolvimento", ChaveDeDesenvolvimento("chave-mestra"))
            : throw new InvalidOperationException("Falta ChaveMestra:Base64: a chave que embrulha as chaves das empresas (ADR-0014).");
    }

    /// <summary>A política de assinatura da ICP-Brasil, se a instalação a configurou: o OID e o hash publicados.</summary>
    private static Politica PoliticaConfigurada(IConfiguration configuracao) =>
        configuracao["Assinatura:Politica:Identificador"] is { Length: > 0 } identificador
            ? new Politica(new PoliticaDeAssinatura(
                identificador,
                configuracao["Assinatura:Politica:AlgoritmoDoHash"] ?? "2.16.840.1.101.3.4.2.1",
                Convert.FromHexString(Obrigatorio(configuracao, "Assinatura:Politica:Hash")),
                configuracao["Assinatura:Politica:Endereco"] is { Length: > 0 } endereco ? new Uri(endereco) : null))
            : new Politica(null);
}

/// <summary>A política de assinatura configurada, ou nenhuma.</summary>
internal sealed record Politica(PoliticaDeAssinatura? Valor);
