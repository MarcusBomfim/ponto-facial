using PontoFacial.Dominio.Biometria.Vivacidade;
using PontoFacial.Dominio.Biometria.Vivacidade.Ensaio;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Testes.Biometria.Vivacidade;

/*
 * Ensaios montados à mão, com relevos em grade, para as taxas serem
 * contadas de cabeça.
 */
public sealed class EnsaioDaProvaDeVidaTests
{
    private static readonly EspecieDeAtaque Plana = EspecieDeAtaque.De("foto plana", "a foto inclinada diante da câmera");

    private static readonly EspecieDeAtaque Curvada = EspecieDeAtaque.De("foto curvada", "a foto enrolada num cilindro");

    [Fact]
    public void O_apcer_e_contado_por_especie_e_o_recusado_antes_do_relevo_nunca_passa()
    {
        var ensaio = new EnsaioDaProvaDeVida();

        // 10 fotos planas: 6 recusadas por tempo, 4 com relevo de 0,01 a 0,04.
        for (var i = 0; i < 6; i++)
        {
            ensaio.RegistrarAtaque(Plana, null);
        }

        foreach (var relevo in new[] { 0.01, 0.02, 0.03, 0.04 })
        {
            ensaio.RegistrarAtaque(Plana, relevo);
        }

        // 4 curvadas: relevos 0,05 a 0,20.
        foreach (var relevo in new[] { 0.05, 0.10, 0.15, 0.20 })
        {
            ensaio.RegistrarAtaque(Curvada, relevo);
        }

        var avaliacao = ensaio.Avaliar(CriterioDaProvaDeVida.De(0.03, "teste"));

        Assert.Equal([Plana, Curvada], ensaio.Especies);
        Assert.Equal(10, ensaio.AtaquesDa(Plana));
        Assert.Equal(2, avaliacao.PorEspecie[0].Apcer.Erros);
        Assert.Equal(10, avaliacao.PorEspecie[0].Apcer.Total);
        Assert.Equal(4, avaliacao.PorEspecie[1].Apcer.Erros);
        Assert.Same(Curvada, avaliacao.PiorEspecie.Especie);
        Assert.Null(avaliacao.Bpcer);
    }

    [Fact]
    public void O_bpcer_conta_como_erro_a_genuina_recusada_antes_do_relevo()
    {
        var ensaio = new EnsaioDaProvaDeVida();
        ensaio.RegistrarAtaque(Plana, 0.0);
        ensaio.RegistrarGenuina(0.4);
        ensaio.RegistrarGenuina(0.25);
        ensaio.RegistrarGenuina(null);
        ensaio.RegistrarGenuina(0.1);

        var bpcer = ensaio.Avaliar(CriterioDaProvaDeVida.De(0.2, "teste")).Bpcer!;

        Assert.Equal(4, ensaio.Genuinas);
        Assert.Equal(2, bpcer.Erros);
        Assert.Equal(4, bpcer.Total);
    }

    [Fact]
    public void A_calibracao_escolhe_o_menor_relevo_que_respeita_a_pior_especie()
    {
        /*
         * 300 de cada espécie, o mínimo para 1% pela regra de três. Até 3
         * podem passar: o critério fica logo acima do quarto maior relevo da
         * espécie que mais se parece com rosto.
         */
        var ensaio = new EnsaioDaProvaDeVida();

        for (var i = 0; i < 300; i++)
        {
            ensaio.RegistrarAtaque(Plana, i < 100 ? null : (i - 100) * 0.0001);   // até 0,0199
            ensaio.RegistrarAtaque(Curvada, i * 0.0005);                           // até 0,1495
        }

        var criterio = ensaio.Calibrar(0.01, "ensaio de teste");
        var avaliacao = ensaio.Avaliar(criterio);

        // As curvadas, do maior para o menor: 0,1495, 0,1490, 0,1485, 0,1480...
        Assert.Equal(0.149, criterio.RelevoMinimo, 9);
        Assert.Equal("ensaio de teste", criterio.Origem);
        Assert.Equal(2, avaliacao.PorEspecie[1].Apcer.Erros);
        Assert.Equal(0, avaliacao.PorEspecie[0].Apcer.Erros);
        // Um passo abaixo, entram 0,1485 e 0,1480 também: passaria do tolerado.
        Assert.Equal(4, ensaio.Avaliar(CriterioDaProvaDeVida.De(0.1479, "teste")).PiorEspecie.Apcer.Erros);
    }

