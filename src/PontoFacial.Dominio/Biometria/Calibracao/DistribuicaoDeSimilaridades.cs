namespace PontoFacial.Dominio.Biometria.Calibracao;

/// <summary>
/// As similaridades de um conjunto de comparações, contadas em faixas de
/// 0,0001 de -1 a 1.
/// </summary>
/// <remarks>
/// Uma base como a LFW dá dezenas de milhões de comparações entre pessoas
/// diferentes; guardar cada número não cabe na memória, e não é preciso. Com
/// a contagem por faixa, a taxa de erro em cada borda de faixa é exata, e o
/// limiar escolhido é uma dessas bordas. A borda pertence à faixa de cima: a
/// similaridade igual ao limiar é aceita, como em
/// <see cref="LimiarDeAceitacao.Aceita"/>.
/// </remarks>
public sealed class DistribuicaoDeSimilaridades
{
    public const int Faixas = 20_000;

    public const double LarguraDaFaixa = 2.0 / Faixas;

    private readonly long[] _contagem = new long[Faixas];

    /// <summary>Quantas comparações foram contadas.</summary>
    public long Total { get; private set; }

    /// <summary>A similaridade da borda de baixo da faixa.</summary>
    public static double BordaDaFaixa(int faixa) => -1.0 + (faixa * LarguraDaFaixa);

    public void Adicionar(double similaridade)
    {
        if (!double.IsFinite(similaridade) || similaridade < -1.000001 || similaridade > 1.000001)
        {
            throw new ArgumentOutOfRangeException(nameof(similaridade), similaridade, "A similaridade de cosseno vai de -1 a 1.");
        }

        _contagem[Faixa(similaridade)]++;
        Total++;
    }

    /// <summary>Junta outra distribuição a esta: o que cada parte contou em paralelo.</summary>
    public void Somar(DistribuicaoDeSimilaridades outra)
    {
        ArgumentNullException.ThrowIfNull(outra);

        for (var i = 0; i < Faixas; i++)
        {
            _contagem[i] += outra._contagem[i];
        }

        Total += outra.Total;
    }

    /// <summary>Quantas comparações ficaram na faixa ou acima dela.</summary>
    public long DaFaixaParaCima(int faixa)
    {
        long soma = 0;

        for (var i = faixa; i < Faixas; i++)
        {
            soma += _contagem[i];
        }

        return soma;
    }

    /// <summary>A contagem de cada faixa ou acima, de uma vez, para todas as faixas.</summary>
    internal long[] AcumuladoDeCimaParaBaixo()
    {
        var acumulado = new long[Faixas + 1];

        for (var i = Faixas - 1; i >= 0; i--)
        {
            acumulado[i] = acumulado[i + 1] + _contagem[i];
        }

        return acumulado;
    }

    /// <summary>
    /// A faixa da similaridade. A folga de um milionésimo de faixa põe na
    /// faixa de cima o número que é a borda dela, mas que a aritmética de
    /// ponto flutuante deixou um tico abaixo.
    /// </summary>
    internal static int Faixa(double similaridade) =>
        Math.Clamp((int)Math.Floor(((similaridade + 1.0) / LarguraDaFaixa) + 1e-6), 0, Faixas - 1);
}
