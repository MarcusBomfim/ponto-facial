using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes.Registro;

namespace PontoFacial.Dominio.Testes.Jornada;

/// <summary>
/// A semana de 16 a 22 de novembro de 2026 do João, com o tratamento do RH
/// em 23/11: a segunda com o intervalo pré-assinalado, a terça com a volta do
/// almoço atrasada, a quarta com extras, a quinta com duas marcações
/// esquecidas, a sexta de feriado, o sábado com uma marcação repetida.
/// </summary>
internal static class SemanaDoJoao
{
    public static (RegistroDePonto Registro, TratamentoDoPonto Tratamento, long NsrRepetida) Montar()
    {
        var registro = RegistroDePonto.Abrir(Exemplos.Obra, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 1));

        long Marcar(int dia, string hora) =>
            registro.MarcarOnLine(IdentificacaoDoTrabalhador.PorCodigoPessoal(Exemplos.Construtora, Exemplos.Joao, Horarios.Em(dia, hora)), Coletor.DispositivoEletronico).Nsr;

        Marcar(16, "07:58");
        Marcar(16, "17:02");
        Marcar(17, "07:55");
        Marcar(17, "12:00");
        Marcar(17, "13:10");
        Marcar(17, "17:00");
        Marcar(18, "08:00");
        Marcar(18, "12:00");
        Marcar(18, "13:00");
        Marcar(18, "19:30");
        Marcar(19, "08:00");
        Marcar(19, "12:00");
        Marcar(21, "08:00");
        var repetida = Marcar(21, "08:01");
        Marcar(21, "12:00");

        var tratamento = TratamentoDoPonto.Do(registro, Exemplos.Joao.Cpf);
        tratamento.Incluir(Horarios.Em(19, "13:00"), "Esqueceu de marcar a volta do almoço; confirmado pelo encarregado.", RegistroDeExemplo.Rh, Exemplos.Em(23, 7, 30));
        tratamento.Incluir(Horarios.Em(19, "17:00"), "Esqueceu de marcar a saída; confirmado pelo encarregado.", RegistroDeExemplo.Rh, Exemplos.Em(23, 7, 31));
        tratamento.Desconsiderar(repetida, "Marcação repetida por engano, um minuto depois da entrada.", RegistroDeExemplo.Rh, Exemplos.Em(23, 7, 32));

        return (registro, tratamento, repetida);
    }

    public static EspelhoDePonto Espelho(TratamentoDoPonto tratamento) => EspelhoDePonto.Gerar(
        tratamento,
        Exemplos.Joao,
        Horarios.DaObraComIntervaloPreAssinalado,
        CalendarioDeFeriados.Nacionais,
        new DateOnly(2026, 11, 16),
        new DateOnly(2026, 11, 22),
        Exemplos.Em(23, 8, 0));
}
