using PontoFacial.Dominio.Jornada;
using static PontoFacial.Dominio.Testes.Jornada.Horarios;

namespace PontoFacial.Dominio.Testes.Jornada;

/*
 * Os dias são de novembro de 2026, no horário da obra: segunda a sexta das
 * 8h às 12h e das 13h às 17h. O dia 23 é uma segunda; o 20, uma sexta de
 * feriado; o 29, um domingo.
 */
public sealed class ApuracaoDoDiaTests
{
    [Fact]
    public void O_dia_no_horario_da_as_oito_horas_e_nada_mais()
    {
        var dia = Apurar(23, DaObra, "08:00", "12:00", "13:00", "17:00");

        Assert.Equal(SituacaoDoDia.Trabalhado, dia.Situacao);
        Assert.Equal(Hm("08:00"), dia.Prevista);
        Assert.Equal(Hm("08:00"), dia.Realizada);
        Assert.Equal(TimeSpan.Zero, dia.Extras);
        Assert.Equal(TimeSpan.Zero, dia.Faltantes);
        Assert.Equal(TimeSpan.Zero, dia.IntervaloSuprimido);
        Assert.False(dia.ToleranciaAplicada);
    }

    [Fact]
    public void Variacoes_de_ate_cinco_minutos_que_somam_ate_dez_nao_contam()
    {
        // 2 + 1 + 2 + 3 = 8 minutos de variação: o dia vale o horário (art. 58, § 1º).
        var dia = Apurar(23, DaObra, "07:58", "12:01", "13:02", "17:03");

        Assert.Equal(Hm("08:00"), dia.Realizada);
        Assert.Equal(TimeSpan.Zero, dia.Extras);
        Assert.Equal(TimeSpan.Zero, dia.Faltantes);
        Assert.True(dia.ToleranciaAplicada);
    }

    [Fact]
    public void Passados_os_dez_minutos_no_dia_toda_variacao_conta()
    {
        // 4 + 3 + 2 + 4 = 13: nenhuma é desconsiderada. Extra antes das 8h (4),
        // dentro do intervalo (3) e depois das 17h (4); falta na volta do intervalo (2).
        var dia = Apurar(23, DaObra, "07:56", "12:03", "13:02", "17:04");

        Assert.Equal(Hm("08:09"), dia.Realizada);
        Assert.Equal(Hm("00:11"), dia.Extras);
        Assert.Equal(Hm("00:02"), dia.Faltantes);
        Assert.False(dia.ToleranciaAplicada);
    }

    [Fact]
    public void A_variacao_de_mais_de_cinco_minutos_conta_inteira_e_as_pequenas_continuam_fora()
    {
        // Chegou 8 minutos antes: conta inteira. Saiu 2 minutos depois: não conta.
        var dia = Apurar(23, DaObra, "07:52", "12:00", "13:00", "17:02");

        Assert.Equal(Hm("00:08"), dia.Extras);
        Assert.Equal(Hm("08:08"), dia.Realizada);
        Assert.True(dia.ToleranciaAplicada);
    }

    [Fact]
    public void Seis_minutos_de_atraso_contam_inteiros()
    {
        Assert.Equal(Hm("00:06"), Apurar(23, DaObra, "08:06", "12:00", "13:00", "17:00").Faltantes);
        Assert.Equal(TimeSpan.Zero, Apurar(23, DaObra, "08:05", "12:00", "13:00", "17:00").Faltantes);
    }

    [Fact]
    public void Dez_minutos_somados_ainda_nao_contam_e_onze_contam_todos()
    {
        var dez = Apurar(23, DaObra, "07:55", "12:00", "13:00", "17:05");
        var onze = Apurar(23, DaObra, "07:55", "12:01", "13:00", "17:05");

        Assert.Equal(TimeSpan.Zero, dez.Extras);
        Assert.Equal(Hm("08:00"), dez.Realizada);
        Assert.Equal(Hm("00:11"), onze.Extras);
    }

    [Fact]
    public void O_atraso_vira_hora_faltante()
    {
        var dia = Apurar(23, DaObra, "08:20", "12:00", "13:00", "17:00");

        Assert.Equal(Hm("00:20"), dia.Faltantes);
        Assert.Equal(Hm("07:40"), dia.Realizada);
        Assert.Equal(TimeSpan.Zero, dia.Extras);
    }

    [Fact]
    public void O_dia_de_horario_sem_marcacao_e_falta()
    {
        var dia = Apurar(23, DaObra);

        Assert.Equal(SituacaoDoDia.Falta, dia.Situacao);
        Assert.Equal(Hm("08:00"), dia.Faltantes);
        Assert.Null(dia.FimDaJornada);
    }

    [Fact]
    public void A_falta_abonada_nao_conta_como_faltante_e_fica_a_parte()
    {
        var dia = ApuracaoDoDia.Apurar(new DateOnly(2026, 11, 23), [], DaObra, CalendarioDeFeriados.Nacionais, Fuso, abono: "Atestado médico");

        Assert.Equal(SituacaoDoDia.Abonado, dia.Situacao);
        Assert.Equal(TimeSpan.Zero, dia.Faltantes);
        Assert.Equal(Hm("08:00"), dia.FaltantesAbonadas);
        Assert.Equal("Atestado médico", dia.Abono);
    }

