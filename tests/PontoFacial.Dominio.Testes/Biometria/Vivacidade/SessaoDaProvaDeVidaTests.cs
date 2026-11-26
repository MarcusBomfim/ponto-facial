using PontoFacial.Dominio.Biometria.Vivacidade;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Testes.Biometria.Vivacidade;

public sealed class SessaoDaProvaDeVidaTests
{
    private static readonly Desafio EsquerdaCimaDireita = Desafio.De([Movimento.Esquerda, Movimento.Cima, Movimento.Direita]);

    private static readonly Desafio SoParaOsLados = Desafio.De([Movimento.Esquerda, Movimento.Direita, Movimento.Esquerda]);

    [Fact]
    public void Quem_segue_o_desafio_com_o_rosto_passa_com_os_dois_vetores()
    {
        var sessao = Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita);

        var prova = Cenas.Rodar(sessao, Cenas.Seguindo(Cenas.Cabeca, EsquerdaCimaDireita, Cenas.Obedecendo));

        Assert.True(prova.Aprovada);
        Assert.Null(prova.Motivo);
        Assert.Null(prova.Mensagem);
        Assert.Equal([Movimento.Esquerda, Movimento.Cima, Movimento.Direita], prova.Movimentos.Select(feito => feito.Movimento));
        Assert.Same(Cenas.RostoDoComeco, prova.ModeloInicial);
        Assert.Same(Cenas.RostoDoFim, prova.ModeloFinal);
        Assert.InRange(prova.MenorRelevo!.Value, Cenas.RelevoVerticalDaCabeca - 0.05, Cenas.RelevoVerticalDaCabeca + 0.05);
        Assert.All(prova.Movimentos, feito => Assert.True(feito.QuadrosMedidos >= 3));
    }

    [Fact]
    public void A_prova_guarda_o_instante_do_comeco_e_o_do_ultimo_quadro()
    {
        var quadros = Cenas.Seguindo(Cenas.Cabeca, EsquerdaCimaDireita, Cenas.Obedecendo);

        var prova = Cenas.Rodar(Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita), quadros);

        Assert.Equal(Cenas.Quadro(0), prova.Inicio);
        Assert.Equal(quadros[^1].Em, prova.Em);
    }

    [Fact]
    public void A_foto_inclinada_na_diagonal_faz_os_movimentos_e_e_recusada_pelo_relevo()
    {
        /*
         * O atacante mais esperto com uma foto plana: inclina na diagonal, e o
         * cisalhamento faz o nariz andar para o lado pedido. Os três
         * movimentos passam; o relevo, não.
         */
        static (double, double) Diagonal(Movimento movimento) => movimento == Movimento.Esquerda ? (40, -35) : (-40, -35);

        var prova = Cenas.Rodar(Cenas.Iniciar(Cenas.Foto, SoParaOsLados), Cenas.Seguindo(Cenas.Foto, SoParaOsLados, Diagonal));

        Assert.False(prova.Aprovada);
        Assert.Equal(MotivoDaRecusa.SemRelevo, prova.Motivo);
        Assert.Equal(3, prova.Movimentos.Count);
        Assert.Equal(0, prova.MenorRelevo!.Value, 6);
        Assert.Null(prova.ModeloInicial);
        Assert.Null(prova.ModeloFinal);
        Assert.Equal("Não foi possível confirmar o rosto. Tente de novo ou marque pelo código pessoal.", prova.Mensagem);
    }

    [Fact]
    public void A_foto_curvada_faz_os_movimentos_e_e_recusada_pelo_relevo()
    {
        var prova = Cenas.Rodar(
            Cenas.Iniciar(Cenas.CurvadaNaVertical(40), SoParaOsLados),
            Cenas.Seguindo(Cenas.CurvadaNaVertical(40), SoParaOsLados, movimento => movimento == Movimento.Esquerda ? (50, 0) : (-50, 0)));

        Assert.Equal(MotivoDaRecusa.SemRelevo, prova.Motivo);
        Assert.InRange(prova.MenorRelevo!.Value, -0.05, 0.05);
    }

    [Fact]
    public void A_foto_plana_nao_consegue_olhar_para_cima()
    {
        /*
         * Inclinar a foto para trás não leva o nariz para perto dos olhos: a
         * proporção entre os pontos de uma reta não muda no plano. O
         * movimento nunca acontece, e o tempo acaba.
         */
        var prova = Cenas.Rodar(
            Cenas.Iniciar(Cenas.Foto, EsquerdaCimaDireita),
            Cenas.Seguindo(Cenas.Foto, EsquerdaCimaDireita, movimento => movimento == Movimento.Esquerda ? (40, -35) : (0, -40))
                .Concat(Enumerable.Range(71, 100).Select(numero => new QuadroDaCamera(Cenas.Quadro(numero), Cenas.Ver(Cenas.Foto, 0, -40)))));

        Assert.Equal(MotivoDaRecusa.TempoEsgotado, prova.Motivo);
        Assert.Single(prova.Movimentos);
        Assert.Null(prova.MenorRelevo);
    }

    [Fact]
    public void A_foto_parada_nao_faz_movimento_nenhum_e_o_tempo_acaba()
    {
        var sessao = Cenas.Iniciar(Cenas.Foto, EsquerdaCimaDireita);
        var parada = Enumerable.Range(1, 200).Select(numero => new QuadroDaCamera(Cenas.Quadro(numero), Cenas.Ver(Cenas.Foto)));

        var prova = Cenas.Rodar(sessao, parada);

        Assert.Equal(MotivoDaRecusa.TempoEsgotado, prova.Motivo);
        Assert.Empty(prova.Movimentos);
        Assert.True(prova.Em - prova.Inicio > SessaoDaProvaDeVida.TempoPorMovimento);
        Assert.True(prova.Em - prova.Inicio <= SessaoDaProvaDeVida.TempoPorMovimento + TimeSpan.FromSeconds(1 / 15.0));
    }

    [Fact]
    public void Ir_para_o_lado_errado_recusa_na_hora()
    {
        var invertido = Cenas.Seguindo(Cenas.Cabeca, EsquerdaCimaDireita, movimento => movimento == Movimento.Esquerda ? (-30, 0) : Cenas.Obedecendo(movimento));

        var prova = Cenas.Rodar(Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita), invertido);

        Assert.Equal(MotivoDaRecusa.MovimentoTrocado, prova.Motivo);
        Assert.Empty(prova.Movimentos);
        Assert.Equal("O rosto virou para outro lado. Tente de novo, seguindo a instrução da tela.", prova.Mensagem);
    }

    [Fact]
    public void Um_video_gravado_em_outra_ordem_erra_o_desafio()
    {
        /*
         * A gravação de uma pessoa de verdade tem relevo; o que a derruba é a
         * ordem sorteada. Gravada em esquerda, direita, cima, ela erra o
         * segundo movimento de quem pede esquerda, cima, direita.
         */
        var gravado = Desafio.De([Movimento.Esquerda, Movimento.Direita, Movimento.Cima]);

        var prova = Cenas.Rodar(Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita), Cenas.Seguindo(Cenas.Cabeca, gravado, Cenas.Obedecendo));

        Assert.Equal(MotivoDaRecusa.MovimentoTrocado, prova.Motivo);
        Assert.Single(prova.Movimentos);
    }

    [Fact]
    public void Perder_o_rosto_no_meio_recusa()
    {
        var quadros = Cenas.Seguindo(Cenas.Cabeca, EsquerdaCimaDireita, Cenas.Obedecendo);
        quadros[30] = quadros[30] with { Pontos = null };

        var prova = Cenas.Rodar(Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita), quadros);

        Assert.Equal(MotivoDaRecusa.RostoPerdido, prova.Motivo);
        Assert.Single(prova.Movimentos);
    }

    [Fact]
    public void Um_buraco_entre_dois_quadros_recusa()
    {
        /*
         * O acompanhamento só garante que é o mesmo rosto se os quadros vierem
         * seguidos: num buraco de um terço de segundo cabe trocar a foto.
         */
        var quadros = Cenas.Seguindo(Cenas.Cabeca, EsquerdaCimaDireita, Cenas.Obedecendo)
            .Select((quadro, i) => i >= 20 ? quadro with { Em = quadro.Em + TimeSpan.FromMilliseconds(300) } : quadro);

        var prova = Cenas.Rodar(Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita), quadros);

        Assert.Equal(MotivoDaRecusa.QuadrosEspacados, prova.Motivo);
    }

    [Fact]
    public void Um_intervalo_de_um_quarto_de_segundo_ainda_vale()
    {
        var seguidos = Cenas.Seguindo(Cenas.Cabeca, EsquerdaCimaDireita, Cenas.Obedecendo);
        var folga = SessaoDaProvaDeVida.IntervaloMaximoEntreQuadros - (seguidos[20].Em - seguidos[19].Em);
        var quadros = seguidos.Select((quadro, i) => i >= 20 ? quadro with { Em = quadro.Em + folga } : quadro);

        Assert.True(Cenas.Rodar(Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita), quadros).Aprovada);
    }

    [Fact]
    public void Quem_vira_e_nao_volta_para_a_frente_esgota_o_tempo()
    {
        var sessao = Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita);
        var virado = Enumerable.Range(1, 120).Select(numero => new QuadroDaCamera(Cenas.Quadro(numero), Cenas.Ver(Cenas.Cabeca, Math.Min(30, numero * 3))));

        var prova = Cenas.Rodar(sessao, virado);

        Assert.Equal(MotivoDaRecusa.TempoEsgotado, prova.Motivo);
        Assert.Empty(prova.Movimentos);
    }

    [Fact]
    public void No_fim_a_sessao_pede_o_vetor_e_espera_por_ele()
    {
        var quadros = Cenas.Seguindo(Cenas.Cabeca, EsquerdaCimaDireita, Cenas.Obedecendo);
        var final = quadros[^1];
        var sessao = Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita);

        foreach (var quadro in quadros.Take(quadros.Count - 1))
        {
            sessao.Registrar(quadro);
        }

        Assert.True(sessao.PedeModelo);
        Assert.Equal(Instrucoes.OlheParaACamera, sessao.Instrucao);

        sessao.Registrar(final with { Modelo = null });
        Assert.False(sessao.Terminou);

        sessao.Registrar(final with { Em = final.Em + TimeSpan.FromSeconds(1 / 15.0) });
        Assert.True(sessao.Resultado!.Aprovada);
        Assert.False(sessao.PedeModelo);
        Assert.Null(sessao.Instrucao);
    }

    [Fact]
    public void Sem_o_vetor_do_fim_o_tempo_acaba()
    {
        var quadros = Cenas.Seguindo(Cenas.Cabeca, EsquerdaCimaDireita, Cenas.Obedecendo);
        var semVetor = quadros.Take(quadros.Count - 1)
            .Concat(Enumerable.Range(quadros.Count, 80).Select(numero => new QuadroDaCamera(Cenas.Quadro(numero), Cenas.Ver(Cenas.Cabeca))));

        var prova = Cenas.Rodar(Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita), semVetor);

        Assert.Equal(MotivoDaRecusa.TempoEsgotado, prova.Motivo);
        Assert.Equal(3, prova.Movimentos.Count);
    }

    [Fact]
    public void A_instrucao_acompanha_os_passos()
    {
        var sessao = Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita);
        var quadros = Cenas.Seguindo(Cenas.Cabeca, EsquerdaCimaDireita, Cenas.Obedecendo);
        var vistas = new List<string?> { sessao.Instrucao };

        foreach (var quadro in quadros)
        {
            sessao.Registrar(quadro);

            if (vistas[^1] != sessao.Instrucao)
            {
                vistas.Add(sessao.Instrucao);
            }
        }

        Assert.Equal(
            [
                "Vire o rosto devagar para a sua esquerda.",
                Instrucoes.VolteParaAFrente,
                "Levante o rosto devagar, olhando para cima.",
                Instrucoes.VolteParaAFrente,
                "Vire o rosto devagar para a sua direita.",
                Instrucoes.VolteParaAFrente,
                Instrucoes.OlheParaACamera,
                null,
            ],
            vistas);
    }

    [Fact]
    public void O_movimento_pedido_so_aparece_enquanto_e_para_ir()
    {
        var sessao = Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita);

        Assert.Equal(Movimento.Esquerda, sessao.MovimentoPedido);

        foreach (var quadro in Cenas.Seguindo(Cenas.Cabeca, EsquerdaCimaDireita, Cenas.Obedecendo).Take(14))
        {
            sessao.Registrar(quadro);
        }

        Assert.Null(sessao.MovimentoPedido);
    }

    [Fact]
    public void Abaixo_do_criterio_o_rosto_de_verdade_e_recusado_e_o_relevo_fica_na_prova()
    {
        var exigente = CriterioDaProvaDeVida.De(0.9, "valor de teste");

        var prova = Cenas.Rodar(Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita, exigente), Cenas.Seguindo(Cenas.Cabeca, EsquerdaCimaDireita, Cenas.Obedecendo));

        Assert.Equal(MotivoDaRecusa.SemRelevo, prova.Motivo);
        Assert.InRange(prova.MenorRelevo!.Value, 0.35, 0.47);
        Assert.Same(exigente, prova.Criterio);
    }

    [Fact]
    public void A_prova_comeca_com_o_rosto_de_frente_e_com_o_vetor()
    {
        Assert.Equal(
            "A prova de vida começa com o rosto na imagem.",
            Assert.Throws<ExcecaoDeDominio>(() => SessaoDaProvaDeVida.Iniciar(EsquerdaCimaDireita, Cenas.Criterio, new QuadroDaCamera(Cenas.Quadro(0), null, Cenas.RostoDoComeco))).Message);

        Assert.Equal(
            "A prova de vida começa com o vetor do rosto de frente: é ele que diz de quem é a prova.",
            Assert.Throws<ExcecaoDeDominio>(() => SessaoDaProvaDeVida.Iniciar(EsquerdaCimaDireita, Cenas.Criterio, new QuadroDaCamera(Cenas.Quadro(0), Cenas.Ver(Cenas.Cabeca)))).Message);

        Assert.Equal(
            "A prova de vida começa com o rosto de frente para a câmera.",
            Assert.Throws<ExcecaoDeDominio>(() => SessaoDaProvaDeVida.Iniciar(EsquerdaCimaDireita, Cenas.Criterio, new QuadroDaCamera(Cenas.Quadro(0), Cenas.Ver(Cenas.Cabeca, 25), Cenas.RostoDoComeco))).Message);
    }

    [Fact]
    public void Os_quadros_chegam_em_ordem_e_nada_entra_depois_do_fim()
    {
        var sessao = Cenas.Iniciar(Cenas.Cabeca, EsquerdaCimaDireita);

        Assert.Throws<ArgumentException>(() => sessao.Registrar(new QuadroDaCamera(Cenas.Quadro(0), Cenas.Ver(Cenas.Cabeca))));

        sessao.Registrar(new QuadroDaCamera(Cenas.Quadro(1), null));

        Assert.True(sessao.Terminou);
        Assert.Throws<InvalidOperationException>(() => sessao.Registrar(new QuadroDaCamera(Cenas.Quadro(2), Cenas.Ver(Cenas.Cabeca))));
    }
}
