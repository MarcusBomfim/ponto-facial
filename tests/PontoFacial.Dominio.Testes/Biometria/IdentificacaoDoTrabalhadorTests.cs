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
    public void O_rosto_identifica_quem_autorizou_enquanto_a_autorizacao_vale()
    {
        var identificacao = IdentificacaoDoTrabalhador.PorRosto(Autorizacao(), Exemplos.Em(23, 12, 1));

        Assert.Equal(MeioDeIdentificacao.Rosto, identificacao.Meio);
        Assert.Equal(Exemplos.Construtora.Documento, identificacao.Empregador);
        Assert.Equal(Exemplos.Joao.Cpf, identificacao.Trabalhador);
    }

    [Fact]
    public void Depois_da_revogacao_o_rosto_nao_identifica_e_o_codigo_pessoal_continua()
    {
        var autorizacao = Autorizacao();
        autorizacao.Revogar(Exemplos.Em(24, 18, 0));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => IdentificacaoDoTrabalhador.PorRosto(autorizacao, Exemplos.Em(25, 7, 55)));
        var peloCodigo = IdentificacaoDoTrabalhador.PorCodigoPessoal(Exemplos.Construtora, Exemplos.Joao, Exemplos.Em(25, 7, 55));

        Assert.Equal(SemAutorizacao, erro.Message);
        Assert.Equal(MeioDeIdentificacao.CodigoPessoal, peloCodigo.Meio);
    }

    [Fact]
    public void Antes_da_concessao_o_rosto_nao_identifica()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => IdentificacaoDoTrabalhador.PorRosto(Autorizacao(), Exemplos.Em(23, 7, 59)));

        Assert.Equal(SemAutorizacao, erro.Message);
    }
}
