using System.Globalization;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Biometria;

/// <summary>
/// A menor similaridade com que o rosto capturado é aceito como o do
/// cadastro, para um reconhecedor, com a origem do valor.
/// </summary>
/// <remarks>
/// O limiar decide entre os dois erros do reconhecimento: aceitar quem não é
/// (falsa aceitação) e recusar quem é (falsa rejeição). Subir o limiar troca
/// um pelo outro. Por isso ele não é um número solto no código: vem de uma
/// calibração, e a origem diz qual (ADR-0004).
/// </remarks>
public sealed class LimiarDeAceitacao
{
    /// <summary>A origem cabe em 200 caracteres.</summary>
    public const int TamanhoDaOrigem = 200;

    private LimiarDeAceitacao(string reconhecedor, double similaridade, string origem)
    {
        Reconhecedor = reconhecedor;
        Similaridade = similaridade;
        Origem = origem;
    }

    public string Reconhecedor { get; }

    /// <summary>A menor similaridade aceita, entre -1 e 1.</summary>
    public double Similaridade { get; }

    /// <summary>De onde o valor saiu: a base de imagens, a falsa aceitação escolhida, a data.</summary>
    public string Origem { get; }

    public static LimiarDeAceitacao De(string reconhecedor, double similaridade, string origem)
    {
        if (!double.IsFinite(similaridade) || similaridade <= -1 || similaridade > 1)
        {
            throw new ExcecaoDeDominio(string.Create(
                CultureInfo.GetCultureInfo("pt-BR"),
                $"O limiar é uma similaridade acima de -1 e até 1; veio {similaridade}."));
        }

        return new LimiarDeAceitacao(
            Textos.Obrigatorio(reconhecedor, "o nome do reconhecedor", ModeloFacial.TamanhoDoReconhecedor),
            similaridade,
            Textos.Obrigatorio(origem, "a origem do limiar", TamanhoDaOrigem));
    }

    /// <summary>A similaridade igual ao limiar é aceita.</summary>
    public bool Aceita(double similaridade) => similaridade >= Similaridade;
}
