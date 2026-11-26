using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Biometria.Vivacidade;

/// <summary>Um ponto da imagem, em pixels, a partir do canto de cima à esquerda.</summary>
public readonly record struct PontoNaImagem(double X, double Y);

/// <summary>
/// Os nove pontos do rosto que a prova de vida acompanha de um quadro para o
/// outro. Cinco são os que o detector acha — os olhos, o nariz e os cantos
/// da boca —; quatro saem deles no primeiro quadro: o ponto entre os olhos,
/// o centro da boca e as duas bochechas na altura do nariz. Direito e
/// esquerdo são os da pessoa.
/// </summary>
/// <remarks>
/// Os pontos são acompanhados na imagem, e não achados de novo a cada
/// quadro. Um detector treinado põe o nariz onde um rosto teria nariz, até
/// numa foto inclinada; o acompanhamento segue o que está de fato na imagem,
/// e é isso que deixa medir se o nariz sai do plano do rosto (ADR-0007).
/// </remarks>
public sealed class PontosDoRosto
{
    /// <summary>Quantos pontos são acompanhados.</summary>
    public const int Quantidade = 9;

    /// <summary>A distância de cada bochecha ao nariz, em distâncias entre os olhos.</summary>
    public const double AfastamentoDasBochechas = 0.7;

    private readonly PontoNaImagem[] _pontos;

    private PontosDoRosto(PontoNaImagem[] pontos)
    {
        _pontos = pontos;
    }

    public PontoNaImagem OlhoDireito => _pontos[0];

    public PontoNaImagem OlhoEsquerdo => _pontos[1];

    public PontoNaImagem Nariz => _pontos[2];

    public PontoNaImagem BocaDireita => _pontos[3];

    public PontoNaImagem BocaEsquerda => _pontos[4];

    /// <summary>A raiz do nariz, entre os olhos.</summary>
    public PontoNaImagem EntreOsOlhos => _pontos[5];

    public PontoNaImagem CentroDaBoca => _pontos[6];

    public PontoNaImagem BochechaDireita => _pontos[7];

    public PontoNaImagem BochechaEsquerda => _pontos[8];

    /// <summary>
    /// Os nove, na ordem: olho direito, olho esquerdo, nariz, boca direita,
    /// boca esquerda, entre os olhos, centro da boca, bochecha direita e
    /// bochecha esquerda.
    /// </summary>
    public IReadOnlyList<PontoNaImagem> Todos => _pontos.AsReadOnly();

    public double DistanciaEntreOlhos => Distancia(OlhoDireito, OlhoEsquerdo);

    /// <summary>Os nove pontos, na ordem de <see cref="Todos"/>.</summary>
    public static PontosDoRosto De(IReadOnlyList<PontoNaImagem> pontos)
    {
        ArgumentNullException.ThrowIfNull(pontos);

        if (pontos.Count != Quantidade)
        {
            throw new ExcecaoDeDominio($"O rosto acompanhado tem {Quantidade} pontos; vieram {pontos.Count}.");
        }

        if (pontos.Any(ponto => !double.IsFinite(ponto.X) || !double.IsFinite(ponto.Y)))
        {
            throw new ExcecaoDeDominio("Um ponto do rosto tem coordenada que não é número.");
        }

        if (Distancia(pontos[0], pontos[1]) < 1)
        {
            throw new ExcecaoDeDominio("Os olhos caem no mesmo lugar da imagem.");
        }

        return new PontosDoRosto([.. pontos]);
    }

    /// <summary>
    /// Os nove pontos a acompanhar, a partir dos cinco do detector no
    /// primeiro quadro.
    /// </summary>
    public static PontosDoRosto ParaAcompanhar(
        PontoNaImagem olhoDireito,
        PontoNaImagem olhoEsquerdo,
        PontoNaImagem nariz,
        PontoNaImagem bocaDireita,
        PontoNaImagem bocaEsquerda)
    {
        var distancia = Distancia(olhoDireito, olhoEsquerdo);

        if (!(distancia >= 1))
        {
            throw new ExcecaoDeDominio("Os olhos caem no mesmo lugar da imagem.");
        }

        // Ao longo da linha dos olhos, do direito para o esquerdo.
        var ux = (olhoEsquerdo.X - olhoDireito.X) / distancia;
        var uy = (olhoEsquerdo.Y - olhoDireito.Y) / distancia;
        var afastamento = AfastamentoDasBochechas * distancia;

        return De(
        [
            olhoDireito,
            olhoEsquerdo,
            nariz,
            bocaDireita,
            bocaEsquerda,
            Meio(olhoDireito, olhoEsquerdo),
            Meio(bocaDireita, bocaEsquerda),
            new PontoNaImagem(nariz.X - (afastamento * ux), nariz.Y - (afastamento * uy)),
            new PontoNaImagem(nariz.X + (afastamento * ux), nariz.Y + (afastamento * uy)),
        ]);
    }

    internal static double Distancia(PontoNaImagem a, PontoNaImagem b) => Math.Sqrt(((b.X - a.X) * (b.X - a.X)) + ((b.Y - a.Y) * (b.Y - a.Y)));

    private static PontoNaImagem Meio(PontoNaImagem a, PontoNaImagem b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
}
