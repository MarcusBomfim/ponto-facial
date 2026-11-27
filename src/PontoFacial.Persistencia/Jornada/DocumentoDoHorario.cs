using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using PontoFacial.Dominio.Jornada;

namespace PontoFacial.Persistencia.Jornada;

/// <summary>
/// O horário contratual como vai para a coluna <c>jsonb</c>: os períodos de
/// cada dia da semana, de 1 (segunda) a 7 (domingo), e o resto do contrato.
/// A leitura é estrita — campo a mais ou a menos é recusado — e o horário
/// volta pela fábrica do domínio, com todas as regras.
/// </summary>
internal sealed record DocumentoDoHorario(
    [property: JsonRequired] int Formato,
    [property: JsonRequired] Dictionary<int, string[][]> Semana,
    [property: JsonRequired] int DescansoSemanal,
    [property: JsonRequired] bool IntervaloPreAssinalado,
    string? IntervaloNegociado,
    [property: JsonRequired] string ViradaDoDia)
{
    /// <summary>A versão deste formato. Muda quando o documento mudar de forma.</summary>
    public const int FormatoAtual = 1;

    private const string Hora = "HH':'mm";

    private const string Duracao = "hh':'mm";

    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
    };

    public static string Json(HorarioContratual horario)
    {
        var semana = new Dictionary<int, string[][]>();

        foreach (var dia in Enum.GetValues<DayOfWeek>())
        {
            var periodos = horario.Do(dia);

            if (periodos.Count > 0)
            {
                semana[Iso(dia)] = [.. periodos.Select(periodo => new[] { periodo.Entrada.ToString(Hora, CultureInfo.InvariantCulture), periodo.Saida.ToString(Hora, CultureInfo.InvariantCulture) })];
            }
        }

        var documento = new DocumentoDoHorario(
            FormatoAtual,
            semana,
            Iso(horario.DescansoSemanal),
            horario.IntervaloPreAssinalado,
            horario.IntervaloMinimo == HorarioContratual.IntervaloDaLei ? null : horario.IntervaloMinimo.ToString(Duracao, CultureInfo.InvariantCulture),
            horario.ViradaDoDia.ToString(Hora, CultureInfo.InvariantCulture));

        return JsonSerializer.Serialize(documento, Opcoes);
    }

    public static HorarioContratual Horario(string json)
    {
        var documento = JsonSerializer.Deserialize<DocumentoDoHorario>(json, Opcoes)
            ?? throw new JsonException("O horário gravado está vazio.");

        if (documento.Formato != FormatoAtual)
        {
            throw new JsonException($"O horário gravado está no formato {documento.Formato}, que esta versão não lê.");
        }

        var semana = documento.Semana.ToDictionary(
            par => DoIso(par.Key),
            par => (IReadOnlyList<Periodo>)[.. par.Value.Select(periodo => periodo.Length == 2
                ? new Periodo(TimeOnly.ParseExact(periodo[0], Hora, CultureInfo.InvariantCulture), TimeOnly.ParseExact(periodo[1], Hora, CultureInfo.InvariantCulture))
                : throw new JsonException("Cada período do horário tem a entrada e a saída."))]);

        return HorarioContratual.De(
            semana,
            DoIso(documento.DescansoSemanal),
            documento.IntervaloPreAssinalado,
            documento.IntervaloNegociado is { } negociado ? TimeSpan.ParseExact(negociado, Duracao, CultureInfo.InvariantCulture) : null,
            TimeOnly.ParseExact(documento.ViradaDoDia, Hora, CultureInfo.InvariantCulture));
    }

    /// <summary>A segunda é 1, e o domingo, 7, como na ISO 8601.</summary>
    private static int Iso(DayOfWeek dia) => dia == DayOfWeek.Sunday ? 7 : (int)dia;

    private static DayOfWeek DoIso(int dia) => dia is >= 1 and <= 7
        ? (dia == 7 ? DayOfWeek.Sunday : (DayOfWeek)dia)
        : throw new JsonException($"O dia {dia} não é um dia da semana: de 1 (segunda) a 7 (domingo).");
}
