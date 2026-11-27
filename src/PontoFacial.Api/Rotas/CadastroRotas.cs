using System.Security.Claims;
using PontoFacial.Api.Configuracao;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Persistencia;

namespace PontoFacial.Api.Rotas;

/// <summary>Os estabelecimentos, os trabalhadores, os horários e os feriados: o que o RH cadastra.</summary>
internal static class CadastroRotas
{
    public static void MapCadastro(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").WithTags("Cadastro");

        api.MapPost("/estabelecimentos", CadastrarEstabelecimento).RequireAuthorization(Autenticacao.SoRh);
        api.MapGet("/estabelecimentos/{estabelecimento:guid}", ObterEstabelecimento).RequireAuthorization(Autenticacao.QualquerPapel);
        api.MapPut("/estabelecimentos/{estabelecimento:guid}/feriados/{data}", CadastrarFeriado).RequireAuthorization(Autenticacao.SoRh);
        api.MapDelete("/estabelecimentos/{estabelecimento:guid}/feriados/{data}", RemoverFeriado).RequireAuthorization(Autenticacao.SoRh);
        api.MapGet("/estabelecimentos/{estabelecimento:guid}/feriados", ListarFeriados).RequireAuthorization(Autenticacao.QualquerPapel);
        api.MapPut("/trabalhadores/{cpf}", GravarTrabalhador).RequireAuthorization(Autenticacao.SoRh);
        api.MapGet("/trabalhadores/{cpf}", ObterTrabalhador).RequireAuthorization(Autenticacao.QualquerPapel);
        api.MapPut("/trabalhadores/{cpf}/horarios/{vigenteDesde}", GravarHorario).RequireAuthorization(Autenticacao.SoRh);
        api.MapGet("/trabalhadores/{cpf}/horarios", ListarHorarios).RequireAuthorization(Autenticacao.QualquerPapel);
    }

    private static async Task<IResult> CadastrarEstabelecimento(PedidoDeEstabelecimento pedido, ClaimsPrincipal usuario, BancoDoPonto banco, CancellationToken cancelamento)
    {
        var empresa = banco.Empresa(usuario);
        var estabelecimento = Estabelecimento.Novo(await empresa.Empregador(cancelamento), pedido.Local, pedido.Fuso);
        var id = await empresa.Estabelecimentos.Cadastrar(estabelecimento, cancelamento);

        return TypedResults.Created($"/api/estabelecimentos/{id}", new EstabelecimentoCadastrado(id, estabelecimento.Local, estabelecimento.Fuso.Id));
    }

    private static async Task<IResult> ObterEstabelecimento(Guid estabelecimento, ClaimsPrincipal usuario, BancoDoPonto banco, CancellationToken cancelamento)
    {
        var lido = await banco.Empresa(usuario).Estabelecimentos.Obter(estabelecimento, cancelamento);

        return TypedResults.Ok(new EstabelecimentoCadastrado(estabelecimento, lido.Local, lido.Fuso.Id));
    }

    private static async Task<IResult> CadastrarFeriado(Guid estabelecimento, DateOnly data, PedidoDeFeriado pedido, ClaimsPrincipal usuario, BancoDoPonto banco, CancellationToken cancelamento)
    {
        await banco.Empresa(usuario).Feriados.Cadastrar(estabelecimento, data, pedido.Nome, cancelamento);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> RemoverFeriado(Guid estabelecimento, DateOnly data, ClaimsPrincipal usuario, BancoDoPonto banco, CancellationToken cancelamento) =>
        await banco.Empresa(usuario).Feriados.Remover(estabelecimento, data, cancelamento)
            ? TypedResults.NoContent()
            : throw new NaoEncontrado($"Não há feriado local em {data:dd/MM/yyyy} neste estabelecimento.");

    private static async Task<IResult> ListarFeriados(Guid estabelecimento, ClaimsPrincipal usuario, BancoDoPonto banco, CancellationToken cancelamento)
    {
        var calendario = await banco.Empresa(usuario).Feriados.Calendario(estabelecimento, cancelamento);

        return TypedResults.Ok(calendario.Locais.OrderBy(par => par.Key).Select(par => new FeriadoLocal(par.Key, par.Value)).ToList());
    }

    private static async Task<IResult> GravarTrabalhador(string cpf, PedidoDeTrabalhador pedido, ClaimsPrincipal usuario, BancoDoPonto banco, CancellationToken cancelamento)
    {
        await banco.Empresa(usuario).Trabalhadores.Gravar(Trabalhador.Novo(pedido.Nome, Cpf.De(cpf), pedido.Admissao, pedido.Cargo), cancelamento);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> ObterTrabalhador(string cpf, ClaimsPrincipal usuario, BancoDoPonto banco, CancellationToken cancelamento)
    {
        var numero = Cpf.De(cpf);

        if (Comum.SoOProprio(usuario, numero) is { } recusa)
        {
            return recusa;
        }

        var trabalhador = await banco.Empresa(usuario).Trabalhadores.Obter(numero, cancelamento);

        return TypedResults.Ok(new TrabalhadorCadastrado(trabalhador.Cpf.Formatado, trabalhador.Nome, trabalhador.Admissao, trabalhador.Cargo));
    }

    private static async Task<IResult> GravarHorario(string cpf, DateOnly vigenteDesde, PedidoDeHorario pedido, ClaimsPrincipal usuario, BancoDoPonto banco, CancellationToken cancelamento)
    {
        await banco.Empresa(usuario).Horarios.Gravar(Cpf.De(cpf), vigenteDesde, Comum.Horario(pedido), cancelamento);

        return TypedResults.NoContent();
    }

    private static async Task<IResult> ListarHorarios(string cpf, ClaimsPrincipal usuario, BancoDoPonto banco, CancellationToken cancelamento)
    {
        var numero = Cpf.De(cpf);

        if (Comum.SoOProprio(usuario, numero) is { } recusa)
        {
            return recusa;
        }

        var horarios = await banco.Empresa(usuario).Horarios.DoTrabalhador(numero, cancelamento);

        return TypedResults.Ok(horarios.Select(horario => new HorarioCadastrado(horario.VigenteDesde, horario.Horario.Descricao())).ToList());
    }
}

internal sealed record PedidoDeEstabelecimento(string Local, string Fuso);

internal sealed record EstabelecimentoCadastrado(Guid Id, string Local, string Fuso);

internal sealed record PedidoDeFeriado(string Nome);

internal sealed record FeriadoLocal(DateOnly Data, string Nome);

internal sealed record PedidoDeTrabalhador(string Nome, DateOnly Admissao, string Cargo);

internal sealed record TrabalhadorCadastrado(string Cpf, string Nome, DateOnly Admissao, string Cargo);

internal sealed record HorarioCadastrado(DateOnly VigenteDesde, string Descricao);
