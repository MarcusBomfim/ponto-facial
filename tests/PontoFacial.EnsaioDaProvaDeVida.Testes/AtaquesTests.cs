using PontoFacial.Dominio.Biometria.Vivacidade;
using PontoFacial.Dominio.Biometria.Vivacidade.Ensaio;

namespace PontoFacial.EnsaioDaProvaDeVida.Testes;

/*
 * Cada espécie de ataque, com um retrato da NASA, pela prova de vida
 * inteira: a câmera simulada, o detector, o acompanhamento e a sessão. O
 * atacante olha a tela a cada quadro e insiste no movimento pedido até a
 * instrução mudar ou o tempo acabar.
 */
public sealed class AtaquesTests
{
    private static readonly Desafio SoDeLado = Desafio.De([Movimento.Esquerda, Movimento.Direita, Movimento.Esquerda]);

    private static readonly Desafio ComCimaEBaixo = Desafio.De([Movimento.Cima, Movimento.Esquerda, Movimento.Baixo]);

    private static ProvaDeVida Atacar(EspecieDeAtaque especie, Desafio desafio, string retrato = "marcos-pontes.jpg")
    {
        var sorteio = new Random(1);
        var atacante = Ataques.Preparar(especie, Retratos.Foto(retrato), [], sorteio);

        return Tentativa.Rodar(Retratos.Reconhecedor, desafio, atacante, sorteio) ?? throw new InvalidOperationException("A prova não começou.");
    }

    /// <summary>O atacante com a forma e os ângulos escolhidos.</summary>
    private static ProvaDeVida Seguir(Superficie superficie, Desafio desafio, double giro, double inclinacao, string retrato = "marcos-pontes.jpg")
    {
        var atacante = new Atacante(new Cena(Retratos.Foto(retrato)), superficie, 400, movimento => movimento switch
        {
            Movimento.Esquerda => (giro, 0),
            Movimento.Direita => (-giro, 0),
            Movimento.Cima => (0, -inclinacao),
            _ => (0, inclinacao),
        });
        var sorteio = new Random(1);

        return Tentativa.Rodar(Retratos.Reconhecedor, desafio, atacante, sorteio) ?? throw new InvalidOperationException("A prova não começou.");
    }

    [Fact]
    public void A_foto_parada_nao_faz_o_primeiro_movimento()
    {
        var prova = Atacar(Especies.FotoParada, ComCimaEBaixo);

        Assert.Equal(MotivoDaRecusa.TempoEsgotado, prova.Motivo);
        Assert.Empty(prova.Movimentos);
    }

    [Fact]
    public void A_foto_plana_inclinada_na_diagonal_faz_os_giros_e_nao_tem_relevo()
    {
        var prova = Atacar(Especies.FotoInclinada, SoDeLado);

        Assert.Equal(MotivoDaRecusa.SemRelevo, prova.Motivo);
        Assert.Equal(3, prova.Movimentos.Count);
        Assert.InRange(prova.MenorRelevo!.Value, -0.06, 0.06);
    }

    [Fact]
    public void A_foto_plana_nao_olha_para_cima_e_o_tempo_acaba()
    {
        /*
         * Inclinada para trás, a foto não aproxima o nariz dos olhos: numa
         * reta do plano, a proporção entre os pontos não muda.
         */
        var prova = Atacar(Especies.FotoInclinada, ComCimaEBaixo);

        Assert.Equal(MotivoDaRecusa.TempoEsgotado, prova.Motivo);
        Assert.Empty(prova.Movimentos);
    }

