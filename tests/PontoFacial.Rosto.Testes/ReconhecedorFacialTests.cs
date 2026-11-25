using PontoFacial.Dominio.Biometria;

namespace PontoFacial.Rosto.Testes;

/*
 * Os modelos de verdade, em fotos de verdade: retratos da NASA, em domínio
 * público (Imagens/CREDITOS.md). As montagens — dois rostos lado a lado, o
 * rosto pequeno, a luz mudada — saem do retrato do astronauta Marcos Pontes.
 */
public sealed class ReconhecedorFacialTests
{
    [Fact]
    public void A_mesma_pessoa_oito_anos_depois_continua_parecida()
    {
        var em2004 = Fotos.Reconhecedor.Capturar(Fotos.Arquivo("sunita-williams-2004.jpg")).Modelo!;
        var em2012 = Fotos.Reconhecedor.Capturar(Fotos.Arquivo("sunita-williams-2012.jpg")).Modelo!;

        Assert.InRange(em2004.Similaridade(em2012), 0.7, 0.9);
    }

    [Fact]
    public void Pessoas_diferentes_tem_quase_nada_em_comum()
    {
        var sunita = Fotos.Reconhecedor.Capturar(Fotos.Arquivo("sunita-williams-2004.jpg")).Modelo!;
        var pontes = Fotos.Reconhecedor.Capturar(Fotos.Arquivo("marcos-pontes.jpg")).Modelo!;

        Assert.InRange(sunita.Similaridade(pontes), -0.2, 0.2);
    }

    [Fact]
    public void A_foto_da_tripulacao_nao_marca_ponto_de_ninguem()
    {
        var resultado = Fotos.Reconhecedor.Capturar(Fotos.Arquivo("tripulacao-expedicao-33.jpg"));

        Assert.Equal(ProblemaNaCaptura.MaisDeUmRosto, resultado.Problema);
    }

    [Fact]
    public void O_retrato_da_um_rosto_com_os_cinco_pontos_no_lugar()
    {
        var resultado = Fotos.Reconhecedor.Capturar(Fotos.Arquivo("marcos-pontes.jpg"));

        Assert.True(resultado.Capturou);
        Assert.True(resultado.Rosto.Confianca >= DetectorDeRostos.ConfiancaPadrao);

        // O olho direito da pessoa fica à esquerda na imagem; os olhos acima
        // do nariz, o nariz acima da boca.
        var rosto = resultado.Rosto;
        Assert.True(rosto.OlhoDireito.X < rosto.OlhoEsquerdo.X);
        Assert.True(rosto.BocaDireita.X < rosto.BocaEsquerda.X);
        Assert.True(Math.Max(rosto.OlhoDireito.Y, rosto.OlhoEsquerdo.Y) < rosto.Nariz.Y);
        Assert.True(rosto.Nariz.Y < Math.Min(rosto.BocaDireita.Y, rosto.BocaEsquerda.Y));
    }

    [Fact]
    public void O_vetor_e_o_do_sface_com_128_numeros_e_norma_1()
    {
        var modelo = Fotos.Reconhecedor.Capturar(Fotos.Arquivo("marcos-pontes.jpg")).Modelo!;

        Assert.Equal("sface-2021dec", modelo.Reconhecedor);
        Assert.Equal(128, modelo.Dimensao);
        Assert.Equal(1.0, Math.Sqrt(modelo.Valores.ToArray().Sum(valor => (double)valor * valor)), 5);
    }

    [Fact]
    public void A_mesma_foto_da_sempre_o_mesmo_vetor()
    {
        var primeira = Fotos.Reconhecedor.Capturar(Fotos.Arquivo("marcos-pontes.jpg")).Modelo!;
        var segunda = Fotos.Reconhecedor.Capturar(Fotos.Arquivo("marcos-pontes.jpg")).Modelo!;

        Assert.Equal(primeira.Valores.ToArray(), segunda.Valores.ToArray());
    }

    [Fact]
    public void A_mesma_pessoa_com_outra_luz_e_outra_resolucao_continua_parecida()
    {
        var retrato = Fotos.Imagem("marcos-pontes.jpg");
        var original = Fotos.Reconhecedor.Capturar(retrato).Modelo!;

        var clareada = Fotos.Reconhecedor.Capturar(Fotos.Clareada(retrato, 40)).Modelo!;
        var reduzida = Fotos.Reconhecedor.Capturar(retrato.Reduzida(420)).Modelo!;

        Assert.True(original.Similaridade(clareada) > 0.9);
        Assert.True(original.Similaridade(reduzida) > 0.9);
    }

