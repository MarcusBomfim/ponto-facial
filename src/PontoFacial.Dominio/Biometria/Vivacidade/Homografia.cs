namespace PontoFacial.Dominio.Biometria.Vivacidade;

/// <summary>
/// A transformação projetiva do plano que leva os olhos e os cantos da boca
/// do quadro de referência ao quadro atual. É o movimento exato, na imagem,
/// de qualquer coisa plana — uma foto, uma tela — vista por uma câmera.
/// </summary>
/// <remarks>
/// As coordenadas são centradas e divididas pela distância entre os olhos
/// antes de resolver o sistema, para que ele não dependa do tamanho da
/// imagem.
/// </remarks>
internal readonly struct Homografia
{
    private readonly double[] _h;

    private readonly PontoNaImagem _origemDaReferencia;

    private readonly PontoNaImagem _origemDoQuadro;

    private readonly double _escalaDaReferencia;

    private readonly double _escalaDoQuadro;

    private Homografia(double[] h, PontoNaImagem origemDaReferencia, PontoNaImagem origemDoQuadro, double escalaDaReferencia, double escalaDoQuadro)
    {
        _h = h;
        _origemDaReferencia = origemDaReferencia;
        _origemDoQuadro = origemDoQuadro;
        _escalaDaReferencia = escalaDaReferencia;
        _escalaDoQuadro = escalaDoQuadro;
    }

    /// <summary>A homografia dos quatro pontos do plano, ou nada se eles estiverem alinhados.</summary>
    public static Homografia? DoPlano(PontosDoRosto referencia, PontosDoRosto atual)
    {
        PontoNaImagem[] de = [referencia.OlhoDireito, referencia.OlhoEsquerdo, referencia.BocaDireita, referencia.BocaEsquerda];
        PontoNaImagem[] para = [atual.OlhoDireito, atual.OlhoEsquerdo, atual.BocaDireita, atual.BocaEsquerda];

        var origemDe = Centro(de);
        var origemPara = Centro(para);
        var escalaDe = referencia.DistanciaEntreOlhos;
        var escalaPara = atual.DistanciaEntreOlhos;

        // Oito equações, oito incógnitas, h₈ = 1. A linha i, coluna j fica em i·9 + j.
        var sistema = new double[8 * 9];

        for (var k = 0; k < 4; k++)
        {
            var x = (de[k].X - origemDe.X) / escalaDe;
            var y = (de[k].Y - origemDe.Y) / escalaDe;
            var u = (para[k].X - origemPara.X) / escalaPara;
            var v = (para[k].Y - origemPara.Y) / escalaPara;

            double[] primeira = [x, y, 1, 0, 0, 0, -x * u, -y * u, u];
            double[] segunda = [0, 0, 0, x, y, 1, -x * v, -y * v, v];
            primeira.CopyTo(sistema, 2 * k * 9);
            segunda.CopyTo(sistema, ((2 * k) + 1) * 9);
        }

        var solucao = Resolver(sistema, 8);

        return solucao is null ? null : new Homografia([.. solucao, 1], origemDe, origemPara, escalaDe, escalaPara);
    }

    /// <summary>Onde o ponto da referência estaria no quadro atual se ele estivesse no plano.</summary>
    public PontoNaImagem Aplicar(PontoNaImagem ponto)
    {
        var x = (ponto.X - _origemDaReferencia.X) / _escalaDaReferencia;
        var y = (ponto.Y - _origemDaReferencia.Y) / _escalaDaReferencia;
        var w = (_h[6] * x) + (_h[7] * y) + 1;
        var u = ((_h[0] * x) + (_h[1] * y) + _h[2]) / w;
        var v = ((_h[3] * x) + (_h[4] * y) + _h[5]) / w;

        return new PontoNaImagem(_origemDoQuadro.X + (_escalaDoQuadro * u), _origemDoQuadro.Y + (_escalaDoQuadro * v));
    }

    /// <summary>
    /// Pela derivada da homografia no centro do rosto: o seno da inclinação
    /// do plano, a direção da imagem em que ele encurtou, e a escala do
    /// quadro atual em relação à referência.
    /// </summary>
    /// <remarks>
    /// A derivada é uma transformação linear de 2×2. Os valores singulares
    /// dela, σ₁ ≥ σ₂, dizem tudo: σ₁ é a escala (a direção que não encurtou),
    /// e σ₂/σ₁ é o cosseno da inclinação do plano em relação à referência.
    /// </remarks>
    public (double Encurtamento, double DirecaoX, double DirecaoY, double Escala) Inclinacao()
    {
        var razao = _escalaDoQuadro / _escalaDaReferencia;
        var a11 = (_h[0] - (_h[6] * _h[2])) * razao;
        var a12 = (_h[1] - (_h[7] * _h[2])) * razao;
        var a21 = (_h[3] - (_h[6] * _h[5])) * razao;
        var a22 = (_h[4] - (_h[7] * _h[5])) * razao;

        var soma = (a11 * a11) + (a12 * a12) + (a21 * a21) + (a22 * a22);
        var determinante = (a11 * a22) - (a12 * a21);
        var raiz = Math.Sqrt(Math.Max(0, (soma * soma) - (4 * determinante * determinante)));
        var maior = Math.Sqrt((soma + raiz) / 2);
        var menor = Math.Sqrt(Math.Max(0, (soma - raiz) / 2));
        var encurtamento = maior == 0 ? 0 : Math.Sqrt(Math.Max(0, 1 - (menor * menor / (maior * maior))));

        // A direção encurtada na imagem: o autovetor de A·Aᵀ do menor autovalor.
        var m11 = (a11 * a11) + (a12 * a12);
        var m12 = (a11 * a21) + (a12 * a22);
        var m22 = (a21 * a21) + (a22 * a22);
        var autovalor = menor * menor;
        var (x1, y1) = (m12, autovalor - m11);
        var (x2, y2) = (autovalor - m22, m12);
        var (x, y) = (x1 * x1) + (y1 * y1) >= (x2 * x2) + (y2 * y2) ? (x1, y1) : (x2, y2);
        var norma = Math.Sqrt((x * x) + (y * y));

        return norma == 0 ? (0, 1, 0, maior) : (encurtamento, x / norma, y / norma, maior);
    }

    private static PontoNaImagem Centro(PontoNaImagem[] pontos) =>
        new(pontos.Average(ponto => ponto.X), pontos.Average(ponto => ponto.Y));

    /// <summary>Eliminação de Gauss com pivô parcial; nada se o sistema for singular.</summary>
    private static double[]? Resolver(double[] sistema, int n)
    {
        var colunas = n + 1;

        for (var c = 0; c < n; c++)
        {
            var pivo = c;

            for (var l = c + 1; l < n; l++)
            {
                if (Math.Abs(sistema[(l * colunas) + c]) > Math.Abs(sistema[(pivo * colunas) + c]))
                {
                    pivo = l;
                }
            }

            if (Math.Abs(sistema[(pivo * colunas) + c]) < 1e-12)
            {
                return null;
            }

            for (var j = 0; j < colunas; j++)
            {
                (sistema[(c * colunas) + j], sistema[(pivo * colunas) + j]) = (sistema[(pivo * colunas) + j], sistema[(c * colunas) + j]);
            }

            for (var l = 0; l < n; l++)
            {
                if (l == c)
                {
                    continue;
                }

                var fator = sistema[(l * colunas) + c] / sistema[(c * colunas) + c];

                for (var j = c; j < colunas; j++)
                {
                    sistema[(l * colunas) + j] -= fator * sistema[(c * colunas) + j];
                }
            }
        }

        var solucao = new double[n];

        for (var c = 0; c < n; c++)
        {
            solucao[c] = sistema[(c * colunas) + n] / sistema[(c * colunas) + c];
        }

        return solucao;
    }
}
