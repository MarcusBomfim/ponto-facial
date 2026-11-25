namespace PontoFacial.Rosto;

/// <summary>Um ponto na imagem, em pixels, a partir do canto de cima à esquerda.</summary>
public readonly record struct Ponto(float X, float Y);

/// <summary>
/// Um rosto que o detector achou: a caixa, os cinco pontos que o alinham e a
/// confiança. Direito e esquerdo são os da pessoa: o olho direito dela fica à
/// esquerda na imagem.
/// </summary>
public sealed record RostoDetectado(
    float X,
    float Y,
    float Largura,
    float Altura,
    Ponto OlhoDireito,
    Ponto OlhoEsquerdo,
    Ponto Nariz,
    Ponto BocaDireita,
    Ponto BocaEsquerda,
    float Confianca)
{
    /// <summary>A distância entre os olhos, em pixels: a medida de quão perto o rosto está.</summary>
    public float DistanciaEntreOlhos =>
        MathF.Sqrt(MathF.Pow(OlhoEsquerdo.X - OlhoDireito.X, 2) + MathF.Pow(OlhoEsquerdo.Y - OlhoDireito.Y, 2));

    public Ponto Centro => new(X + (Largura / 2), Y + (Altura / 2));

    /// <summary>Os cinco pontos na ordem do modelo de alinhamento.</summary>
    public Ponto[] Pontos => [OlhoDireito, OlhoEsquerdo, Nariz, BocaDireita, BocaEsquerda];

    /// <summary>O mesmo rosto nas coordenadas de uma imagem <paramref name="fator"/> vezes maior.</summary>
    public RostoDetectado Escalado(float fator)
    {
        Ponto Escalar(Ponto ponto) => new(ponto.X * fator, ponto.Y * fator);

        return new RostoDetectado(
            X * fator,
            Y * fator,
            Largura * fator,
            Altura * fator,
            Escalar(OlhoDireito),
            Escalar(OlhoEsquerdo),
            Escalar(Nariz),
            Escalar(BocaDireita),
            Escalar(BocaEsquerda),
            Confianca);
    }
}
