using PontoFacial.Dominio.Biometria;

namespace PontoFacial.Rosto.Testes;

public sealed class LimiaresCalibradosTests
{
    [Fact]
    public void O_limiar_da_lfw_e_do_sface_e_diz_de_onde_veio()
    {
        var limiar = LimiaresCalibrados.SFaceNaLfw;

        Assert.Equal(ReconhecedorFacial.Reconhecedor, limiar.Reconhecedor);
        Assert.Equal(0.4317, limiar.Similaridade);
        Assert.Equal("lfw (13.233 imagens), falsa aceitação 1:10.000, 2026-11-24", limiar.Origem);
    }

    [Fact]
    public void Com_o_limiar_da_lfw_a_sunita_de_2012_confere_com_o_cadastro_de_2004_e_o_pontes_nao()
    {
        var limiar = LimiaresCalibrados.SFaceNaLfw;
        var retrato = Fotos.Imagem("sunita-williams-2004.jpg");
        var autorizacao = AutorizacaoBiometrica.Conceder(Exemplos.Empregador, Exemplos.Trabalhador, Exemplos.Termo, Exemplos.Em(8, 0));
        var cadastro = CadastroFacial.Cadastrar(
            autorizacao,
            [
                Fotos.Reconhecedor.Capturar(retrato).Modelo!,
                Fotos.Reconhecedor.Capturar(Fotos.Clareada(retrato, 25)).Modelo!,
                Fotos.Reconhecedor.Capturar(Fotos.Clareada(retrato, -25)).Modelo!,
            ],
            limiar,
            Exemplos.Em(8, 5));

        var sunita = cadastro.Conferir(Fotos.Reconhecedor.Capturar(Fotos.Arquivo("sunita-williams-2012.jpg")).Modelo!, limiar, Exemplos.Em(9, 0));
        var pontes = cadastro.Conferir(Fotos.Reconhecedor.Capturar(Fotos.Arquivo("marcos-pontes.jpg")).Modelo!, limiar, Exemplos.Em(9, 1));

        Assert.True(sunita.Conferiu);
        Assert.False(pontes.Conferiu);
    }
}
