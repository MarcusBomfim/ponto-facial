namespace PontoFacial.Dominio.Biometria.Calibracao;

/// <summary>
/// Um limiar e os dois erros que ele dá: a falsa aceitação, que a ISO/IEC
/// 19795-1 chama de FMR, e a falsa rejeição, a FNMR.
/// </summary>
public sealed class PontoDeOperacao
{
    internal PontoDeOperacao(double limiar, TaxaDeErro falsaAceitacao, TaxaDeErro falsaRejeicao)
    {
        Limiar = limiar;
        FalsaAceitacao = falsaAceitacao;
        FalsaRejeicao = falsaRejeicao;
    }

    /// <summary>A menor similaridade aceita.</summary>
    public double Limiar { get; }

    /// <summary>As comparações entre pessoas diferentes que alcançaram o limiar.</summary>
    public TaxaDeErro FalsaAceitacao { get; }

    /// <summary>As comparações da mesma pessoa que ficaram abaixo do limiar.</summary>
    public TaxaDeErro FalsaRejeicao { get; }
}
