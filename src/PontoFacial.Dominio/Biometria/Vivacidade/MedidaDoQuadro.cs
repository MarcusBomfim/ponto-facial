namespace PontoFacial.Dominio.Biometria.Vivacidade;

/// <summary>
/// Quanto o rosto girou desde o quadro de referência e quanto o nariz sai do
/// plano do rosto, medido pelos pontos acompanhados.
/// </summary>
/// <remarks>
/// <para>
/// O giro e a inclinação dizem para onde a cabeça foi: o nariz anda em
/// relação aos olhos — para o lado quando a pessoa vira, para cima ou para
/// baixo quando ela levanta ou abaixa o rosto.
/// </para>
/// <para>
/// O relevo diz se o que girou tem volume. Os olhos e os cantos da boca
/// definem um plano; a homografia que leva esses quatro pontos da
/// referência ao quadro atual é exatamente o movimento de uma foto ou de uma
/// tela, que são planas. Cada outro ponto que não acompanha a homografia está
/// fora do plano, e o quanto se desvia, dividido pelo quanto o plano se
/// inclinou, dá a sua profundidade.
/// </para>
/// <para>
/// O relevo lateral compara o nariz com as bochechas na altura dele; o
/// vertical, com a reta que liga a raiz do nariz ao centro da boca. Papel e
/// tela se curvam numa direção por vez — são superfícies desenvolvíveis: por
/// cada ponto passa uma reta que fica na superfície. Curvado num sentido, o
/// papel dá relevo lateral e nenhum vertical; no outro, o contrário. O nariz
/// de uma pessoa sobressai nas duas direções. Por isso o
/// <see cref="Relevo"/> é o menor dos dois (ADR-0007).
/// </para>
/// <para>
/// As profundidades são medidas em meias distâncias entre os olhos da
/// referência: com os olhos a 6,3 cm, 1 é uns 3 cm.
/// </para>
/// </remarks>
public sealed class MedidaDoQuadro
{
    private MedidaDoQuadro(double giro, double inclinacao, double encurtamento, double relevoLateral, double relevoVertical)
    {
        Giro = giro;
        Inclinacao = inclinacao;
        Encurtamento = encurtamento;
        RelevoLateral = relevoLateral;
        RelevoVertical = relevoVertical;
    }

    /// <summary>
    /// Quanto o nariz andou ao longo da linha dos olhos, em distâncias entre os
    /// olhos: positivo quando a pessoa vira para a esquerda dela.
    /// </summary>
    public double Giro { get; }

    /// <summary>
    /// Quanto o nariz andou entre os olhos e a boca, em frações dessa
    /// distância: negativo quando a pessoa levanta o rosto.
    /// </summary>
    public double Inclinacao { get; }

    /// <summary>
    /// O seno do quanto o plano do rosto se inclinou em relação à câmera, de 0
    /// (de frente, como na referência) a 1. Com pouca inclinação, o relevo não
    /// se mede.
    /// </summary>
    public double Encurtamento { get; }

    /// <summary>O nariz à frente das bochechas, em meias distâncias entre os olhos.</summary>
    public double RelevoLateral { get; }

    /// <summary>O nariz à frente da reta entre a raiz do nariz e o centro da boca, em meias distâncias entre os olhos.</summary>
    public double RelevoVertical { get; }

    /// <summary>O menor dos dois relevos: perto de zero em papel e tela, curvados ou não.</summary>
    public double Relevo => Math.Min(RelevoLateral, RelevoVertical);

    public static MedidaDoQuadro Entre(PontosDoRosto referencia, PontosDoRosto atual)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        ArgumentNullException.ThrowIfNull(atual);

        var (hReferencia, vReferencia) = Pose(referencia);
        var (hAtual, vAtual) = Pose(atual);
        var giro = hAtual - hReferencia;
        var inclinacao = vAtual - vReferencia;

        var plano = Homografia.DoPlano(referencia, atual);

        if (plano is null)
        {
            return new MedidaDoQuadro(giro, inclinacao, 0, double.NaN, double.NaN);
        }

        var (encurtamento, direcaoX, direcaoY, escala) = plano.Value.Inclinacao();

        if (encurtamento < 1e-6)
        {
            return new MedidaDoQuadro(giro, inclinacao, encurtamento, double.NaN, double.NaN);
        }

