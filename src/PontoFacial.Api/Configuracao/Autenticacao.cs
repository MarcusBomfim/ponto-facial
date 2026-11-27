using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Api.Configuracao;

/// <summary>
/// Quem chama a API, pelo token: a empresa, o CPF e o papel — RH, gestor ou
/// trabalhador (ADR-0017).
/// </summary>
internal static class Autenticacao
{
    public const string Rh = "rh";

    public const string Gestor = "gestor";

    public const string Trabalhador = "trabalhador";

    /// <summary>A política do RH: cadastra, trata, gera os arquivos.</summary>
    public const string SoRh = "so-rh";

    /// <summary>A política de quem lê o ponto dos outros: o RH e o gestor.</summary>
    public const string RhOuGestor = "rh-ou-gestor";

    /// <summary>A política de quem marca o ponto e cuida da própria biometria.</summary>
    public const string SoTrabalhador = "so-trabalhador";

    /// <summary>Qualquer papel da empresa: o que muda é o que cada um vê.</summary>
    public const string QualquerPapel = "qualquer-papel";

    public const string ClaimDaEmpresa = "empresa";

    public const string ClaimDoCpf = "cpf";

    public const string ClaimDoPapel = "role";

    public static IServiceCollection AdicionarAutenticacao(this IServiceCollection servicos, IConfiguration configuracao, IHostEnvironment ambiente)
    {
        var chave = configuracao["Autenticacao:Chave"] is { Length: > 0 } base64
            ? Convert.FromBase64String(base64)
            : ambiente.IsDevelopment()
                ? Servicos.ChaveDeDesenvolvimento("jwt")
                : throw new InvalidOperationException("Falta Autenticacao:Chave, a chave que confere os tokens.");

        servicos
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opcoes =>
            {
                opcoes.MapInboundClaims = false;
                opcoes.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = Servicos.Obrigatorio(configuracao, "Autenticacao:Emissor"),
                    ValidAudience = Servicos.Obrigatorio(configuracao, "Autenticacao:Publico"),
                    IssuerSigningKey = new SymmetricSecurityKey(chave),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RoleClaimType = ClaimDoPapel,
                    NameClaimType = ClaimDoCpf,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        servicos
            .AddAuthorizationBuilder()
            .AddPolicy(SoRh, politica => Da(politica).RequireRole(Rh))
            .AddPolicy(RhOuGestor, politica => Da(politica).RequireRole(Rh, Gestor))
            .AddPolicy(SoTrabalhador, politica => Da(politica).RequireRole(Trabalhador))
            .AddPolicy(QualquerPapel, politica => Da(politica).RequireRole(Rh, Gestor, Trabalhador));

        return servicos;
    }

    /// <summary>A empresa do token, em nome de quem tudo se lê e grava.</summary>
    public static Guid Empresa(this ClaimsPrincipal usuario) => Guid.Parse(usuario.FindFirstValue(ClaimDaEmpresa)!);

    /// <summary>O CPF de quem chama: o trabalhador que marca, ou o responsável no RH.</summary>
    public static Cpf Cpf(this ClaimsPrincipal usuario) => Dominio.Documentos.Cpf.De(usuario.FindFirstValue(ClaimDoCpf)!);

    /// <summary>Se quem chama é o próprio trabalhador, e não o RH nem o gestor.</summary>
    public static bool SoPodeVerOProprio(this ClaimsPrincipal usuario) => !usuario.IsInRole(Rh) && !usuario.IsInRole(Gestor);

    /// <summary>Todo papel precisa de um token com a empresa e um CPF que confere.</summary>
    private static AuthorizationPolicyBuilder Da(AuthorizationPolicyBuilder politica) =>
        politica
            .RequireAuthenticatedUser()
            .RequireAssertion(contexto =>
                Guid.TryParse(contexto.User.FindFirstValue(ClaimDaEmpresa), out _)
                && contexto.User.FindFirstValue(ClaimDoCpf) is { } cpf
                && CpfConfere(cpf));

    private static bool CpfConfere(string cpf)
    {
        try
        {
            Dominio.Documentos.Cpf.De(cpf);

            return true;
        }
        catch (Dominio.Comum.ExcecaoDeDominio)
        {
            return false;
        }
    }
}
