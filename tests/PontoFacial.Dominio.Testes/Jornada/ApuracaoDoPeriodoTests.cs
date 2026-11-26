using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Jornada;
using static PontoFacial.Dominio.Testes.Jornada.Horarios;

namespace PontoFacial.Dominio.Testes.Jornada;

public sealed class ApuracaoDoPeriodoTests
{
    [Fact]
    public void A_semana_da_obra_dia_a_dia_com_os_totais()
    {
        // Segunda a domingo, de 23 a 29 de novembro de 2026.
        DateTimeOffset[] marcacoes =
        [
            .. Marcacoes(23, "07:58", "12:01", "13:02", "17:03"),   // no horário, pela tolerância
            .. Marcacoes(24, "08:00", "12:00", "13:00", "19:00"),   // 2 horas extras
            .. Marcacoes(25, "08:20", "12:00", "13:00", "17:00"),   // 20 minutos de atraso
            //  26: falta
            .. Marcacoes(27, "08:00", "12:00", "13:00"),            // marcação ímpar
            .. Marcacoes(28, "08:00", "12:00"),                     // sábado no horário
            .. Marcacoes(29, "08:00", "10:00"),                     // domingo
        ];

        var semana = ApuracaoDoPeriodo.Apurar(new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 29), marcacoes, DaObra, CalendarioDeFeriados.Nacionais, Fuso);

        Assert.Equal(7, semana.Dias.Count);
        Assert.Equal(
            [SituacaoDoDia.Trabalhado, SituacaoDoDia.Trabalhado, SituacaoDoDia.Trabalhado, SituacaoDoDia.Falta, SituacaoDoDia.Pendente, SituacaoDoDia.Trabalhado, SituacaoDoDia.Trabalhado],
            semana.Dias.Select(dia => dia.Situacao));
        Assert.Equal(TimeSpan.FromHours(44), semana.Prevista);
        Assert.Equal(Hm("02:00"), semana.Extras);
        Assert.Equal(Hm("02:00"), semana.ExtrasEmDescanso);
        Assert.Equal(TimeSpan.FromHours(8) + Hm("00:20"), semana.Faltantes);
        Assert.Equal(1, semana.Faltas);
        Assert.Equal(1, semana.Pendentes);
        Assert.Equal(0, semana.DiasAcimaDoLimiteDeExtras);
    }

    [Fact]
    public void O_dia_abonado_sai_das_faltas_e_entra_nos_abonos()
    {
        var semana = ApuracaoDoPeriodo.Apurar(
            new DateOnly(2026, 11, 23),
            new DateOnly(2026, 11, 24),
            Marcacoes(24, "08:00", "12:00", "13:00", "17:00"),
            DaObra,
            CalendarioDeFeriados.Nacionais,
            Fuso,
            abonos: new Dictionary<DateOnly, string> { [new DateOnly(2026, 11, 23)] = "Atestado médico" });

        Assert.Equal(0, semana.Faltas);
        Assert.Equal(1, semana.Abonos);
        Assert.Equal(TimeSpan.Zero, semana.Faltantes);
        Assert.Equal(Hm("08:00"), semana.FaltantesAbonadas);
    }

    [Fact]
    public void O_turno_da_noite_fica_no_dia_em_que_comecou_pela_virada()
    {
        DateTimeOffset[] marcacoes = [Em(23, "22:00"), Em(24, "02:00"), Em(24, "03:00"), Em(24, "05:00")];

        var periodo = ApuracaoDoPeriodo.Apurar(new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 24), marcacoes, Noturno, CalendarioDeFeriados.Nacionais, Fuso);

        Assert.Equal(4, periodo.Dias[0].Marcacoes.Count);
        Assert.Equal(Hm("06:51"), periodo.Dias[0].Realizada);
        Assert.Equal(SituacaoDoDia.Falta, periodo.Dias[1].Situacao);
    }

    [Fact]
    public void O_descanso_entre_jornadas_vem_da_jornada_anterior_mesmo_com_um_dia_no_meio()
    {
        DateTimeOffset[] marcacoes =
        [
            .. Marcacoes(23, "08:00", "12:00", "13:00", "23:30"),
            .. Marcacoes(24, "08:00", "12:00", "13:00", "17:00"),
        ];

        var periodo = ApuracaoDoPeriodo.Apurar(new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 24), marcacoes, DaObra, CalendarioDeFeriados.Nacionais, Fuso);

        Assert.Equal(TimeSpan.Zero, periodo.Dias[0].DescansoSuprimido);
        Assert.Equal(Hm("02:30"), periodo.Dias[1].DescansoSuprimido);
        Assert.Equal(Hm("02:30"), periodo.DescansoSuprimido);
    }

    [Fact]
    public void O_primeiro_dia_usa_a_ultima_marcacao_antes_do_periodo()
    {
        var periodo = ApuracaoDoPeriodo.Apurar(
            new DateOnly(2026, 11, 23),
            new DateOnly(2026, 11, 23),
            Marcacoes(23, "08:00", "12:00", "13:00", "17:00"),
            DaObra,
            CalendarioDeFeriados.Nacionais,
            Fuso,
            fimDaJornadaAnterior: Em(22, "23:00"));

        Assert.Equal(Hm("02:00"), periodo.Dias[0].DescansoSuprimido);
    }

    [Fact]
    public void Marcacoes_fora_do_periodo_ficam_de_fora()
    {
        var periodo = ApuracaoDoPeriodo.Apurar(
            new DateOnly(2026, 11, 24),
            new DateOnly(2026, 11, 24),
            [.. Marcacoes(23, "08:00", "17:00"), .. Marcacoes(24, "08:00", "12:00", "13:00", "17:00")],
            DaObra,
            CalendarioDeFeriados.Nacionais,
            Fuso);

        Assert.Single(periodo.Dias);
        Assert.Equal(4, periodo.Dias[0].Marcacoes.Count);
    }

    [Fact]
    public void O_periodo_vai_de_um_dia_a_um_ano()
    {
        Assert.Throws<ExcecaoDeDominio>(() => ApuracaoDoPeriodo.Apurar(new DateOnly(2026, 11, 24), new DateOnly(2026, 11, 23), [], DaObra, CalendarioDeFeriados.Nacionais, Fuso));
        Assert.Throws<ExcecaoDeDominio>(() => ApuracaoDoPeriodo.Apurar(new DateOnly(2026, 1, 1), new DateOnly(2027, 1, 2), [], DaObra, CalendarioDeFeriados.Nacionais, Fuso));
    }
}
