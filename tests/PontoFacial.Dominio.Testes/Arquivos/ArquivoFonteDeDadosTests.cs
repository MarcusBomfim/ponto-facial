using System.Text;
using PontoFacial.Dominio.Arquivos;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes.Registro;

namespace PontoFacial.Dominio.Testes.Arquivos;

/*
 * O AFD no leiaute 004 do gov.br, conferido posição por posição. O registro
 * do teste tem uma operação de cada tipo do REP-P, em 23/11/2026.
 */
public sealed class ArquivoFonteDeDadosTests
{
    private static readonly DateOnly Dia = new(2026, 11, 23);

    [Fact]
    public void O_crc_e_o_crc16_kermit_do_exemplo_do_leiaute()
    {
        Assert.Equal(0x2189, Crc16.Kermit("123456789"u8));
        Assert.Equal("2189", Crc16.DoRegistro("123456789"));
        Assert.Equal(0x0000, Crc16.Kermit([]));
    }

    [Fact]
    public void Cada_registro_tem_o_tamanho_do_leiaute_e_o_crc_dele()
    {
        var afd = Gerar(RegistroDoDia());

        Assert.Equal([302, 331, 118, 118, 36, 137, 73, 118, 64, 100], afd.Linhas.Select(linha => linha.Length));

        foreach (var linha in afd.Linhas.Where(linha => linha[9] is >= '1' and <= '5' && !linha.StartsWith("999999999", StringComparison.Ordinal)))
        {
            Assert.Equal(Crc16.DoRegistro(linha[..^4]), linha[^4..]);
        }
    }

    [Fact]
    public void O_cabecalho_traz_o_empregador_o_programa_e_o_periodo_nas_posicoes_do_leiaute()
    {
        var cabecalho = Gerar(RegistroDoDia()).Linhas[0];

        Assert.Equal("000000000", cabecalho[..9]);
        Assert.Equal("1", cabecalho[9..10]);
        Assert.Equal("1", cabecalho[10..11]);
        Assert.Equal("11222333000181", cabecalho[11..25]);
        Assert.Equal(new string(' ', 14), cabecalho[25..39]);
        Assert.Equal("Construtora Horizonte Ltda.".PadRight(150), cabecalho[39..189]);
        Assert.Equal("00005120260012345", cabecalho[189..206]);
        Assert.Equal("2026-11-23", cabecalho[206..216]);
        Assert.Equal("2026-11-23", cabecalho[216..226]);
        Assert.Equal("2026-11-23T18:00:00-0300", cabecalho[226..250]);
        Assert.Equal("004", cabecalho[250..253]);
        Assert.Equal("1", cabecalho[253..254]);
        Assert.Equal("11444777000161", cabecalho[254..268]);
        Assert.Equal(new string(' ', 30), cabecalho[268..298]);

        // O CRC em hexadecimal, com as letras em maiúsculas.
        Assert.Equal("19E8", cabecalho[298..302]);
    }

    [Fact]
    public void As_operacoes_saem_nos_campos_de_cada_tipo()
    {
        var linhas = Gerar(RegistroDoDia()).Linhas;

        // Tipo 2: o CPF do responsável em 14 posições, o empregador e o local.
        Assert.Equal("000000001", linhas[1][..9]);
        Assert.Equal("2026-11-23T06:00:00-0300", linhas[1][10..34]);
        Assert.Equal("00012345678909", linhas[1][34..48]);
        Assert.Equal("1", linhas[1][48..49]);
        Assert.Equal("11222333000181", linhas[1][49..63]);
        Assert.Equal("Rua das Palmeiras, 100 - Santos/SP".PadRight(100), linhas[1][227..327]);

        // Tipo 5: a inclusão do João, com o nome em 52 posições e o responsável em 11.
        Assert.Equal("0000000025", linhas[2][..10]);
        Assert.Equal("I052998224725", linhas[2][34..47]);
        Assert.Equal("João da Silva".PadRight(52), linhas[2][47..99]);
        Assert.Equal("    ", linhas[2][99..103]);
        Assert.Equal("12345678909", linhas[2][103..114]);

        // Tipo 6: o evento sensível, sem CRC.
        Assert.Equal("0000000046" + "2026-11-23T06:03:00-0300" + "07", linhas[4]);

        // Tipo 4: as duas horas e o responsável em 11.
        Assert.Equal("0000000064" + "2026-11-23T09:00:00-0300" + "2026-11-23T08:57:00-0300" + "12345678909", linhas[6][..69]);

        // Tipo 5 de exclusão.
        Assert.Equal("E011144477735", linhas[7][34..47]);
    }

