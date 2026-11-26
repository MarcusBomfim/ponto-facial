using System.Security.Cryptography;

namespace PontoFacial.Persistencia.Biometria;

/// <summary>
/// O AES-256-GCM, como este sistema o usa: chave de 32 bytes, nonce de 12
/// sorteado a cada cifra, etiqueta de 16, e o contexto — os dados associados
/// — amarrando a cifra ao lugar dela.
/// </summary>
/// <remarks>
/// O contexto não vai cifrado, mas entra na etiqueta: a cifra só abre com o
/// mesmo contexto com que foi feita. É ele que impede de colar a cifra do
/// rosto de um trabalhador na linha de outro (ADR-0014).
/// </remarks>
internal static class Cifra
{
    public const int TamanhoDaChave = 32;

    public const int TamanhoDoNonce = 12;

    public const int TamanhoDaEtiqueta = 16;

    /// <summary>O nonce sorteado e a cifra, com a etiqueta no fim.</summary>
    public static (byte[] Nonce, byte[] Cifrado) Cifrar(ReadOnlySpan<byte> chave, ReadOnlySpan<byte> aberto, ReadOnlySpan<byte> contexto)
    {
        var nonce = RandomNumberGenerator.GetBytes(TamanhoDoNonce);
        var cifrado = new byte[aberto.Length + TamanhoDaEtiqueta];

        using var aes = new AesGcm(chave, TamanhoDaEtiqueta);
        aes.Encrypt(nonce, aberto, cifrado.AsSpan(0, aberto.Length), cifrado.AsSpan(aberto.Length), contexto);

        return (nonce, cifrado);
    }

    /// <exception cref="AuthenticationTagMismatchException">
    /// A cifra, o nonce ou o contexto não são os da cifra feita, ou a chave é outra.
    /// </exception>
    public static byte[] Decifrar(ReadOnlySpan<byte> chave, ReadOnlySpan<byte> nonce, ReadOnlySpan<byte> cifrado, ReadOnlySpan<byte> contexto)
    {
        if (nonce.Length != TamanhoDoNonce || cifrado.Length < TamanhoDaEtiqueta)
        {
            throw new AuthenticationTagMismatchException();
        }

        var aberto = new byte[cifrado.Length - TamanhoDaEtiqueta];

        using var aes = new AesGcm(chave, TamanhoDaEtiqueta);
        aes.Decrypt(nonce, cifrado[..^TamanhoDaEtiqueta], cifrado[^TamanhoDaEtiqueta..], aberto, contexto);

        return aberto;
    }
}
