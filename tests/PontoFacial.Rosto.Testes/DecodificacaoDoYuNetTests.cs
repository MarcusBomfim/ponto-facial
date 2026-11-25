namespace PontoFacial.Rosto.Testes;

/*
 * Saídas do YuNet montadas à mão para uma entrada de 32×32: grade de 4×4 no
 * passo 8, de 2×2 no passo 16 e de 1×1 no passo 32.
 */
public sealed class DecodificacaoDoYuNetTests
{
    [Fact]
    public void A_celula_confiante_vira_um_rosto_nas_coordenadas_da_imagem()
    {
        // Linha 1, coluna 2 do passo 8: centro em ((2 + 0,5) × 8, (1 + 0,5) × 8) = (20, 12),
        // largura e^(ln 2) × 8 = 16 e altura e^(ln 3) × 8 = 24; o canto fica em (12, 0).
        var saidas = Vazias();
        var celula = (1 * 4) + 2;
        saidas[0].Classe[celula] = 0.9f;
        saidas[0].Objeto[celula] = 1f;
        (saidas[0].Caixa[celula * 4], saidas[0].Caixa[(celula * 4) + 1]) = (0.5f, 0.5f);
        (saidas[0].Caixa[(celula * 4) + 2], saidas[0].Caixa[(celula * 4) + 3]) = (MathF.Log(2), MathF.Log(3));
        (saidas[0].Pontos[celula * 10], saidas[0].Pontos[(celula * 10) + 1]) = (0.25f, -0.5f);

        var rosto = Assert.Single(DecodificacaoDoYuNet.Decodificar(saidas, 32, 32, 0.9f, 0.3f));

        Assert.Equal(12f, rosto.X, 4);
        Assert.Equal(0f, rosto.Y, 4);
        Assert.Equal(16f, rosto.Largura, 4);
        Assert.Equal(24f, rosto.Altura, 4);
        Assert.Equal(MathF.Sqrt(0.9f), rosto.Confianca, 5);
        Assert.Equal(new Ponto(18, 4), rosto.OlhoDireito);
    }

    [Fact]
    public void A_confianca_e_a_media_geometrica_das_notas_com_cada_uma_entre_0_e_1()
    {
        var saidas = Vazias();
        saidas[2].Classe[0] = 1.5f;
        saidas[2].Objeto[0] = 0.81f;

        var rosto = Assert.Single(DecodificacaoDoYuNet.Decodificar(saidas, 32, 32, 0.5f, 0.3f));

        Assert.Equal(0.9f, rosto.Confianca, 5);
    }

    [Fact]
    public void Abaixo_da_confianca_minima_nao_e_rosto()
    {
        var saidas = Vazias();
        saidas[1].Classe[3] = 0.8f;
        saidas[1].Objeto[3] = 0.8f;

        Assert.Empty(DecodificacaoDoYuNet.Decodificar(saidas, 32, 32, 0.9f, 0.3f));
    }

    [Fact]
    public void Das_caixas_que_se_sobrepoem_fica_a_mais_confiante()
    {
        var forte = Rosto(0, 0, 10, 10, 0.95f);
        var repetida = Rosto(1, 1, 10, 10, 0.92f);
        var outra = Rosto(30, 30, 10, 10, 0.91f);

        var mantidos = DecodificacaoDoYuNet.SuprimirRepetidos([repetida, outra, forte], 0.3f);

        Assert.Equal([forte, outra], mantidos);
    }

    [Fact]
    public void A_sobreposicao_e_a_intersecao_sobre_a_uniao()
    {
        var a = Rosto(0, 0, 10, 10, 1);

        Assert.Equal(1f, DecodificacaoDoYuNet.Sobreposicao(a, a), 5);
        Assert.Equal(0f, DecodificacaoDoYuNet.Sobreposicao(a, Rosto(20, 0, 10, 10, 1)));
        Assert.Equal(1f / 3, DecodificacaoDoYuNet.Sobreposicao(a, Rosto(5, 0, 10, 10, 1)), 5);
    }

    private static List<DecodificacaoDoYuNet.SaidasDoPasso> Vazias() =>
    [
        .. DecodificacaoDoYuNet.Passos.Select(passo =>
        {
            var celulas = (32 / passo) * (32 / passo);
            return new DecodificacaoDoYuNet.SaidasDoPasso(passo, new float[celulas], new float[celulas], new float[celulas * 4], new float[celulas * 10]);
        }),
    ];

    private static RostoDetectado Rosto(float x, float y, float largura, float altura, float confianca) =>
        new(x, y, largura, altura, default, default, default, default, default, confianca);
}
