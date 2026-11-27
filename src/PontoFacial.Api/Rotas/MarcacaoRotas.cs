using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using PontoFacial.Api.Configuracao;
using PontoFacial.Assinaturas;
using PontoFacial.Dominio.Arquivos;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Registro;
using PontoFacial.Persistencia;

namespace PontoFacial.Api.Rotas;

/// <summary>
/// A marcação do trabalhador e o comprovante dela, em PDF assinado (arts. 79
/// e 80 da Portaria 671).
/// </summary>
/// <remarks>
/// <para>
/// O trabalhador marca autenticado: o token é dele, e ele entrou com a senha
/// que só ele sabe. É o código pessoal do domínio, e a marcação vale o mesmo
/// que a pelo rosto (ADR-0002). A marcação pelo rosto, com a prova de vida
/// diante da câmera, entra na Parte 7, com o aplicativo.
/// </para>
/// <para>
/// A hora da marcação é a do relógio do servidor, lida dentro da fila do
/// estabelecimento, e não a de quando o pedido chegou: duas marcações juntas
/// na virada do minuto nunca fazem o relógio do registro voltar (ADR-0013).
/// </para>
/// </remarks>
internal static class MarcacaoRotas
{
    /// <summary>O mínimo que o trabalhador consegue extrair dos comprovantes (art. 80, parágrafo único, III).</summary>
    public static readonly TimeSpan JanelaDosComprovantes = TimeSpan.FromHours(48);

    public static void MapMarcacoes(this IEndpointRouteBuilder app)
    {
        var estabelecimento = app.MapGroup("/api/estabelecimentos/{estabelecimento:guid}").WithTags("Marcação");

        estabelecimento.MapPost("/marcacoes", Marcar).RequireAuthorization(Autenticacao.SoTrabalhador);
        estabelecimento.MapGet("/marcacoes/{nsr:long}/comprovante", Comprovante).RequireAuthorization(Autenticacao.QualquerPapel);
        estabelecimento.MapGet("/comprovantes", Comprovantes).RequireAuthorization(Autenticacao.SoTrabalhador);
    }

    private static async Task<IResult> Marcar(Guid estabelecimento, PedidoDeMarcacao pedido, ClaimsPrincipal usuario, BancoDoPonto banco, TimeProvider relogio, CancellationToken cancelamento)
    {
        if (!Enum.IsDefined(pedido.Coletor))
        {
            throw new ExcecaoDeDominio("O coletor não é um dos do Arquivo Fonte de Dados.");
        }

        var empresa = banco.Empresa(usuario);
        var empregador = await empresa.Empregador(cancelamento);
        var trabalhador = await empresa.Trabalhadores.Obter(usuario.Cpf(), cancelamento);

        var marcacao = await empresa.Registros.Gravar(
            estabelecimento,
            registro => registro.MarcarOnLine(IdentificacaoDoTrabalhador.PorCodigoPessoal(empregador, trabalhador, relogio.GetUtcNow()), pedido.Coletor),
            cancelamento);

        var comprovante = $"/api/estabelecimentos/{estabelecimento}/marcacoes/{marcacao.Nsr}/comprovante";

        return TypedResults.Created(comprovante, new MarcacaoFeita(marcacao.Nsr, Comum.NaHora(marcacao.MarcadaEm), marcacao.Hash, comprovante));
    }

    /// <summary>O comprovante em PDF assinado: o trabalhador só o dele; o RH e o gestor, o de qualquer um.</summary>
    private static async Task<IResult> Comprovante(
        Guid estabelecimento,
        long nsr,
        ClaimsPrincipal usuario,
        BancoDoPonto banco,
        ProgramaDoPonto programa,
        X509Certificate2 certificado,
        Politica politica,
        TimeProvider relogio,
        CancellationToken cancelamento)
    {
        var comprovante = await banco.Empresa(usuario).Registros.Comprovante(estabelecimento, nsr, programa.RegistroNoInpi, cancelamento);

        if (Comum.SoOProprio(usuario, comprovante.CpfDoTrabalhador) is { } recusa)
        {
            return recusa;
        }

        var pdf = ComprovanteEmPdf.Assinado(comprovante, certificado, relogio.GetUtcNow(), politica.Valor);

        return TypedResults.File(pdf, "application/pdf", $"comprovante-{nsr:D9}.pdf");
    }

    /// <summary>As marcações do trabalhador nas últimas 48 horas, cada uma com o endereço do comprovante.</summary>
    private static async Task<IResult> Comprovantes(Guid estabelecimento, ClaimsPrincipal usuario, BancoDoPonto banco, TimeProvider relogio, CancellationToken cancelamento)
    {
        var desde = relogio.GetUtcNow() - JanelaDosComprovantes;
        var tratamento = await banco.Empresa(usuario).Tratamentos.Obter(estabelecimento, usuario.Cpf(), cancelamento);

        return TypedResults.Ok(tratamento.Registradas()
            .Where(marcacao => marcacao.GravadaEm >= desde)
            .Select(marcacao => new MarcacaoFeita(
                marcacao.Nsr,
                Comum.NaHora(marcacao.MarcadaEm),
                marcacao.Hash,
                $"/api/estabelecimentos/{estabelecimento}/marcacoes/{marcacao.Nsr}/comprovante"))
            .ToList());
    }
}

internal sealed record PedidoDeMarcacao(Coletor Coletor);

/// <summary>A marcação gravada: o NSR, a hora, o hash e onde está o comprovante.</summary>
internal sealed record MarcacaoFeita(long Nsr, string MarcadaEm, string Hash, string Comprovante);
