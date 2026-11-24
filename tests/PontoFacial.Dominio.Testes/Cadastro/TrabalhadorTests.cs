using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Testes.Cadastro;

public sealed class TrabalhadorTests
{
    private static readonly DateOnly Admissao = new(2026, 3, 2);

    [Fact]
    public void O_trabalhador_e_identificado_pelo_cpf()
    {
        var trabalhador = Trabalhador.Novo(" João  da Silva ", Cpf.De("529.982.247-25"), Admissao, "Pedreiro");

        Assert.Equal("João da Silva", trabalhador.Nome);
        Assert.Equal("52998224725", trabalhador.Cpf.Numero);
        Assert.Equal(Admissao, trabalhador.Admissao);
        Assert.Equal("Pedreiro", trabalhador.Cargo);
    }

    [Fact]
    public void O_nome_cabe_no_campo_de_52_caracteres_do_arquivo_fonte_de_dados()
    {
        Trabalhador.Novo(new string('J', 52), Cpf.De("52998224725"), Admissao, "Pedreiro");

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Trabalhador.Novo(new string('J', 53), Cpf.De("52998224725"), Admissao, "Pedreiro"));

        Assert.Equal("O nome do trabalhador tem 53 caracteres; o máximo é 52.", erro.Message);
    }

    [Fact]
    public void Sem_cargo_o_trabalhador_e_recusado()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Trabalhador.Novo("João da Silva", Cpf.De("52998224725"), Admissao, " "));

        Assert.Equal("Falta o cargo ou a função.", erro.Message);
    }
}
