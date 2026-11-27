using System.Text;
using PontoFacial.Dominio.Arquivos;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes.Jornada;
using PontoFacial.Dominio.Testes.Registro;

namespace PontoFacial.Dominio.Testes.Arquivos;

/*
 * O AEJ no leiaute 002 do gov.br. A semana do João é a do espelho de ponto:
 * o intervalo pré-assinalado na segunda, as duas marcações incluídas na
 * quinta, o feriado na sexta, a marcação repetida desconsiderada no sábado,
 * o descanso no domingo.
 */
public sealed class ArquivoEletronicoDeJornadaTests
{
    private static readonly DateOnly Segunda = new(2026, 11, 16);

    private static readonly DateOnly Domingo = new(2026, 11, 22);

    [Fact]
    public void A_semana_do_joao_no_aej_linha_por_linha()
    {
        var (_, tratamento, _) = SemanaDoJoao.Montar();

        var aej = Gerar([Vinculo(Exemplos.Joao, tratamento)], Segunda, Domingo);

        Assert.Equal(
            [
                "01|1|11222333000181|||Construtora Horizonte Ltda.|2026-11-16|2026-11-22|2026-11-23T08:00:00-0300|002",
                "02|1|3|00005120260012345",
                "03|1|52998224725|João da Silva",
                "04|1|480|0800|1200|1300|1700",
                "04|2|240|0800|1200",
                "05|1|2026-11-16T07:58:00-0300|1|E|001|O|1|",
                "05|1|2026-11-16T12:00:00-0300||S|001|P||",
                "05|1|2026-11-16T13:00:00-0300||E|002|P||",
                "05|1|2026-11-16T17:02:00-0300|1|S|002|O||",
                "05|1|2026-11-17T07:55:00-0300|1|E|001|O|1|",
                "05|1|2026-11-17T12:00:00-0300|1|S|001|O||",
                "05|1|2026-11-17T13:10:00-0300|1|E|002|O||",
                "05|1|2026-11-17T17:00:00-0300|1|S|002|O||",
                "05|1|2026-11-18T08:00:00-0300|1|E|001|O|1|",
                "05|1|2026-11-18T12:00:00-0300|1|S|001|O||",
                "05|1|2026-11-18T13:00:00-0300|1|E|002|O||",
                "05|1|2026-11-18T19:30:00-0300|1|S|002|O||",
                "05|1|2026-11-19T08:00:00-0300|1|E|001|O|1|",
                "05|1|2026-11-19T12:00:00-0300|1|S|001|O||",
                "05|1|2026-11-19T13:00:00-0300||E|002|I||Esqueceu de marcar a volta do almoço; confirmado pelo encarregado.",
                "05|1|2026-11-19T17:00:00-0300||S|002|I||Esqueceu de marcar a saída; confirmado pelo encarregado.",
                "05|1|2026-11-21T08:00:00-0300|1|E|001|O|2|",
                "05|1|2026-11-21T08:01:00-0300|1|D|001|O||Marcação repetida por engano, um minuto depois da entrada.",
                "05|1|2026-11-21T12:00:00-0300|1|S|001|O||",
                "07|1|1|2026-11-22||",
                "08|Ponto Facial|0.6.0|1|11444777000161|Desenvolvedora de Exemplo Ltda.|contato@exemplo.com.br",
                "99|1|1|1|2|19|0|1|1",
                "ASSINATURA_DIGITAL_EM_ARQUIVO_P7S".PadRight(100),
            ],
            aej.Linhas);
        Assert.Equal("AEJ_11222333000181_20261116_20261122.txt", aej.NomeDoArquivo);
    }

