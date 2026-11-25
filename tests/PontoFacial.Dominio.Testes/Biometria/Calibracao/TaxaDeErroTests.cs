using PontoFacial.Dominio.Biometria.Calibracao;

namespace PontoFacial.Dominio.Testes.Biometria.Calibracao;

/*
 * Os intervalos de Wilson conferidos contra valores calculados à parte, com
 * z = 1,96.
 */
public sealed class TaxaDeErroTests
{
    [Fact]
    public void Zero_erros_em_cem_ainda_deixam_ate_uns_3_7_por_cento()
    {
        var taxa = TaxaDeErro.De(0, 100);

        Assert.Equal(0, taxa.Valor);
        Assert.Equal(0, taxa.Inferior);
        Assert.Equal(0.0370, taxa.Superior, 4);
    }

    [Fact]
    public void Cinco_erros_em_cem_dao_de_2_15_a_11_18_por_cento()
    {
        var taxa = TaxaDeErro.De(5, 100);

        Assert.Equal(0.05, taxa.Valor, 9);
        Assert.Equal(0.0215, taxa.Inferior, 4);
        Assert.Equal(0.1118, taxa.Superior, 4);
    }

    [Fact]
    public void Mais_comparacoes_estreitam_o_intervalo()
    {
        var cem = TaxaDeErro.De(5, 100);
        var dezMil = TaxaDeErro.De(500, 10_000);

        Assert.Equal(cem.Valor, dezMil.Valor, 9);
        Assert.True(dezMil.Superior - dezMil.Inferior < (cem.Superior - cem.Inferior) / 5);
    }

    [Fact]
    public void Todos_errados_vao_ate_1()
    {
        var taxa = TaxaDeErro.De(100, 100);

        Assert.Equal(1, taxa.Superior);
        Assert.Equal(0.9630, taxa.Inferior, 4);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 10)]
    [InlineData(11, 10)]
    public void Taxa_sem_comparacoes_ou_com_mais_erros_que_comparacoes_nao_existe(long erros, long total)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TaxaDeErro.De(erros, total));
    }
}
