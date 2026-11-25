using SkiaSharp;

namespace PontoFacial.Rosto;

/// <summary>
/// Põe a imagem de pé pela orientação que a câmera gravou no arquivo (a
/// etiqueta Orientation do EXIF). O celular grava a foto como o sensor a viu
/// e anota o giro; quem lê tem de aplicar, ou o rosto chega deitado e o
/// detector não o acha.
/// </summary>
internal static class Orientacao
{
    public static (byte[] Bgr, int Largura, int Altura) Aplicar(byte[] bgr, int largura, int altura, SKEncodedOrigin origem)
    {
        if (origem is SKEncodedOrigin.TopLeft)
        {
            return (bgr, largura, altura);
        }

        // As quatro que trocam linha por coluna deixam a imagem deitada de pé.
        var deitada = origem is SKEncodedOrigin.LeftTop or SKEncodedOrigin.RightTop
            or SKEncodedOrigin.RightBottom or SKEncodedOrigin.LeftBottom;
        var (novaLargura, novaAltura) = deitada ? (altura, largura) : (largura, altura);
        var resultado = new byte[bgr.Length];

        for (var y = 0; y < novaAltura; y++)
        {
            for (var x = 0; x < novaLargura; x++)
            {
                // De onde vem, na imagem gravada, o pixel (x, y) da imagem de pé.
                var (xGravado, yGravado) = origem switch
                {
                    SKEncodedOrigin.TopRight => (largura - 1 - x, y),
                    SKEncodedOrigin.BottomRight => (largura - 1 - x, altura - 1 - y),
                    SKEncodedOrigin.BottomLeft => (x, altura - 1 - y),
                    SKEncodedOrigin.LeftTop => (y, x),
                    SKEncodedOrigin.RightTop => (y, altura - 1 - x),
                    SKEncodedOrigin.RightBottom => (largura - 1 - y, altura - 1 - x),
                    SKEncodedOrigin.LeftBottom => (largura - 1 - y, x),
                    _ => (x, y),
                };

                Array.Copy(bgr, ((yGravado * largura) + xGravado) * 3, resultado, ((y * novaLargura) + x) * 3, 3);
            }
        }

        return (resultado, novaLargura, novaAltura);
    }
}
