namespace PontoFacial.Dominio.Biometria.Calibracao;

/// <summary>
/// Uma taxa de erro medida — quantos erros em quantas comparações —, com o
/// intervalo de 95% de Wilson.
/// </summary>
/// <remarks>
/// Medir zero erros em cem comparações não quer dizer que o erro é zero: quer
/// dizer que ele, com 95% de confiança, fica abaixo de uns 3,7%. O intervalo
/// de Wilson diz isso, e continua certo perto de zero, onde o intervalo da
/// aproximação normal dá número negativo.
/// </remarks>
public sealed class TaxaDeErro
{
    /// <summary>O quantil da normal para 95% nos dois lados.</summary>
    private const double Z = 1.959963984540054;

    private TaxaDeErro(long erros, long total)
    {
        Erros = erros;
        Total = total;

        var p = (double)erros / total;
        var z2n = Z * Z / total;
        var centro = (p + (z2n / 2)) / (1 + z2n);
        var meiaLargura = Z * Math.Sqrt((p * (1 - p) / total) + (z2n / (4 * total))) / (1 + z2n);

        // Nos extremos a conta dá exatamente 0 e 1; o ponto flutuante deixaria
        // um resto de 10⁻¹⁸.
        Inferior = erros == 0 ? 0 : Math.Max(0, centro - meiaLargura);
        Superior = erros == total ? 1 : Math.Min(1, centro + meiaLargura);
    }

    public long Erros { get; }

    public long Total { get; }

    public double Valor => (double)Erros / Total;

    /// <summary>O limite de baixo do intervalo de 95%.</summary>
    public double Inferior { get; }

    /// <summary>O limite de cima do intervalo de 95%.</summary>
    public double Superior { get; }

    public static TaxaDeErro De(long erros, long total)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(total);
        ArgumentOutOfRangeException.ThrowIfNegative(erros);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(erros, total);

        return new TaxaDeErro(erros, total);
    }
}
