using PontoFacial.Dominio.Biometria.Vivacidade;

namespace PontoFacial.Dominio.Testes.Biometria.Vivacidade;

public sealed class MedidaDoQuadroTests
{
    private static MedidaDoQuadro Medir(Ponto3D[] pontos, double giro, double inclinacao, bool perspectiva = true) =>
        MedidaDoQuadro.Entre(Cenas.Ver(pontos, perspectiva: perspectiva), Cenas.Ver(pontos, giro, inclinacao, perspectiva));

    [Fact]
    public void Virar_para_a_esquerda_da_pessoa_e_giro_positivo_e_para_a_direita_negativo()
    {
        Assert.True(Medir(Cenas.Cabeca, 30, 0).Giro > SessaoDaProvaDeVida.GiroMinimo);
        Assert.True(Medir(Cenas.Cabeca, -30, 0).Giro < -SessaoDaProvaDeVida.GiroMinimo);
    }

    [Fact]
    public void Levantar_o_rosto_e_inclinacao_negativa_e_abaixar_positiva()
    {
        Assert.True(Medir(Cenas.Cabeca, 0, -25).Inclinacao < -SessaoDaProvaDeVida.InclinacaoMinima);
        Assert.True(Medir(Cenas.Cabeca, 0, 25).Inclinacao > SessaoDaProvaDeVida.InclinacaoMinima);
    }

    [Fact]
    public void Sem_movimento_nada_se_inclina_e_o_relevo_nao_se_mede()
    {
        var medida = Medir(Cenas.Cabeca, 0, 0);

        Assert.Equal(0, medida.Giro, 12);
        Assert.Equal(0, medida.Inclinacao, 12);
        Assert.Equal(0, medida.Encurtamento, 6);
        Assert.True(double.IsNaN(medida.Relevo));
    }

    [Theory]
    [InlineData(30, 0)]
    [InlineData(-30, 0)]
    [InlineData(0, 25)]
    [InlineData(0, -25)]
    [InlineData(20, 20)]
    public void Sem_perspectiva_a_medida_devolve_o_relevo_exato_da_cabeca(double giro, double inclinacao)
    {
        /*
         * Com a câmera longe (projeção ortográfica) e o plano dos olhos e da
         * boca de frente na referência, a conta é exata: o relevo medido é o
         * da cabeça, em qualquer direção do movimento.
         */
        var medida = Medir(Cenas.Cabeca, giro, inclinacao, perspectiva: false);

        Assert.Equal(Cenas.RelevoLateralDaCabeca, medida.RelevoLateral, 6);
        Assert.Equal(Cenas.RelevoVerticalDaCabeca, medida.RelevoVertical, 6);
        Assert.Equal(Cenas.RelevoVerticalDaCabeca, medida.Relevo, 6);
    }

    [Fact]
    public void Com_a_camera_a_50_cm_o_relevo_da_cabeca_fica_perto_do_exato()
    {
        var medida = Medir(Cenas.Cabeca, 30, 0);

        Assert.InRange(medida.RelevoLateral, Cenas.RelevoLateralDaCabeca - 0.1, Cenas.RelevoLateralDaCabeca + 0.1);
        Assert.InRange(medida.RelevoVertical, Cenas.RelevoVerticalDaCabeca - 0.05, Cenas.RelevoVerticalDaCabeca + 0.05);
    }

    [Theory]
    [InlineData(35, 0)]
    [InlineData(0, -30)]
    [InlineData(35, 35)]
    [InlineData(-40, -30)]
    public void A_foto_plana_nao_tem_relevo_nenhum_por_mais_que_se_incline(double giro, double inclinacao)
    {
        /*
         * A homografia é o movimento exato de um plano visto pela câmera, com
         * perspectiva e tudo: na foto, todo ponto a acompanha, e o relevo é
         * zero.
         */
        var medida = Medir(Cenas.Foto, giro, inclinacao);

        Assert.True(medida.Encurtamento > 0.4);
        Assert.Equal(0, medida.RelevoLateral, 9);
        Assert.Equal(0, medida.RelevoVertical, 9);
    }

    [Fact]
    public void A_foto_plana_inclinada_na_diagonal_finge_o_giro_mas_nao_o_relevo()
    {
        /*
         * Inclinada na diagonal, a foto cisalha: o nariz anda em relação aos
         * olhos como se a cabeça tivesse virado. Por isso o giro sozinho não
         * prova nada.
         */
        var medida = Medir(Cenas.Foto, -40, -35);

        Assert.True(Math.Abs(medida.Giro) > SessaoDaProvaDeVida.GiroMinimo);
        Assert.Equal(0, medida.Relevo, 9);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(63)]
    [InlineData(95)]
    public void A_foto_curvada_na_vertical_tem_relevo_lateral_mas_nao_vertical(double raio)
    {
        var medida = Medir(Cenas.CurvadaNaVertical(raio), 30, 0);

        Assert.True(medida.RelevoLateral > 0.2);
        Assert.InRange(medida.RelevoVertical, -0.03, 0.03);
    }

    [Theory]
    [InlineData(40)]
    [InlineData(63)]
    public void A_foto_curvada_na_horizontal_tem_relevo_vertical_mas_nao_lateral(double raio)
    {
        var medida = Medir(Cenas.CurvadaNaHorizontal(raio), 0, 25);

        Assert.True(medida.RelevoVertical > 0.1);
        Assert.InRange(medida.RelevoLateral, -0.03, 0.03);
    }

    [Theory]
    [InlineData(30)]
    [InlineData(50)]
    public void A_foto_dobrada_em_v_imita_o_nariz_de_lado_mas_nao_de_cima_a_baixo(double meioAngulo)
    {
        var medida = Medir(Cenas.DobradaNaVertical(meioAngulo), 30, 0);

        Assert.True(medida.RelevoLateral > 0.5);
        Assert.InRange(medida.RelevoVertical, -0.03, 0.03);
        Assert.InRange(medida.Relevo, -0.03, 0.03);
    }

    [Fact]
    public void O_sentido_do_relevo_vem_do_movimento_e_nao_do_nariz()
    {
        /*
         * A profundidade tem o sinal do lado para onde o rosto foi: o nariz de
         * uma cabeça fica à frente nos dois giros, e o de uma foto não ganha
         * sinal nenhum do ruído.
         */
        Assert.True(Medir(Cenas.Cabeca, 30, 0).RelevoLateral > 0);
        Assert.True(Medir(Cenas.Cabeca, -30, 0).RelevoLateral > 0);
        Assert.True(Medir(Cenas.Cabeca, 0, -25).RelevoVertical > 0);
    }
}
