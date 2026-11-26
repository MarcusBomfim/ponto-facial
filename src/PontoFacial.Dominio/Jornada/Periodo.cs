using System.Globalization;

namespace PontoFacial.Dominio.Jornada;

/// <summary>
/// Um período do horário de trabalho, de uma hora a outra do relógio. Se a
/// saída é igual ou anterior à entrada, o período passa da meia-noite: das
/// 22h às 5h é um período só.
/// </summary>
public readonly record struct Periodo(TimeOnly Entrada, TimeOnly Saida)
{
    /// <summary>A duração de relógio, sem a hora noturna reduzida.</summary>
    public TimeSpan Duracao => Saida - Entrada;

    public bool PassaDaMeiaNoite => Saida <= Entrada;

    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Entrada:HH\\:mm}–{Saida:HH\\:mm}");
}
