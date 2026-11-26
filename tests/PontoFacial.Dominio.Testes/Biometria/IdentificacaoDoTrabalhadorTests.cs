using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Testes.Biometria;

public sealed class IdentificacaoDoTrabalhadorTests
{
    private const string SemAutorizacao =
        "Sem autorização biométrica vigente, o rosto não identifica o trabalhador. O código pessoal continua valendo.";

    private static AutorizacaoBiometrica Autorizacao() =>
        AutorizacaoBiometrica.Conceder(Exemplos.Construtora, Exemplos.Joao, Exemplos.Termo, Exemplos.Em(23, 8, 0));

    [Fact]
    public void O_codigo_pessoal_nao_depende_de_autorizacao()
    {
        var identificacao = IdentificacaoDoTrabalhador.PorCodigoPessoal(Exemplos.Construtora, Exemplos.Maria, Exemplos.Em(23, 7, 58));

        Assert.Equal(MeioDeIdentificacao.CodigoPessoal, identificacao.Meio);
        Assert.Equal(Exemplos.Construtora.Documento, identificacao.Empregador);
        Assert.Equal(Exemplos.Maria.Cpf, identificacao.Trabalhador);
        Assert.Equal(Exemplos.Em(23, 7, 58), identificacao.Em);
    }

    [Fact]
    public void O_rosto_conferido_identifica_quem_autorizou_no_instante_da_captura()
    {
        var autorizacao = Autorizacao();

        var identificacao = IdentificacaoDoTrabalhador.PorRosto(autorizacao, RostosDeTeste.JoaoConferido(autorizacao, Exemplos.Em(23, 12, 1)));

        Assert.Equal(MeioDeIdentificacao.Rosto, identificacao.Meio);
        Assert.Equal(Exemplos.Construtora.Documento, identificacao.Empregador);
        Assert.Equal(Exemplos.Joao.Cpf, identificacao.Trabalhador);
        Assert.Equal(Exemplos.Em(23, 12, 1), identificacao.Em);
    }

    [Fact]
    public void O_rosto_que_nao_conferiu_nao_identifica()
    {
        var autorizacao = Autorizacao();
        var mariaNoLugarDoJoao = RostosDeTeste.CadastroDoJoao(autorizacao)
            .Conferir(RostosDeTeste.Prova(RostosDeTeste.MariaNaPortaria, Exemplos.Em(23, 12, 1)), RostosDeTeste.Limiar);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => IdentificacaoDoTrabalhador.PorRosto(autorizacao, mariaNoLugarDoJoao));

        Assert.Equal("O rosto não conferiu com o cadastro do trabalhador. Tente de novo ou marque pelo código pessoal.", erro.Message);
    }

    [Fact]
    public void O_rosto_sem_prova_de_vida_nao_identifica_nem_conferindo()
    {
        /*
         * O vetor solto confere com o cadastro — é o do João. Mas pode ter
         * saído de uma foto dele: sem prova de vida, não marca ponto.
         */
        var autorizacao = Autorizacao();
        var vetorSolto = RostosDeTeste.CadastroDoJoao(autorizacao).Conferir(RostosDeTeste.JoaoNaPortaria, RostosDeTeste.Limiar, Exemplos.Em(23, 12, 1));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => IdentificacaoDoTrabalhador.PorRosto(autorizacao, vetorSolto));

        Assert.True(vetorSolto.Conferiu);
        Assert.Null(vetorSolto.Prova);
        Assert.Equal(
            "Sem prova de vida aprovada, o rosto não identifica o trabalhador: uma foto dele conferiria do mesmo jeito.",
            erro.Message);
    }

    [Fact]
    public void A_identificacao_pelo_rosto_tem_o_instante_do_fim_da_prova_de_vida()
    {
        var autorizacao = Autorizacao();
        var conferencia = RostosDeTeste.JoaoConferido(autorizacao, Exemplos.Em(23, 12, 1));

        var identificacao = IdentificacaoDoTrabalhador.PorRosto(autorizacao, conferencia);

        Assert.Equal(conferencia.Prova!.Em, identificacao.Em);
        Assert.True(conferencia.Prova.Em - conferencia.Prova.Inicio > TimeSpan.FromSeconds(3));
    }

    [Fact]
    public void A_conferencia_de_outro_cadastro_nao_serve()
    {
        var doJoao = Autorizacao();
        var daMaria = AutorizacaoBiometrica.Conceder(Exemplos.Construtora, Exemplos.Maria, Exemplos.Termo, Exemplos.Em(23, 8, 0));

        var erro = Assert.Throws<ExcecaoDeDominio>(() =>
            IdentificacaoDoTrabalhador.PorRosto(daMaria, RostosDeTeste.JoaoConferido(doJoao, Exemplos.Em(23, 12, 1))));

        Assert.Equal("A conferência do rosto é de outro cadastro que não o da autorização.", erro.Message);
    }

    [Fact]
    public void Depois_da_revogacao_o_rosto_nao_identifica_e_o_codigo_pessoal_continua()
    {
        var autorizacao = Autorizacao();
        var cadastro = RostosDeTeste.CadastroDoJoao(autorizacao);
        autorizacao.Revogar(Exemplos.Em(24, 18, 0));

        var conferencia = cadastro.Conferir(RostosDeTeste.Prova(RostosDeTeste.JoaoNaPortaria, Exemplos.Em(25, 7, 55)), RostosDeTeste.Limiar);
        var erro = Assert.Throws<ExcecaoDeDominio>(() => IdentificacaoDoTrabalhador.PorRosto(autorizacao, conferencia));
        var peloCodigo = IdentificacaoDoTrabalhador.PorCodigoPessoal(Exemplos.Construtora, Exemplos.Joao, Exemplos.Em(25, 7, 55));

        Assert.True(conferencia.Conferiu);
        Assert.Equal(SemAutorizacao, erro.Message);
        Assert.Equal(MeioDeIdentificacao.CodigoPessoal, peloCodigo.Meio);
    }

    [Fact]
    public void Antes_da_concessao_o_rosto_nao_identifica()
    {
        var autorizacao = Autorizacao();
        var conferencia = RostosDeTeste.JoaoConferido(autorizacao, Exemplos.Em(23, 7, 59));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => IdentificacaoDoTrabalhador.PorRosto(autorizacao, conferencia));

        Assert.Equal(SemAutorizacao, erro.Message);
    }
}