    [Fact]
    public void O_abono_cobre_a_saida_antecipada_do_dia_trabalhado()
    {
        var dia = ApuracaoDoDia.Apurar(
            new DateOnly(2026, 11, 23),
            Marcacoes(23, "08:00", "12:00", "13:00", "15:00"),
            DaObra,
            CalendarioDeFeriados.Nacionais,
            Fuso,
            abono: "Consulta médica com declaração");

        Assert.Equal(SituacaoDoDia.Trabalhado, dia.Situacao);
        Assert.Equal(TimeSpan.Zero, dia.Faltantes);
        Assert.Equal(Hm("02:00"), dia.FaltantesAbonadas);
        Assert.Equal(Hm("06:00"), dia.Realizada);
    }

    [Fact]
    public void Trabalho_no_domingo_e_extra_em_descanso()
    {
        var dia = Apurar(29, DaObra, "08:00", "12:00");

        Assert.Equal(SituacaoDoDia.Trabalhado, dia.Situacao);
        Assert.True(dia.DiaDeDescanso);
        Assert.Equal(TimeSpan.Zero, dia.Prevista);
        Assert.Equal(Hm("04:00"), dia.ExtrasEmDescanso);
        Assert.Equal(TimeSpan.Zero, dia.Extras);
    }

    [Fact]
    public void No_feriado_nao_ha_horario_e_o_trabalho_e_extra_em_descanso()
    {
        var trabalhado = Apurar(20, DaObra, "08:00", "12:00", "13:00", "15:00");
        var folgado = Apurar(20, DaObra);

        Assert.Equal("Dia Nacional de Zumbi e da Consciência Negra", trabalhado.Feriado);
        Assert.Equal(TimeSpan.Zero, trabalhado.Prevista);
        Assert.Equal(Hm("06:00"), trabalhado.ExtrasEmDescanso);
        Assert.Equal(SituacaoDoDia.Feriado, folgado.Situacao);
        Assert.Equal(TimeSpan.Zero, folgado.Faltantes);
    }

    [Fact]
    public void Sem_sabado_no_horario_o_sabado_e_folga_e_o_trabalho_nele_e_extra_comum()
    {
        var semSabado = HorarioContratual.De(Semana(false));

        var folga = Apurar(28, semSabado);
        var trabalhado = Apurar(28, semSabado, "08:00", "11:00");

        Assert.Equal(SituacaoDoDia.Folga, folga.Situacao);
        Assert.Equal(Hm("03:00"), trabalhado.Extras);
        Assert.False(trabalhado.DiaDeDescanso);
    }

    [Fact]
    public void Mais_de_duas_horas_extras_no_dia_passam_do_limite_do_art_59()
    {
        var dia = Apurar(23, DaObra, "08:00", "12:00", "13:00", "20:30");

        Assert.Equal(Hm("03:30"), dia.Extras);
        Assert.True(dia.AcimaDoLimiteDeExtras);
        Assert.False(Apurar(23, DaObra, "08:00", "12:00", "13:00", "19:00").AcimaDoLimiteDeExtras);
    }

    [Fact]
    public void O_intervalo_curto_e_suprimido_no_que_faltou()
    {
        // Meia hora de almoço num dia de 9 horas: faltou meia hora do intervalo
        // (art. 71, § 4º), e a meia hora trabalhada nele é extra.
        var dia = Apurar(23, DaObra, "08:00", "12:00", "12:30", "17:30");

        Assert.Equal(Hm("09:00"), dia.Realizada);
        Assert.Equal(Hm("00:30"), dia.IntervaloSuprimido);
        Assert.Equal(Hm("01:00"), dia.Extras);
    }

    [Fact]
    public void Com_a_norma_coletiva_de_trinta_minutos_o_mesmo_intervalo_basta()
    {
        var negociado = HorarioContratual.De(Semana(true), intervaloNegociado: TimeSpan.FromMinutes(30));

        Assert.Equal(TimeSpan.Zero, Apurar(23, negociado, "08:00", "12:00", "12:30", "17:30").IntervaloSuprimido);
    }

    [Fact]
    public void Sem_intervalo_nenhum_a_hora_inteira_e_suprimida()
    {
        var dia = Apurar(23, DaObra, "08:00", "16:00");

        Assert.Equal(Hm("01:00"), dia.IntervaloSuprimido);
        Assert.Equal(Hm("01:00"), dia.Extras);
        Assert.Equal(Hm("01:00"), dia.Faltantes);
    }

    [Fact]
    public void Entre_quatro_e_seis_horas_o_intervalo_e_de_quinze_minutos()
    {
        var dia = Apurar(28, DaObra, "08:00", "14:00");

        Assert.Equal(Hm("06:00"), dia.Realizada);
        Assert.Equal(Hm("00:15"), dia.IntervaloSuprimido);
        Assert.Equal(Hm("02:00"), dia.Extras);
    }

