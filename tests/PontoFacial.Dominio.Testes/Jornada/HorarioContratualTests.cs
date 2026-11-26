using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Jornada;
using static PontoFacial.Dominio.Testes.Jornada.Horarios;

namespace PontoFacial.Dominio.Testes.Jornada;

public sealed class HorarioContratualTests
{
    private static Dictionary<DayOfWeek, IReadOnlyList<Periodo>> Dias(params (DayOfWeek Dia, string Periodos)[] dias) =>
        dias.ToDictionary(
            dia => dia.Dia,
            dia => (IReadOnlyList<Periodo>)[.. dia.Periodos.Split(' ').Select(periodo => new Periodo(TimeOnly.Parse(periodo[..5], System.Globalization.CultureInfo.InvariantCulture), TimeOnly.Parse(periodo[6..], System.Globalization.CultureInfo.InvariantCulture)))]);

    [Fact]
    public void O_horario_da_obra_soma_44_horas_e_se_descreve_para_o_espelho()
    {
        Assert.Equal(TimeSpan.FromHours(44), DaObra.DuracaoSemanal);
        Assert.Equal(Hm("08:00"), DaObra.DuracaoDo(DayOfWeek.Monday));
        Assert.Equal(Hm("04:00"), DaObra.DuracaoDo(DayOfWeek.Saturday));
        Assert.Empty(DaObra.Do(DayOfWeek.Sunday));
        Assert.Equal(
            "seg a sex 08:00–12:00 e 13:00–17:00; sáb 08:00–12:00; 44:00 por semana; descanso aos domingos",
            DaObra.Descricao());
        Assert.EndsWith("; intervalo pré-assinalado", DaObraComIntervaloPreAssinalado.Descricao(), StringComparison.Ordinal);
    }

    [Fact]
    public void O_turno_da_noite_e_computado_com_a_hora_reduzida()
    {
        Assert.Equal(Hm("06:51"), Noturno.DuracaoDo(DayOfWeek.Monday));
        Assert.Equal(
            "seg a sex 22:00–02:00 e 03:00–05:00; 34:15 por semana; descanso aos domingos",
            Noturno.Descricao());
    }

    [Fact]
    public void Mais_de_44_horas_por_semana_nao_vale()
    {
        var semana = Semana(true);
        semana[DayOfWeek.Saturday] = [new(new(8, 0), new(12, 0)), new(new(13, 0), new(17, 0))];

        var erro = Assert.Throws<ExcecaoDeDominio>(() => HorarioContratual.De(semana));

        Assert.Equal("O horário soma 48:00 por semana; o máximo é 44:00 (Constituição, art. 7º, XIII).", erro.Message);
    }

    [Fact]
    public void Mais_de_dez_horas_num_dia_nao_vale_nem_com_compensacao()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => HorarioContratual.De(Dias((DayOfWeek.Monday, "07:00-12:00 13:00-19:00"))));

        Assert.Equal("Segunda-feira: o horário soma 11:00; o máximo é 10:00 por dia, mesmo com compensação (art. 59, § 2º, da CLT).", erro.Message);
    }

    [Fact]
    public void Dia_de_mais_de_seis_horas_pede_uma_hora_de_intervalo()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => HorarioContratual.De(Dias((DayOfWeek.Monday, "08:00-12:00 12:30-16:30"))));

        Assert.Equal(
            "Segunda-feira: um dia de 08:00 pede intervalo de pelo menos 01:00 (art. 71 da CLT); o maior do horário é de 00:30.",
            erro.Message);

        var negociado = HorarioContratual.De(Dias((DayOfWeek.Monday, "08:00-12:00 12:30-16:30")), intervaloNegociado: TimeSpan.FromMinutes(30));
        Assert.Equal(TimeSpan.FromMinutes(30), negociado.IntervaloMinimo);
    }

    [Fact]
    public void Dia_de_mais_de_quatro_horas_pede_quinze_minutos()
    {
        Assert.Throws<ExcecaoDeDominio>(() => HorarioContratual.De(Dias((DayOfWeek.Saturday, "08:00-13:00"))));

        var comPausa = HorarioContratual.De(Dias((DayOfWeek.Saturday, "08:00-10:30 10:45-13:15")));
        Assert.Equal(Hm("05:00"), comPausa.DuracaoDo(DayOfWeek.Saturday));
    }

    [Fact]
    public void A_norma_coletiva_nao_baixa_o_intervalo_de_trinta_minutos()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => HorarioContratual.De(Semana(true), intervaloNegociado: TimeSpan.FromMinutes(20)));

        Assert.Equal("O intervalo fixado em norma coletiva vai de 30 minutos a uma hora (art. 611-A, III, da CLT).", erro.Message);
    }

    [Fact]
    public void O_descanso_semanal_nao_tem_horario()
    {
        var semana = Semana(true);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => HorarioContratual.De(semana, descansoSemanal: DayOfWeek.Saturday));

        Assert.Equal("Sábado é o descanso semanal: não tem horário de trabalho.", erro.Message);
    }

    [Fact]
    public void O_turno_que_atravessa_a_virada_pede_outra_virada()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => HorarioContratual.De(Dias((DayOfWeek.Monday, "22:00-02:00 03:00-05:00"))));

        Assert.Equal(
            "Segunda-feira: o período 22:00–02:00 atravessa a virada do dia, às 00:00. Escolha uma virada fora do turno.",
            erro.Message);
    }

    [Fact]
    public void Periodos_sobrepostos_nao_valem()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => HorarioContratual.De(Dias((DayOfWeek.Monday, "08:00-12:00 11:00-14:00"))));

        Assert.Equal("Segunda-feira: os períodos 08:00–12:00 e 11:00–14:00 se sobrepõem.", erro.Message);
    }

    [Fact]
    public void Com_descanso_em_outro_dia_a_descricao_diz_qual()
    {
        var semana = Semana(false);
        semana.Remove(DayOfWeek.Monday);
        semana[DayOfWeek.Sunday] = [new(new(8, 0), new(12, 0)), new(new(13, 0), new(17, 0))];

        var horario = HorarioContratual.De(semana, descansoSemanal: DayOfWeek.Monday);

        Assert.EndsWith("descanso às segundas-feiras", horario.Descricao(), StringComparison.Ordinal);
        Assert.StartsWith("ter a sex 08:00–12:00 e 13:00–17:00; dom 08:00–12:00 e 13:00–17:00", horario.Descricao(), StringComparison.Ordinal);
    }

    [Fact]
    public void As_duracoes_se_escrevem_em_horas_e_minutos()
    {
        Assert.Equal("44:00", Duracoes.Formatar(TimeSpan.FromHours(44)));
        Assert.Equal("06:51", Duracoes.Formatar(TimeSpan.FromMinutes(411.43)));
        Assert.Equal("-00:58", Duracoes.Formatar(TimeSpan.FromMinutes(-58)));
        Assert.Equal("00:00", Duracoes.Formatar(TimeSpan.FromSeconds(-10)));
    }
}
