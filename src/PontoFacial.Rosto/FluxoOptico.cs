namespace PontoFacial.Rosto;

/// <summary>
/// A imagem em tons de cinza, em níveis de metade do tamanho cada: o
/// movimento grande vira pequeno nos níveis de cima.
/// </summary>
internal sealed class Piramide
{
    public const int Niveis = 3;

    private readonly float[][] _niveis;

    private readonly int[] _larguras;

    private readonly int[] _alturas;

    private Piramide(float[][] niveis, int[] larguras, int[] alturas)
    {
        _niveis = niveis;
        _larguras = larguras;
        _alturas = alturas;
    }

    public static Piramide De(Imagem imagem)
    {
        var niveis = new float[Niveis][];
        var larguras = new int[Niveis];
        var alturas = new int[Niveis];

        var cinza = new float[imagem.Largura * imagem.Altura];

        for (var i = 0; i < cinza.Length; i++)
        {
            cinza[i] = (0.114f * imagem.Bgr[i * 3]) + (0.587f * imagem.Bgr[(i * 3) + 1]) + (0.299f * imagem.Bgr[(i * 3) + 2]);
        }

        niveis[0] = cinza;
        larguras[0] = imagem.Largura;
        alturas[0] = imagem.Altura;

        for (var n = 1; n < Niveis; n++)
        {
            var largura = Math.Max(1, larguras[n - 1] / 2);
            var altura = Math.Max(1, alturas[n - 1] / 2);
            var abaixo = niveis[n - 1];
            var larguraAbaixo = larguras[n - 1];
            var nivel = new float[largura * altura];

            for (var y = 0; y < altura; y++)
            {
                for (var x = 0; x < largura; x++)
                {
                    var x0 = Math.Min(2 * x, larguraAbaixo - 1);
                    var x1 = Math.Min((2 * x) + 1, larguraAbaixo - 1);
                    var y0 = Math.Min(2 * y, alturas[n - 1] - 1);
                    var y1 = Math.Min((2 * y) + 1, alturas[n - 1] - 1);

                    nivel[(y * largura) + x] = (abaixo[(y0 * larguraAbaixo) + x0] + abaixo[(y0 * larguraAbaixo) + x1]
                        + abaixo[(y1 * larguraAbaixo) + x0] + abaixo[(y1 * larguraAbaixo) + x1]) / 4;
                }
            }

            niveis[n] = nivel;
            larguras[n] = largura;
            alturas[n] = altura;
        }

        return new Piramide(niveis, larguras, alturas);
    }

    /// <summary>O cinza no ponto, por interpolação bilinear; fora da imagem, o da borda.</summary>
    public float Amostra(int nivel, double x, double y)
    {
        var largura = _larguras[nivel];
        var altura = _alturas[nivel];
        x = Math.Clamp(x, 0, Math.Max(0, largura - 1.0001));
        y = Math.Clamp(y, 0, Math.Max(0, altura - 1.0001));

        var x0 = (int)x;
        var y0 = (int)y;
        var x1 = Math.Min(x0 + 1, largura - 1);
        var y1 = Math.Min(y0 + 1, altura - 1);
        var fx = (float)(x - x0);
        var fy = (float)(y - y0);
        var cinza = _niveis[nivel];

        return (cinza[(y0 * largura) + x0] * (1 - fx) * (1 - fy))
            + (cinza[(y0 * largura) + x1] * fx * (1 - fy))
            + (cinza[(y1 * largura) + x0] * (1 - fx) * fy)
            + (cinza[(y1 * largura) + x1] * fx * fy);
    }
}

/// <summary>
/// O fluxo óptico de Lucas e Kanade, em pirâmide: onde um pedaço da imagem
/// anterior foi parar na seguinte, achado pelo gradiente, do nível mais
/// reduzido ao da imagem inteira.
/// </summary>
/// <remarks>
/// Cada ponto também é acompanhado de volta, da imagem seguinte para a
/// anterior. Se a volta não cai onde ele estava, o ponto se perdeu: pedaço
/// liso demais, rosto coberto, foto trocada (Kalal, Mikolajczyk e Matas,
/// "Forward-Backward Error", 2010).
/// </remarks>
internal static class FluxoOptico
{
    /// <summary>O maior erro da ida e volta, em pixels, para o ponto continuar valendo.</summary>
    public const double ErroDeIdaEVolta = 1.0;

