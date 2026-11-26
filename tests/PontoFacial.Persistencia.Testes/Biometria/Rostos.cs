using PontoFacial.Dominio.Biometria;

namespace PontoFacial.Persistencia.Testes.Biometria;

/// <summary>
/// Rostos de mentira do tamanho dos de verdade: 128 números, como os do
/// SFace. Cada pessoa é um vetor sorteado com semente fixa, e cada captura é
/// ele com um pouco de ruído.
/// </summary>
internal static class Rostos
{
    public const string Reconhecedor = "sface-2021dec";

    public static readonly LimiarDeAceitacao Limiar = LimiarDeAceitacao.De(Reconhecedor, 0.4317, "LFW, FMR 1:10.000");

    public static readonly ModeloFacial[] CapturasDoJoao = Capturas(semente: 529, quantas: 3);

    public static readonly ModeloFacial[] CapturasDaMaria = Capturas(semente: 111, quantas: 3);

    public static readonly ModeloFacial JoaoNaPortaria = Capturas(semente: 529, quantas: 4)[3];

    /// <summary>O mesmo rosto de base, com ruído diferente em cada captura.</summary>
    private static ModeloFacial[] Capturas(int semente, int quantas)
    {
        var pessoa = new Random(semente);
        var rosto = Enumerable.Range(0, 128).Select(_ => (float)((pessoa.NextDouble() * 2) - 1)).ToArray();
        var ruido = new Random(semente * 7);

        return
        [
            .. Enumerable.Range(0, quantas).Select(_ => ModeloFacial.De(
                Reconhecedor,
                [.. rosto.Select(valor => valor + (float)((ruido.NextDouble() - 0.5) * 0.2))])),
        ];
    }
}
