namespace PontoFacial.Api.Configuracao;

/// <summary>As mensagens de log da API, geradas pelo compilador: sem custo quando o nível está desligado.</summary>
internal static partial class Registros
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Sem Assinatura:Certificado: os documentos saem assinados por um certificado de desenvolvimento, que não vale para a fiscalização.")]
    public static partial void CertificadoDeDesenvolvimento(this ILogger log);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Conteúdo adulterado no banco: {Mensagem}")]
    public static partial void ConteudoAdulterado(this ILogger log, Exception erro, string mensagem);

    [LoggerMessage(Level = LogLevel.Information, Message = "Migrações aplicadas: {Migracoes}")]
    public static partial void MigracoesAplicadas(this ILogger log, string migracoes);
}
