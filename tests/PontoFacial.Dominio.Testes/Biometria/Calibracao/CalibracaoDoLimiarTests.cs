using PontoFacial.Dominio.Biometria.Calibracao;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Testes.Biometria.Calibracao;

/*
 * Distribuições feitas à mão, em grade regular, para que cada taxa possa
 * ser contada de cabeça: 100.000 impostoras de -0,5 a 0,5 em passos de
 * 0,00001, e 1.000 genuínas de 0,3 a 0,8 em passos de 0,0005.
 */
public sealed class CalibracaoDoLimiarTests
{
    [Fact]
    public void O_limiar_e_o_menor_que_fica_dentro_da_falsa_aceitacao_tolerada()
    {
        var (genuinas, impostoras) = Grades();

        var calibracao = CalibracaoDoLimiar.Calibrar("teste", genuinas, impostoras, 1e-3, "grade de teste");

        // 1:1.000 de 100.000 são 100 impostoras: de 0,49900 a 0,49999.
        Assert.Equal(0.499, calibracao.Limiar.Similaridade, 9);
        Assert.Equal(100, calibracao.Operacao.FalsaAceitacao.Erros);
        Assert.Equal(100_000, calibracao.Operacao.FalsaAceitacao.Total);

        // Uma faixa abaixo, entrariam 110: passaria do tolerado.
        Assert.Equal(110, impostoras.DaFaixaParaCima(Faixa(0.4989)));
    }

    [Fact]
    public void A_falsa_rejeicao_e_medida_no_limiar_escolhido()
    {
        var (genuinas, impostoras) = Grades();

        var calibracao = CalibracaoDoLimiar.Calibrar("teste", genuinas, impostoras, 1e-3, "grade de teste");

        // As genuínas abaixo de 0,499 são as 398 primeiras: de 0,3000 a 0,4985.
        Assert.Equal(398, calibracao.Operacao.FalsaRejeicao.Erros);
        Assert.Equal(1_000, calibracao.Operacao.FalsaRejeicao.Total);
        Assert.Equal(0.398, calibracao.Operacao.FalsaRejeicao.Valor, 9);
    }

    [Fact]
    public void O_limiar_guarda_o_reconhecedor_e_a_origem()
    {
        var (genuinas, impostoras) = Grades();

        var calibracao = CalibracaoDoLimiar.Calibrar("sface-2021dec", genuinas, impostoras, 1e-3, "grade de teste");

        Assert.Equal("sface-2021dec", calibracao.Limiar.Reconhecedor);
        Assert.Equal("grade de teste", calibracao.Limiar.Origem);
        Assert.Equal(1e-3, calibracao.FalsaAceitacaoAlvo);
    }

    [Fact]
    public void O_erro_igual_fica_onde_as_duas_taxas_se_cruzam()
    {
        // Impostoras de -0,5 a 0,5 e genuínas de 0 a 1, ambas uniformes: as
        // duas taxas valem 25% em 0,25.
        var genuinas = new DistribuicaoDeSimilaridades();
        var impostoras = new DistribuicaoDeSimilaridades();
        for (var i = 0; i < 100_000; i++)
        {
            impostoras.Adicionar(-0.5 + (i * 1e-5));
            genuinas.Adicionar(i * 1e-5);
        }

        var calibracao = CalibracaoDoLimiar.Calibrar("teste", genuinas, impostoras, 1e-3, "uniformes");

        Assert.Equal(0.25, calibracao.ErroIgual.Limiar, 4);
        Assert.Equal(0.25, calibracao.ErroIgual.FalsaAceitacao.Valor, 3);
        Assert.Equal(0.25, calibracao.ErroIgual.FalsaRejeicao.Valor, 3);
    }

    [Fact]
    public void A_curva_vai_ate_onde_as_comparacoes_permitem_medir()
    {
        var (genuinas, impostoras) = Grades();

        var calibracao = CalibracaoDoLimiar.Calibrar("teste", genuinas, impostoras, 1e-3, "grade de teste");

        // Com 100.000 impostoras, mede-se até 1:10.000 (pede 30.000); 1:100.000 pediria 300.000.
        Assert.Equal(4, calibracao.Curva.Count);
        Assert.Equal([0.4, 0.49, 0.499, 0.4999], calibracao.Curva.Select(ponto => Math.Round(ponto.Operacao.Limiar, 6)));
        Assert.Equal([10_000L, 1_000, 100, 10], calibracao.Curva.Select(ponto => ponto.Operacao.FalsaAceitacao.Erros));
    }

    [Fact]
    public void Poucas_comparacoes_impostoras_nao_medem_o_alvo()
    {
        var genuinas = new DistribuicaoDeSimilaridades();
        var impostoras = new DistribuicaoDeSimilaridades();
        genuinas.Adicionar(0.8);
        for (var i = 0; i < 1_000; i++)
        {
            impostoras.Adicionar(0);
        }

        var erro = Assert.Throws<ExcecaoDeDominio>(() => CalibracaoDoLimiar.Calibrar("teste", genuinas, impostoras, 1e-4, "poucas"));

        Assert.Equal(
            "Para medir falsa aceitação de 1:10.000 são precisas pelo menos 30.000 comparações entre pessoas diferentes; vieram 1.000.",
            erro.Message);
    }

    [Fact]
    public void Sem_genuinas_a_falsa_rejeicao_nao_se_mede()
    {
        var (_, impostoras) = Grades();

        var erro = Assert.Throws<ExcecaoDeDominio>(() =>
            CalibracaoDoLimiar.Calibrar("teste", new DistribuicaoDeSimilaridades(), impostoras, 1e-3, "sem genuínas"));

        Assert.Equal("Sem comparações da mesma pessoa, a falsa rejeição não se mede.", erro.Message);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(-0.1)]
    public void O_alvo_e_uma_fracao_entre_0_e_meio(double alvo)
    {
        var (genuinas, impostoras) = Grades();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => CalibracaoDoLimiar.Calibrar("teste", genuinas, impostoras, alvo, "alvo errado"));

        Assert.Equal("A falsa aceitação tolerada é uma fração acima de 0 e abaixo de 0,5.", erro.Message);
    }

    private static (DistribuicaoDeSimilaridades Genuinas, DistribuicaoDeSimilaridades Impostoras) Grades()
    {
        var genuinas = new DistribuicaoDeSimilaridades();
        var impostoras = new DistribuicaoDeSimilaridades();

        for (var i = 0; i < 100_000; i++)
        {
            impostoras.Adicionar(-0.5 + (i * 1e-5));
        }

        for (var i = 0; i < 1_000; i++)
        {
            genuinas.Adicionar(0.3 + (i * 0.0005));
        }

        return (genuinas, impostoras);
    }

    private static int Faixa(double borda) => (int)Math.Round((borda + 1) / DistribuicaoDeSimilaridades.LarguraDaFaixa);
}
