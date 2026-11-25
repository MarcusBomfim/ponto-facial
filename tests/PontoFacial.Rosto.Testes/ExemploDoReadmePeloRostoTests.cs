using PontoFacial.Dominio.Biometria;

namespace PontoFacial.Rosto.Testes;

/*
 * A seção "O rosto" do README, com as fotos de verdade. Os números que a
 * página mostra, com duas casas, estão conferidos aqui.
 */
public sealed class ExemploDoReadmePeloRostoTests
{
    [Fact]
    public void Do_retrato_de_2004_a_conferencia_em_2012()
    {
        using var reconhecedor = new ReconhecedorFacial(ReconhecedorFacial.PastaPadrao);
        var limiar = LimiaresCalibrados.SFaceNaLfw;

        // A foto vira 128 números com norma 1; a foto em si não fica.
        var em2004 = reconhecedor.Capturar(Fotos.Arquivo("sunita-williams-2004.jpg"));
        Assert.True(em2004.Capturou);
        Assert.Equal(128, em2004.Modelo.Dimensao);

        // Oito anos depois, outro traje, outra luz: a mesma pessoa.
        var em2012 = reconhecedor.Capturar(Fotos.Arquivo("sunita-williams-2012.jpg")).Modelo!;
        Assert.Equal(0.78, Math.Round(em2004.Modelo.Similaridade(em2012), 2));
        Assert.True(limiar.Aceita(em2004.Modelo.Similaridade(em2012)));

        // Outra pessoa fica longe do limiar de 0,4317.
        var pontes = reconhecedor.Capturar(Fotos.Arquivo("marcos-pontes.jpg")).Modelo!;
        Assert.Equal(0.05, Math.Round(em2004.Modelo.Similaridade(pontes), 2));
        Assert.False(limiar.Aceita(em2004.Modelo.Similaridade(pontes)));

        // Na foto da tripulação, ninguém marca ponto.
        var tripulacao = reconhecedor.Capturar(Fotos.Arquivo("tripulacao-expedicao-33.jpg"));
        Assert.Equal("Há mais de um rosto na imagem: só quem vai marcar o ponto deve aparecer.", tripulacao.Mensagem);
    }
}
