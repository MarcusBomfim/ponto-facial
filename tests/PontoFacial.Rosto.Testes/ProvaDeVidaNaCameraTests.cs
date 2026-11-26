using PontoFacial.Dominio.Biometria.Vivacidade;

namespace PontoFacial.Rosto.Testes;

/*
 * O acompanhamento e a prova de vida com o retrato de verdade, parado ou
 * deslocado de pixels inteiros. As fotos inclinadas, curvadas e dobradas
 * estão no ensaio (tools/PontoFacial.EnsaioDaProvaDeVida), que tem a câmera
 * simulada.
 */
public sealed class ProvaDeVidaNaCameraTests
{
    private static readonly CriterioDaProvaDeVida Criterio = CriterioDaProvaDeVida.De(0.2, "valor de teste");

    private static readonly Desafio Desafio = Desafio.De([Movimento.Esquerda, Movimento.Cima, Movimento.Direita]);

    private static DateTimeOffset Quadro(int numero) => new DateTimeOffset(2026, 11, 25, 7, 58, 0, TimeSpan.FromHours(-3)) + TimeSpan.FromSeconds(numero / 15.0);

    /// <summary>A imagem deslocada de pixels inteiros, com a borda repetida.</summary>
    private static Imagem Deslocada(Imagem imagem, int dx, int dy)
    {
        var origem = imagem.CopiarBgr();
        var bgr = new byte[origem.Length];

        for (var y = 0; y < imagem.Altura; y++)
        {
            for (var x = 0; x < imagem.Largura; x++)
            {
                var deX = Math.Clamp(x - dx, 0, imagem.Largura - 1);
                var deY = Math.Clamp(y - dy, 0, imagem.Altura - 1);
                Array.Copy(origem, ((deY * imagem.Largura) + deX) * 3, bgr, ((y * imagem.Largura) + x) * 3, 3);
            }
        }

        return Imagem.DeBgr(imagem.Largura, imagem.Altura, bgr);
    }

    private static PontosDoRosto Pontos(RostoDetectado rosto) => PontosDoRosto.ParaAcompanhar(
        new(rosto.OlhoDireito.X, rosto.OlhoDireito.Y),
        new(rosto.OlhoEsquerdo.X, rosto.OlhoEsquerdo.Y),
        new(rosto.Nariz.X, rosto.Nariz.Y),
        new(rosto.BocaDireita.X, rosto.BocaDireita.Y),
        new(rosto.BocaEsquerda.X, rosto.BocaEsquerda.Y));

    [Fact]
    public void Os_nove_pontos_andam_com_a_imagem()
    {
        var retrato = Fotos.Imagem("marcos-pontes.jpg");
        var inicio = Pontos(Fotos.Reconhecedor.Capturar(retrato).Rosto!);
        var acompanhamento = new AcompanhamentoDoRosto(retrato, inicio);

        var depois = acompanhamento.Avancar(Deslocada(retrato, 4, -3))!;

        for (var i = 0; i < PontosDoRosto.Quantidade; i++)
        {
            Assert.Equal(inicio.Todos[i].X + 4, depois.Todos[i].X, 0.15);
            Assert.Equal(inicio.Todos[i].Y - 3, depois.Todos[i].Y, 0.15);
        }
    }

    [Fact]
    public void Trocado_o_rosto_o_acompanhamento_se_perde_e_nao_volta()
    {
        var retrato = Fotos.Imagem("marcos-pontes.jpg");
        var acompanhamento = new AcompanhamentoDoRosto(retrato, Pontos(Fotos.Reconhecedor.Capturar(retrato).Rosto!));

        Assert.Null(acompanhamento.Avancar(Fotos.Imagem("sunita-williams-2004.jpg").Reduzida(499)));
        Assert.True(acompanhamento.Perdido);
        Assert.Null(acompanhamento.Avancar(retrato));
    }

