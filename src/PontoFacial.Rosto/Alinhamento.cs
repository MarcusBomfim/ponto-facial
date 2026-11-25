namespace PontoFacial.Rosto;

/// <summary>
/// Uma semelhança do plano — girar, mudar de escala e deslocar, sem
/// deformar: <c>x' = a·x − b·y + tx</c>, <c>y' = b·x + a·y + ty</c>.
/// </summary>
internal readonly record struct Semelhanca(double A, double B, double Tx, double Ty)
{
    public (double X, double Y) Aplicar(double x, double y) => ((A * x) - (B * y) + Tx, (B * x) + (A * y) + Ty);

    public Semelhanca Inversa()
    {
        var escala = (A * A) + (B * B);
        var a = A / escala;
        var b = -B / escala;

        return new Semelhanca(a, b, -((a * Tx) - (b * Ty)), -((b * Tx) + (a * Ty)));
    }
}

/// <summary>
/// Leva o rosto à posição em que o SFace foi treinado: os cinco pontos sobre
/// o molde de 112×112 do ArcFace, pela semelhança que melhor os encaixa, e o
/// recorte por interpolação bilinear — o <c>alignCrop</c> do OpenCV.
/// </summary>
internal static class Alinhamento
{
    public const int Lado = 112;

    /// <summary>Olho direito, olho esquerdo, nariz, canto direito e canto esquerdo da boca, no recorte de 112×112.</summary>
    public static readonly (double X, double Y)[] Molde =
    [
        (38.2946, 51.6963), (73.5318, 51.5014), (56.0252, 71.7366), (41.5493, 92.3655), (70.7299, 92.2041),
    ];

    /// <summary>
    /// A semelhança que leva os pontos do rosto aos do molde com o menor erro
    /// quadrático. Em duas dimensões, é a mesma de Umeyama, que o OpenCV usa,
    /// em forma fechada.
    /// </summary>
    public static Semelhanca Estimar(IReadOnlyList<Ponto> pontos)
    {
        if (pontos.Count != Molde.Length)
        {
            throw new ArgumentException($"O alinhamento usa {Molde.Length} pontos; vieram {pontos.Count}.", nameof(pontos));
        }

        double mediaX = 0, mediaY = 0, mediaU = 0, mediaV = 0;

        for (var i = 0; i < Molde.Length; i++)
        {
            mediaX += pontos[i].X / (double)Molde.Length;
            mediaY += pontos[i].Y / (double)Molde.Length;
            mediaU += Molde[i].X / Molde.Length;
            mediaV += Molde[i].Y / Molde.Length;
        }

        double somaA = 0, somaB = 0, quadrados = 0;

        for (var i = 0; i < Molde.Length; i++)
        {
            var x = pontos[i].X - mediaX;
            var y = pontos[i].Y - mediaY;
            var u = Molde[i].X - mediaU;
            var v = Molde[i].Y - mediaV;

            somaA += (x * u) + (y * v);
            somaB += (x * v) - (y * u);
            quadrados += (x * x) + (y * y);
        }

        if (quadrados == 0)
        {
            throw new ArgumentException("Os cinco pontos do rosto caem no mesmo lugar.", nameof(pontos));
        }

        var a = somaA / quadrados;
        var b = somaB / quadrados;

        return new Semelhanca(a, b, mediaU - ((a * mediaX) - (b * mediaY)), mediaV - ((b * mediaX) + (a * mediaY)));
    }

    /// <summary>
    /// O recorte de 112×112 já como entrada do SFace: planos vermelho, verde e
    /// azul, de 0 a 255. Fora da imagem, preto.
    /// </summary>
    public static float[] RecortarParaOSFace(Imagem imagem, Semelhanca semelhanca)
    {
        var deVolta = semelhanca.Inversa();
        var plano = Lado * Lado;
        var entrada = new float[3 * plano];

        for (var v = 0; v < Lado; v++)
        {
            for (var u = 0; u < Lado; u++)
            {
                var (x, y) = deVolta.Aplicar(u, v);
                var (azul, verde, vermelho) = Bilinear(imagem, x, y);
                var destino = (v * Lado) + u;

                entrada[destino] = vermelho;
                entrada[plano + destino] = verde;
                entrada[(2 * plano) + destino] = azul;
            }
        }

        return entrada;
    }

    private static (float Azul, float Verde, float Vermelho) Bilinear(Imagem imagem, double x, double y)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var fx = (float)(x - x0);
        var fy = (float)(y - y0);

        float azul = 0, verde = 0, vermelho = 0;

        void Somar(int px, int py, float peso)
        {
            if (peso == 0 || px < 0 || py < 0 || px >= imagem.Largura || py >= imagem.Altura)
            {
                return;
            }

            var i = ((py * imagem.Largura) + px) * 3;
            azul += imagem.Bgr[i] * peso;
            verde += imagem.Bgr[i + 1] * peso;
            vermelho += imagem.Bgr[i + 2] * peso;
        }

        Somar(x0, y0, (1 - fx) * (1 - fy));
        Somar(x0 + 1, y0, fx * (1 - fy));
        Somar(x0, y0 + 1, (1 - fx) * fy);
        Somar(x0 + 1, y0 + 1, fx * fy);

        return (azul, verde, vermelho);
    }
}
