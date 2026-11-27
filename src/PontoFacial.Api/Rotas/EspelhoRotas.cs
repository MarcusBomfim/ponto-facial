using System.Security.Claims;
using PontoFacial.Api.Configuracao;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Persistencia;

namespace PontoFacial.Api.Rotas;

/// <summary>
/// O espelho de ponto do art. 84: o trabalhador vê o dele, o RH e o gestor,
/// o de todos. Sai em texto, uma informação por linha, com o horário que
/// valia no período (ADR-0015).
/// </summary>
internal static class EspelhoRotas
{
    public static void MapEspelho(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/estabelecimentos/{estabelecimento:guid}/trabalhadores/{cpf}/espelho", Espelho)
            .WithTags("Espelho")
            .RequireAuthorization(Autenticacao.QualquerPapel);

    private static async Task<IResult> Espelho(Guid estabelecimento, string cpf, DateOnly de, DateOnly ate, ClaimsPrincipal usuario, BancoDoPonto banco, TimeProvider relogio, CancellationToken cancelamento)
    {
        var numero = Cpf.De(cpf);

        if (Comum.SoOProprio(usuario, numero) is { } recusa)
        {
            return recusa;
        }

        var empresa = banco.Empresa(usuario);
        var trabalhador = await empresa.Trabalhadores.Obter(numero, cancelamento);
        var horario = await empresa.Horarios.NoPeriodo(numero, de, ate, cancelamento)
            ?? throw new ExcecaoDeDominio($"{trabalhador.Nome} não tem horário contratual em {de:dd/MM/yyyy}: o espelho precisa do horário e da jornada do contrato (art. 84, IV).");
        var calendario = await empresa.Feriados.Calendario(estabelecimento, cancelamento);
        var tratamento = await empresa.Tratamentos.Obter(estabelecimento, numero, cancelamento);
        var espelho = EspelhoDePonto.Gerar(tratamento, trabalhador, horario, calendario, de, ate, relogio.GetUtcNow());

        return TypedResults.Text(string.Join('\n', espelho.Linhas()) + "\n", "text/plain; charset=utf-8");
    }
}
