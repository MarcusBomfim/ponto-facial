using System.IO.Compression;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using PontoFacial.Api.Configuracao;
using PontoFacial.Assinaturas;
using PontoFacial.Dominio.Arquivos;
using PontoFacial.Persistencia;

namespace PontoFacial.Api.Rotas;

/// <summary>
/// O AFD e o AEJ para o Auditor-Fiscal do Trabalho, cada um com a assinatura
/// CAdES no .p7s ao lado (arts. 81, 83 e 88 da Portaria 671), num .zip só:
/// os dois arquivos saem juntos, e o .p7s assina exatamente o .txt que vai com
/// ele.
/// </summary>
internal static class ArquivosRotas
{
    public static void MapArquivos(this IEndpointRouteBuilder app)
    {
        var arquivos = app.MapGroup("/api/estabelecimentos/{estabelecimento:guid}").WithTags("Arquivos da Portaria").RequireAuthorization(Autenticacao.SoRh);

        arquivos.MapGet("/afd", Afd);
        arquivos.MapGet("/aej", Aej);
    }

    private static async Task<IResult> Afd(
        Guid estabelecimento,
        DateOnly de,
        DateOnly ate,
        ClaimsPrincipal usuario,
        BancoDoPonto banco,
        ProgramaDoPonto programa,
        X509Certificate2 certificado,
        Politica politica,
        TimeProvider relogio,
        CancellationToken cancelamento)
    {
        var agora = relogio.GetUtcNow();
        var afd = await banco.Empresa(usuario).Arquivos.Afd(estabelecimento, de, ate, programa, agora, cancelamento);

        return Pacote(afd.NomeDoArquivo, afd.Conteudo(), certificado, politica, agora);
    }

    private static async Task<IResult> Aej(
        Guid estabelecimento,
        DateOnly de,
        DateOnly ate,
        ClaimsPrincipal usuario,
        BancoDoPonto banco,
        ProgramaDoPonto programa,
        X509Certificate2 certificado,
        Politica politica,
        TimeProvider relogio,
        CancellationToken cancelamento)
    {
        var agora = relogio.GetUtcNow();
        var aej = await banco.Empresa(usuario).Arquivos.Aej(estabelecimento, de, ate, programa, agora, cancelamento);

        return Pacote(aej.NomeDoArquivo, aej.Conteudo(), certificado, politica, agora);
    }

    /// <summary>O .zip com o arquivo e o .p7s dele, de mesmo nome.</summary>
    private static Microsoft.AspNetCore.Http.HttpResults.FileContentHttpResult Pacote(string nome, byte[] conteudo, X509Certificate2 certificado, Politica politica, DateTimeOffset agora)
    {
        var assinatura = AssinaturaCades.Destacada(conteudo, certificado, agora, politica.Valor);

        using var memoria = new MemoryStream();

        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            Escrever(zip, nome, conteudo);
            Escrever(zip, Path.ChangeExtension(nome, ".p7s"), assinatura);
        }

        return TypedResults.File(memoria.ToArray(), "application/zip", Path.ChangeExtension(nome, ".zip"));
    }

    private static void Escrever(ZipArchive zip, string nome, byte[] conteudo)
    {
        using var entrada = zip.CreateEntry(nome, CompressionLevel.Optimal).Open();
        entrada.Write(conteudo);
    }
}
