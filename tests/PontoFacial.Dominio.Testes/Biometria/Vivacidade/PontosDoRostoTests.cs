using PontoFacial.Dominio.Biometria.Vivacidade;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Testes.Biometria.Vivacidade;

public sealed class PontosDoRostoTests
{
    [Fact]
    public void Os_quatro_pontos_a_mais_saem_dos_cinco_do_detector()
    {
        var pontos = PontosDoRosto.ParaAcompanhar(new(100, 100), new(160, 100), new(130, 140), new(110, 170), new(150, 170));

        Assert.Equal(new PontoNaImagem(130, 100), pontos.EntreOsOlhos);
        Assert.Equal(new PontoNaImagem(130, 170), pontos.CentroDaBoca);
        Assert.Equal(new PontoNaImagem(88, 140), pontos.BochechaDireita);
        Assert.Equal(new PontoNaImagem(172, 140), pontos.BochechaEsquerda);
        Assert.Equal(60, pontos.DistanciaEntreOlhos, 9);
        Assert.Equal(PontosDoRosto.Quantidade, pontos.Todos.Count);
    }

    [Fact]
    public void Com_a_cabeca_inclinada_as_bochechas_seguem_a_linha_dos_olhos()
    {
        // Olhos a 45°: as bochechas saem do nariz na mesma diagonal.
        var pontos = PontosDoRosto.ParaAcompanhar(new(100, 100), new(130, 130), new(100, 140), new(80, 160), new(110, 190));
        var afastamento = 0.7 * Math.Sqrt(1800) / Math.Sqrt(2);

        Assert.Equal(100 - afastamento, pontos.BochechaDireita.X, 9);
        Assert.Equal(140 - afastamento, pontos.BochechaDireita.Y, 9);
        Assert.Equal(100 + afastamento, pontos.BochechaEsquerda.X, 9);
        Assert.Equal(140 + afastamento, pontos.BochechaEsquerda.Y, 9);
    }

    [Fact]
    public void Os_nove_pontos_chegam_na_ordem_e_sao_numeros()
    {
        var pontos = Enumerable.Range(0, 9).Select(i => new PontoNaImagem(i * 10, i)).ToList();

        var rosto = PontosDoRosto.De(pontos);

        Assert.Equal(pontos, rosto.Todos);
        Assert.Equal(pontos[2], rosto.Nariz);
        Assert.Equal(pontos[8], rosto.BochechaEsquerda);

        Assert.Equal(
            "O rosto acompanhado tem 9 pontos; vieram 5.",
            Assert.Throws<ExcecaoDeDominio>(() => PontosDoRosto.De(pontos.Take(5).ToList())).Message);

        pontos[4] = new PontoNaImagem(double.NaN, 3);
        Assert.Equal(
            "Um ponto do rosto tem coordenada que não é número.",
            Assert.Throws<ExcecaoDeDominio>(() => PontosDoRosto.De(pontos)).Message);
    }

    [Fact]
    public void Os_olhos_no_mesmo_lugar_nao_sao_rosto()
    {
        Assert.Equal(
            "Os olhos caem no mesmo lugar da imagem.",
            Assert.Throws<ExcecaoDeDominio>(() => PontosDoRosto.ParaAcompanhar(new(100, 100), new(100.5, 100), new(100, 140), new(90, 170), new(110, 170))).Message);
    }
}
