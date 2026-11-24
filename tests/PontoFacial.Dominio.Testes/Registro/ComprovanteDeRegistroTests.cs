using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Registro;
using static PontoFacial.Dominio.Testes.Exemplos;
using static PontoFacial.Dominio.Testes.Registro.RegistroDeExemplo;

namespace PontoFacial.Dominio.Testes.Registro;

public sealed class ComprovanteDeRegistroTests
{
    [Fact]
    public void Traz_o_que_o_art_79_pede_na_ordem_dos_incisos()
    {
        var registro = ComJoaoEMaria();
        var marcacao = registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.DispositivoEletronico);

        var comprovante = registro.Comprovante(marcacao.Nsr, RegistroNoInpi);

        Assert.Equal(
            [
                "Comprovante de Registro de Ponto do Trabalhador",
                "NSR: 000000004",
                "Empregador: Construtora Horizonte Ltda.",
                "CNPJ: 11.222.333/0001-81",
                "Local: Rua das Palmeiras, 100 - Santos/SP",
                "Trabalhador: João da Silva",
                "CPF: 529.982.247-25",
                "Data e horário: 23/11/2026 07:58 (UTC-03:00)",
                "Registro no INPI: BR512026001234-5",
                $"Hash (SHA-256): {marcacao.Hash}",
            ],
            comprovante.Linhas());
    }

    [Fact]
    public void O_empregador_pessoa_fisica_vem_pelo_cpf_e_com_o_caepf()
    {
        var sitio = Empregador.Novo(Cpf.De("123.456.789-09"), "Ana Lima", "123.456.789/001-23");
        var registro = RegistroDePonto.Abrir(Estabelecimento.Novo(sitio, "Estrada do Sítio, km 3 - Registro/SP", "America/Sao_Paulo"), Rh, Em(23, 6, 0));
        registro.IncluirTrabalhador(Joao, Rh, Em(23, 6, 1));
        var marcacao = registro.MarcarOnLine(IdentificacaoDoTrabalhador.PorCodigoPessoal(sitio, Joao, Em(23, 7, 0)), Coletor.AplicativoMovel);

        var linhas = registro.Comprovante(marcacao.Nsr, RegistroNoInpi).Linhas();

        Assert.Equal("CPF: 123.456.789-09", linhas[3]);
        Assert.Equal("CEI/CAEPF/CNO: 12345678900123", linhas[4]);
    }

    [Fact]
    public void O_nome_do_trabalhador_e_o_de_quando_ele_marcou()
    {
        var registro = ComJoaoEMaria();
        var antes = registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.DispositivoEletronico);
        registro.AlterarTrabalhador(
            Trabalhador.Novo("João da Silva Santos", Joao.Cpf, Joao.Admissao, Joao.Cargo), Rh, Em(23, 10, 0));
        var depois = registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 12, 0)), Coletor.DispositivoEletronico);

        Assert.Equal("João da Silva", registro.Comprovante(antes.Nsr, RegistroNoInpi).NomeDoTrabalhador);
        Assert.Equal("João da Silva Santos", registro.Comprovante(depois.Nsr, RegistroNoInpi).NomeDoTrabalhador);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(99)]
    public void Nsr_que_nao_e_de_marcacao_nao_tem_comprovante(long nsr)
    {
        var registro = ComJoaoEMaria();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => registro.Comprovante(nsr, RegistroNoInpi));

        Assert.Equal($"O NSR {nsr} não é de uma marcação deste registro de ponto.", erro.Message);
    }

    [Fact]
    public void Sem_o_registro_no_inpi_nao_ha_comprovante()
    {
        var registro = ComJoaoEMaria();
        var marcacao = registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.DispositivoEletronico);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => registro.Comprovante(marcacao.Nsr, " "));

        Assert.Equal("Falta o número de registro do programa no INPI.", erro.Message);
    }
}
