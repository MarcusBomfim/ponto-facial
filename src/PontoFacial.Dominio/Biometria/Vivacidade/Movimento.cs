namespace PontoFacial.Dominio.Biometria.Vivacidade;

/// <summary>
/// Um movimento da cabeça que a prova de vida pede. Esquerda e direita são as
/// da pessoa: quem vira para a própria esquerda leva o nariz para a direita
/// da imagem da câmera, que não é espelhada.
/// </summary>
public enum Movimento
{
    Esquerda = 1,

    Direita = 2,

    Cima = 3,

    Baixo = 4,
}

/// <summary>O que dizer a quem está diante da câmera em cada passo.</summary>
public static class Instrucoes
{
    public const string OlheParaACamera = "Olhe para a câmera.";

    public const string VolteParaAFrente = "Volte a olhar para a câmera.";

    public static string Do(Movimento movimento) => movimento switch
    {
        Movimento.Esquerda => "Vire o rosto devagar para a sua esquerda.",
        Movimento.Direita => "Vire o rosto devagar para a sua direita.",
        Movimento.Cima => "Levante o rosto devagar, olhando para cima.",
        Movimento.Baixo => "Abaixe o rosto devagar, olhando para baixo.",
        _ => throw new ArgumentOutOfRangeException(nameof(movimento), movimento, "Movimento desconhecido."),
    };

    /// <summary>Esquerda e direita: o giro em que o relevo do nariz aparece de lado.</summary>
    public static bool EhLateral(this Movimento movimento) => movimento is Movimento.Esquerda or Movimento.Direita;
}
