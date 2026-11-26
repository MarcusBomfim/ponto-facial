using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes.Registro;

namespace PontoFacial.Dominio.Testes.Jornada;

public sealed class EspelhoDePontoTests
{
    [Fact]
    public void A_semana_do_joao_no_espelho_do_art_84()
    {
        var (_, tratamento, _) = SemanaDoJoao.Montar();

        var linhas = SemanaDoJoao.Espelho(tratamento).Linhas();

        Assert.Equal(
            [
                "Espelho de Ponto Eletrônico",
                "Empregador: Construtora Horizonte Ltda.",
                "CNPJ: 11.222.333/0001-81",
                "Local: Rua das Palmeiras, 100 - Santos/SP",
                "Trabalhador: João da Silva",
                "CPF: 529.982.247-25",
                "Admissão: 02/03/2026",
                "Cargo: Pedreiro",
                "Emitido em: 23/11/2026 08:00 (UTC-03:00)",
                "Período: 16/11/2026 a 22/11/2026",
                "Horário contratual: seg a sex 08:00–12:00 e 13:00–17:00; sáb 08:00–12:00; 44:00 por semana; descanso aos domingos; intervalo pré-assinalado",
                "",
                "Dia        Marcações                                 Jornada  Noturna  Extras  Em descanso  Faltas  Ocorrências",
                "seg 16/11  07:58 12:00P 13:00P 17:02                   08:00    00:00   00:00        00:00   00:00  Tolerância do art. 58",
                "ter 17/11  07:55 12:00 13:10 17:00                     07:50    00:00   00:00        00:00   00:10  Tolerância do art. 58",
                "qua 18/11  08:00 12:00 13:00 19:30                     10:30    00:00   02:30        00:00   00:00  Mais de 2 horas extras (art. 59)",
                "qui 19/11  08:00 12:00 13:00I 17:00I                   08:00    00:00   00:00        00:00   00:00",
                "sex 20/11                                              00:00    00:00   00:00        00:00   00:00  Feriado: Dia Nacional de Zumbi e da Consciência Negra",
                "sáb 21/11  08:00 08:01D 12:00                          04:00    00:00   00:00        00:00   00:00",
                "dom 22/11                                              00:00    00:00   00:00        00:00   00:00  Descanso semanal",
                "",
                "Horas do horário: 36:00",
                "Jornada realizada, com a hora noturna reduzida: 38:20",
                "Noturnas: 00:00",
                "Extras: 02:30",
                "Extras em descanso ou feriado: 00:00",
                "Faltantes: 00:10",
                "Abonadas: 00:00",
                "Intervalo suprimido: 00:00",
                "Descanso entre jornadas suprimido: 00:00",
                "Dias com falta: 0; abonados: 0; pendentes de tratamento: 0; acima do limite de extras: 1",
                "Marcações: P pré-assinalada; I incluída no tratamento; D desconsiderada no tratamento, não conta.",
                "Tratamentos:",
                "  13:00 de 19/11/2026 incluída por 123.456.789-09 em 23/11/2026 07:30: Esqueceu de marcar a volta do almoço; confirmado pelo encarregado.",
                "  17:00 de 19/11/2026 incluída por 123.456.789-09 em 23/11/2026 07:31: Esqueceu de marcar a saída; confirmado pelo encarregado.",
                "  08:01 de 21/11/2026 (NSR 16) desconsiderada por 123.456.789-09 em 23/11/2026 07:32: Marcação repetida por engano, um minuto depois da entrada.",
            ],
            linhas);
    }

    [Fact]
    public void Sem_o_tratamento_a_quinta_fica_com_meio_dia_e_o_sabado_pendente()
    {
        var (registro, _, _) = SemanaDoJoao.Montar();
        var semTratamento = TratamentoDoPonto.Do(registro, Exemplos.Joao.Cpf);

        var espelho = SemanaDoJoao.Espelho(semTratamento);

        var quinta = espelho.Apuracao.Dias[3];
        Assert.Equal(TimeSpan.FromHours(4), quinta.Realizada);
        Assert.Equal(TimeSpan.FromHours(4), quinta.Faltantes);
        Assert.Equal(SituacaoDoDia.Pendente, espelho.Apuracao.Dias[5].Situacao);
        Assert.StartsWith("sáb 21/11  08:00 08:01 12:00 ", espelho.Linhas().Single(linha => linha.StartsWith("sáb", StringComparison.Ordinal)), StringComparison.Ordinal);
        Assert.DoesNotContain("Tratamentos:", espelho.Linhas());
    }

