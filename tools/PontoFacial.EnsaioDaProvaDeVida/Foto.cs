using PontoFacial.Rosto;

namespace PontoFacial.EnsaioDaProvaDeVida;

/// <summary>Uma foto de rosto que vai virar instrumento de ataque, com os cinco pontos que o detector achou nela.</summary>
internal sealed class Foto
{
    private static readonly byte[] Parede = [120, 125, 130];

    private readonly byte[] _bgr;

    public Foto(Imagem imagem, RostoDetectado rosto)
    {
        ArgumentNullException.ThrowIfNull(imagem);
        ArgumentNullException.ThrowIfNull(rosto);

        _bgr = imagem.CopiarBgr();
        Largura = imagem.Largura;
        Altura = imagem.Altura;
        Rosto = rosto;
    }

    public int Largura { get; }

    public int Altura { get; }

    public RostoDetectado Rosto { get; }

    public double DistanciaEntreOlhos => Rosto.DistanciaEntreOlhos;

    /// <summary>O centro do rosto: a média dos olhos e dos cantos da boca.</summary>
    public double CentroX => (Rosto.OlhoDireito.X + Rosto.OlhoEsquerdo.X + Rosto.BocaDireita.X + Rosto.BocaEsquerda.X) / 4.0;

    public double CentroY => (Rosto.OlhoDireito.Y + Rosto.OlhoEsquerdo.Y + Rosto.BocaDireita.Y + Rosto.BocaEsquerda.Y) / 4.0;

    /// <summary>A parede atrás da foto, de um cinza neutro.</summary>
    public static void PintarFundo(byte[] destino, int indice) => Parede.CopyTo(destino, indice);

    /// <summary>A cor da foto no ponto, por interpolação bilinear; fora da foto, a parede.</summary>
    public void Pintar(double x, double y, byte[] destino, int indice)
    {
        if (x < 0 || y < 0 || x > Largura - 1 || y > Altura - 1)
        {
            PintarFundo(destino, indice);
            return;
        }

        var x0 = (int)x;
        var y0 = (int)y;
        var x1 = Math.Min(x0 + 1, Largura - 1);
        var y1 = Math.Min(y0 + 1, Altura - 1);
        var fx = x - x0;
        var fy = y - y0;

        for (var canal = 0; canal < 3; canal++)
        {
            var valor = (_bgr[(((y0 * Largura) + x0) * 3) + canal] * (1 - fx) * (1 - fy))
                + (_bgr[(((y0 * Largura) + x1) * 3) + canal] * fx * (1 - fy))
                + (_bgr[(((y1 * Largura) + x0) * 3) + canal] * (1 - fx) * fy)
                + (_bgr[(((y1 * Largura) + x1) * 3) + canal] * fx * fy);
            destino[indice + canal] = (byte)Math.Round(valor);
        }
    }
}