    private const int Iteracoes = 30;

    /// <summary>O menor autovalor da matriz do gradiente, por pixel da janela, para a janela ter textura.</summary>
    private const double TexturaMinima = 0.01;

    /// <summary>Onde o ponto foi parar, conferido pela volta; nada se ele se perdeu.</summary>
    public static (double X, double Y)? Acompanhar(Piramide anterior, Piramide seguinte, double x, double y, int meiaJanela)
    {
        var ida = Seguir(anterior, seguinte, x, y, meiaJanela);

        if (ida is not { } destino)
        {
            return null;
        }

        var volta = Seguir(seguinte, anterior, destino.X, destino.Y, meiaJanela);

        if (volta is not { } origem || Math.Sqrt(((origem.X - x) * (origem.X - x)) + ((origem.Y - y) * (origem.Y - y))) > ErroDeIdaEVolta)
        {
            return null;
        }

        return destino;
    }

    internal static (double X, double Y)? Seguir(Piramide de, Piramide para, double x, double y, int meiaJanela)
    {
        double deslocamentoX = 0, deslocamentoY = 0;

        for (var nivel = Piramide.Niveis - 1; nivel >= 0; nivel--)
        {
            var escala = 1 << nivel;
            var px = x / escala;
            var py = y / escala;
            var w = Math.Max(3, (meiaJanela / escala) + 2);
            var lado = (2 * w) + 1;
            var modelo = new float[lado * lado];
            var gx = new float[lado * lado];
            var gy = new float[lado * lado];
            double g11 = 0, g12 = 0, g22 = 0;

            for (int j = -w, k = 0; j <= w; j++)
            {
                for (var i = -w; i <= w; i++, k++)
                {
                    modelo[k] = de.Amostra(nivel, px + i, py + j);
                    gx[k] = (de.Amostra(nivel, px + i + 1, py + j) - de.Amostra(nivel, px + i - 1, py + j)) / 2;
                    gy[k] = (de.Amostra(nivel, px + i, py + j + 1) - de.Amostra(nivel, px + i, py + j - 1)) / 2;
                    g11 += gx[k] * gx[k];
                    g12 += gx[k] * gy[k];
                    g22 += gy[k] * gy[k];
                }
            }

            var determinante = (g11 * g22) - (g12 * g12);
            var menorAutovalor = ((g11 + g22) / 2) - Math.Sqrt((((g11 - g22) / 2) * ((g11 - g22) / 2)) + (g12 * g12));

            if (menorAutovalor / (lado * lado) < TexturaMinima)
            {
                return null;
            }

            double dx = deslocamentoX, dy = deslocamentoY;

            for (var iteracao = 0; iteracao < Iteracoes; iteracao++)
            {
                double b1 = 0, b2 = 0;

                for (int j = -w, k = 0; j <= w; j++)
                {
                    for (var i = -w; i <= w; i++, k++)
                    {
                        var diferenca = modelo[k] - para.Amostra(nivel, px + i + dx, py + j + dy);
                        b1 += diferenca * gx[k];
                        b2 += diferenca * gy[k];
                    }
                }

                var passoX = ((g22 * b1) - (g12 * b2)) / determinante;
                var passoY = ((g11 * b2) - (g12 * b1)) / determinante;
                dx += passoX;
                dy += passoY;

                if ((passoX * passoX) + (passoY * passoY) < 1e-4)
                {
                    break;
                }
            }

            (deslocamentoX, deslocamentoY) = nivel > 0 ? (2 * dx, 2 * dy) : (dx, dy);
        }

        return double.IsFinite(deslocamentoX) && double.IsFinite(deslocamentoY) ? (x + deslocamentoX, y + deslocamentoY) : null;
    }
}
