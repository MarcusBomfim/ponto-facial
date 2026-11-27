using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PontoFacial.Dominio.Comum;
using PontoFacial.Persistencia;

namespace PontoFacial.Api.Configuracao;

/// <summary>
/// O que deu errado vira um ProblemDetails (RFC 9457), com um tipo para cada
/// caso e a mensagem do domínio, que já é escrita para quem usa o sistema. O
/// catálogo está em <c>docs/api/problemas.md</c>.
/// </summary>
internal sealed class TratadorDeErros(IProblemDetailsService problemas, ILogger<TratadorDeErros> log) : IExceptionHandler
{
    public const string Prefixo = "https://github.com/MarcusBomfim/ponto-facial/blob/main/docs/api/problemas.md#";

    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception erro, CancellationToken cancelamento)
    {
        var (status, tipo, titulo) = erro switch
        {
            ExcecaoDeDominio => (StatusCodes.Status422UnprocessableEntity, "regra-do-ponto", "A regra do ponto recusou o pedido."),
            NaoEncontrado => (StatusCodes.Status404NotFound, "nao-encontrado", "Não encontrado."),
            JaExiste => (StatusCodes.Status409Conflict, "ja-existe", "Já existe."),
            ConteudoAdulterado => (StatusCodes.Status500InternalServerError, "conteudo-adulterado", "O que está no banco não é o que o sistema gravou."),
            BadHttpRequestException => (StatusCodes.Status400BadRequest, "pedido-invalido", "O pedido não está no formato da API."),
            _ => (0, string.Empty, string.Empty),
        };

        if (status == 0)
        {
            return false;
        }

        if (erro is ConteudoAdulterado)
        {
            log.ConteudoAdulterado(erro, erro.Message);
        }

        contexto.Response.StatusCode = status;

        return await problemas.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = erro,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Type = Prefixo + tipo,
                Title = titulo,
                Detail = erro is BadHttpRequestException ? erro.InnerException?.Message ?? erro.Message : erro.Message,
            },
        });
    }
}