    [Fact]
    public void A_foto_curvada_de_lado_nao_passa_nem_pelo_giro_nem_pelo_relevo()
    {
        /*
         * A curva do papel dá ao nariz menos da metade do relevo de um rosto:
         * para ele andar o bastante, a foto tem de virar tanto que a bochecha
         * do outro lado fica de perfil e o acompanhamento se perde.
         */
        foreach (var (raio, giro) in new[] { (45.0, 45.0), (70.0, 50.0), (100.0, 30.0) })
        {
            var prova = Seguir(new Curvada(raio, 90), SoDeLado, giro, 0);

            Assert.False(prova.Aprovada);
            Assert.All(prova.Movimentos, feito => Assert.InRange(feito.Relevo, -0.08, 0.08));
        }
    }

    [Fact]
    public void A_foto_curvada_de_cima_a_baixo_nao_tem_relevo_de_lado()
    {
        foreach (var raio in new[] { 60.0, 80.0 })
        {
            var prova = Seguir(new Curvada(raio, 0), ComCimaEBaixo, giro: 45, inclinacao: 40);

            Assert.False(prova.Aprovada);
            Assert.All(prova.Movimentos, feito => Assert.True(feito.Relevo < 0.1, $"relevo {feito.Relevo}"));
        }
    }

    [Fact]
    public void A_foto_muito_curvada_nem_passa_por_rosto_de_frente()
    {
        // Enrolada de cima para baixo com 5 cm de raio, a testa e o queixo
        // somem na curva: o detector não aceita o rosto, e a prova não começa.
        var atacante = new Atacante(new Cena(Retratos.Foto("sunita-williams-2004.jpg")), new Curvada(50, 0), 400, _ => (0, 0));

        Assert.Null(Tentativa.Rodar(Retratos.Reconhecedor, ComCimaEBaixo, atacante, new Random(1)));
    }

    [Fact]
    public void A_foto_dobrada_nao_passa()
    {
        foreach (var (dobra, eixo, giro) in new[] { (40.0, 90.0, 40.0), (30.0, 0.0, 45.0) })
        {
            var prova = Seguir(new Dobrada(dobra, eixo), ComCimaEBaixo, giro, 40);

            Assert.False(prova.Aprovada);
            Assert.All(prova.Movimentos, feito => Assert.True(feito.Relevo < 0.1, $"relevo {feito.Relevo}"));
        }
    }

    [Fact]
    public void A_troca_de_fotos_perde_o_rosto()
    {
        /*
         * Duas fotos da mesma astronauta, oito anos de diferença, postas no
         * mesmo lugar: o acompanhamento não atravessa a troca.
         */
        var frente = Retratos.Foto("sunita-williams-2004.jpg");
        var outra = Retratos.Foto("sunita-williams-2012.jpg");
        var sorteio = new Random(1);
        var atacante = Ataques.Preparar(Especies.TrocaDeFotos, frente, [(outra, 0.2, 0), (outra, -0.2, 0)], sorteio);

        var prova = Tentativa.Rodar(Retratos.Reconhecedor, SoDeLado, atacante, sorteio)!;

        Assert.Equal(MotivoDaRecusa.RostoPerdido, prova.Motivo);
        Assert.Null(prova.MenorRelevo);
    }

    [Fact]
    public void Um_relevo_de_verdade_aparece_na_medida()
    {
        /*
         * O contrário dos ataques: a foto sobre um calombo no lugar do nariz,
         * que sobressai nas duas direções. Não é um rosto — é a prova de que,
         * quando há relevo, a câmera simulada, o acompanhamento e a medida o
         * enxergam.
         */
        var foto = Retratos.Foto("marcos-pontes.jpg");
        var relevo = new ComRelevo((foto.Rosto.Nariz.Y - foto.CentroY) * (Cena.OlhosEmMilimetros / foto.DistanciaEntreOlhos));

        var prova = Seguir(relevo, ComCimaEBaixo, giro: 30, inclinacao: 25);

        Assert.Equal(3, prova.Movimentos.Count);
        Assert.True(prova.MenorRelevo > 0.2, $"relevo {prova.MenorRelevo}");
        Assert.All(prova.Movimentos, feito => Assert.True(feito.QuadrosMedidos >= 3));
    }
}