    [Fact]
    public void A_marcacao_e_a_linha_do_tipo_7_que_ela_ja_escreve_e_a_cadeia_confere_no_arquivo()
    {
        var registro = RegistroDoDia();
        var afd = Gerar(registro);
        var marcacoes = afd.Linhas.Where(linha => linha.Length == MarcacaoDePonto.TamanhoDaLinha && linha[9] == '7').ToList();

        Assert.Equal(registro.Marcacoes.Select(marcacao => marcacao.LinhaDoAfd), marcacoes);
        Assert.True(CadeiaDeMarcacoes.Conferir(marcacoes).EstaIntegra);
    }

    [Fact]
    public void O_trailer_conta_cada_tipo_e_a_ultima_linha_aponta_a_assinatura_no_p7s()
    {
        var linhas = Gerar(RegistroDoDia()).Linhas;

        Assert.Equal("999999999" + "000000001" + "000000000" + "000000001" + "000000003" + "000000001" + "000000001" + "9", linhas[^2]);
        Assert.Equal("ASSINATURA_DIGITAL_EM_ARQUIVO_P7S".PadRight(100), linhas[^1]);
    }

    [Fact]
    public void O_arquivo_e_iso_8859_1_com_cr_e_lf_em_cada_linha_e_o_nome_do_leiaute()
    {
        var afd = Gerar(RegistroDoDia());
        var conteudo = afd.Conteudo();
        var texto = Encoding.Latin1.GetString(conteudo);

        Assert.Equal("AFD512026001234511222333000181REP_P.txt", afd.NomeDoArquivo);
        Assert.Equal(afd.Linhas.Count, texto.Split("\r\n").Length - 1);
        Assert.EndsWith("\r\n", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("\r\n\r\n", texto, StringComparison.Ordinal);
        Assert.Equal(conteudo.Length, afd.Linhas.Sum(linha => linha.Length + 2));

        // O "ã" do João é um byte só, 0xE3, como a ISO 8859-1 o escreve.
        var joao = texto.IndexOf("João", StringComparison.Ordinal);
        Assert.Equal(0xE3, conteudo[joao + 2]);
    }

    [Fact]
    public void So_as_operacoes_gravadas_no_periodo_entram()
    {
        var registro = RegistroDoDia();
        registro.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Joao, Exemplos.Em(24, 7, 58)), Coletor.DispositivoEletronico);

        var doDia24 = ArquivoFonteDeDados.Gerar(Exemplos.Obra, Programas.PontoFacial, registro.Operacoes, Dia.AddDays(1), Dia.AddDays(1), Exemplos.Em(24, 18, 0));

