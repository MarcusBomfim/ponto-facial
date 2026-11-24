using System.Security.Cryptography;
using System.Text;
using PontoFacial.Dominio.Registro;
using static PontoFacial.Dominio.Testes.Exemplos;
using static PontoFacial.Dominio.Testes.Registro.RegistroDeExemplo;

namespace PontoFacial.Dominio.Testes.Registro;

/*
 * As adulterações que um registro de ponto sofre na vida real — a hora
 * trocada, a marcação apagada, a marcação repetida, o registro reescrito
 * inteiro — feitas nas linhas do arquivo, e o que a conferência diz de cada
 * uma.
 */
public sealed class CadeiaDeMarcacoesTests
{
    private const string NaoConfere = "não confere com o hash dela: ela foi alterada, ou alguma antes dela foi alterada, incluída ou removida.";

    [Fact]
    public void A_cadeia_como_foi_gravada_confere()
    {
        var conferencia = CadeiaDeMarcacoes.Conferir(Linhas(Registro()));

        Assert.True(conferencia.EstaIntegra);
        Assert.Null(conferencia.Linha);
        Assert.Null(conferencia.Motivo);
    }

    [Fact]
    public void Trocar_a_hora_de_uma_marcacao_quebra_a_cadeia_nela()
    {
        // Maria chegou às 8h01; alguém pôs 8h00.
        var linhas = Linhas(Registro());
        linhas[1] = TrocarMinuto(linhas[1], "00");

        var conferencia = CadeiaDeMarcacoes.Conferir(linhas);

        Assert.False(conferencia.EstaIntegra);
        Assert.Equal(2, conferencia.Linha);
        Assert.Equal($"A marcação do NSR 5 {NaoConfere}", conferencia.Motivo);
    }

    [Fact]
    public void Refazer_o_hash_da_marcacao_trocada_quebra_a_cadeia_na_seguinte()
    {
        var linhas = Linhas(Registro());
        var campos = TrocarMinuto(linhas[1], "00")[..73];
        linhas[1] = campos + Sha256(campos + linhas[0][73..]);

        var conferencia = CadeiaDeMarcacoes.Conferir(linhas);

        Assert.Equal(3, conferencia.Linha);
        Assert.Equal($"A marcação do NSR 6 {NaoConfere}", conferencia.Motivo);
    }

    [Fact]
    public void Apagar_uma_marcacao_quebra_a_cadeia_na_seguinte()
    {
        var linhas = Linhas(Registro());
        linhas.RemoveAt(1);

        var conferencia = CadeiaDeMarcacoes.Conferir(linhas);

        Assert.Equal(2, conferencia.Linha);
        Assert.Equal($"A marcação do NSR 6 {NaoConfere}", conferencia.Motivo);
    }

    [Fact]
    public void Repetir_uma_marcacao_e_apontado_pela_ordem_do_nsr()
    {
        var linhas = Linhas(Registro());
        linhas.Insert(1, linhas[0]);

        var conferencia = CadeiaDeMarcacoes.Conferir(linhas);

        Assert.Equal(2, conferencia.Linha);
        Assert.Equal("O NSR 4 vem depois do NSR 4: as marcações têm de estar em ordem de NSR.", conferencia.Motivo);
    }

    [Theory]
    [InlineData("000000001")]
    [InlineData("")]
    public void Linha_que_nao_e_marcacao_do_tipo_7_e_apontada(string linha)
    {
        var conferencia = CadeiaDeMarcacoes.Conferir([linha]);

        Assert.Equal(1, conferencia.Linha);
        Assert.Equal("A linha 1 não é uma marcação do tipo 7: a marcação tem 137 caracteres e o tipo na posição 10.", conferencia.Motivo);
    }

    [Fact]
    public void Quem_reescreve_a_cadeia_inteira_passa_pela_cadeia_mas_nao_pelo_comprovante()
    {
        var registro = Registro();
        var comprovanteDaMaria = registro.Comprovante(5, RegistroNoInpi);
        var linhas = Linhas(registro);

        // Troca a hora da Maria e refaz todos os hashes dali em diante.
        linhas[1] = TrocarMinuto(linhas[1], "00");
        for (var i = 1; i < linhas.Count; i++)
        {
            var campos = linhas[i][..73];
            linhas[i] = campos + Sha256(campos + linhas[i - 1][73..]);
        }

        var semComprovante = CadeiaDeMarcacoes.Conferir(linhas);
        var comComprovante = CadeiaDeMarcacoes.Conferir(linhas, [comprovanteDaMaria]);

        Assert.True(semComprovante.EstaIntegra);
        Assert.False(comComprovante.EstaIntegra);
        Assert.Null(comComprovante.Linha);
        Assert.Equal(
            "O comprovante do NSR 5 não confere com o registro: a marcação que o trabalhador recebeu não está mais lá como foi feita.",
            comComprovante.Motivo);
    }

    [Fact]
    public void O_comprovante_de_uma_marcacao_que_sumiu_nao_confere()
    {
        var registro = Registro();
        var comprovante = registro.Comprovante(6, RegistroNoInpi);
        var linhas = Linhas(registro).Take(2);

        var conferencia = CadeiaDeMarcacoes.Conferir(linhas, [comprovante]);

        Assert.False(conferencia.EstaIntegra);
        Assert.StartsWith("O comprovante do NSR 6 não confere com o registro", conferencia.Motivo, StringComparison.Ordinal);
    }

    [Fact]
    public void Os_comprovantes_de_um_registro_integro_conferem()
    {
        var registro = Registro();

        var conferencia = CadeiaDeMarcacoes.Conferir(
            Linhas(registro),
            [registro.Comprovante(4, RegistroNoInpi), registro.Comprovante(5, RegistroNoInpi), registro.Comprovante(6, RegistroNoInpi)]);

        Assert.True(conferencia.EstaIntegra);
    }

    /// <summary>João às 7h58, Maria às 8h01 e João às 12h: NSR 4, 5 e 6.</summary>
    private static RegistroDePonto Registro()
    {
        var registro = ComJoaoEMaria();
        registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.DispositivoEletronico);
        registro.MarcarOnLine(PeloCodigo(Maria, Em(23, 8, 1)), Coletor.DispositivoEletronico);
        registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 12, 0)), Coletor.AplicativoMovel);

        return registro;
    }

    private static List<string> Linhas(RegistroDePonto registro) => [.. registro.Marcacoes.Select(marcacao => marcacao.LinhaDoAfd)];

    /// <summary>Os minutos da data e hora da marcação ficam nas posições 25 e 26 da linha.</summary>
    private static string TrocarMinuto(string linha, string minuto) => linha[..24] + minuto + linha[26..];

    private static string Sha256(string texto) => Convert.ToHexStringLower(SHA256.HashData(Encoding.Latin1.GetBytes(texto)));
}
