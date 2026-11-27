using System.Globalization;
using System.Security.Claims;
using PontoFacial.Api.Configuracao;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Persistencia;

namespace PontoFacial.Api.Rotas;

/// <summary>O que as rotas repetem: a empresa do token, o CPF da rota e quem pode ver o quê.</summary>
internal static class Comum
{
    public static EmpresaNoBanco Empresa(this BancoDoPonto banco, ClaimsPrincipal usuario) => banco.Empresa(usuario.Empresa());

    /// <summary>
    /// O trabalhador só vê o que é dele; o RH e o gestor veem o de todos. A
    /// recusa diz por quê, sem mostrar nada do outro.
    /// </summary>
    public static IResult? SoOProprio(ClaimsPrincipal usuario, Cpf cpf) =>
        usuario.SoPodeVerOProprio() && usuario.Cpf() != cpf
            ? TypedResults.Problem(
                type: TratadorDeErros.Prefixo + "so-o-proprio",
                title: "O trabalhador só vê o que é dele.",
                detail: "Este pedido é sobre outro trabalhador.",
                statusCode: StatusCodes.Status403Forbidden)
            : null;

    /// <summary>O instante na hora do estabelecimento, como o registro a grava: ao minuto.</summary>
    public static string NaHora(DateTimeOffset instante) => instante.ToString("yyyy-MM-dd'T'HH:mmzzz", CultureInfo.InvariantCulture);

    /// <summary>
    /// O horário como a API o recebe: os períodos de cada dia, de 1 (segunda) a
    /// 7 (domingo), em "HH:mm". Ele nasce pela fábrica do domínio, que recusa o
    /// que a CLT não deixa.
    /// </summary>
    public static HorarioContratual Horario(PedidoDeHorario pedido)
    {
        var semana = new Dictionary<DayOfWeek, IReadOnlyList<Periodo>>();

        foreach (var (dia, periodos) in pedido.Semana)
        {
            semana[DiaDaSemana(dia)] = [.. periodos.Select(periodo => periodo.Length == 2
                ? new Periodo(Hora(periodo[0]), Hora(periodo[1]))
                : throw new ExcecaoDeDominio("Cada período do horário tem a entrada e a saída: [\"08:00\", \"12:00\"]."))];
        }

        return HorarioContratual.De(
            semana,
            DiaDaSemana(pedido.DescansoSemanal),
            pedido.IntervaloPreAssinalado,
            pedido.IntervaloNegociadoEmMinutos is { } minutos ? TimeSpan.FromMinutes(minutos) : null,
            pedido.ViradaDoDia is { } virada ? Hora(virada) : default);
    }

    private static TimeOnly Hora(string texto) =>
        TimeOnly.TryParseExact(texto, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora)
            ? hora
            : throw new ExcecaoDeDominio($"\"{texto}\" não é uma hora no formato HH:mm.");

    private static DayOfWeek DiaDaSemana(int dia) => dia switch
    {
        >= 1 and <= 6 => (DayOfWeek)dia,
        7 => DayOfWeek.Sunday,
        _ => throw new ExcecaoDeDominio($"O dia {dia} não é um dia da semana: de 1 (segunda) a 7 (domingo)."),
    };
}

/// <summary>O horário contratual no corpo do pedido.</summary>
/// <param name="Semana">Os períodos de cada dia de trabalho, pelo número do dia: 1 é segunda, 7 é domingo.</param>
/// <param name="DescansoSemanal">O dia do descanso semanal, de 1 a 7.</param>
/// <param name="IntervaloPreAssinalado">Se o intervalo vale sem ser marcado (art. 74, § 2º, da CLT).</param>
/// <param name="IntervaloNegociadoEmMinutos">O intervalo da norma coletiva, de 30 a 60 minutos; nulo, o da lei.</param>
/// <param name="ViradaDoDia">A hora em que o dia de trabalho vira, em "HH:mm"; nula, a meia-noite.</param>
internal sealed record PedidoDeHorario(
    Dictionary<int, string[][]> Semana,
    int DescansoSemanal,
    bool IntervaloPreAssinalado,
    int? IntervaloNegociadoEmMinutos = null,
    string? ViradaDoDia = null);
