namespace PontoFacial.Rosto;

/// <summary>Qual rosto tomar quando a imagem tem mais de um.</summary>
public enum EscolhaDoRosto
{
    /// <summary>Só vale a imagem com um rosto. É a regra da marcação e do cadastro.</summary>
    SoUm = 1,

    /// <summary>O mais perto do centro. Para bases de imagens como a LFW, que trazem gente ao fundo.</summary>
    OMaisCentral = 2,
}

/// <summary>
/// Da imagem ao vetor do rosto: lê, acha o rosto numa cópia reduzida,
/// confere que dá para reconhecer e extrai o vetor do rosto alinhado na
/// imagem inteira.
/// </summary>
/// <remarks>
/// A detecção roda com o lado maior em 320 pixels: o YuNet acha rostos de uns
/// 10 a 300 pixels, e o rosto de uma selfie em resolução cheia passa disso. O
/// alinhamento usa a imagem inteira, para o recorte de 112×112 não sair de
/// uma imagem já reduzida.
/// </remarks>
public sealed class ReconhecedorFacial : IDisposable
{
    public const int LadoDaDeteccao = 320;

    /// <summary>
    /// A menor distância entre os olhos, em pixels da imagem original. No
    /// recorte do SFace os olhos ficam a 35 pixels; um rosto menor que isso
    /// teria de ser ampliado, e o vetor sairia de pixels inventados.
    /// </summary>
    public const float DistanciaMinimaEntreOlhos = 30f;

    private readonly DetectorDeRostos _detector;

    private readonly ExtratorDoSFace _extrator;

    /// <param name="pastaDosModelos">Onde estão os dois arquivos ONNX.</param>
    /// <param name="threads">Núcleos por inferência; zero deixa o ONNX Runtime decidir.</param>
    public ReconhecedorFacial(string pastaDosModelos, int threads = 0)
    {
        _detector = new DetectorDeRostos(Path.Combine(pastaDosModelos, DetectorDeRostos.ArquivoDoModelo), threads: threads);
        _extrator = new ExtratorDoSFace(Path.Combine(pastaDosModelos, ExtratorDoSFace.ArquivoDoModelo), threads);
    }

    /// <summary>A pasta que o build preenche ao lado do programa.</summary>
    public static string PastaPadrao => Path.Combine(AppContext.BaseDirectory, "modelos");

    public static string Reconhecedor => ExtratorDoSFace.Reconhecedor;

    public ResultadoDaCaptura Capturar(ReadOnlySpan<byte> arquivo, EscolhaDoRosto escolha = EscolhaDoRosto.SoUm) =>
        Imagem.TentarDecodificar(arquivo, out var imagem)
            ? Capturar(imagem!, escolha)
            : ResultadoDaCaptura.Falha(ProblemaNaCaptura.ImagemIlegivel);

    public ResultadoDaCaptura Capturar(Imagem imagem, EscolhaDoRosto escolha = EscolhaDoRosto.SoUm)
    {
        ArgumentNullException.ThrowIfNull(imagem);

        var reduzida = imagem.Reduzida(LadoDaDeteccao);
        var fator = (float)imagem.Largura / reduzida.Largura;
        var rostos = _detector.Detectar(reduzida);

        if (rostos.Count == 0)
        {
            return ResultadoDaCaptura.Falha(ProblemaNaCaptura.NenhumRosto);
        }

        if (rostos.Count > 1 && escolha == EscolhaDoRosto.SoUm)
        {
            return ResultadoDaCaptura.Falha(ProblemaNaCaptura.MaisDeUmRosto);
        }

        var centro = new Ponto(reduzida.Largura / 2f, reduzida.Altura / 2f);
        var rosto = rostos
            .MinBy(r => MathF.Pow(r.Centro.X - centro.X, 2) + MathF.Pow(r.Centro.Y - centro.Y, 2))!
            .Escalado(fator);

        if (rosto.Pontos.Any(ponto => ponto.X < 0 || ponto.Y < 0 || ponto.X >= imagem.Largura || ponto.Y >= imagem.Altura))
        {
            return ResultadoDaCaptura.Falha(ProblemaNaCaptura.RostoCortado, rosto);
        }

        if (rosto.DistanciaEntreOlhos < DistanciaMinimaEntreOlhos)
        {
            return ResultadoDaCaptura.Falha(ProblemaNaCaptura.RostoLonge, rosto);
        }

        return ResultadoDaCaptura.Sucesso(_extrator.Extrair(imagem, rosto), rosto);
    }

    public void Dispose()
    {
        _detector.Dispose();
        _extrator.Dispose();
    }
}