        Assert.Equal(["000000000", "000000008", "999999999"], doDia24.Linhas.Take(3).Select(linha => linha[..9]));
        Assert.Equal("999999999" + "000000000" + "000000000" + "000000000" + "000000000" + "000000000" + "000000001" + "9", doDia24.Linhas[^2]);
    }

    [Fact]
    public void O_empregador_pelo_cpf_e_o_cno_vao_nos_campos_deles()
    {
        var domestico = Empregador.Novo(Cpf.De("390.533.447-05"), "Ana Pereira", "51.234.567.897-0");
        var casa = Estabelecimento.Novo(domestico, "Rua das Flores, 20 - Santos/SP", "America/Sao_Paulo");
        var registro = RegistroDePonto.Abrir(casa, RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 0));

        var cabecalho = ArquivoFonteDeDados.Gerar(casa, Programas.PontoFacial, registro.Operacoes, Dia, Dia, Exemplos.Em(23, 18, 0)).Linhas[0];

        Assert.Equal("2", cabecalho[10..11]);
        Assert.Equal("39053344705   ", cabecalho[11..25]);
        Assert.Equal("00512345678970", cabecalho[25..39]);
    }

    [Fact]
    public void O_periodo_ao_contrario_e_as_operacoes_fora_de_ordem_sao_recusados()
    {
        var registro = RegistroDoDia();

        Assert.Equal(
            "O período do arquivo termina antes de começar.",
            Assert.Throws<ExcecaoDeDominio>(() => ArquivoFonteDeDados.Gerar(Exemplos.Obra, Programas.PontoFacial, registro.Operacoes, Dia, Dia.AddDays(-1), Exemplos.Em(23, 18, 0))).Message);
        Assert.Throws<ArgumentException>(() => ArquivoFonteDeDados.Gerar(Exemplos.Obra, Programas.PontoFacial, registro.Operacoes.Reverse(), Dia, Dia, Exemplos.Em(23, 18, 0)));
    }

    [Fact]
    public void O_programa_precisa_do_registro_no_inpi_que_caiba_no_campo_e_de_um_email()
    {
        Assert.Equal(
            "O registro no INPI \"BR123456789012345678\" tem 18 dígitos; o campo dos arquivos tem até 17.",
            Assert.Throws<ExcecaoDeDominio>(() => ProgramaDoPonto.De("Ponto Facial", "1", "BR123456789012345678", Cnpj.De("11.444.777/0001-61"), "Dev", "a@b.c")).Message);
        Assert.Throws<ExcecaoDeDominio>(() => ProgramaDoPonto.De("Ponto Facial", "1", "BR-", Cnpj.De("11.444.777/0001-61"), "Dev", "a@b.c"));
        Assert.Throws<ExcecaoDeDominio>(() => ProgramaDoPonto.De("Ponto Facial", "1", "BR512026001234-5", Cnpj.De("11.444.777/0001-61"), "Dev", "sem arroba"));
        Assert.Throws<ExcecaoDeDominio>(() => ProgramaDoPonto.De("Ponto Facial", "123456789", "BR512026001234-5", Cnpj.De("11.444.777/0001-61"), "Dev", "a@b.c"));
    }

    /// <summary>
    /// O registro da obra em 23/11: a abertura e as inclusões (NSR 1 a 3), um
    /// evento sensível (4), a entrada do João (5), um ajuste do relógio (6) e
    /// a exclusão da Maria (7).
    /// </summary>
    private static RegistroDePonto RegistroDoDia()
    {
        var registro = RegistroDeExemplo.ComJoaoEMaria();
        registro.RegistrarEvento(EventoSensivel.DisponibilidadeDeServico, Exemplos.Em(23, 6, 3));
        registro.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 7, 58, 31)), Coletor.DispositivoEletronico);
        registro.AjustarRelogio(Exemplos.Em(23, 9, 0), Exemplos.Em(23, 8, 57), RegistroDeExemplo.Rh);
        registro.ExcluirTrabalhador(Exemplos.Maria.Cpf, RegistroDeExemplo.Rh, Exemplos.Em(23, 17, 30));

        return registro;
    }

    private static ArquivoFonteDeDados Gerar(RegistroDePonto registro) =>
        ArquivoFonteDeDados.Gerar(Exemplos.Obra, Programas.PontoFacial, registro.Operacoes, Dia, Dia, Exemplos.Em(23, 18, 0));
}
