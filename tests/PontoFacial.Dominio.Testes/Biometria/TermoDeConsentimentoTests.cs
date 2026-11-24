using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Testes.Biometria;

public sealed class TermoDeConsentimentoTests
{
    [Fact]
    public void O_hash_e_o_sha256_do_texto_em_utf8()
    {
        // O vetor de teste "abc" da norma do SHA-256 (FIPS 180-2).
        var termo = TermoDeConsentimento.Novo("v1", "abc");

        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", termo.Hash);
    }

    [Fact]
    public void Um_espaco_a_mais_e_outro_texto_e_outro_hash()
    {
        var mostrado = TermoDeConsentimento.Novo("2026.1", Exemplos.TextoDoTermo);
        var trocado = TermoDeConsentimento.Novo("2026.1", Exemplos.TextoDoTermo + " ");

        Assert.NotEqual(mostrado.Hash, trocado.Hash);
    }

    [Fact]
    public void O_texto_e_guardado_como_foi_mostrado()
    {
        const string texto = "  Autorizo o uso do meu rosto.\n\nPosso revogar quando quiser.  ";

        var termo = TermoDeConsentimento.Novo("2026.1", texto);

        Assert.Equal(texto, termo.Texto);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \n ")]
    public void Sem_texto_o_termo_e_recusado(string texto)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => TermoDeConsentimento.Novo("2026.1", texto));

        Assert.Equal("Falta o texto do termo de consentimento.", erro.Message);
    }

    [Fact]
    public void A_versao_e_obrigatoria_e_cabe_em_20_caracteres()
    {
        var vazia = Assert.Throws<ExcecaoDeDominio>(() => TermoDeConsentimento.Novo(" ", Exemplos.TextoDoTermo));
        var longa = Assert.Throws<ExcecaoDeDominio>(() => TermoDeConsentimento.Novo(new string('9', 21), Exemplos.TextoDoTermo));

        Assert.Equal("Falta a versão do termo de consentimento.", vazia.Message);
        Assert.Equal("A versão do termo de consentimento tem 21 caracteres; o máximo é 20.", longa.Message);
    }
}