    [Fact]
    public void Com_o_intervalo_pre_assinalado_quem_marca_so_entrada_e_saida_tem_o_intervalo_lancado()
    {
        var dia = Apurar(23, DaObraComIntervaloPreAssinalado, "07:58", "17:02");

        Assert.Equal([Em(23, "12:00"), Em(23, "13:00")], dia.PreAssinaladas);
        Assert.Equal(4, dia.Marcacoes.Count);
        Assert.Equal(Hm("08:00"), dia.Realizada);
        Assert.Equal(TimeSpan.Zero, dia.IntervaloSuprimido);
        Assert.True(dia.ToleranciaAplicada);
    }

    [Fact]
    public void Sem_o_intervalo_pre_assinalado_as_mesmas_marcacoes_suprimem_o_intervalo()
    {
        var dia = Apurar(23, DaObra, "07:58", "17:02");

        Assert.Empty(dia.PreAssinaladas);
        Assert.Equal(Hm("01:00"), dia.IntervaloSuprimido);
    }

    [Fact]
    public void Marcacao_impar_deixa_o_dia_pendente_sem_conta()
    {
        var dia = Apurar(23, DaObra, "08:00", "12:00", "13:00");

        Assert.Equal(SituacaoDoDia.Pendente, dia.Situacao);
        Assert.Equal(TimeSpan.Zero, dia.Realizada);
        Assert.Equal(TimeSpan.Zero, dia.Faltantes);
        Assert.Equal(Hm("08:00"), dia.Prevista);
    }

    [Fact]
    public void Sete_horas_de_relogio_a_noite_valem_oito()
    {
        // 22h às 5h, sem intervalo: 420 minutos × 8/7 = 480 (art. 73, § 1º).
        var dia = ApuracaoDoDia.Apurar(new DateOnly(2026, 11, 23), [Em(23, "22:00"), Em(24, "05:00")], Noturno, CalendarioDeFeriados.Nacionais, Fuso);

        Assert.Equal(Hm("08:00"), dia.Realizada);
        Assert.Equal(Hm("08:00"), dia.Noturna);
    }

    [Fact]
    public void O_turno_da_noite_no_horario_nao_tem_extra_nem_falta()
    {
        // 6 horas de relógio, todas noturnas: 360 × 8/7 = 411,4 minutos, 6:51.
        var dia = ApuracaoDoDia.Apurar(new DateOnly(2026, 11, 23), [Em(23, "22:00"), Em(24, "02:00"), Em(24, "03:00"), Em(24, "05:00")], Noturno, CalendarioDeFeriados.Nacionais, Fuso);

        Assert.Equal(Hm("06:51"), dia.Prevista);
        Assert.Equal(Hm("06:51"), dia.Realizada);
        Assert.Equal(Hm("06:51"), dia.Noturna);
        Assert.Equal(TimeSpan.Zero, dia.Extras);
        Assert.Equal(TimeSpan.Zero, dia.Faltantes);
    }

    [Fact]
    public void A_prorrogacao_depois_das_cinco_de_quem_comecou_de_noite_tambem_e_noturna()
    {
        // Até 6h30: 90 minutos depois das 5h, noturnos pela prorrogação
        // (art. 73, § 5º; Súmula 60, II). 450 × 8/7 = 514,3; o extra, 90 × 8/7 = 102,9.
        var dia = ApuracaoDoDia.Apurar(new DateOnly(2026, 11, 23), [Em(23, "22:00"), Em(24, "02:00"), Em(24, "03:00"), Em(24, "06:30")], Noturno, CalendarioDeFeriados.Nacionais, Fuso);

        Assert.Equal(Hm("08:34"), dia.Realizada);
        Assert.Equal(Hm("08:34"), dia.Noturna);
        Assert.Equal(Hm("01:43"), dia.Extras);
    }

    [Fact]
    public void Quem_comecou_de_dia_e_ficou_ate_as_onze_da_noite_tem_so_a_hora_das_22_como_noturna()
    {
        // 08h às 12h e 13h às 23h: 14 horas de relógio, a última noturna (60 × 8/7 = 68,6).
        var dia = Apurar(23, DaObra, "08:00", "12:00", "13:00", "23:00");

        Assert.Equal(Hm("01:09"), dia.Noturna);
        Assert.Equal(Hm("14:09"), dia.Realizada);
        Assert.Equal(Hm("06:09"), dia.Extras);
    }

    [Fact]
    public void Menos_de_onze_horas_entre_jornadas_e_descanso_suprimido()
    {
        var dia = ApuracaoDoDia.Apurar(
            new DateOnly(2026, 11, 24),
            Marcacoes(24, "08:00", "12:00", "13:00", "17:00"),
            DaObra,
            CalendarioDeFeriados.Nacionais,
            Fuso,
            fimDaJornadaAnterior: Em(23, "23:00"));

        // Das 23h às 8h são 9 horas: faltaram 2 das 11 (art. 66).
        Assert.Equal(Hm("02:00"), dia.DescansoSuprimido);
    }
}
