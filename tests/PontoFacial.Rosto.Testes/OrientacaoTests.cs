using SkiaSharp;

namespace PontoFacial.Rosto.Testes;

/*
 * Uma imagem gravada de 3×2, em que o azul de cada pixel diz de onde ele veio
 * — 10 × linha + coluna —:
 *
 *    0  1  2
 *   10 11 12
 *
 * e a imagem de pé que cada orientação do EXIF manda mostrar.
 */
public sealed class OrientacaoTests
{
    public static TheoryData<SKEncodedOrigin, int, int, byte[]> Orientacoes => new()
    {
        { SKEncodedOrigin.TopLeft, 3, 2, [0, 1, 2, 10, 11, 12] },
        { SKEncodedOrigin.TopRight, 3, 2, [2, 1, 0, 12, 11, 10] },
        { SKEncodedOrigin.BottomRight, 3, 2, [12, 11, 10, 2, 1, 0] },
        { SKEncodedOrigin.BottomLeft, 3, 2, [10, 11, 12, 0, 1, 2] },
        { SKEncodedOrigin.LeftTop, 2, 3, [0, 10, 1, 11, 2, 12] },
        { SKEncodedOrigin.RightTop, 2, 3, [10, 0, 11, 1, 12, 2] },
        { SKEncodedOrigin.RightBottom, 2, 3, [12, 2, 11, 1, 10, 0] },
        { SKEncodedOrigin.LeftBottom, 2, 3, [2, 12, 1, 11, 0, 10] },
    };

    [Theory]
    [MemberData(nameof(Orientacoes))]
    public void Cada_orientacao_do_exif_poe_a_imagem_de_pe(SKEncodedOrigin origem, int largura, int altura, byte[] azuis)
    {
        var (bgr, novaLargura, novaAltura) = Orientacao.Aplicar(Gravada(), 3, 2, origem);

        Assert.Equal((largura, altura), (novaLargura, novaAltura));
        Assert.Equal(azuis, bgr.Where((_, i) => i % 3 == 0));
    }

    [Fact]
    public void O_pixel_inteiro_vai_junto()
    {
        var (bgr, _, _) = Orientacao.Aplicar(Gravada(), 3, 2, SKEncodedOrigin.RightTop);

        // O primeiro pixel de pé é o 10 gravado: azul 10, verde 110, vermelho 210.
        Assert.Equal([10, 110, 210], bgr[..3]);
    }

    private static byte[] Gravada() =>
    [
        0, 100, 200, 1, 101, 201, 2, 102, 202,
        10, 110, 210, 11, 111, 211, 12, 112, 212,
    ];
}
