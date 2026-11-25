using System.Globalization;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Biometria.Calibracao;

/// <summary>
/// O limiar de aceitação escolhido pela falsa aceitação que se tolera,
/// medido em comparações da mesma pessoa (genuínas) e de pessoas diferentes
/// (impostoras).
/// </summary>
/// <remarks>
/// <para>
/// A regra é a da verificação biométrica: escolhe-se primeiro quanto de
/// falsa aceitação se tolera — no ponto, a chance de um colega passar pelo
/// outro —, e o limiar é o menor que fica dentro dela. A falsa rejeição é a
/// consequência, medida e informada, e não o critério (ADR-0004).
/// </para>
/// <para>
/// Uma taxa só se mede com comparações suficientes. Pela regra de três, com
/// N comparações sem erro, o erro verdadeiro ainda pode chegar a uns 3/N com
/// 95% de confiança; por isso a calibração recusa um alvo menor que 3 sobre o
/// número de comparações impostoras.
/// </para>
/// </remarks>
public sealed class CalibracaoDoLimiar
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly double[] AlvosDaCurva = [1e-1, 1e-2, 1e-3, 1e-4, 1e-5, 1e-6, 1e-7];

    private CalibracaoDoLimiar(
        LimiarDeAceitacao limiar,
        double falsaAceitacaoAlvo,
        PontoDeOperacao operacao,
        PontoDeOperacao erroIgual,
        IReadOnlyList<PontoDaCurva> curva)
    {
        Limiar = limiar;
        FalsaAceitacaoAlvo = falsaAceitacaoAlvo;
        Operacao = operacao;
        ErroIgual = erroIgual;
        Curva = curva;
    }

    public LimiarDeAceitacao Limiar { get; }

    /// <summary>A falsa aceitação tolerada, de que o limiar saiu.</summary>
    public double FalsaAceitacaoAlvo { get; }

    /// <summary>O limiar escolhido e os dois erros medidos nele.</summary>
    public PontoDeOperacao Operacao { get; }

    /// <summary>O limiar em que os dois erros mais se aproximam: a taxa de erro igual (EER).</summary>
    public PontoDeOperacao ErroIgual { get; }

    /// <summary>Os limiares para falsa aceitação de 1:10 a 1:10.000.000, enquanto as comparações permitem medir.</summary>
    public IReadOnlyList<PontoDaCurva> Curva { get; }

    public static CalibracaoDoLimiar Calibrar(
        string reconhecedor,
        DistribuicaoDeSimilaridades genuinas,
        DistribuicaoDeSimilaridades impostoras,
        double falsaAceitacaoAlvo,
        string origem)
    {
        ArgumentNullException.ThrowIfNull(genuinas);
        ArgumentNullException.ThrowIfNull(impostoras);

        if (!(falsaAceitacaoAlvo > 0 && falsaAceitacaoAlvo < 0.5))
        {
            throw new ExcecaoDeDominio("A falsa aceitação tolerada é uma fração acima de 0 e abaixo de 0,5.");
        }

        if (genuinas.Total == 0)
        {
            throw new ExcecaoDeDominio("Sem comparações da mesma pessoa, a falsa rejeição não se mede.");
        }

        var minimo = (long)Math.Ceiling(3 / falsaAceitacaoAlvo);

        if (impostoras.Total < minimo)
        {
            throw new ExcecaoDeDominio(string.Create(
                PtBr,
                $"Para medir falsa aceitação de 1:{1 / falsaAceitacaoAlvo:N0} são precisas pelo menos {minimo:N0} comparações entre pessoas diferentes; vieram {impostoras.Total:N0}."));
        }

        var impostorasDaFaixaParaCima = impostoras.AcumuladoDeCimaParaBaixo();
        var genuinasDaFaixaParaCima = genuinas.AcumuladoDeCimaParaBaixo();

        PontoDeOperacao Ponto(int faixa) => new(
            DistribuicaoDeSimilaridades.BordaDaFaixa(faixa),
            TaxaDeErro.De(impostorasDaFaixaParaCima[faixa], impostoras.Total),
            TaxaDeErro.De(genuinas.Total - genuinasDaFaixaParaCima[faixa], genuinas.Total));

        int FaixaPara(double alvo)
        {
            // A falsa aceitação só cai de uma faixa para a de cima: a primeira
            // que fica dentro do alvo é o menor limiar que serve.
            var faixa = 0;

            while (faixa < DistribuicaoDeSimilaridades.Faixas - 1
                && impostorasDaFaixaParaCima[faixa] > alvo * impostoras.Total)
            {
                faixa++;
            }

            return faixa;
        }

        var operacao = Ponto(FaixaPara(falsaAceitacaoAlvo));

        var faixaDoErroIgual = Enumerable.Range(0, DistribuicaoDeSimilaridades.Faixas)
            .MinBy(faixa => Math.Abs(
                ((double)impostorasDaFaixaParaCima[faixa] / impostoras.Total)
                - ((double)(genuinas.Total - genuinasDaFaixaParaCima[faixa]) / genuinas.Total)));

        var curva = AlvosDaCurva
            .Where(alvo => impostoras.Total >= Math.Ceiling(3 / alvo))
            .Select(alvo => new PontoDaCurva(alvo, Ponto(FaixaPara(alvo))))
            .ToList();

        return new CalibracaoDoLimiar(
            LimiarDeAceitacao.De(reconhecedor, operacao.Limiar, origem),
            falsaAceitacaoAlvo,
            operacao,
            Ponto(faixaDoErroIgual),
            curva.AsReadOnly());
    }
}
