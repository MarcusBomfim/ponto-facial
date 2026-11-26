using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Biometria.Vivacidade;

namespace PontoFacial.Rosto;

/// <summary>
/// Os limiares que saíram das calibrações e dos ensaios, cada um com o
/// relatório em <c>docs/calibracao</c> ou <c>docs/ensaio</c>. São o ponto de partida: a empresa recalibra com as
/// capturas do seu próprio pessoal (ADR-0006).
/// </summary>
public static class LimiaresCalibrados
{
    /// <summary>
    /// O SFace na LFW inteira, para falsa aceitação de 1:10.000: falsa
    /// rejeição de 0,83% em 230.802 comparações da mesma pessoa
    /// (<c>docs/calibracao/lfw-sface-2021dec.md</c>).
    /// </summary>
    public static LimiarDeAceitacao SFaceNaLfw { get; } = LimiarDeAceitacao.De(
        ExtratorDoSFace.Reconhecedor,
        0.4317,
        "lfw (13.233 imagens), falsa aceitação 1:10.000, 2026-11-24");

    /// <summary>
    /// O relevo mínimo da prova de vida, do ensaio simulado com fotos da LFW:
    /// APCER de até 1% na pior das oito espécies de ataque, a foto curvada na
    /// diagonal (<c>docs/ensaio/prova-de-vida-lfw.md</c>). A falsa rejeição
    /// não foi medida: o ensaio não teve gente de verdade (ADR-0008).
    /// </summary>
    public static CriterioDaProvaDeVida ProvaDeVidaNaLfw { get; } = CriterioDaProvaDeVida.De(
        0.157,
        "ensaio simulado na lfw (300 por espécie, 8 espécies), APCER 1%, 2026-11-25");
}
