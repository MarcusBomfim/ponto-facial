using Npgsql;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Testes;

namespace PontoFacial.Persistencia.Testes.Cadastro;

public sealed class CadastroNoBancoTests(BancoDeTestes banco)
{
    [Fact]
    public async Task O_empregador_volta_do_banco_como_foi_cadastrado_com_o_cei()
    {
        var empregador = Empregador.Novo(BancoDeTestes.CnpjNovo(), "Construtora Horizonte Ltda.", "51.234.567.897-0");

        var empresa = await banco.Banco.Cadastrar(empregador, TestContext.Current.CancellationToken);
        var lido = await empresa.Empregador(TestContext.Current.CancellationToken);

        Assert.Equal(empregador.Documento, lido.Documento);
        Assert.Equal("Construtora Horizonte Ltda.", lido.Nome);
        Assert.Equal("512345678970", lido.CeiCaepfOuCno);
    }

    [Fact]
    public async Task O_mesmo_cnpj_nao_vira_duas_empresas()
    {
        var (_, empregador) = await banco.NovaEmpresa();

        var erro = await Assert.ThrowsAsync<JaExiste>(() => banco.Banco.Cadastrar(Empregador.Novo(empregador.Documento, "Outra razão social"), TestContext.Current.CancellationToken));

        Assert.Equal($"O empregador de CNPJ {empregador.Documento.Formatado} já está cadastrado.", erro.Message);
    }

    [Fact]
    public async Task O_estabelecimento_volta_do_banco_com_o_fuso_e_e_so_do_empregador_da_empresa()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var manaus = Estabelecimento.Novo(empregador, "Av. Djalma Batista, 1661 - Manaus/AM", "America/Manaus");

        var id = await empresa.Estabelecimentos.Cadastrar(manaus, TestContext.Current.CancellationToken);
        var lido = await empresa.Estabelecimentos.Obter(id, TestContext.Current.CancellationToken);

        Assert.Equal("Av. Djalma Batista, 1661 - Manaus/AM", lido.Local);
        Assert.Equal("America/Manaus", lido.Fuso.Id);
        Assert.Equal(empregador.Documento, lido.Empregador.Documento);

        var deOutro = await Assert.ThrowsAsync<InvalidOperationException>(() => empresa.Estabelecimentos.Cadastrar(Exemplos.Obra, TestContext.Current.CancellationToken));
        Assert.Equal($"O estabelecimento é do empregador 11.222.333/0001-81, e esta empresa é o {empregador.Documento.Formatado}.", deOutro.Message);
    }

    [Fact]
    public async Task O_trabalhador_e_atualizado_pelo_cpf()
    {
        var (empresa, _) = await banco.NovaEmpresa();
        await empresa.Trabalhadores.Gravar(Exemplos.Joao, TestContext.Current.CancellationToken);
        await empresa.Trabalhadores.Gravar(
            Trabalhador.Novo("João da Silva", Exemplos.Joao.Cpf, Exemplos.Joao.Admissao, "Mestre de obras"),
            TestContext.Current.CancellationToken);

        var lido = await empresa.Trabalhadores.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken);

        Assert.Equal("Mestre de obras", lido.Cargo);
        Assert.Equal(new DateOnly(2026, 3, 2), lido.Admissao);

        var ninguem = await Assert.ThrowsAsync<NaoEncontrado>(() => empresa.Trabalhadores.Obter(Cpf.De("390.533.447-05"), TestContext.Current.CancellationToken));
        Assert.Equal("O trabalhador 390.533.447-05 não está cadastrado nesta empresa.", ninguem.Message);
    }

    [Fact]
    public async Task O_cpf_do_trabalhador_nao_muda_nem_pela_aplicacao()
    {
        var (empresa, _) = await banco.NovaEmpresa();
        await empresa.Trabalhadores.Gravar(Exemplos.Joao, TestContext.Current.CancellationToken);

        var erro = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoAplicacao(empresa.Id, "update ponto.trabalhadores set cpf = '39053344705'"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, erro.SqlState);
    }

    [Fact]
    public async Task O_cadastro_mexido_por_fora_nao_volta_do_banco()
    {
        var (empresa, _) = await banco.NovaEmpresa();
        await empresa.Trabalhadores.Gravar(Exemplos.Joao, TestContext.Current.CancellationToken);
        await banco.ComoDono("update ponto.trabalhadores set nome = '   ' where empregador_id = $1", empresa.Id);

        var erro = await Assert.ThrowsAsync<ConteudoAdulterado>(() => empresa.Trabalhadores.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));

        Assert.Equal("O cadastro do trabalhador 529.982.247-25 no banco não passa pelas regras do domínio: Falta o nome do trabalhador.", erro.Message);
    }

    [Fact]
    public async Task O_empregador_pessoa_fisica_entra_pelo_cpf()
    {
        var domestico = Empregador.Novo(Cpf.De("390.533.447-05"), "Ana Pereira");

        var empresa = await banco.Banco.Cadastrar(domestico, TestContext.Current.CancellationToken);

        Assert.Equal(domestico.Documento, (await empresa.Empregador(TestContext.Current.CancellationToken)).Documento);
        var erro = await Assert.ThrowsAsync<JaExiste>(() => banco.Banco.Cadastrar(domestico, TestContext.Current.CancellationToken));
        Assert.Equal("O empregador de CPF 390.533.447-05 já está cadastrado.", erro.Message);
    }
}
