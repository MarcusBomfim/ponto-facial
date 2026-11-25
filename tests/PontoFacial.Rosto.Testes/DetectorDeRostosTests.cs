namespace PontoFacial.Rosto.Testes;

public sealed class DetectorDeRostosTests
{
    private static readonly DetectorDeRostos Detector =
        new(Path.Combine(ReconhecedorFacial.PastaPadrao, DetectorDeRostos.ArquivoDoModelo));

    [Fact]
    public void Acha_as_seis_pessoas_da_foto_da_tripulacao()
    {
        var rostos = Detector.Detectar(Fotos.Imagem("tripulacao-expedicao-33.jpg"));

        Assert.Equal(6, rostos.Count);
        Assert.All(rostos, rosto => Assert.True(rosto.Confianca >= DetectorDeRostos.ConfiancaPadrao));
    }

    [Fact]
    public void Na_imagem_lisa_nao_ha_rosto()
    {
        Assert.Empty(Detector.Detectar(Fotos.Lisa(640, 480, 200)));
    }

    [Fact]
    public void A_imagem_de_tamanho_que_nao_e_multiplo_de_32_e_completada_e_o_rosto_fica_no_lugar()
    {
        // 400×499: o modelo recebe 416×512, e as coordenadas continuam as da imagem.
        var retrato = Fotos.Imagem("marcos-pontes.jpg");

        var rosto = Assert.Single(Detector.Detectar(retrato));

        Assert.InRange(rosto.Centro.X, retrato.Largura * 0.3, retrato.Largura * 0.7);
        Assert.InRange(rosto.Centro.Y, retrato.Altura * 0.15, retrato.Altura * 0.6);
    }
}
