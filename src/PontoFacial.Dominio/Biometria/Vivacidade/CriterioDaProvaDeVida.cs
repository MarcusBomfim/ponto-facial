using System.Globalization;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Biometria.Vivacidade;

/// <summary>
/// O menor relevo do nariz com que a prova de vida aceita o rosto, com a
/// origem do valor.
/// </summary>
/// <remarks>
/// Como o limiar de aceitação do rosto, o critério decide entre dois erros:
/// aceitar um ataque — uma foto, uma tela — e recusar uma pessoa. Ele sai de
/// um ensaio (<see cref="Ensaio.EnsaioDaProvaDeVida"/>), e a origem diz qual.
/// </remarks>
public sealed class CriterioDaProvaDeVida
{
    /// <summary>A origem cabe em 200 caracteres.</summary>
    public const int TamanhoDaOrigem = 200;

    private CriterioDaProvaDeVida(double relevoMinimo, string origem)
    {
        RelevoMinimo = relevoMinimo;
        Origem = origem;
    }

    /// <summary>O menor relevo aceito, em meias distâncias entre os olhos, acima de 0 e até 2.</summary>
    public double RelevoMinimo { get; }

    /// <summary>De onde o valor saiu: o ensaio, o erro tolerado, a data.</summary>
    public string Origem { get; }

    public static CriterioDaProvaDeVida De(double relevoMinimo, string origem)
    {
        if (!double.IsFinite(relevoMinimo) || relevoMinimo <= 0 || relevoMinimo > 2)
        {
            throw new ExcecaoDeDominio(string.Create(
                CultureInfo.GetCultureInfo("pt-BR"),
                $"O relevo mínimo fica acima de 0 e até 2 meias distâncias entre os olhos; veio {relevoMinimo}."));
        }

        return new CriterioDaProvaDeVida(relevoMinimo, Textos.Obrigatorio(origem, "a origem do critério", TamanhoDaOrigem));
    }

    /// <summary>O relevo igual ao mínimo é aceito.</summary>
    public bool Aceita(double relevo) => relevo >= RelevoMinimo;
}
