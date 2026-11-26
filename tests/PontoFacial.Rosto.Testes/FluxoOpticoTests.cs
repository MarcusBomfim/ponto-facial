namespace PontoFacial.Rosto.Testes;

/*
 * O fluxo óptico em imagens feitas à mão: uma textura sorteada, e a mesma
 * deslocada de uma fração de pixel conhecida.
 */
public sealed class FluxoOpticoTests
{
    private const int Lado = 200;

    /// <summary>Uma textura de manchas suaves, como pele e sombra, sempre a mesma.</summary>
    private static double Textura(double x, double y, int semente = 7)
    {
        var valor = 128.0;
        var sorteio = new Random(semente);

        for (var i = 0; i < 40; i++)
        {
            var cx = sorteio.NextDouble() * Lado;
            var cy = sorteio.NextDouble() * Lado;
            var raio = 4 + (sorteio.NextDouble() * 12);
            var altura = (sorteio.NextDouble() - 0.5) * 160;
            valor += altura * Math.Exp(-(((x - cx) * (x - cx)) + ((y - cy) * (y - cy))) / (2 * raio * raio));
        }

        return Math.Clamp(valor, 0, 255);
    }

    private static Imagem Desenhar(Func<double, double, double> cinza)
    {
        var bgr = new byte[Lado * Lado * 3];

        for (var y = 0; y < Lado; y++)
        {
            for (var x = 0; x < Lado; x++)
            {
                var valor = (byte)Math.Round(cinza(x, y));
                bgr[((y * Lado) + x) * 3] = valor;
                bgr[(((y * Lado) + x) * 3) + 1] = valor;
                bgr[(((y * Lado) + x) * 3) + 2] = valor;
            }
        }

        return Imagem.DeBgr(Lado, Lado, bgr);
    }

    [Theory]
    [InlineData(2.5, -1.75)]
    [InlineData(-6.25, 4.5)]
    [InlineData(11.0, 7.0)]
    public void Acha_o_deslocamento_com_fracao_de_pixel(double dx, double dy)
    {
        var antes = Piramide.De(Desenhar((x, y) => Textura(x, y)));
        var depois = Piramide.De(Desenhar((x, y) => Textura(x - dx, y - dy)));

        var destino = FluxoOptico.Acompanhar(antes, depois, 100, 100, 10);

        Assert.NotNull(destino);
        Assert.Equal(100 + dx, destino.Value.X, 0.15);
        Assert.Equal(100 + dy, destino.Value.Y, 0.15);
    }

    [Fact]
    public void Numa_imagem_trocada_a_volta_nao_cai_no_lugar_e_o_ponto_se_perde()
    {
        var antes = Piramide.De(Desenhar((x, y) => Textura(x, y)));
        var outra = Piramide.De(Desenhar((x, y) => Textura(x, y, semente: 99)));

        Assert.Null(FluxoOptico.Acompanhar(antes, outra, 100, 100, 10));
    }

    [Fact]
    public void Num_pedaco_liso_nao_ha_o_que_acompanhar()
    {
        var lisa = Piramide.De(Desenhar((_, _) => 90));

        Assert.Null(FluxoOptico.Acompanhar(lisa, lisa, 100, 100, 10));
    }
}
