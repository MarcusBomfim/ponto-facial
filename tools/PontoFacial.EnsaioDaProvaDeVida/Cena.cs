using PontoFacial.Rosto;

namespace PontoFacial.EnsaioDaProvaDeVida;

/// <summary>
/// Como a câmera do coletor vê uma foto segurada diante dela: a foto em
/// tamanho real, na forma da superfície, girada, a uma distância, com o
/// tremor da mão e o ruído do sensor.
/// </summary>
/// <remarks>
/// <para>
/// A imagem é feita raio a raio: para cada pixel da câmera, o raio que sai
/// dela encontra a superfície em algum ponto da foto, e o pixel recebe a cor
/// dali. A geometria é exata — a perspectiva, a curva, a dobra —; o que a
/// cena não imita é a luz: o brilho do papel e da tela, o moiré, o borrão
/// do movimento (ADR-0008).
/// </para>
/// <para>
/// A câmera tem 640×480 pixels e 60° de abertura na horizontal, como uma
/// webcam comum. A foto é impressa em tamanho real: os olhos a 63 mm.
/// </para>
/// </remarks>
internal sealed class Cena
{
    public const int Largura = 640;

    public const int Altura = 480;

    /// <summary>A distância focal em pixels: 60° de abertura em 640 pixels.</summary>
    public const double Focal = 554;

    public const double OlhosEmMilimetros = 63;

    private readonly Foto _foto;

    private readonly double _milimetrosPorPixel;

    private readonly double _centroX;

    private readonly double _centroY;

    /// <param name="foto">A foto e os cinco pontos do rosto nela.</param>
    /// <param name="escala">De outra foto, quando várias são mostradas no mesmo lugar, como na troca de fotos.</param>
    public Cena(Foto foto, Foto? escala = null)
    {
        _foto = foto;
        var medida = escala ?? foto;
        _milimetrosPorPixel = OlhosEmMilimetros / medida.DistanciaEntreOlhos;
        _centroX = medida.CentroX;
        _centroY = medida.CentroY;
    }

    /// <summary>
    /// O quadro com a foto na superfície dada, girada como uma cabeça: o giro
    /// positivo leva o que está à frente para a direita da imagem, e a
    /// inclinação positiva, para baixo.
    /// </summary>
    public Imagem Ver(Superficie superficie, Pose pose, Random? ruido = null)
    {
        var giro = pose.Giro * Math.PI / 180;
        var inclinacao = pose.Inclinacao * Math.PI / 180;
        double cg = Math.Cos(giro), sg = Math.Sin(giro), ci = Math.Cos(inclinacao), si = Math.Sin(inclinacao);

        // Da foto para a câmera: primeiro o giro, depois a inclinação.
        double[] r =
        [
            cg, 0, -sg,
            -si * sg, ci, -si * cg,
            ci * sg, si, ci * cg,
        ];

        // A câmera, no espaço da foto: Rᵀ·(−T).
        var t = new Vetor(-pose.DeslocamentoX, -pose.DeslocamentoY, -pose.Distancia);
        var origem = new Vetor(
            (r[0] * t.X) + (r[3] * t.Y) + (r[6] * t.Z),
            (r[1] * t.X) + (r[4] * t.Y) + (r[7] * t.Z),
            (r[2] * t.X) + (r[5] * t.Y) + (r[8] * t.Z));

        var bgr = new byte[Largura * Altura * 3];

        for (var y = 0; y < Altura; y++)
        {
            for (var x = 0; x < Largura; x++)
            {
                var dx = (x + 0.5 - (Largura / 2.0)) / Focal;
                var dy = (y + 0.5 - (Altura / 2.0)) / Focal;
                var direcao = new Vetor(
                    (r[0] * dx) + (r[3] * dy) + r[6],
                    (r[1] * dx) + (r[4] * dy) + r[7],
                    (r[2] * dx) + (r[5] * dy) + r[8]);
                var destino = ((y * Largura) + x) * 3;

                if (superficie.Intersectar(origem, direcao, out var u, out var v))
                {
                    _foto.Pintar(_centroX + (u / _milimetrosPorPixel), _centroY + (v / _milimetrosPorPixel), bgr, destino);
                }
                else
                {
                    Foto.PintarFundo(bgr, destino);
                }
            }
        }

        if (ruido is not null)
        {
            Ruido(bgr, ruido);
        }

        return Imagem.DeBgr(Largura, Altura, bgr);
    }

    /// <summary>O ruído do sensor: normal, com desvio de 3 níveis de cinza.</summary>
    private static void Ruido(byte[] bgr, Random sorteio)
    {
        for (var i = 0; i < bgr.Length; i++)
        {
            var normal = Math.Sqrt(-2 * Math.Log(1 - sorteio.NextDouble())) * Math.Cos(2 * Math.PI * sorteio.NextDouble());
            bgr[i] = (byte)Math.Clamp(Math.Round(bgr[i] + (3 * normal)), 0, 255);
        }
    }
}

/// <summary>Como a foto é segurada num quadro: o giro e a inclinação em graus, a distância e o deslocamento em milímetros.</summary>
internal readonly record struct Pose(double Giro, double Inclinacao, double Distancia = 400, double DeslocamentoX = 0, double DeslocamentoY = 0)
{
    /// <summary>A mesma pose, com o tremor da mão: frações de grau e de milímetro.</summary>
    public Pose ComTremor(Random sorteio) => this with
    {
        Giro = Giro + (0.3 * Normal(sorteio)),
        Inclinacao = Inclinacao + (0.3 * Normal(sorteio)),
        DeslocamentoX = DeslocamentoX + (0.5 * Normal(sorteio)),
        DeslocamentoY = DeslocamentoY + (0.5 * Normal(sorteio)),
    };

    private static double Normal(Random sorteio) =>
        Math.Sqrt(-2 * Math.Log(1 - sorteio.NextDouble())) * Math.Cos(2 * Math.PI * sorteio.NextDouble());
}
