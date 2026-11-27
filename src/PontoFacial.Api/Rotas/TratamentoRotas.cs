using System.Security.Claims;
using PontoFacial.Api.Configuracao;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Persistencia;

namespace PontoFacial.Api.Rotas;

/// <summary>
/// O tratamento do ponto (art. 82 da Portaria 671): só o RH inclui,
/// desconsidera e abona, e o responsável de cada tratamento é quem está no
/// token (ADR-0017).
/// </summary>
internal static class TratamentoRotas
{
    public static void MapTratamento(this IEndpointRouteBuilder app)
    {
        var tratamento = app
            .MapGroup("/api/estabelecimentos/{estabelecimento:guid}/trabalhadores/{cpf}")
            .WithTags("Tratamento")
            .RequireAuthorization(Autenticacao.SoRh);

        tratamento.MapPost("/inclusoes", Incluir);
        tratamento.MapPost("/desconsideracoes", Desconsiderar);
        tratamento.MapPost("/abonos", Abonar);
    }

    private static async Task<IResult> Incluir(Guid estabelecimento, string cpf, PedidoDeInclusao pedido, ClaimsPrincipal usuario, BancoDoPonto banco, TimeProvider relogio, CancellationToken cancelamento)
    {
        var responsavel = usuario.Cpf();
        var tratada = await banco.Empresa(usuario).Tratamentos.Tratar(
            estabelecimento,
            Cpf.De(cpf),
            tratamento => tratamento.Incluir(pedido.MarcadaEm, pedido.Motivo, responsavel, relogio.GetUtcNow()),
            cancelamento);

        return TypedResults.Created($"/api/estabelecimentos/{estabelecimento}/trabalhadores/{cpf}/espelho", Feito(tratada));
    }

    private static async Task<IResult> Desconsiderar(Guid estabelecimento, string cpf, PedidoDeDesconsideracao pedido, ClaimsPrincipal usuario, BancoDoPonto banco, TimeProvider relogio, CancellationToken cancelamento)
    {
        var responsavel = usuario.Cpf();
        var tratada = await banco.Empresa(usuario).Tratamentos.Tratar(
            estabelecimento,
            Cpf.De(cpf),
            tratamento => tratamento.Desconsiderar(pedido.Nsr, pedido.Motivo, responsavel, relogio.GetUtcNow()),
            cancelamento);

        return TypedResults.Created($"/api/estabelecimentos/{estabelecimento}/trabalhadores/{cpf}/espelho", Feito(tratada));
    }

    private static async Task<IResult> Abonar(Guid estabelecimento, string cpf, PedidoDeAbono pedido, ClaimsPrincipal usuario, BancoDoPonto banco, TimeProvider relogio, CancellationToken cancelamento)
    {
        var responsavel = usuario.Cpf();
        var abono = await banco.Empresa(usuario).Tratamentos.Tratar(
            estabelecimento,
            Cpf.De(cpf),
            tratamento => tratamento.Abonar(pedido.Dia, pedido.Motivo, responsavel, relogio.GetUtcNow()),
            cancelamento);

        return TypedResults.Created(
            $"/api/estabelecimentos/{estabelecimento}/trabalhadores/{cpf}/espelho",
            new TratamentoFeito("A", null, null, abono.Dia, abono.Motivo, abono.Responsavel.Formatado, Comum.NaHora(abono.Em)));
    }

    private static TratamentoFeito Feito(MarcacaoTratada tratada) => new(
        ((char)tratada.Tipo).ToString(),
        Comum.NaHora(tratada.MarcadaEm),
        tratada.Nsr,
        null,
        tratada.Motivo,
        tratada.Responsavel.Formatado,
        Comum.NaHora(tratada.Em));
}

/// <param name="MarcadaEm">A hora da marcação que faltou, com o fuso: <c>2026-11-19T13:00:00-03:00</c>.</param>
internal sealed record PedidoDeInclusao(DateTimeOffset MarcadaEm, string Motivo);

internal sealed record PedidoDeDesconsideracao(long Nsr, string Motivo);

internal sealed record PedidoDeAbono(DateOnly Dia, string Motivo);

/// <summary>O tratamento gravado, com a letra do espelho: I, D ou A.</summary>
internal sealed record TratamentoFeito(string Tipo, string? MarcadaEm, long? Nsr, DateOnly? Dia, string Motivo, string Responsavel, string Em);
