using System.Security.Cryptography;
using System.Text;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Registro;
using static PontoFacial.Dominio.Testes.Exemplos;
using static PontoFacial.Dominio.Testes.Registro.RegistroDeExemplo;

namespace PontoFacial.Dominio.Testes.Registro;

/*
 * O leiaute do registro do tipo 7 do Arquivo Fonte de Dados, campo a campo,
 * e o hash calculado aqui por conta própria — SHA-256 sobre o texto em ISO
 * 8859-1 —, com o qual o domínio tem de concordar.
 */
public sealed class MarcacaoDePontoTests
{
    private const string Entrada = "000000004" + "7" + "2026-11-23T07:58:00-0300" + "052998224725" + "2026-11-23T07:58:00-0300" + "01" + "0";

    [Fact]
    public void Os_campos_seguem_o_leiaute_do_registro_do_tipo_7()
    {
        var marcacao = ComJoaoEMaria().MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.AplicativoMovel);

        Assert.Equal(Entrada, marcacao.Campos);
        Assert.Equal(73, marcacao.Campos.Length);
    }

    [Fact]
    public void A_off_line_leva_1_e_as_duas_horas()
    {
        var marcacao = ComJoaoEMaria().MarcarOffLine(PeloCodigo(Maria, Em(23, 7, 50)), Coletor.DispositivoEletronico, Em(23, 9, 30));

        Assert.Equal(
            "000000004" + "7" + "2026-11-23T07:50:00-0300" + "011144477735" + "2026-11-23T09:30:00-0300" + "04" + "1",
            marcacao.Campos);
    }

    [Fact]
    public void Os_segundos_sao_sempre_00()
    {
        var marcacao = ComJoaoEMaria().MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58, 59)), Coletor.AplicativoMovel);

        Assert.Equal(Entrada, marcacao.Campos);
    }

    [Theory]
    [InlineData("America/Rio_Branco", "-0500")]
    [InlineData("America/Manaus", "-0400")]
    [InlineData("America/Noronha", "-0200")]
    public void O_fuso_vai_com_sinal_e_quatro_digitos(string fuso, string noArquivo)
    {
        var lugar = Estabelecimento.Novo(Construtora, "Rua Principal, 1", fuso);
        var registro = RegistroDePonto.Abrir(lugar, Rh, Em(23, 6, 0));
        registro.IncluirTrabalhador(Joao, Rh, Em(23, 6, 1));

        var marcacao = registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 12, 0)), Coletor.AplicativoMovel);

        Assert.Equal(noArquivo, marcacao.Campos[29..34]);
        Assert.Equal(noArquivo, marcacao.Campos[65..70]);
    }

    [Fact]
    public void O_hash_da_primeira_marcacao_e_o_sha256_dos_campos()
    {
        var marcacao = ComJoaoEMaria().MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.AplicativoMovel);

        Assert.Null(marcacao.HashAnterior);
        Assert.Equal(Sha256(Entrada), marcacao.Hash);

        // O mesmo valor, calculado fora do .NET (sha256sum): se a regra do
        // cálculo mudar, este número avisa.
        Assert.Equal("21ee8110355e598f83719681745466fbbd09573b66748fb5bf021d2baac76e05", marcacao.Hash);
    }

    [Fact]
    public void O_hash_das_seguintes_inclui_o_da_anterior()
    {
        var registro = ComJoaoEMaria();
        var primeira = registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.AplicativoMovel);
        registro.RegistrarEvento(EventoSensivel.IndisponibilidadeDeServico, Em(23, 10, 0));

        var segunda = registro.MarcarOnLine(PeloCodigo(Maria, Em(23, 12, 0)), Coletor.AplicativoMovel);

        // O evento do meio leva o NSR 5 e não entra na cadeia: só a marcação tem hash.
        Assert.Equal(6, segunda.Nsr);
        Assert.Equal(primeira.Hash, segunda.HashAnterior);
        Assert.Equal(Sha256(segunda.Campos + primeira.Hash), segunda.Hash);
    }

    [Fact]
    public void A_linha_do_arquivo_e_os_campos_seguidos_do_hash()
    {
        var marcacao = ComJoaoEMaria().MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.AplicativoMovel);

        Assert.Equal(137, marcacao.LinhaDoAfd.Length);
        Assert.Equal(Entrada + marcacao.Hash, marcacao.LinhaDoAfd);
    }

    private static string Sha256(string texto) => Convert.ToHexStringLower(SHA256.HashData(Encoding.Latin1.GetBytes(texto)));
}
