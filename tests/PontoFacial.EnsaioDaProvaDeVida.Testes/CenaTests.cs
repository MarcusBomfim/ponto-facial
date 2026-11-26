namespace PontoFacial.EnsaioDaProvaDeVida.Testes;

/*
 * A câmera simulada é o instrumento do ensaio: se ela errar a geometria, o
 * ensaio mede outra coisa. Estes testes conferem a escala, o centro, o
 * sentido do giro e as superfícies umas contra as outras.
 */
public sealed class CenaTests
{
    [Fact]
    public void A_foto_de_frente_aparece_no_centro_em_tamanho_real()
    {
        // Olhos a 63 mm, a 40 cm, com 554 pixels de distância focal: 87,3 pixels.
        var cena = new Cena(Retratos.Foto("marcos-pontes.jpg"));

        var rosto = Assert.Single(Retratos.Reconhecedor.Detectar(cena.Ver(new Plana(), new Pose(0, 0, 400))));
        var centroX = (rosto.OlhoDireito.X + rosto.OlhoEsquerdo.X + rosto.BocaDireita.X + rosto.BocaEsquerda.X) / 4;
        var centroY = (rosto.OlhoDireito.Y + rosto.OlhoEsquerdo.Y + rosto.BocaDireita.Y + rosto.BocaEsquerda.Y) / 4;

        Assert.InRange(rosto.DistanciaEntreOlhos, 63 * 554 / 400.0 * 0.95, 63 * 554 / 400.0 * 1.05);
        Assert.InRange(centroX, 315, 325);
        Assert.InRange(centroY, 235, 245);
    }

    [Fact]
    public void Mais_longe_o_rosto_fica_menor_na_proporcao()
    {
        var cena = new Cena(Retratos.Foto("marcos-pontes.jpg"));

        var perto = Assert.Single(Retratos.Reconhecedor.Detectar(cena.Ver(new Plana(), new Pose(0, 0, 350))));
        var longe = Assert.Single(Retratos.Reconhecedor.Detectar(cena.Ver(new Plana(), new Pose(0, 0, 450))));

        // O detector erra um ou dois pixels em cada medida: 6% de folga na razão.
        Assert.Equal(450.0 / 350, perto.DistanciaEntreOlhos / longe.DistanciaEntreOlhos, 0.08);
    }

    [Fact]
    public void A_foto_curvada_com_raio_enorme_e_a_plana()
    {
        var cena = new Cena(Retratos.Foto("sunita-williams-2004.jpg"));
        var pose = new Pose(20, -10, 400);

        Assert.True(Retratos.Diferenca(cena.Ver(new Plana(), pose), cena.Ver(new Curvada(1e7, 90), pose)) < 0.5);
        Assert.True(Retratos.Diferenca(cena.Ver(new Plana(), pose), cena.Ver(new Curvada(1e7, 30), pose)) < 0.5);
        Assert.True(Retratos.Diferenca(cena.Ver(new Plana(), pose), cena.Ver(new Dobrada(0, 90), pose)) < 0.5);
    }

    [Fact]
    public void A_curva_de_eixo_deitado_e_a_de_eixo_de_pe_virada_de_lado()
    {
        /*
         * Com a foto de frente, o cilindro de pé encolhe a imagem de lado a
         * lado; o deitado, de cima para baixo. Com 6 cm de raio, os olhos a
         * 3,15 cm do meio ficam a 3,0 cm na projeção: uns 4 pixels a menos.
         */
        var cena = new Cena(Retratos.Foto("marcos-pontes.jpg"));
        var dePe = Assert.Single(Retratos.Reconhecedor.Detectar(cena.Ver(new Curvada(60, 90), new Pose(0, 0, 400))));
        var deitada = Assert.Single(Retratos.Reconhecedor.Detectar(cena.Ver(new Curvada(60, 0), new Pose(0, 0, 400))));
        var plana = Assert.Single(Retratos.Reconhecedor.Detectar(cena.Ver(new Plana(), new Pose(0, 0, 400))));

        Assert.True(dePe.DistanciaEntreOlhos < plana.DistanciaEntreOlhos - 2);
        Assert.True(Math.Abs(deitada.DistanciaEntreOlhos - plana.DistanciaEntreOlhos) < Math.Abs(dePe.DistanciaEntreOlhos - plana.DistanciaEntreOlhos));
        Assert.True(Altura(deitada) < Altura(plana) - 2);
        Assert.True(Math.Abs(Altura(dePe) - Altura(plana)) < Math.Abs(Altura(deitada) - Altura(plana)));
    }

    [Fact]
    public void O_giro_positivo_leva_o_que_esta_a_frente_para_a_direita_da_imagem()
    {
        // No relevo de mentira, o nariz está à frente: girado, ele anda para a direita dos olhos.
        var foto = Retratos.Foto("marcos-pontes.jpg");
        var cena = new Cena(foto);
        var alturaDoNariz = (foto.Rosto.Nariz.Y - foto.CentroY) * (Cena.OlhosEmMilimetros / foto.DistanciaEntreOlhos);
        var relevo = new ComRelevo(alturaDoNariz);

        var deFrente = Assert.Single(Retratos.Reconhecedor.Detectar(cena.Ver(relevo, new Pose(0, 0, 400))));
        var girado = Assert.Single(Retratos.Reconhecedor.Detectar(cena.Ver(relevo, new Pose(25, 0, 400))));

        Assert.True(Desvio(girado) > Desvio(deFrente) + 0.05);
    }

    [Fact]
    public void O_ruido_do_sensor_muda_os_pixels_e_e_o_mesmo_com_a_mesma_semente()
    {
        var cena = new Cena(Retratos.Foto("marcos-pontes.jpg"));
        var limpa = cena.Ver(new Plana(), new Pose(0, 0, 400));

        var umaVez = cena.Ver(new Plana(), new Pose(0, 0, 400), new Random(3));
        var outraVez = cena.Ver(new Plana(), new Pose(0, 0, 400), new Random(3));

        Assert.InRange(Retratos.Diferenca(limpa, umaVez), 1.5, 3.5);
        Assert.Equal(0, Retratos.Diferenca(umaVez, outraVez));
    }

    private static double Altura(Rosto.RostoDetectado rosto) =>
        ((rosto.BocaDireita.Y + rosto.BocaEsquerda.Y) / 2) - ((rosto.OlhoDireito.Y + rosto.OlhoEsquerdo.Y) / 2);

    private static double Desvio(Rosto.RostoDetectado rosto) =>
        (rosto.Nariz.X - ((rosto.OlhoDireito.X + rosto.OlhoEsquerdo.X) / 2)) / rosto.DistanciaEntreOlhos;
}
