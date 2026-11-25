using System.Diagnostics.CodeAnalysis;
using PontoFacial.Dominio.Biometria;

namespace PontoFacial.Rosto;

/// <summary>Por que a imagem não deu um rosto para comparar.</summary>
public enum ProblemaNaCaptura
{
    ImagemIlegivel = 1,

    NenhumRosto = 2,

    MaisDeUmRosto = 3,

    /// <summary>O rosto é pequeno demais na imagem para o reconhecimento valer.</summary>
    RostoLonge = 4,

    /// <summary>Algum dos cinco pontos do rosto caiu fora da imagem.</summary>
    RostoCortado = 5,
}

/// <summary>
/// O que saiu de uma imagem: o vetor do rosto e onde ele estava, ou o
/// problema, com a frase para quem está diante da câmera.
/// </summary>
public sealed class ResultadoDaCaptura
{
    private ResultadoDaCaptura(ModeloFacial? modelo, RostoDetectado? rosto, ProblemaNaCaptura? problema)
    {
        Modelo = modelo;
        Rosto = rosto;
        Problema = problema;
    }

    public ModeloFacial? Modelo { get; }

    public RostoDetectado? Rosto { get; }

    public ProblemaNaCaptura? Problema { get; }

    [MemberNotNullWhen(true, nameof(Modelo), nameof(Rosto))]
    public bool Capturou => Problema is null;

    /// <summary>O que dizer a quem está diante da câmera.</summary>
    public string? Mensagem => Problema switch
    {
        null => null,
        ProblemaNaCaptura.ImagemIlegivel => "A imagem não pôde ser lida. Tente de novo.",
        ProblemaNaCaptura.NenhumRosto => "Nenhum rosto na imagem. Olhe para a câmera, com o rosto inteiro aparecendo.",
        ProblemaNaCaptura.MaisDeUmRosto => "Há mais de um rosto na imagem: só quem vai marcar o ponto deve aparecer.",
        ProblemaNaCaptura.RostoLonge => "O rosto está longe: chegue mais perto da câmera.",
        ProblemaNaCaptura.RostoCortado => "O rosto está cortado na borda da imagem: centralize o rosto.",
        _ => throw new InvalidOperationException($"Problema sem mensagem: {Problema}."),
    };

    internal static ResultadoDaCaptura Sucesso(ModeloFacial modelo, RostoDetectado rosto) => new(modelo, rosto, null);

    internal static ResultadoDaCaptura Falha(ProblemaNaCaptura problema, RostoDetectado? rosto = null) => new(null, rosto, problema);
}
