using System.Security.Claims;
using PontoFacial.Api.Configuracao;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Persistencia;
using PontoFacial.Persistencia.Biometria;

namespace PontoFacial.Api.Rotas;

/// <summary>
/// A biometria por consentimento: o trabalhador lê o termo, autoriza e
/// revoga, ele mesmo, sem pedir a ninguém (LGPD, art. 8º, § 5º). No
/// desligamento, é o RH quem encerra. Nos dois casos, o modelo do rosto sai
/// do banco na mesma transação (ADR-0014).
/// </summary>
internal static class BiometriaRotas
{
    public static void MapBiometria(this IEndpointRouteBuilder app)
    {
        var biometria = app.MapGroup("/api/biometria").WithTags("Biometria").RequireAuthorization(Autenticacao.SoTrabalhador);

        biometria.MapGet("/termo", Termo);
        biometria.MapGet("/autorizacao", Autorizacao);
        biometria.MapPost("/autorizacao", Conceder);
        biometria.MapDelete("/autorizacao", Revogar);

        app.MapDelete("/api/trabalhadores/{cpf}/biometria", Desligar).WithTags("Biometria").RequireAuthorization(Autenticacao.SoRh);
    }

    private static TermoMostrado Termo(TermoDeConsentimento termo) => new(termo.Versao, termo.Texto, termo.Hash, AutorizacaoBiometrica.Finalidade);

    private static async Task<IResult> Autorizacao(ClaimsPrincipal usuario, BancoDoPonto banco, IChaveMestra chaveMestra, CancellationToken cancelamento)
    {
        var vigente = await banco.Empresa(usuario).Biometria(chaveMestra).Autorizacoes.Vigente(usuario.Cpf(), cancelamento);

        return vigente is null
            ? throw new NaoEncontrado("Você não tem autorização biométrica vigente: a marcação é pelo código pessoal.")
            : TypedResults.Ok(new AutorizacaoMostrada(vigente.Termo.Versao, vigente.Termo.Hash, Comum.NaHora(vigente.ConcedidaEm)));
    }

    /// <summary>
    /// O trabalhador autoriza dizendo qual versão do termo leu. Se o termo
    /// mudou desde que ele o abriu, a autorização é recusada: ninguém
    /// consente com um texto que não leu.
    /// </summary>
    private static async Task<IResult> Conceder(PedidoDeAutorizacao pedido, ClaimsPrincipal usuario, BancoDoPonto banco, IChaveMestra chaveMestra, TermoDeConsentimento termo, TimeProvider relogio, CancellationToken cancelamento)
    {
        if (pedido.HashDoTermo != termo.Hash)
        {
            throw new ExcecaoDeDominio($"O termo de consentimento mudou: a versão em vigor é a {termo.Versao}. Leia o termo de novo antes de autorizar.");
        }

        var empresa = banco.Empresa(usuario);
        var empregador = await empresa.Empregador(cancelamento);
        var trabalhador = await empresa.Trabalhadores.Obter(usuario.Cpf(), cancelamento);
        var autorizacao = AutorizacaoBiometrica.Conceder(empregador, trabalhador, termo, relogio.GetUtcNow());
        await empresa.Biometria(chaveMestra).Autorizacoes.Conceder(autorizacao, cancelamento);

        return TypedResults.Created("/api/biometria/autorizacao", new AutorizacaoMostrada(termo.Versao, termo.Hash, Comum.NaHora(autorizacao.ConcedidaEm)));
    }

    private static async Task<IResult> Revogar(ClaimsPrincipal usuario, BancoDoPonto banco, IChaveMestra chaveMestra, TimeProvider relogio, CancellationToken cancelamento)
    {
        var agora = relogio.GetUtcNow();
        var eliminados = await banco.Empresa(usuario).Biometria(chaveMestra).Autorizacoes.Encerrar(usuario.Cpf(), autorizacao => autorizacao.Revogar(agora), cancelamento);

        return TypedResults.Ok(new Encerramento(eliminados.Count));
    }

    private static async Task<IResult> Desligar(string cpf, ClaimsPrincipal usuario, BancoDoPonto banco, IChaveMestra chaveMestra, TimeProvider relogio, CancellationToken cancelamento)
    {
        var agora = relogio.GetUtcNow();
        var eliminados = await banco.Empresa(usuario).Biometria(chaveMestra).Autorizacoes.Encerrar(Cpf.De(cpf), autorizacao => autorizacao.EncerrarPeloDesligamento(agora), cancelamento);

        return TypedResults.Ok(new Encerramento(eliminados.Count));
    }
}

internal sealed record TermoMostrado(string Versao, string Texto, string Hash, string Finalidade);

/// <param name="HashDoTermo">O SHA-256 do termo que o trabalhador leu, como <c>GET /api/biometria/termo</c> o mostrou.</param>
internal sealed record PedidoDeAutorizacao(string HashDoTermo);

internal sealed record AutorizacaoMostrada(string VersaoDoTermo, string HashDoTermo, string ConcedidaEm);

/// <summary>A autorização acabou; os modelos do rosto apagados com ela.</summary>
internal sealed record Encerramento(int ModelosApagados);
