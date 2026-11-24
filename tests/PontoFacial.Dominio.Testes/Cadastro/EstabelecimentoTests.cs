using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Testes.Cadastro;

public sealed class EstabelecimentoTests
{
    private static readonly Empregador Construtora = Empregador.Novo(Cnpj.De("11222333000181"), "Construtora Horizonte Ltda.");

    [Theory]
    [InlineData("America/Noronha", -2)]
    [InlineData("America/Sao_Paulo", -3)]
    [InlineData("America/Recife", -3)]
    [InlineData("America/Manaus", -4)]
    [InlineData("America/Cuiaba", -4)]
    [InlineData("America/Rio_Branco", -5)]
    public void A_hora_local_tem_o_deslocamento_do_fuso_do_lugar(string fuso, int horas)
    {
        var obra = Estabelecimento.Novo(Construtora, "Rua das Palmeiras, 100", fuso);

        var local = obra.NaHoraLocal(new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(TimeSpan.FromHours(horas), local.Offset);
        Assert.Equal(12 + horas, local.Hour);
    }

    [Fact]
    public void O_horario_de_verao_de_antes_de_2019_vem_da_base_de_fusos()
    {
        // O Brasil teve horário de verão até fevereiro de 2019: em dezembro de
        // 2018, São Paulo estava em -02:00. Uma marcação daquela época, lida
        // hoje, tem de mostrar a hora que o relógio da parede mostrava.
        var sede = Estabelecimento.Novo(Construtora, "Av. Paulista, 1000", "America/Sao_Paulo");

        var local = sede.NaHoraLocal(new DateTimeOffset(2018, 12, 3, 11, 0, 0, TimeSpan.Zero));

        Assert.Equal(TimeSpan.FromHours(-2), local.Offset);
    }

    [Theory]
    [InlineData("Europe/Lisbon")]
    [InlineData("UTC")]
    [InlineData("E. South America Standard Time")]
    [InlineData("america/sao_paulo")]
    public void Fuso_que_nao_e_do_brasil_pelo_nome_iana_e_recusado(string fuso)
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => Estabelecimento.Novo(Construtora, "Av. Paulista, 1000", fuso));

        Assert.Equal(
            $"\"{fuso}\" não é um fuso do Brasil. Use o nome da base IANA, como America/Sao_Paulo ou America/Manaus.",
            erro.Message);
    }

    [Fact]
    public void Todo_fuso_da_lista_existe_na_base_do_sistema()
    {
        Assert.All(Estabelecimento.FusosDoBrasil, fuso => Estabelecimento.Novo(Construtora, "Av. Paulista, 1000", fuso));
    }

    [Fact]
    public void O_local_cabe_no_campo_de_100_caracteres_e_e_obrigatorio()
    {
        var longo = Assert.Throws<ExcecaoDeDominio>(() => Estabelecimento.Novo(Construtora, new string('R', 101), "America/Sao_Paulo"));
        var vazio = Assert.Throws<ExcecaoDeDominio>(() => Estabelecimento.Novo(Construtora, "", "America/Sao_Paulo"));

        Assert.Equal("O local do estabelecimento tem 101 caracteres; o máximo é 100.", longo.Message);
        Assert.Equal("Falta o local do estabelecimento.", vazio.Message);
    }
}