    [Fact]
    public void O_relevo_igual_a_um_passo_fica_abaixo_do_criterio()
    {
        // Com 0,124 em ponto flutuante, a divisão por 0,001 cai em 123,999...;
        // o critério tem de ficar acima, ou o 0,124 passaria.
        var ensaio = new EnsaioDaProvaDeVida();

        for (var i = 0; i < 300; i++)
        {
            ensaio.RegistrarAtaque(Plana, i < 296 ? 0.0 : 0.124);
        }

        var criterio = ensaio.Calibrar(0.01, "teste");

        Assert.Equal(0.125, criterio.RelevoMinimo, 9);
        Assert.Equal(0, ensaio.Avaliar(criterio).PiorEspecie.Apcer.Erros);
    }

    [Fact]
    public void Sem_ataque_que_chegue_ao_fim_o_criterio_e_o_menor_possivel()
    {
        var ensaio = new EnsaioDaProvaDeVida();

        for (var i = 0; i < 300; i++)
        {
            ensaio.RegistrarAtaque(Plana, null);
        }

        Assert.Equal(EnsaioDaProvaDeVida.Passo, ensaio.Calibrar(0.01, "teste").RelevoMinimo, 9);
    }

    [Fact]
    public void A_regra_de_tres_vale_para_cada_especie()
    {
        var ensaio = new EnsaioDaProvaDeVida();

        for (var i = 0; i < 300; i++)
        {
            ensaio.RegistrarAtaque(Plana, 0.0);
        }

        for (var i = 0; i < 299; i++)
        {
            ensaio.RegistrarAtaque(Curvada, 0.0);
        }

        Assert.Equal(
            "Para medir APCER de 1% na espécie \"foto curvada\" são precisas pelo menos 300 apresentações; vieram 299.",
            Assert.Throws<ExcecaoDeDominio>(() => ensaio.Calibrar(0.01, "teste")).Message);
    }

    [Fact]
    public void O_erro_tolerado_e_uma_fracao_e_o_ensaio_precisa_de_ataque()
    {
        var vazio = new EnsaioDaProvaDeVida();

        Assert.Equal("O ensaio não tem apresentação de ataque nenhuma.", Assert.Throws<ExcecaoDeDominio>(() => vazio.Calibrar(0.01, "teste")).Message);
        Assert.Equal("O ensaio não tem apresentação de ataque nenhuma.", Assert.Throws<ExcecaoDeDominio>(() => vazio.Avaliar(Cenas.Criterio)).Message);
        Assert.Equal(
            "O erro de ataque tolerado é uma fração acima de 0 e abaixo de 0,5.",
            Assert.Throws<ExcecaoDeDominio>(() => vazio.Calibrar(0.5, "teste")).Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => vazio.RegistrarGenuina(double.NaN));
    }

    [Fact]
    public void Ataques_com_mais_relevo_que_qualquer_criterio_nao_se_calibram()
    {
        var ensaio = new EnsaioDaProvaDeVida();

        for (var i = 0; i < 300; i++)
        {
            ensaio.RegistrarAtaque(Plana, 3.0);
        }

        Assert.StartsWith("Os ataques do ensaio mostram mais relevo", Assert.Throws<ExcecaoDeDominio>(() => ensaio.Calibrar(0.01, "teste")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void O_criterio_fica_entre_zero_e_dois_e_aceita_o_igual()
    {
        var criterio = CriterioDaProvaDeVida.De(0.2, "  ensaio   de teste ");

        Assert.True(criterio.Aceita(0.2));
        Assert.False(criterio.Aceita(0.1999));
        Assert.Equal("ensaio de teste", criterio.Origem);
        Assert.Throws<ExcecaoDeDominio>(() => CriterioDaProvaDeVida.De(0, "teste"));
        Assert.Throws<ExcecaoDeDominio>(() => CriterioDaProvaDeVida.De(2.01, "teste"));
        Assert.Throws<ExcecaoDeDominio>(() => CriterioDaProvaDeVida.De(double.NaN, "teste"));
        Assert.Equal("Falta a origem do critério.", Assert.Throws<ExcecaoDeDominio>(() => CriterioDaProvaDeVida.De(0.2, " ")).Message);
    }

    [Fact]
    public void Cada_recusa_tem_a_sua_frase()
    {
        foreach (var motivo in Enum.GetValues<MotivoDaRecusa>())
        {
            Assert.False(string.IsNullOrWhiteSpace(MensagensDaRecusa.Da(motivo)));
        }

        Assert.Equal(5, Enum.GetValues<MotivoDaRecusa>().Select(MensagensDaRecusa.Da).Distinct().Count());
    }
}
