using Npgsql;

namespace PontoFacial.Persistencia;

/// <summary>
/// Como um instante com fuso vai para o banco e volta: o instante em UTC,
/// numa coluna <c>timestamptz</c>, e o deslocamento em minutos, na coluna ao
/// lado.
/// </summary>
/// <remarks>
/// O <c>timestamptz</c> guarda o instante, e não o fuso em que ele foi
/// escrito. Para o registro de ponto, o fuso importa: o hash da marcação foi
/// calculado sobre <c>07:58:00-0300</c>, e não sobre <c>10:58:00+0000</c>.
/// Refazer o fuso pela base IANA na leitura dependeria da base não mudar o
/// passado; o deslocamento gravado, não.
/// </remarks>
internal static class Instantes
{
    public static DateTimeOffset Utc(DateTimeOffset instante) => instante.ToUniversalTime();

    public static object? Utc(DateTimeOffset? instante) => instante?.ToUniversalTime();

    public static short Deslocamento(DateTimeOffset instante) => checked((short)instante.Offset.TotalMinutes);

    public static object? Deslocamento(DateTimeOffset? instante) => instante is { } valor ? Deslocamento(valor) : null;

    public static DateTimeOffset Ler(NpgsqlDataReader linha, int instante, int deslocamento) =>
        linha.GetFieldValue<DateTimeOffset>(instante).ToOffset(TimeSpan.FromMinutes(linha.GetInt16(deslocamento)));

    public static DateTimeOffset? LerOuNulo(NpgsqlDataReader linha, int instante, int deslocamento) =>
        linha.IsDBNull(instante) ? null : Ler(linha, instante, deslocamento);
}
