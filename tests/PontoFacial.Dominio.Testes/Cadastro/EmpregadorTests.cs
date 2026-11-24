using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Testes.Cadastro;

public sealed class EmpregadorTests
{
    [Fact]
    public void A_empresa_e_identificada_pelo_cnpj_e_a_pessoa_pelo_cpf()
    {
        var empresa = Empregador.Novo(Documento.De("11.222.333/0001-81"), "Construtora Horizonte Ltda.");
        var domestico = Empregador.Novo(Documento.De("529.982.247-25"), "Ana Lima");

        Assert.IsType<Cnpj>(empresa.Documento);
        Assert.IsType<Cpf>(domestico.Documento);
    }

    [Fact]
    public void O_nome_perde_os_espacos_que_sobram()
    {
        var empregador = Empregador.Novo(Cnpj.De("11222333000181"), "  Construtora   Horizonte  Ltda. ");

        Assert.Equal("Construtora Horizonte Ltda.", empregador.Nome);
    }

    [Fact]
    public void O_nome_cabe_no_campo_de_150_caracteres_do_arquivo_fonte_de_dados()
    {
        Empregador.Novo(Cnpj.De("11222333000181"), new string('A', 150));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => Empregador.Novo(Cnpj.De("11222333000181"), new string('A', 151)));

        Assert.Equal("A razão social ou o nome do empregador tem 151 caracteres; o máximo é 150.", erro.Message);
    }

    [Fact]
    public void Sem_nome_o_empregador_e_recusado()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Empregador.Novo(Cnpj.De("11222333000181"), "   "));

        Assert.Equal("Falta a razão social ou o nome do empregador.", erro.Message);
    }

    [Theory]
    [InlineData("12.345.67890/12", "123456789012")]
    [InlineData("123.456.789/001-23", "12345678900123")]
    [InlineData("123456789012", "123456789012")]
    public void O_cei_e_o_cno_tem_12_digitos_e_o_caepf_14(string texto, string numero)
    {
        var empregador = Empregador.Novo(Cpf.De("52998224725"), "Sítio Boa Vista", texto);

        Assert.Equal(numero, empregador.CeiCaepfOuCno);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sem_cei_caepf_nem_cno_o_campo_fica_vazio(string? texto)
    {
        var empregador = Empregador.Novo(Cnpj.De("11222333000181"), "Construtora Horizonte Ltda.", texto);

        Assert.Null(empregador.CeiCaepfOuCno);
    }

    [Theory]
    [InlineData("1234567890123")]
    [InlineData("12.345.6789A/12")]
    public void Outro_tamanho_ou_letra_no_cei_caepf_ou_cno_e_recusado(string texto)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Empregador.Novo(Cnpj.De("11222333000181"), "Construtora Horizonte Ltda.", texto));

        Assert.Equal($"\"{texto}\" não é CEI, CAEPF nem CNO: o CEI e o CNO têm 12 dígitos, e o CAEPF, 14.", erro.Message);
    }
}
