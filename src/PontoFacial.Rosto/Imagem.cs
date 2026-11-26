using System.Runtime.InteropServices;
using SkiaSharp;

namespace PontoFacial.Rosto;

/// <summary>
/// Uma imagem em memória, de pé, com três bytes por pixel na ordem azul,
/// verde e vermelho — a ordem em que os modelos do OpenCV foram treinados.
/// </summary>
/// <remarks>
/// A imagem vive só enquanto o rosto é extraído: nada aqui a grava, e o que
/// sai do reconhecedor é o vetor do rosto, não a foto (ADR-0004).
/// </remarks>
public sealed class Imagem
{
    private Imagem(int largura, int altura, byte[] bgr)
    {
        Largura = largura;
        Altura = altura;
        Bgr = bgr;
    }

    public int Largura { get; }

    public int Altura { get; }

    /// <summary>Linha a linha, de cima para baixo: azul, verde e vermelho de cada pixel.</summary>
    internal byte[] Bgr { get; }

    /// <summary>Monta a imagem a partir dos pixels, já na ordem azul, verde e vermelho.</summary>
    public static Imagem DeBgr(int largura, int altura, ReadOnlySpan<byte> bgr)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(largura);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(altura);

        if (bgr.Length != largura * altura * 3)
        {
            throw new ArgumentException($"Uma imagem de {largura}×{altura} tem {largura * altura * 3} bytes; vieram {bgr.Length}.", nameof(bgr));
        }

        return new Imagem(largura, altura, bgr.ToArray());
    }

    /// <summary>Uma cópia dos pixels, na ordem de <see cref="DeBgr"/>.</summary>
    public byte[] CopiarBgr() => [.. Bgr];

    /// <summary>
    /// Lê um JPEG, PNG ou WebP, e põe a imagem de pé pela orientação que a
    /// câmera gravou: a foto de celular tirada de lado chega deitada.
    /// </summary>
    public static bool TentarDecodificar(ReadOnlySpan<byte> arquivo, out Imagem? imagem)
    {
        imagem = null;

        using var dados = SKData.CreateCopy(arquivo);
        using var codec = SKCodec.Create(dados);

        if (codec is null)
        {
            return false;
        }

        var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        var resultado = codec.GetPixels(info, bitmap.GetPixels());

        if (resultado is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            return false;
        }

        var bgr = DeBgra(bitmap.GetPixelSpan(), info.Width, info.Height, bitmap.RowBytes);
        var (orientada, largura, altura) = Orientacao.Aplicar(bgr, info.Width, info.Height, codec.EncodedOrigin);
        imagem = new Imagem(largura, altura, orientada);

        return true;
    }

    /// <summary>
    /// A imagem reduzida para que o lado maior tenha no máximo
    /// <paramref name="ladoMaximo"/> pixels, sem mudar a proporção. Se já
    /// cabe, é a própria.
    /// </summary>
    public Imagem Reduzida(int ladoMaximo)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ladoMaximo);

        var maior = Math.Max(Largura, Altura);

        if (maior <= ladoMaximo)
        {
            return this;
        }

        var fator = (double)ladoMaximo / maior;
        var largura = Math.Max(1, (int)Math.Round(Largura * fator));
        var altura = Math.Max(1, (int)Math.Round(Altura * fator));

        using var original = ParaBitmap();
        using var reduzida = original.Resize(
            new SKImageInfo(largura, altura, SKColorType.Bgra8888, SKAlphaType.Unpremul),
            new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));

        return new Imagem(largura, altura, DeBgra(reduzida.GetPixelSpan(), largura, altura, reduzida.RowBytes));
    }

    private SKBitmap ParaBitmap()
    {
        var bitmap = new SKBitmap(new SKImageInfo(Largura, Altura, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        var destino = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(bitmap.GetPixelSpan()), bitmap.ByteCount);

        for (int y = 0, origem = 0; y < Altura; y++)
        {
            var linha = y * bitmap.RowBytes;

            for (var x = 0; x < Largura; x++, origem += 3)
            {
                var pixel = linha + (x * 4);
                destino[pixel] = Bgr[origem];
                destino[pixel + 1] = Bgr[origem + 1];
                destino[pixel + 2] = Bgr[origem + 2];
                destino[pixel + 3] = 255;
            }
        }

        return bitmap;
    }

    private static byte[] DeBgra(ReadOnlySpan<byte> bgra, int largura, int altura, int bytesPorLinha)
    {
        var bgr = new byte[largura * altura * 3];

        for (int y = 0, destino = 0; y < altura; y++)
        {
            var linha = y * bytesPorLinha;

            for (var x = 0; x < largura; x++, destino += 3)
            {
                var pixel = linha + (x * 4);
                bgr[destino] = bgra[pixel];
                bgr[destino + 1] = bgra[pixel + 1];
                bgr[destino + 2] = bgra[pixel + 2];
            }
        }

        return bgr;
    }
}
