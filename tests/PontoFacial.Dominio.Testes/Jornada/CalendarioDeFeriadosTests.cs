using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Jornada;

namespace PontoFacial.Dominio.Testes.Jornada;

public sealed class CalendarioDeFeriadosTests
{
    [Fact]
    public void Os_nove_feriados_nacionais_de_2026()
    {
        var feriados = Enumerable.Range(0, 365)
            .Select(dia => new DateOnly(2026, 1, 1).AddDays(dia))
            .Select(data => (Data: data, Nome: CalendarioDeFeriados.Nacionais.FeriadoEm(data)))
            .Where(par => par.Nome is not null)
            .Select(par => $"{par.Data:dd/MM} {par.Nome}")
            .ToList();

        Assert.Equal(
            [
                "01/01 Confraternização Universal",
                "21/04 Tiradentes",
                "01/05 Dia do Trabalho",
                "07/09 Independência do Brasil",
                "12/10 Nossa Senhora Aparecida",
                "02/11 Finados",
                "15/11 Proclamação da República",
                "20/11 Dia Nacional de Zumbi e da Consciência Negra",
                "25/12 Natal",
            ],
            feriados);
    }

    [Fact]
    public void O_20_de_novembro_e_feriado_nacional_desde_2024()
    {
        Assert.Null(CalendarioDeFeriados.Nacionais.FeriadoEm(new DateOnly(2023, 11, 20)));
        Assert.NotNull(CalendarioDeFeriados.Nacionais.FeriadoEm(new DateOnly(2024, 11, 20)));
    }

    [Theory]
    [InlineData(2024, 3, 29)]
    [InlineData(2025, 4, 18)]
    [InlineData(2026, 4, 3)]
    [InlineData(2027, 3, 26)]
    public void A_sexta_feira_da_paixao_e_dois_dias_antes_da_pascoa(int ano, int mes, int dia)
    {
        Assert.Equal(new DateOnly(ano, mes, dia), CalendarioDeFeriados.SextaFeiraDaPaixao(ano));
        Assert.Equal(DayOfWeek.Friday, CalendarioDeFeriados.SextaFeiraDaPaixao(ano).DayOfWeek);
    }

    [Fact]
    public void Os_feriados_locais_entram_por_cadastro()
    {
        var santos = CalendarioDeFeriados.Nacionais.ComLocais(
        [
            (new DateOnly(2026, 1, 26), "Aniversário de Santos"),
            (CalendarioDeFeriados.SextaFeiraDaPaixao(2026), "Sexta-feira da Paixão"),
        ]);

        Assert.Equal("Aniversário de Santos", santos.FeriadoEm(new DateOnly(2026, 1, 26)));
        Assert.Equal("Sexta-feira da Paixão", santos.FeriadoEm(new DateOnly(2026, 4, 3)));
        Assert.Equal("Natal", santos.FeriadoEm(new DateOnly(2026, 12, 25)));
        Assert.Null(CalendarioDeFeriados.Nacionais.FeriadoEm(new DateOnly(2026, 4, 3)));
        Assert.Equal(2, santos.Locais.Count);
    }

    [Fact]
    public void Um_feriado_local_nao_repete_o_nacional_nem_outro_local()
    {
        var erroNacional = Assert.Throws<ExcecaoDeDominio>(() =>
            CalendarioDeFeriados.Nacionais.ComLocais([(new DateOnly(2026, 11, 20), "Consciência Negra")]));
        var comUm = CalendarioDeFeriados.Nacionais.ComLocais([(new DateOnly(2026, 1, 26), "Aniversário de Santos")]);
        var erroLocal = Assert.Throws<ExcecaoDeDominio>(() => comUm.ComLocais([(new DateOnly(2026, 1, 26), "Outro")]));

        Assert.Equal("20/11/2026 já é feriado nacional: Dia Nacional de Zumbi e da Consciência Negra.", erroNacional.Message);
        Assert.Equal("26/01/2026 já está no calendário como Aniversário de Santos.", erroLocal.Message);
    }
}
