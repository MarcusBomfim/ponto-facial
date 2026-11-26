namespace PontoFacial.Dominio.Biometria.Vivacidade;

/// <summary>
/// Um quadro da câmera durante a prova de vida, como chega ao domínio: o
/// instante, os pontos do rosto acompanhados até ali e, quando a sessão
/// pede, o vetor do rosto. A imagem não chega.
/// </summary>
/// <param name="Em">O instante em que a câmera fez o quadro.</param>
/// <param name="Pontos">Os pontos acompanhados; nada quando o rosto se perdeu.</param>
/// <param name="Modelo">O vetor do rosto deste quadro, quando a sessão o pediu.</param>
public sealed record QuadroDaCamera(DateTimeOffset Em, PontosDoRosto? Pontos, ModeloFacial? Modelo = null);
