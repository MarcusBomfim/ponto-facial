using PontoFacial.Dominio.Biometria.Vivacidade;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Testes.Biometria.Vivacidade;

public sealed class DesafioTests
{
    [Fact]
    public void Ha_56_desafios_tres_movimentos_entre_quatro_com_algum_para_o_lado()
    {
        // 4³ = 64 sequências, menos as 2³ = 8 só de cima e baixo.
        Assert.Equal(56, Desafio.Possibilidades);
    }

    [Fact]
    public void O_sorteio_pede_um_numero_entre_todos_os_desafios_e_cada_um_sai_uma_vez()
    {
        var pedidos = new List<int>();
        var desafios = Enumerable.Range(0, Desafio.Possibilidades)
            .Select(indice => Desafio.Sortear(n =>
            {
                pedidos.Add(n);
                return indice;
            }).ToString())
            .ToList();

        Assert.All(pedidos, n => Assert.Equal(56, n));
        Assert.Equal(56, desafios.Distinct().Count());
    }

    [Fact]
    public void Todo_desafio_tem_tres_movimentos_e_algum_para_o_lado()
    {
        for (var indice = 0; indice < Desafio.Possibilidades; indice++)
        {
            var desafio = Desafio.Sortear(_ => indice);

            Assert.Equal(Desafio.MovimentosPorDesafio, desafio.Movimentos.Count);
            Assert.Contains(desafio.Movimentos, movimento => movimento.EhLateral());
        }
    }

    [Fact]
    public void O_sorteio_de_verdade_da_desafios_validos_e_variados()
    {
        var sorteados = Enumerable.Range(0, 200).Select(_ => Desafio.Sortear().ToString()).ToHashSet();

        // Duzentos sorteios em 56 desafios: ficar abaixo de 30 diferentes
        // teria chance desprezível com o gerador justo.
        Assert.True(sorteados.Count > 30);
    }

    [Fact]
    public void Um_sorteio_fora_da_faixa_e_erro_de_quem_sorteia()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Desafio.Sortear(n => n));
        Assert.Throws<ArgumentOutOfRangeException>(() => Desafio.Sortear(_ => -1));
    }

    [Fact]
    public void Um_desafio_escolhido_segue_as_mesmas_regras()
    {
        var desafio = Desafio.De([Movimento.Cima, Movimento.Esquerda, Movimento.Baixo]);

        Assert.Equal([Movimento.Cima, Movimento.Esquerda, Movimento.Baixo], desafio.Movimentos);
        Assert.Equal("Cima, Esquerda, Baixo", desafio.ToString());

        Assert.Equal(
            "O desafio precisa de pelo menos um movimento para o lado.",
            Assert.Throws<ExcecaoDeDominio>(() => Desafio.De([Movimento.Cima, Movimento.Baixo, Movimento.Cima])).Message);
        Assert.Equal(
            "O desafio tem 3 movimentos; vieram 2.",
            Assert.Throws<ExcecaoDeDominio>(() => Desafio.De([Movimento.Esquerda, Movimento.Direita])).Message);
        Assert.Equal(
            "O desafio tem um movimento desconhecido.",
            Assert.Throws<ExcecaoDeDominio>(() => Desafio.De([Movimento.Esquerda, (Movimento)9, Movimento.Cima])).Message);
    }

    [Fact]
    public void Cada_movimento_tem_a_sua_instrucao()
    {
        Assert.Equal("Vire o rosto devagar para a sua esquerda.", Instrucoes.Do(Movimento.Esquerda));
        Assert.Equal("Vire o rosto devagar para a sua direita.", Instrucoes.Do(Movimento.Direita));
        Assert.Equal("Levante o rosto devagar, olhando para cima.", Instrucoes.Do(Movimento.Cima));
        Assert.Equal("Abaixe o rosto devagar, olhando para baixo.", Instrucoes.Do(Movimento.Baixo));
    }
}
