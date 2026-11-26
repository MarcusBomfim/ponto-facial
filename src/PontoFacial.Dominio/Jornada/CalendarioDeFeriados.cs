using System.Globalization;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Jornada;

/// <summary>
/// Os feriados de um estabelecimento: os nacionais, que a lei federal
/// declara, e os do estado e do município, que a empresa cadastra.
/// </summary>
/// <remarks>
/// <para>
/// Os nacionais são os da Lei 662/1949, com a redação da Lei 10.607/2002
/// (1º de janeiro, 21 de abril, 1º de maio, 7 de setembro, 2 e 15 de
/// novembro e 25 de dezembro), o 12 de outubro da Lei 6.802/1980 e o 20 de
/// novembro da Lei 14.759/2023, feriado nacional desde 2024.
/// </para>
/// <para>
/// A Sexta-feira da Paixão não está entre eles: pela Lei 9.093/1995, é um
/// dos feriados religiosos que cada município declara. Por isso ela entra
/// como feriado local, e <see cref="SextaFeiraDaPaixao"/> diz a data.
/// </para>
/// </remarks>
public sealed class CalendarioDeFeriados
{
    /// <summary>O nome do feriado cabe em 80 caracteres.</summary>
    public const int TamanhoDoNome = 80;

    /// <summary>
    /// Os feriados das leis em vigor. Só o 20 de novembro tem ano de início,
    /// por ser recente: a Lei 14.759 é de 21 de dezembro de 2023.
    /// </summary>
    private static readonly (int Mes, int Dia, string Nome, int DesdeOAno)[] DaLeiFederal =
    [
        (1, 1, "Confraternização Universal", 1),
        (4, 21, "Tiradentes", 1),
        (5, 1, "Dia do Trabalho", 1),
        (9, 7, "Independência do Brasil", 1),
        (10, 12, "Nossa Senhora Aparecida", 1),
        (11, 2, "Finados", 1),
        (11, 15, "Proclamação da República", 1),
        (11, 20, "Dia Nacional de Zumbi e da Consciência Negra", 2024),
        (12, 25, "Natal", 1),
    ];

    private readonly Dictionary<DateOnly, string> _locais;

    private CalendarioDeFeriados(Dictionary<DateOnly, string> locais) => _locais = locais;

    /// <summary>Só os feriados nacionais.</summary>
    public static CalendarioDeFeriados Nacionais { get; } = new([]);

    /// <summary>Os feriados locais, cada um com a data e o nome.</summary>
    public IReadOnlyDictionary<DateOnly, string> Locais => _locais.AsReadOnly();

    /// <summary>Este calendário com mais feriados do estado ou do município.</summary>
    public CalendarioDeFeriados ComLocais(IEnumerable<(DateOnly Data, string Nome)> feriados)
    {
        ArgumentNullException.ThrowIfNull(feriados);

        var locais = new Dictionary<DateOnly, string>(_locais);

        foreach (var (data, nome) in feriados)
        {
            var limpo = Textos.Obrigatorio(nome, "o nome do feriado", TamanhoDoNome);

            if (Nacional(data) is { } nacional)
            {
                throw new ExcecaoDeDominio(string.Create(
                    CultureInfo.GetCultureInfo("pt-BR"),
                    $"{data:dd/MM/yyyy} já é feriado nacional: {nacional}."));
            }

            if (!locais.TryAdd(data, limpo))
            {
                throw new ExcecaoDeDominio(string.Create(
                    CultureInfo.GetCultureInfo("pt-BR"),
                    $"{data:dd/MM/yyyy} já está no calendário como {locais[data]}."));
            }
        }

        return new CalendarioDeFeriados(locais);
    }

    /// <summary>O nome do feriado na data, nacional ou local; nada se é dia comum.</summary>
    public string? FeriadoEm(DateOnly data) => Nacional(data) ?? (_locais.TryGetValue(data, out var nome) ? nome : null);

    /// <summary>
    /// A Sexta-feira da Paixão do ano: dois dias antes da Páscoa, pelo
    /// cálculo gregoriano de Meeus, Jones e Butcher.
    /// </summary>
    public static DateOnly SextaFeiraDaPaixao(int ano)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(ano, 1583);

        var a = ano % 19;
        var b = ano / 100;
        var c = ano % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var mes = (h + l - (7 * m) + 114) / 31;
        var dia = ((h + l - (7 * m) + 114) % 31) + 1;

        return new DateOnly(ano, mes, dia).AddDays(-2);
    }

    private static string? Nacional(DateOnly data) =>
        DaLeiFederal.FirstOrDefault(feriado => feriado.Mes == data.Month && feriado.Dia == data.Day && data.Year >= feriado.DesdeOAno).Nome;
}
