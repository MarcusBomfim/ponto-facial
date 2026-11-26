using System.Security.Cryptography;

namespace PontoFacial.Persistencia.Biometria;

/// <summary>
/// A chave que embrulha as chaves das empresas. Ela não fica no banco: quem
/// copia o banco inteiro leva as chaves das empresas embrulhadas, e não abre
/// nenhuma (ADR-0014).
/// </summary>
/// <remarks>
/// Em produção, é um serviço de chaves — um cofre na nuvem, um HSM —, que
/// embrulha e desembrulha sem deixar a chave mestra sair dele; por isso as
/// operações são assíncronas. A <see cref="ChaveMestraLocal"/> é a chave na
/// memória do processo, para desenvolvimento e para quem guarda a chave num
/// arquivo de segredo.
/// </remarks>
public interface IChaveMestra
{
    /// <summary>Como a chave mestra se chama: o nome fica gravado ao lado de cada chave que ela embrulhou.</summary>
    string Id { get; }

    /// <summary>A chave embrulhada, amarrada ao contexto.</summary>
    Task<byte[]> Embrulhar(ReadOnlyMemory<byte> chave, ReadOnlyMemory<byte> contexto, CancellationToken cancelamento);

    /// <exception cref="AuthenticationTagMismatchException">O embrulho foi mexido, o contexto é outro, ou a chave mestra é outra.</exception>
    Task<byte[]> Desembrulhar(ReadOnlyMemory<byte> embrulhada, ReadOnlyMemory<byte> contexto, CancellationToken cancelamento);
}

/// <summary>A chave mestra na memória do processo: 32 bytes, com o AES-256-GCM.</summary>
public sealed class ChaveMestraLocal : IChaveMestra
{
    /// <summary>O nome cabe em 100 caracteres, a coluna onde fica gravado.</summary>
    public const int TamanhoDoId = 100;

    private readonly byte[] _chave;

    public ChaveMestraLocal(string id, ReadOnlySpan<byte> chave)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > TamanhoDoId)
        {
            throw new ArgumentException($"A chave mestra precisa de um nome, de até {TamanhoDoId} caracteres.", nameof(id));
        }

        if (chave.Length != Cifra.TamanhoDaChave)
        {
            throw new ArgumentException($"A chave mestra tem {Cifra.TamanhoDaChave} bytes; vieram {chave.Length}.", nameof(chave));
        }

        Id = id;
        _chave = chave.ToArray();
    }

    public string Id { get; }

    /// <summary>A chave em base64, como fica num arquivo de segredo.</summary>
    public static ChaveMestraLocal DeBase64(string id, string base64)
    {
        ArgumentNullException.ThrowIfNull(base64);

        return new ChaveMestraLocal(id, Convert.FromBase64String(base64));
    }

    /// <summary>Uma chave sorteada agora, pelo gerador criptográfico.</summary>
    public static ChaveMestraLocal Sortear(string id) => new(id, RandomNumberGenerator.GetBytes(Cifra.TamanhoDaChave));

    public Task<byte[]> Embrulhar(ReadOnlyMemory<byte> chave, ReadOnlyMemory<byte> contexto, CancellationToken cancelamento)
    {
        var (nonce, cifrado) = Cifra.Cifrar(_chave, chave.Span, contexto.Span);

        return Task.FromResult<byte[]>([.. nonce, .. cifrado]);
    }

    public Task<byte[]> Desembrulhar(ReadOnlyMemory<byte> embrulhada, ReadOnlyMemory<byte> contexto, CancellationToken cancelamento)
    {
        var embrulho = embrulhada.Span;

        if (embrulho.Length < Cifra.TamanhoDoNonce)
        {
            throw new AuthenticationTagMismatchException();
        }

        return Task.FromResult(Cifra.Decifrar(_chave, embrulho[..Cifra.TamanhoDoNonce], embrulho[Cifra.TamanhoDoNonce..], contexto.Span));
    }
}
