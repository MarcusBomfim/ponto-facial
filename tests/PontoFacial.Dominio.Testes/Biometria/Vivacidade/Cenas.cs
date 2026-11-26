using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Biometria.Vivacidade;

namespace PontoFacial.Dominio.Testes.Biometria.Vivacidade;

/// <summary>Um ponto no espaço, em milímetros: X para a direita da imagem, Y para baixo, Z para a câmera.</summary>
internal readonly record struct Ponto3D(double X, double Y, double Z);

/// <summary>
/// Uma cabeça de teste e as fotos dela — plana, curvada, dobrada —, com os
/// nove pontos que a prova de vida acompanha, vistos por uma câmera.
/// </summary>
/// <remarks>
/// As medidas da cabeça são de teste, escolhidas para as contas saírem de
/// cabeça, e não medidas de gente: o plano dos olhos e dos cantos da boca é
/// Z = 0, o nariz fica 24 mm à frente, a raiz do nariz 10, o centro da boca
/// 12, e as bochechas 6 mm atrás. Com os olhos a 62 mm, o relevo lateral é
/// (24 + 6) / 31 = 0,968 e o vertical, 24/31 menos a reta entre 10/31 e
/// 12/31 na altura do nariz (38 de 66 mm): 0,415.
/// </remarks>
internal static class Cenas
{
    public const double RelevoLateralDaCabeca = (24.0 + 6.0) / 31.0;

    public const double RelevoVerticalDaCabeca = (24.0 / 31.0) - (((1 - (38.0 / 66.0)) * (10.0 / 31.0)) + (38.0 / 66.0 * (12.0 / 31.0)));

    public static readonly CriterioDaProvaDeVida Criterio = CriterioDaProvaDeVida.De(0.2, "valor de teste");

    public static readonly ModeloFacial RostoDoComeco = ModeloFacial.De("teste-4d", [1f, 0.1f, 0f, 0f]);

    public static readonly ModeloFacial RostoDoFim = ModeloFacial.De("teste-4d", [1f, 0f, 0.1f, 0f]);

    /// <summary>Olho direito, olho esquerdo, nariz, boca direita, boca esquerda, entre os olhos, centro da boca, bochechas.</summary>
    public static readonly Ponto3D[] Cabeca =
    [
        new(-31, 0, 0), new(31, 0, 0), new(0, 38, 24), new(-24, 66, 0), new(24, 66, 0),
        new(0, 0, 10), new(0, 66, 12), new(-43.4, 38, -6), new(43.4, 38, -6),
    ];

    private static readonly DateTimeOffset Comeco = new(2026, 11, 25, 7, 58, 0, TimeSpan.FromHours(-3));

    /// <summary>A foto da cabeça de frente: os mesmos pontos, achatados.</summary>
    public static Ponto3D[] Foto => [.. Cabeca.Select(ponto => ponto with { Z = 0 })];

    /// <summary>A foto enrolada num cilindro de eixo vertical, com o meio para a câmera.</summary>
    public static Ponto3D[] CurvadaNaVertical(double raio) =>
        [.. Foto.Select(ponto => new Ponto3D(raio * Math.Sin(ponto.X / raio), ponto.Y, -raio * (1 - Math.Cos(ponto.X / raio))))];

    /// <summary>A foto enrolada num cilindro de eixo horizontal, que passa pelo nariz.</summary>
    public static Ponto3D[] CurvadaNaHorizontal(double raio) =>
        [.. Foto.Select(ponto => new Ponto3D(ponto.X, 38 + (raio * Math.Sin((ponto.Y - 38) / raio)), -raio * (1 - Math.Cos((ponto.Y - 38) / raio))))];

    /// <summary>A foto dobrada em V pela linha do nariz, com a dobra para a câmera.</summary>
    public static Ponto3D[] DobradaNaVertical(double meioAnguloGraus)
    {
        var angulo = meioAnguloGraus * Math.PI / 180;

        return [.. Foto.Select(ponto => new Ponto3D(ponto.X * Math.Cos(angulo), ponto.Y, -Math.Abs(ponto.X) * Math.Sin(angulo)))];
    }