    [Fact]
    public void Antes_do_rosto_de_frente_a_prova_nao_comeca()
    {
        var prova = new ProvaDeVidaNaCamera(Fotos.Reconhecedor, Desafio, Criterio);

        Assert.Equal(Instrucoes.OlheParaACamera, prova.Instrucao);

        prova.Processar(Fotos.Lisa(320, 240, 90), Quadro(0));
        prova.Processar(Fotos.Imagem("tripulacao-expedicao-33.jpg"), Quadro(1));

        Assert.False(prova.Comecou);
        Assert.Null(prova.Resultado);
    }

    [Fact]
    public void O_retrato_parado_comeca_a_prova_e_esgota_o_tempo()
    {
        /*
         * Um retrato diante da câmera, sem mexer: a prova começa, porque o
         * rosto está de frente, pede o primeiro movimento e, seis segundos
         * depois, recusa.
         */
        var retrato = Fotos.Imagem("marcos-pontes.jpg");
        var prova = new ProvaDeVidaNaCamera(Fotos.Reconhecedor, Desafio, Criterio);
        var numero = 0;

        prova.Processar(retrato, Quadro(numero++));
        Assert.True(prova.Comecou);
        Assert.Equal("Vire o rosto devagar para a sua esquerda.", prova.Instrucao);

        while (!prova.Terminou && numero < 200)
        {
            prova.Processar(Deslocada(retrato, numero % 2, 0), Quadro(numero++));
        }

        Assert.Equal(MotivoDaRecusa.TempoEsgotado, prova.Resultado!.Motivo);
        Assert.InRange(numero, 90, 95);
    }

    [Fact]
    public void Outro_rosto_no_lugar_perde_a_prova()
    {
        var prova = new ProvaDeVidaNaCamera(Fotos.Reconhecedor, Desafio, Criterio);
        prova.Processar(Fotos.Imagem("marcos-pontes.jpg"), Quadro(0));

        prova.Processar(Fotos.Imagem("sunita-williams-2004.jpg").Reduzida(499), Quadro(1));

        Assert.Equal(MotivoDaRecusa.RostoPerdido, prova.Resultado!.Motivo);
        Assert.Throws<InvalidOperationException>(() => prova.Processar(Fotos.Imagem("marcos-pontes.jpg"), Quadro(2)));
    }

    [Fact]
    public void A_conferencia_pelo_detector_aceita_os_pontos_dentro_da_caixa_com_folga()
    {
        // Uma caixa de 100×120 em (100, 100): com a folga de 15%, vale de 85 a 215 e de 82 a 238.
        var rosto = new RostoDetectado(100, 100, 100, 120, new(130, 140), new(170, 140), new(150, 170), new(135, 200), new(165, 200), 0.95f);
        PontosDoRosto Com(double xDoOlhoEsquerdo, double yDaBoca) => PontosDoRosto.De(
        [
            new(130, 140), new(xDoOlhoEsquerdo, 140), new(150, 170), new(135, yDaBoca), new(165, yDaBoca),
            new(150, 140), new(150, yDaBoca), new(115, 170), new(185, 170),
        ]);

        Assert.True(ProvaDeVidaNaCamera.ContemOsPontos(rosto, Com(170, 200)));
        Assert.True(ProvaDeVidaNaCamera.ContemOsPontos(rosto, Com(214, 237)));
        Assert.False(ProvaDeVidaNaCamera.ContemOsPontos(rosto, Com(216, 200)));
        Assert.False(ProvaDeVidaNaCamera.ContemOsPontos(rosto, Com(170, 239)));
    }

    [Fact]
    public void O_detector_acha_os_rostos_nas_coordenadas_da_imagem_inteira()
    {
        var retrato = Fotos.Imagem("sunita-williams-2004.jpg");
        var capturado = Fotos.Reconhecedor.Capturar(retrato);

        var detectado = Assert.Single(Fotos.Reconhecedor.Detectar(retrato));

        Assert.Equal(capturado.Rosto!.OlhoDireito, detectado.OlhoDireito);
        Assert.Equal(1, capturado.Modelo!.Similaridade(Fotos.Reconhecedor.Extrair(retrato, detectado)), 6);
    }
}
