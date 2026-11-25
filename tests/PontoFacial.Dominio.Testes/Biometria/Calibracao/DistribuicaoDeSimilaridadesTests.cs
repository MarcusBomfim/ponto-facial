using PontoFacial.Dominio.Biometria.Calibracao;

namespace PontoFacial.Dominio.Testes.Biometria.Calibracao;

public sealed class DistribuicaoDeSimilaridadesTests
{
    [Fact]
    public void Conta_cada_similaridade_na_sua_faixa()
    {
        var distribuicao = new DistribuicaoDeSimilaridades();

        distribuicao.Adicionar(0.25);
        distribuicao.Adicionar(0.30);
        distribuicao.Adicionar(-0.70);

        Assert.Equal(3, distribuicao.Total);
        Assert.Equal(2, distribuicao.DaFaixaParaCima(Faixa(0.25)));
        Assert.Equal(1, distribuicao.DaFaixaParaCima(Faixa(0.2501)));
    }

    [Fact]
    public void A_borda_pertence_a_faixa_de_cima()
    {
        // 0,4999 é a borda de uma faixa; feita por conta, chega um tico abaixo.
        var distribuicao = new DistribuicaoDeSimilaridades();
        var borda = -0.5 + (99_990 * 1e-5);

        distribuicao.Adicionar(borda);

        Assert.Equal(1, distribuicao.DaFaixaParaCima(Faixa(0.4999)));
    }

    [Fact]
    public void Os_extremos_cabem_na_primeira_e_na_ultima_faixa()
    {
        var distribuicao = new DistribuicaoDeSimilaridades();

        distribuicao.Adicionar(-1.0);
        distribuicao.Adicionar(1.0);

        Assert.Equal(2, distribuicao.DaFaixaParaCima(0));
        Assert.Equal(1, distribuicao.DaFaixaParaCima(DistribuicaoDeSimilaridades.Faixas - 1));
    }

    [Theory]
    [InlineData(1.01)]
    [InlineData(-1.01)]
    [InlineData(double.NaN)]
    public void Similaridade_fora_de_menos_1_a_1_e_recusada(double similaridade)
    {
        var distribuicao = new DistribuicaoDeSimilaridades();

        Assert.Throws<ArgumentOutOfRangeException>(() => distribuicao.Adicionar(similaridade));
    }

    [Fact]
    public void As_partes_contadas_em_paralelo_somam_o_todo()
    {
        var todo = new DistribuicaoDeSimilaridades();
        var metade = new DistribuicaoDeSimilaridades();
        var outraMetade = new DistribuicaoDeSimilaridades();

        for (var i = 0; i < 1_000; i++)
        {
            var similaridade = -1 + (i * 0.002);
            todo.Adicionar(similaridade);
            (i % 2 == 0 ? metade : outraMetade).Adicionar(similaridade);
        }

        metade.Somar(outraMetade);

        Assert.Equal(todo.Total, metade.Total);
        Assert.All(
            Enumerable.Range(0, DistribuicaoDeSimilaridades.Faixas / 100).Select(i => i * 100),
            faixa => Assert.Equal(todo.DaFaixaParaCima(faixa), metade.DaFaixaParaCima(faixa)));
    }

    [Fact]
    public void A_borda_de_cada_faixa_vai_de_menos_1_em_passos_de_um_decimo_de_milesimo()
    {
        Assert.Equal(-1.0, DistribuicaoDeSimilaridades.BordaDaFaixa(0), 12);
        Assert.Equal(0.0, DistribuicaoDeSimilaridades.BordaDaFaixa(10_000), 12);
        Assert.Equal(0.3631, DistribuicaoDeSimilaridades.BordaDaFaixa(13_631), 12);
    }

    private static int Faixa(double borda) => (int)Math.Round((borda + 1) / DistribuicaoDeSimilaridades.LarguraDaFaixa);
}