        // O sentido em que os pontos da frente andam é o do movimento: para o
        // lado do giro, ou para cima ou para baixo.
        var (sentidoX, sentidoY) = Math.Abs(giro) / SessaoDaProvaDeVida.GiroMinimo >= Math.Abs(inclinacao) / SessaoDaProvaDeVida.InclinacaoMinima
            ? Eixos(atual).Lado(Math.Sign(giro))
            : Eixos(atual).Altura(Math.Sign(inclinacao));

        if ((direcaoX * sentidoX) + (direcaoY * sentidoY) < 0)
        {
            direcaoX = -direcaoX;
            direcaoY = -direcaoY;
        }

        var meiaDistancia = referencia.DistanciaEntreOlhos / 2;

        double Profundidade(PontoNaImagem naReferencia, PontoNaImagem noQuadro)
        {
            var previsto = plano.Value.Aplicar(naReferencia);
            var desvio = ((noQuadro.X - previsto.X) * direcaoX) + ((noQuadro.Y - previsto.Y) * direcaoY);

            return desvio / (escala * encurtamento) / meiaDistancia;
        }

        var nariz = Profundidade(referencia.Nariz, atual.Nariz);
        var bochechas = (Profundidade(referencia.BochechaDireita, atual.BochechaDireita)
            + Profundidade(referencia.BochechaEsquerda, atual.BochechaEsquerda)) / 2;

        // A reta da raiz do nariz ao centro da boca, na altura em que o nariz
        // estava na referência.
        var raiz = referencia.EntreOsOlhos;
        var boca = referencia.CentroDaBoca;
        var comprimento = ((boca.X - raiz.X) * (boca.X - raiz.X)) + ((boca.Y - raiz.Y) * (boca.Y - raiz.Y));
        var fracao = (((referencia.Nariz.X - raiz.X) * (boca.X - raiz.X)) + ((referencia.Nariz.Y - raiz.Y) * (boca.Y - raiz.Y))) / comprimento;
        var reta = ((1 - fracao) * Profundidade(raiz, atual.EntreOsOlhos)) + (fracao * Profundidade(boca, atual.CentroDaBoca));

        return new MedidaDoQuadro(giro, inclinacao, encurtamento, nariz - bochechas, nariz - reta);
    }

    /// <summary>
    /// A posição do nariz: ao longo da linha dos olhos, a partir do meio
    /// deles, em distâncias entre os olhos; e na altura, de 0 nos olhos a 1 na
    /// boca.
    /// </summary>
    internal static (double Lado, double Altura) Pose(PontosDoRosto pontos)
    {
        var eixos = Eixos(pontos);
        var olhos = new PontoNaImagem((pontos.OlhoDireito.X + pontos.OlhoEsquerdo.X) / 2, (pontos.OlhoDireito.Y + pontos.OlhoEsquerdo.Y) / 2);
        var boca = new PontoNaImagem((pontos.BocaDireita.X + pontos.BocaEsquerda.X) / 2, (pontos.BocaDireita.Y + pontos.BocaEsquerda.Y) / 2);
        var altura = ((boca.X - olhos.X) * eixos.WX) + ((boca.Y - olhos.Y) * eixos.WY);
        var naLinha = ((pontos.Nariz.X - olhos.X) * eixos.UX) + ((pontos.Nariz.Y - olhos.Y) * eixos.UY);
        var abaixo = ((pontos.Nariz.X - olhos.X) * eixos.WX) + ((pontos.Nariz.Y - olhos.Y) * eixos.WY);

        return (naLinha / pontos.DistanciaEntreOlhos, abaixo / altura);
    }

    private static EixosDoRosto Eixos(PontosDoRosto pontos)
    {
        var distancia = pontos.DistanciaEntreOlhos;
        var ux = (pontos.OlhoEsquerdo.X - pontos.OlhoDireito.X) / distancia;
        var uy = (pontos.OlhoEsquerdo.Y - pontos.OlhoDireito.Y) / distancia;

        return new EixosDoRosto(ux, uy, -uy, ux);
    }

    /// <summary>
    /// A linha dos olhos (U, do olho direito para o esquerdo, que na imagem é
    /// da esquerda para a direita) e a perpendicular que desce para a boca (W).
    /// </summary>
    private readonly record struct EixosDoRosto(double UX, double UY, double WX, double WY)
    {
        public (double X, double Y) Lado(int sinal) => (sinal * UX, sinal * UY);

        public (double X, double Y) Altura(int sinal) => (sinal * WX, sinal * WY);
    }
}