    [Fact]
    public void A_falta_vira_ausencia_e_o_dia_abonado_nao()
    {
        var registro = RegistroDePonto.Abrir(Exemplos.Obra, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 1));
        var tratamento = TratamentoDoPonto.Do(registro, Exemplos.Joao.Cpf);
        tratamento.Abonar(new DateOnly(2026, 11, 17), "Atestado médico.", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0));

        var ausencias = Gerar([Vinculo(Exemplos.Joao, tratamento)], Segunda, new DateOnly(2026, 11, 18)).Linhas.Where(linha => linha.StartsWith("07|", StringComparison.Ordinal));

        // Segunda e quarta, faltas; a terça, abonada, fica de fora.
        Assert.Equal(["07|1|2|2026-11-16||", "07|1|2|2026-11-18||"], ausencias);
    }

    [Fact]
    public void O_horario_noturno_vai_com_a_duracao_pela_hora_reduzida()
    {
        var registro = RegistroDePonto.Abrir(Exemplos.Obra, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 1));
        var vinculo = new JornadaDoVinculo(Exemplos.Joao, TratamentoDoPonto.Do(registro, Exemplos.Joao.Cpf), Horarios.Noturno, CalendarioDeFeriados.Nacionais);

        var horarios = Gerar([vinculo], Segunda, Segunda).Linhas.Where(linha => linha.StartsWith("04|", StringComparison.Ordinal));

        // Seis horas de relógio, todas entre 22h e 5h: 360 minutos × 8/7, ao minuto.
        Assert.Equal(["04|1|411|2200|0200|0300|0500"], horarios);
    }

    [Fact]
    public void O_vinculo_que_comeca_ou_acaba_no_meio_do_periodo_so_tem_falta_dentro_dele()
    {
        var registro = RegistroDePonto.Abrir(Exemplos.Obra, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 1));
        var tratamento = TratamentoDoPonto.Do(registro, Exemplos.Joao.Cpf);
        var vinculo = Vinculo(Exemplos.Joao, tratamento) with { Desde = new DateOnly(2026, 11, 17), Ate = new DateOnly(2026, 11, 18) };

        var ausencias = Gerar([vinculo], Segunda, new DateOnly(2026, 11, 20)).Linhas.Where(linha => linha.StartsWith("07|", StringComparison.Ordinal));

        Assert.Equal(["07|1|2|2026-11-17||", "07|1|2|2026-11-18||"], ausencias);
    }

    [Fact]
    public void Dois_vinculos_dividem_os_horarios_e_cada_marcacao_leva_o_id_do_seu()
    {
        var registro = RegistroDeExemplo.ComJoaoEMaria();
        registro.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 8, 0)), Coletor.DispositivoEletronico);
        registro.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Maria, Exemplos.Em(23, 8, 1)), Coletor.AplicativoMovel);
        var dia = new DateOnly(2026, 11, 23);

        var linhas = Gerar(
            [Vinculo(Exemplos.Joao, TratamentoDoPonto.Do(registro, Exemplos.Joao.Cpf)), Vinculo(Exemplos.Maria, TratamentoDoPonto.Do(registro, Exemplos.Maria.Cpf))],
            dia,
            dia).Linhas;

        Assert.Equal(["03|1|52998224725|João da Silva", "03|2|11144477735|Maria Souza"], linhas.Where(linha => linha.StartsWith("03|", StringComparison.Ordinal)));
        Assert.Single(linhas, linha => linha.StartsWith("04|", StringComparison.Ordinal));
        Assert.Equal(
            ["05|1|2026-11-23T08:00:00-0300|1|E|001|O|1|", "05|2|2026-11-23T08:01:00-0300|1|E|001|O|1|"],
            linhas.Where(linha => linha.StartsWith("05|", StringComparison.Ordinal)));
    }

    [Fact]
    public void O_motivo_com_barra_vertical_ou_quebra_de_linha_nao_quebra_o_arquivo()
    {
        var registro = RegistroDePonto.Abrir(Exemplos.Obra, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 1));
        var tratamento = TratamentoDoPonto.Do(registro, Exemplos.Joao.Cpf);
        tratamento.Incluir(Horarios.Em(16, "08:00"), "Coletor parado | sem energia", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0));

        var incluida = Assert.Single(Gerar([Vinculo(Exemplos.Joao, tratamento)], Segunda, Segunda).Linhas, linha => linha.StartsWith("05|", StringComparison.Ordinal));

        Assert.Equal("05|1|2026-11-16T08:00:00-0300||E|001|I|1|Coletor parado   sem energia", incluida);
        Assert.Equal(9, incluida.Split('|').Length);
    }

    [Fact]
    public void O_cabecalho_separa_o_caepf_do_cno_e_o_arquivo_e_iso_8859_1_com_cr_e_lf()
    {
        var produtor = Empregador.Novo(Cpf.De("390.533.447-05"), "Ana Pereira", "12345678901234");
        var sitio = Estabelecimento.Novo(produtor, "Estrada do Sítio, km 3 - Registro/SP", "America/Sao_Paulo");
        var registro = RegistroDePonto.Abrir(sitio, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 1));

        var aej = ArquivoEletronicoDeJornada.Gerar(
            produtor,
            Programas.PontoFacial,
            [Vinculo(Exemplos.Joao, TratamentoDoPonto.Do(registro, Exemplos.Joao.Cpf))],
            Segunda,
            Segunda,
            Exemplos.Em(23, 8, 0));
        var texto = Encoding.Latin1.GetString(aej.Conteudo());

        Assert.StartsWith("01|2|39053344705|12345678901234||Ana Pereira|", aej.Linhas[0], StringComparison.Ordinal);
        Assert.Equal(aej.Linhas.Count, texto.Split("\r\n").Length - 1);
        Assert.Equal(0xE3, aej.Conteudo()[texto.IndexOf("João", StringComparison.Ordinal) + 2]);
    }

    [Fact]
    public void O_ponto_de_outro_empregador_nao_entra_e_o_periodo_ao_contrario_e_recusado()
    {
        var outro = Empregador.Novo(Cnpj.De("11.444.777/0001-61"), "Outra Ltda.");
        var (_, tratamento, _) = SemanaDoJoao.Montar();

        Assert.Equal(
            "O ponto de João da Silva é de outro empregador.",
            Assert.Throws<ExcecaoDeDominio>(() => ArquivoEletronicoDeJornada.Gerar(outro, Programas.PontoFacial, [Vinculo(Exemplos.Joao, tratamento)], Segunda, Domingo, Exemplos.Em(23, 8, 0))).Message);
        Assert.Throws<ExcecaoDeDominio>(() => Gerar([Vinculo(Exemplos.Joao, tratamento)], Domingo, Segunda));
    }

    private static JornadaDoVinculo Vinculo(Trabalhador trabalhador, TratamentoDoPonto tratamento) =>
        new(trabalhador, tratamento, Horarios.DaObraComIntervaloPreAssinalado, CalendarioDeFeriados.Nacionais);

    private static ArquivoEletronicoDeJornada Gerar(IReadOnlyList<JornadaDoVinculo> vinculos, DateOnly de, DateOnly ate) =>
        ArquivoEletronicoDeJornada.Gerar(Exemplos.Construtora, Programas.PontoFacial, vinculos, de, ate, Exemplos.Em(23, 8, 0));
}
