using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes.Registro;

namespace PontoFacial.Dominio.Testes.Jornada;

public sealed class TratamentoDoPontoTests
{
    [Fact]
    public void O_tratamento_nao_toca_no_registro()
    {
        var (registro, tratamento, repetida) = SemanaDoJoao.Montar();
        var operacoes = registro.Operacoes.Count;
        var hashDaRepetida = registro.Marcacoes.Single(marcacao => marcacao.Nsr == repetida).Hash;

        tratamento.Incluir(Horarios.Em(19, "18:00"), "Saída de serviço externo.", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0));

        Assert.Equal(operacoes, registro.Operacoes.Count);
        Assert.Equal(hashDaRepetida, registro.Marcacoes.Single(marcacao => marcacao.Nsr == repetida).Hash);
        Assert.Equal(15, tratamento.Registradas().Count);
    }

    [Fact]
    public void As_consideradas_sao_as_do_registro_menos_as_desconsideradas_mais_as_incluidas()
    {
        var (_, tratamento, _) = SemanaDoJoao.Montar();

        var consideradas = tratamento.Consideradas();

        Assert.Equal(16, consideradas.Count);
        Assert.DoesNotContain(Horarios.Em(21, "08:01"), consideradas);
        Assert.Contains(Horarios.Em(19, "13:00"), consideradas);
        Assert.Equal(consideradas.Order(), consideradas);
    }

    [Fact]
    public void Cada_tratamento_guarda_motivo_responsavel_e_momento()
    {
        var (_, tratamento, repetida) = SemanaDoJoao.Montar();

        var desconsiderada = tratamento.Marcacoes.Single(tratada => tratada.Tipo == TipoDeTratamento.Desconsiderada);

        Assert.Equal(repetida, desconsiderada.Nsr);
        Assert.Equal(Horarios.Em(21, "08:01"), desconsiderada.MarcadaEm);
        Assert.Equal(RegistroDeExemplo.Rh, desconsiderada.Responsavel);
        Assert.Equal(Exemplos.Em(23, 7, 32), desconsiderada.Em);
        Assert.StartsWith("Marcação repetida", desconsiderada.Motivo, StringComparison.Ordinal);
        Assert.All(tratamento.Marcacoes.Where(tratada => tratada.Tipo == TipoDeTratamento.Incluida), tratada => Assert.Null(tratada.Nsr));
    }

    [Fact]
    public void A_inclusao_vai_ao_minuto_na_hora_da_obra()
    {
        var (_, tratamento, _) = SemanaDoJoao.Montar();

        var incluida = tratamento.Incluir(new DateTimeOffset(2026, 11, 19, 21, 30, 45, TimeSpan.Zero), "Saída de serviço externo.", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0));

        Assert.Equal(Horarios.Em(19, "18:30"), incluida.MarcadaEm);
        Assert.Equal(TimeSpan.FromHours(-3), incluida.MarcadaEm.Offset);
    }

    [Fact]
    public void Nao_se_inclui_marcacao_repetida_nem_do_futuro_nem_sem_motivo()
    {
        var (_, tratamento, _) = SemanaDoJoao.Montar();

        Assert.Equal(
            "Já há marcação às 13:00 de 19/11/2026.",
            Assert.Throws<ExcecaoDeDominio>(() => tratamento.Incluir(Horarios.Em(19, "13:00"), "De novo.", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0))).Message);
        Assert.Equal(
            "Não se inclui marcação que ainda não aconteceu.",
            Assert.Throws<ExcecaoDeDominio>(() => tratamento.Incluir(Horarios.Em(24, "08:00"), "Adiantado.", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0))).Message);
        Assert.Equal(
            "Falta o motivo do tratamento.",
            Assert.Throws<ExcecaoDeDominio>(() => tratamento.Incluir(Horarios.Em(19, "18:00"), " ", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0))).Message);
    }

    [Fact]
    public void So_se_desconsidera_marcacao_do_trabalhador_e_uma_vez()
    {
        var (registro, tratamento, repetida) = SemanaDoJoao.Montar();
        registro.IncluirTrabalhador(Exemplos.Maria, RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 0));
        var daMaria = registro.MarcarOnLine(
            IdentificacaoDoTrabalhador.PorCodigoPessoal(Exemplos.Construtora, Exemplos.Maria, Exemplos.Em(23, 7, 0)),
            Coletor.DispositivoEletronico).Nsr;

        Assert.Equal(
            $"O NSR {daMaria} não é de uma marcação deste trabalhador.",
            Assert.Throws<ExcecaoDeDominio>(() => tratamento.Desconsiderar(daMaria, "Engano.", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0))).Message);
        Assert.Equal(
            "O NSR 2 não é de uma marcação deste trabalhador.",
            Assert.Throws<ExcecaoDeDominio>(() => tratamento.Desconsiderar(2, "Engano.", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0))).Message);
        Assert.Equal(
            $"A marcação do NSR {repetida} já foi desconsiderada.",
            Assert.Throws<ExcecaoDeDominio>(() => tratamento.Desconsiderar(repetida, "De novo.", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0))).Message);
    }

    [Fact]
    public void O_dia_se_abona_uma_vez_com_motivo()
    {
        var (_, tratamento, _) = SemanaDoJoao.Montar();
        var abono = tratamento.Abonar(new DateOnly(2026, 11, 17), "Atestado médico", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0));

        Assert.Equal(new DateOnly(2026, 11, 17), abono.Dia);
        Assert.Same(abono, tratamento.Abonos[new DateOnly(2026, 11, 17)]);
        Assert.Equal(
            "O dia 17/11/2026 já foi abonado: Atestado médico.",
            Assert.Throws<ExcecaoDeDominio>(() => tratamento.Abonar(new DateOnly(2026, 11, 17), "Outro", RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 1))).Message);
    }

    [Fact]
    public void So_ha_tratamento_de_quem_foi_incluido_no_registro()
    {
        var (registro, _, _) = SemanaDoJoao.Montar();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => TratamentoDoPonto.Do(registro, Cpf.De("111.444.777-35")));

        Assert.Equal("O trabalhador não foi incluído neste registro de ponto.", erro.Message);
    }
}