    [Fact]
    public void O_dia_abonado_aparece_com_o_motivo_e_quem_abonou()
    {
        var (_, tratamento, _) = SemanaDoJoao.Montar();
        tratamento.Abonar(new DateOnly(2026, 11, 17), "Declaração de comparecimento a consulta médica.", RegistroDeExemplo.Rh, Exemplos.Em(23, 7, 40));

        var linhas = SemanaDoJoao.Espelho(tratamento).Linhas();

        Assert.Contains(
            "ter 17/11  07:55 12:00 13:10 17:00                     07:50    00:00   00:00        00:00   00:00  Abono: Declaração de comparecimento a consulta médica.; Tolerância do art. 58",
            linhas);
        Assert.Contains("Abonadas: 00:10", linhas);
        Assert.Contains("  17/11/2026 abonado por 123.456.789-09 em 23/11/2026 07:40: Declaração de comparecimento a consulta médica.", linhas);
    }

    [Fact]
    public void O_espelho_do_mes_mostra_o_cei_caepf_ou_cno_e_um_dia_por_linha()
    {
        var empregador = Empregador.Novo(Documento.De("11.222.333/0001-81"), "Construtora Horizonte Ltda.", "51.234.56789/70");
        var obra = Estabelecimento.Novo(empregador, "Rua das Palmeiras, 100 - Santos/SP", "America/Sao_Paulo");
        var registro = RegistroDePonto.Abrir(obra, RegistroDeExemplo.Rh, Exemplos.Em(1, 5, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(1, 5, 1));

        var espelho = EspelhoDePonto.Gerar(
            TratamentoDoPonto.Do(registro, Exemplos.Joao.Cpf),
            Exemplos.Joao,
            Horarios.DaObra,
            CalendarioDeFeriados.Nacionais,
            new DateOnly(2026, 11, 1),
            new DateOnly(2026, 11, 30),
            Exemplos.Em(30, 18, 0));
        var linhas = espelho.Linhas();

        Assert.Equal("CEI/CAEPF/CNO: 512345678970", linhas[3]);
        Assert.Equal(30, espelho.Apuracao.Dias.Count);
        Assert.Contains("seg 02/11                                              00:00    00:00   00:00        00:00   00:00  Feriado: Finados", linhas);
        Assert.Contains("ter 03/11                                              00:00    00:00   00:00        00:00   08:00  Falta", linhas);
    }

    [Fact]
    public void O_espelho_e_do_trabalhador_do_tratamento()
    {
        var (_, tratamento, _) = SemanaDoJoao.Montar();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => EspelhoDePonto.Gerar(
            tratamento, Exemplos.Maria, Horarios.DaObra, CalendarioDeFeriados.Nacionais, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 22), Exemplos.Em(23, 8, 0)));

        Assert.Equal("O tratamento é de outro trabalhador.", erro.Message);
    }

    [Fact]
    public void O_descanso_do_primeiro_dia_conta_da_ultima_marcacao_antes_do_periodo()
    {
        var (registro, tratamento, _) = SemanaDoJoao.Montar();
        foreach (var (dia, hora) in new[] { (22, "22:00"), (22, "23:00"), (23, "08:00"), (23, "12:00"), (23, "13:00"), (23, "17:00") })
        {
            registro.MarcarOnLine(
                IdentificacaoDoTrabalhador.PorCodigoPessoal(Exemplos.Construtora, Exemplos.Joao, Horarios.Em(dia, hora)),
                Coletor.DispositivoEletronico);
        }

        // Do domingo às 23h até a segunda às 8h são 9 horas: faltaram 2.
        var espelho = EspelhoDePonto.Gerar(
            tratamento, Exemplos.Joao, Horarios.DaObra, CalendarioDeFeriados.Nacionais, new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 23), Exemplos.Em(23, 18, 0));

        Assert.Equal(TimeSpan.FromHours(2), espelho.Apuracao.Dias[0].DescansoSuprimido);
    }
}
