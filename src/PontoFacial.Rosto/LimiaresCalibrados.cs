using PontoFacial.Dominio.Biometria;

namespace PontoFacial.Rosto;

/// <summary>
/// Os limiares que saíram das calibrações, cada um com o relatório em
/// <c>docs/calibracao</c>. São o ponto de partida: a empresa recalibra com as
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
}
