using System.Security.Claims;
using PontoFacial.Api.Configuracao;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Registro;
using PontoFacial.Persistencia;

namespace PontoFacial.Api.Rotas;

/// <summary>
/// O registro de ponto de cada estabelecimento: o RH o abre e inclui, altera
/// e exclui os trabalhadores. A hora de cada operação é a do relógio do
/// servidor, lida dentro da fila do estabelecimento (ADR-0017).
/// </summary>
internal static class RegistroRotas
{
    public static void MapRegistro(this IEndpointRouteBuilder app)
    {
        var registro = app.MapGroup("/api/estabelecimentos/{estabelecimento:guid}/registro").WithTags("Registro").RequireAuthorization(Autenticacao.SoRh);

        registro.MapPost("/", Abrir);
        registro.MapPost("/inclusoes", Incluir);
        registro.MapPost("/alteracoes", Alterar);
        registro.MapPost("/exclusoes", Excluir);
    }

    private static async Task<IResult> Abrir(Guid estabelecimento, ClaimsPrincipal usuario, BancoDoPonto banco, TimeProvider relogio, CancellationToken cancelamento)
    {
        var abertura = await banco.Empresa(usuario).Registros.Abrir(estabelecimento, usuario.Cpf(), relogio.GetUtcNow(), cancelamento);

        return TypedResults.Created($"/api/estabelecimentos/{estabelecimento}/registro", Feita(abertura));
    }

    private static async Task<IResult> Incluir(Guid estabelecimento, PedidoSobreTrabalhador pedido, ClaimsPrincipal usuario, BancoDoPonto banco, TimeProvider relogio, CancellationToken cancelamento)
    {
        var empresa = banco.Empresa(usuario);
        var trabalhador = await empresa.Trabalhadores.Obter(Cpf.De(pedido.Cpf), cancelamento);
        var responsavel = usuario.Cpf();
        var inclusao = await empresa.Registros.Gravar(estabelecimento, registro => registro.IncluirTrabalhador(trabalhador, responsavel, relogio.GetUtcNow()), cancelamento);

        return TypedResults.Created($"/api/estabelecimentos/{estabelecimento}/registro", Feita(inclusao));
    }

    /// <summary>O nome do trabalhador mudou no cadastro: o registro grava o nome novo.</summary>
    private static async Task<IResult> Alterar(Guid estabelecimento, PedidoSobreTrabalhador pedido, ClaimsPrincipal usuario, BancoDoPonto banco, TimeProvider relogio, CancellationToken cancelamento)
    {
        var empresa = banco.Empresa(usuario);
        var trabalhador = await empresa.Trabalhadores.Obter(Cpf.De(pedido.Cpf), cancelamento);
        var responsavel = usuario.Cpf();
        var alteracao = await empresa.Registros.Gravar(estabelecimento, registro => registro.AlterarTrabalhador(trabalhador, responsavel, relogio.GetUtcNow()), cancelamento);

        return TypedResults.Created($"/api/estabelecimentos/{estabelecimento}/registro", Feita(alteracao));
    }

    private static async Task<IResult> Excluir(Guid estabelecimento, PedidoSobreTrabalhador pedido, ClaimsPrincipal usuario, BancoDoPonto banco, TimeProvider relogio, CancellationToken cancelamento)
    {
        var cpf = Cpf.De(pedido.Cpf);
        var responsavel = usuario.Cpf();
        var exclusao = await banco.Empresa(usuario).Registros.Gravar(estabelecimento, registro => registro.ExcluirTrabalhador(cpf, responsavel, relogio.GetUtcNow()), cancelamento);

        return TypedResults.Created($"/api/estabelecimentos/{estabelecimento}/registro", Feita(exclusao));
    }

    private static OperacaoGravada Feita(Operacao operacao) => new(operacao.Nsr, operacao.TipoDoRegistro, Comum.NaHora(operacao.GravadaEm));
}

internal sealed record PedidoSobreTrabalhador(string Cpf);

/// <summary>Uma operação gravada no registro: o NSR, o tipo no AFD e a hora do registro.</summary>
internal sealed record OperacaoGravada(long Nsr, int Tipo, string GravadaEm);
