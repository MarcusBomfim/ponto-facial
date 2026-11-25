using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Testes.Biometria;

public sealed class LimiarDeAceitacaoTests
{
    [Fact]
    public void A_similaridade_igual_ao_limiar_e_aceita()
    {
        var limiar = LimiarDeAceitacao.De("sface-2021dec", 0.4, "valor de teste");

        Assert.True(limiar.Aceita(0.4));
        Assert.True(limiar.Aceita(0.9));
        Assert.False(limiar.Aceita(0.3999));
    }

    [Theory]
    [InlineData(-1.0)]
    [InlineData(1.0001)]
    [InlineData(double.NaN)]
    public void O_limiar_e_uma_similaridade_acima_de_menos_1_e_ate_1(double valor)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => LimiarDeAceitacao.De("sface-2021dec", valor, "valor de teste"));

        Assert.StartsWith("O limiar é uma similaridade acima de -1 e até 1; veio ", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void O_limiar_diz_de_onde_saiu()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => LimiarDeAceitacao.De("sface-2021dec", 0.4, " "));

        Assert.Equal("Falta a origem do limiar.", erro.Message);
    }
}