    [Fact]
    public void Imagem_sem_rosto_diz_para_olhar_para_a_camera()
    {
        var resultado = Fotos.Reconhecedor.Capturar(Fotos.Lisa(320, 240, 128));

        Assert.Equal(ProblemaNaCaptura.NenhumRosto, resultado.Problema);
        Assert.Equal("Nenhum rosto na imagem. Olhe para a câmera, com o rosto inteiro aparecendo.", resultado.Mensagem);
        Assert.Null(resultado.Modelo);
    }

    [Fact]
    public void Arquivo_que_nao_e_imagem_e_ilegivel()
    {
        var resultado = Fotos.Reconhecedor.Capturar("isto não é uma foto"u8);

        Assert.Equal(ProblemaNaCaptura.ImagemIlegivel, resultado.Problema);
    }

    [Fact]
    public void Dois_rostos_na_imagem_nao_marcam_ponto()
    {
        var retrato = Fotos.Imagem("marcos-pontes.jpg");
        var dois = Fotos.LadoALado(retrato, retrato);

        var resultado = Fotos.Reconhecedor.Capturar(dois);

        Assert.Equal(ProblemaNaCaptura.MaisDeUmRosto, resultado.Problema);
        Assert.Equal("Há mais de um rosto na imagem: só quem vai marcar o ponto deve aparecer.", resultado.Mensagem);
    }

    [Fact]
    public void Numa_base_de_fotos_vale_o_rosto_mais_perto_do_centro()
    {
        var retrato = Fotos.Imagem("marcos-pontes.jpg");
        var dois = Fotos.LadoALado(Fotos.Lisa(retrato.Largura / 2, retrato.Altura, 0), Fotos.LadoALado(retrato, retrato));

        var resultado = Fotos.Reconhecedor.Capturar(dois, EscolhaDoRosto.OMaisCentral);

        // Com meia largura de preto à esquerda, o primeiro retrato fica mais perto do centro.
        Assert.True(resultado.Capturou);
        Assert.True(resultado.Rosto.Centro.X < dois.Largura * 0.6);
    }

    [Fact]
    public void Rosto_pequeno_demais_pede_para_chegar_perto()
    {
        var pequena = Fotos.Imagem("marcos-pontes.jpg").Reduzida(100);

        var resultado = Fotos.Reconhecedor.Capturar(pequena);

        Assert.Equal(ProblemaNaCaptura.RostoLonge, resultado.Problema);
        Assert.Equal("O rosto está longe: chegue mais perto da câmera.", resultado.Mensagem);
        Assert.True(resultado.Rosto!.DistanciaEntreOlhos < ReconhecedorFacial.DistanciaMinimaEntreOlhos);
    }

    [Fact]
    public void Tres_capturas_do_mesmo_rosto_fazem_o_cadastro_no_dominio()
    {
        // O limiar publicado pelo OpenCV para o SFace; o calibrado vem da LFW.
        var limiar = LimiarDeAceitacao.De(ReconhecedorFacial.Reconhecedor, 0.363, "OpenCV Zoo, limiar publicado para o SFace");
        var retrato = Fotos.Imagem("marcos-pontes.jpg");
        var autorizacao = AutorizacaoBiometrica.Conceder(Exemplos.Empregador, Exemplos.Trabalhador, Exemplos.Termo, Exemplos.Em(8, 0));
        ModeloFacial[] capturas =
        [
            Fotos.Reconhecedor.Capturar(retrato).Modelo!,
            Fotos.Reconhecedor.Capturar(Fotos.Clareada(retrato, 30)).Modelo!,
            Fotos.Reconhecedor.Capturar(retrato.Reduzida(420)).Modelo!,
        ];

        var cadastro = CadastroFacial.Cadastrar(autorizacao, capturas, limiar, Exemplos.Em(8, 5));
        var conferencia = cadastro.Conferir(Fotos.Reconhecedor.Capturar(Fotos.Clareada(retrato, -30)).Modelo!, limiar, Exemplos.Em(9, 0));

        Assert.True(conferencia.Conferiu);
    }
}
