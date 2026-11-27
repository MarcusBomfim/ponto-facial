namespace PontoFacial.Assinaturas;

/// <summary>
/// A política de assinatura que a assinatura declara seguir: o identificador
/// dela, o hash do documento da política e onde ele pode ser lido.
/// </summary>
/// <remarks>
/// A ICP-Brasil publica as suas políticas de assinatura — a AD-RB é a de
/// referência básica — com o identificador e o hash de cada versão. Esses
/// valores vêm da configuração da instalação, copiados da lista publicada; o
/// código não os inventa (ADR-0016).
/// </remarks>
/// <param name="Identificador">O OID da política.</param>
/// <param name="AlgoritmoDoHash">O OID do algoritmo do hash do documento da política: <c>2.16.840.1.101.3.4.2.1</c>, o SHA-256.</param>
/// <param name="Hash">O hash do documento da política, como publicado.</param>
/// <param name="Endereco">Onde o documento da política pode ser lido, se há.</param>
public sealed record PoliticaDeAssinatura(string Identificador, string AlgoritmoDoHash, ReadOnlyMemory<byte> Hash, Uri? Endereco = null);
