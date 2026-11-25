namespace PontoFacial.Rosto.Testes;

public sealed class AlinhamentoTests
{
    [Fact]
    public void O_molde_sobre_si_mesmo_e_a_identidade()
    {
        var semelhanca = Alinhamento.Estimar(PontosDoMolde());

        Assert.Equal(1, semelhanca.A, 6);
        Assert.Equal(0, semelhanca.B, 6);
        Assert.Equal(0, semelhanca.Tx, 4);
        Assert.Equal(0, semelhanca.Ty, 4);
    }

    [Fact]
    public void Um_rosto_girado_e_maior_volta_ao_molde_pela_semelhanca_exata()
    {
        // O rosto na foto está 2,5 vezes maior que o molde, girado 20 graus e
        // deslocado: a estimativa tem de achar a semelhança que o desfaz.
        var angulo = 20 * Math.PI / 180;
        var desfaz = new Semelhanca(0.4 * Math.Cos(angulo), 0.4 * Math.Sin(angulo), 10, -5);
        var naFoto = desfaz.Inversa();
        var pontos = Alinhamento.Molde.Select(p => naFoto.Aplicar(p.X, p.Y)).Select(p => new Ponto((float)p.X, (float)p.Y)).ToList();

        var estimada = Alinhamento.Estimar(pontos);

        Assert.Equal(desfaz.A, estimada.A, 5);
        Assert.Equal(desfaz.B, estimada.B, 5);
        Assert.Equal(desfaz.Tx, estimada.Tx, 3);
        Assert.Equal(desfaz.Ty, estimada.Ty, 3);
    }

    [Fact]
    public void A_inversa_desfaz_a_semelhanca()
    {
        var semelhanca = new Semelhanca(1.3, -0.4, 7, 21);

        var (x, y) = semelhanca.Inversa().Aplicar(semelhanca.Aplicar(15, -8).X, semelhanca.Aplicar(15, -8).Y);

        Assert.Equal(15, x, 9);
        Assert.Equal(-8, y, 9);
    }

    [Fact]
    public void Com_o_rosto_ja_no_molde_o_recorte_e_a_propria_imagem_em_planos_vermelho_verde_e_azul()
    {
        // O azul de cada pixel é a coluna; o verde, a linha; o vermelho, 7.
        var bgr = new byte[112 * 112 * 3];
        for (var y = 0; y < 112; y++)
        {
            for (var x = 0; x < 112; x++)
            {
                var i = ((y * 112) + x) * 3;
                (bgr[i], bgr[i + 1], bgr[i + 2]) = ((byte)x, (byte)y, 7);
            }
        }

        var recorte = Alinhamento.RecortarParaOSFace(Imagem.DeBgr(112, 112, bgr), Alinhamento.Estimar(PontosDoMolde()));

        const int Plano = 112 * 112;
        Assert.Equal(7f, recorte[(30 * 112) + 40], 3);
        Assert.Equal(30f, recorte[Plano + (30 * 112) + 40], 3);
        Assert.Equal(40f, recorte[(2 * Plano) + (30 * 112) + 40], 3);
    }

    [Fact]
    public void Fora_da_imagem_o_recorte_e_preto()
    {
        var branca = Imagem.DeBgr(10, 10, Enumerable.Repeat((byte)255, 300).ToArray());

        // Pontos do molde num quadrado de 10×10: o recorte de 112×112 sai
        // quase todo de fora da imagem.
        var recorte = Alinhamento.RecortarParaOSFace(branca, Alinhamento.Estimar(PontosDoMolde()));

        Assert.Equal(0f, recorte[(100 * 112) + 100]);
    }

    [Fact]
    public void O_alinhamento_usa_cinco_pontos()
    {
        Assert.Throws<ArgumentException>(() => Alinhamento.Estimar([new Ponto(1, 1), new Ponto(2, 2)]));
    }

    private static List<Ponto> PontosDoMolde() => [.. Alinhamento.Molde.Select(p => new Ponto((float)p.X, (float)p.Y))];
}