    /// <summary>
    /// Os pontos vistos por uma câmera a 50 cm, com a cabeça (ou a foto)
    /// girada: o giro positivo leva o nariz para a direita da imagem — a
    /// pessoa vira para a esquerda dela —, e a inclinação positiva o leva
    /// para baixo.
    /// </summary>
    public static PontosDoRosto Ver(Ponto3D[] pontos, double giroGraus = 0, double inclinacaoGraus = 0, bool perspectiva = true)
    {
        var giro = giroGraus * Math.PI / 180;
        var inclinacao = inclinacaoGraus * Math.PI / 180;

        return PontosDoRosto.De([.. pontos.Select(ponto =>
        {
            var x = (ponto.X * Math.Cos(giro)) + (ponto.Z * Math.Sin(giro));
            var z = (-ponto.X * Math.Sin(giro)) + (ponto.Z * Math.Cos(giro));
            var y = (ponto.Y * Math.Cos(inclinacao)) + (z * Math.Sin(inclinacao));
            z = (-ponto.Y * Math.Sin(inclinacao)) + (z * Math.Cos(inclinacao));
            var escala = perspectiva ? 800 / (500 - z) : 1.6;

            return new PontoNaImagem(320 + (x * escala), 200 + (y * escala));
        })]);
    }

    public static DateTimeOffset Quadro(int numero) => Comeco + TimeSpan.FromSeconds(numero / 15.0);

    /// <summary>
    /// Uma pessoa — ou quem segura uma foto — que segue o desafio a 15
    /// quadros por segundo: em cada movimento vai em 10 quadros até o ângulo
    /// dado, fica 3, volta em 10; no fim, um quadro de frente com o vetor do
    /// rosto.
    /// </summary>
    /// <param name="pose">O giro e a inclinação, em graus, do auge de cada movimento.</param>
    public static List<QuadroDaCamera> Seguindo(Ponto3D[] pontos, Desafio desafio, Func<Movimento, (double Giro, double Inclinacao)> pose)
    {
        var quadros = new List<QuadroDaCamera>();
        var numero = 1;

        foreach (var movimento in desafio.Movimentos)
        {
            var (giro, inclinacao) = pose(movimento);
            IEnumerable<double> fracoes = [.. Enumerable.Range(1, 10).Select(i => i / 10.0), 1, 1, 1, .. Enumerable.Range(0, 10).Select(i => 1 - ((i + 1) / 10.0))];

            foreach (var fracao in fracoes)
            {
                quadros.Add(new QuadroDaCamera(Quadro(numero++), Ver(pontos, giro * fracao, inclinacao * fracao)));
            }
        }

        quadros.Add(new QuadroDaCamera(Quadro(numero), Ver(pontos), RostoDoFim));

        return quadros;
    }

    /// <summary>A pose de quem obedece: 30° para os lados, 25° para cima e para baixo.</summary>
    public static (double Giro, double Inclinacao) Obedecendo(Movimento movimento) => movimento switch
    {
        Movimento.Esquerda => (30, 0),
        Movimento.Direita => (-30, 0),
        Movimento.Cima => (0, -25),
        _ => (0, 25),
    };

    public static SessaoDaProvaDeVida Iniciar(Ponto3D[] pontos, Desafio desafio, CriterioDaProvaDeVida? criterio = null) =>
        SessaoDaProvaDeVida.Iniciar(desafio, criterio ?? Criterio, new QuadroDaCamera(Quadro(0), Ver(pontos), RostoDoComeco));

    /// <summary>Registra os quadros até a sessão terminar, e devolve o resultado.</summary>
    public static ProvaDeVida Rodar(SessaoDaProvaDeVida sessao, IEnumerable<QuadroDaCamera> quadros)
    {
        foreach (var quadro in quadros)
        {
            sessao.Registrar(quadro);

            if (sessao.Terminou)
            {
                break;
            }
        }

        return sessao.Resultado ?? throw new InvalidOperationException("A sessão não terminou com os quadros dados.");
    }
}
