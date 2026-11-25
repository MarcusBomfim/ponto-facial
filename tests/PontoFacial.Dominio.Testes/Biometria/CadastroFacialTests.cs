using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Testes.Biometria;

/*
 * Vetores de quatro dimensões fazem o papel do rosto: as três capturas do
 * João apontam quase para o mesmo lado; a da Maria, para outro.
 */
public sealed class CadastroFacialTests
{
    private static readonly LimiarDeAceitacao Limiar = LimiarDeAceitacao.De("teste-4d", 0.5, "valor de teste");

    private static readonly ModeloFacial[] CapturasDoJoao =
    [
        ModeloFacial.De("teste-4d", [1f, 0.2f, 0f, 0f]),
        ModeloFacial.De("teste-4d", [1f, 0f, 0.2f, 0f]),
        ModeloFacial.De("teste-4d", [1f, 0f, 0f, 0.2f]),
    ];

    private static readonly ModeloFacial RostoDaMaria = ModeloFacial.De("teste-4d", [0f, 1f, 0f, 0f]);

    [Fact]
    public void O_modelo_e_a_media_das_capturas_com_norma_1()
    {
        var cadastro = CadastroFacial.Cadastrar(Autorizacao(), CapturasDoJoao, Limiar, Exemplos.Em(23, 8, 0));

        var esperado = ModeloFacial.De("teste-4d", [3f, 0.2f, 0.2f, 0.2f]);
        Assert.Equal(1.0, cadastro.Modelo.Similaridade(esperado), 6);
        Assert.Equal(Exemplos.Construtora.Documento, cadastro.Empregador);
        Assert.Equal(Exemplos.Joao.Cpf, cadastro.Trabalhador);
        Assert.Equal(Exemplos.Em(23, 8, 0), cadastro.CadastradoEm);
    }

    [Fact]
    public void Sem_autorizacao_vigente_o_rosto_nao_e_cadastrado()
    {
        var autorizacao = Autorizacao();
        autorizacao.Revogar(Exemplos.Em(23, 7, 30));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => CadastroFacial.Cadastrar(autorizacao, CapturasDoJoao, Limiar, Exemplos.Em(23, 8, 0)));

        Assert.Equal("Sem autorização biométrica vigente, o rosto não é cadastrado.", erro.Message);
    }

    [Fact]
    public void O_cadastro_pede_pelo_menos_tres_capturas()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() =>
            CadastroFacial.Cadastrar(Autorizacao(), CapturasDoJoao[..2], Limiar, Exemplos.Em(23, 8, 0)));

        Assert.Equal("O cadastro do rosto pede pelo menos 3 capturas; vieram 2.", erro.Message);
    }

    [Fact]
    public void Uma_captura_de_outro_rosto_recusa_o_cadastro_inteiro()
    {
        ModeloFacial[] capturas = [CapturasDoJoao[0], CapturasDoJoao[1], RostoDaMaria];

        var erro = Assert.Throws<ExcecaoDeDominio>(() => CadastroFacial.Cadastrar(Autorizacao(), capturas, Limiar, Exemplos.Em(23, 8, 0)));

        Assert.Equal(
            "As capturas 1 e 3 não conferem entre si: similaridade 0,196, abaixo do limiar 0,500. "
            + "Refaça o cadastro só com o trabalhador diante da câmera.",
            erro.Message);
    }

    [Fact]
    public void Captura_de_outro_reconhecedor_e_recusada()
    {
        ModeloFacial[] capturas = [CapturasDoJoao[0], CapturasDoJoao[1], ModeloFacial.De("outro", [1f, 0f, 0f, 0f])];

        var erro = Assert.Throws<ExcecaoDeDominio>(() => CadastroFacial.Cadastrar(Autorizacao(), capturas, Limiar, Exemplos.Em(23, 8, 0)));

        Assert.Equal("A captura 3 é do reconhecedor outro, e o limiar, do teste-4d.", erro.Message);
    }

    [Fact]
    public void A_conferencia_aceita_o_mesmo_rosto_e_recusa_outro()
    {
        var cadastro = CadastroFacial.Cadastrar(Autorizacao(), CapturasDoJoao, Limiar, Exemplos.Em(23, 8, 0));

        var doJoao = cadastro.Conferir(ModeloFacial.De("teste-4d", [1f, 0.1f, 0.1f, 0f]), Limiar, Exemplos.Em(24, 7, 58));
        var daMaria = cadastro.Conferir(RostoDaMaria, Limiar, Exemplos.Em(24, 7, 59));

        Assert.True(doJoao.Conferiu);
        Assert.False(daMaria.Conferiu);
        Assert.Equal(Exemplos.Joao.Cpf, daMaria.Trabalhador);
        Assert.Equal(Exemplos.Em(24, 7, 59), daMaria.Em);
        Assert.Same(Limiar, daMaria.Limiar);
    }

    [Fact]
    public void A_conferencia_usa_um_limiar_do_mesmo_reconhecedor()
    {
        var cadastro = CadastroFacial.Cadastrar(Autorizacao(), CapturasDoJoao, Limiar, Exemplos.Em(23, 8, 0));
        var deOutro = LimiarDeAceitacao.De("sface-2021dec", 0.4, "valor de teste");

        var erro = Assert.Throws<ExcecaoDeDominio>(() => cadastro.Conferir(CapturasDoJoao[0], deOutro, Exemplos.Em(24, 7, 58)));

        Assert.Equal("O limiar é do reconhecedor sface-2021dec, e o cadastro, do teste-4d.", erro.Message);
    }

    private static AutorizacaoBiometrica Autorizacao() =>
        AutorizacaoBiometrica.Conceder(Exemplos.Construtora, Exemplos.Joao, Exemplos.Termo, Exemplos.Em(23, 7, 0));
}
