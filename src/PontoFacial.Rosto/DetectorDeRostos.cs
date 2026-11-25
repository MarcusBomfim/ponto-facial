using Microsoft.ML.OnnxRuntime;

namespace PontoFacial.Rosto;

/// <summary>
/// Acha os rostos de uma imagem com o YuNet: a caixa, os cinco pontos e a
/// confiança de cada um.
/// </summary>
/// <remarks>
/// O modelo recebe a imagem como ela é — azul, verde e vermelho de 0 a 255,
/// sem normalizar —, com a largura e a altura completadas com preto até
/// múltiplos de 32, como o OpenCV faz. Ele acha rostos de uns 10 a 300 pixels;
/// quem chama reduz a imagem antes, se o rosto puder ser maior que isso.
/// </remarks>
public sealed class DetectorDeRostos : IDisposable
{
    public const string ArquivoDoModelo = "face_detection_yunet_2026may.onnx";

    /// <summary>A confiança abaixo da qual a caixa não é rosto. A do OpenCV para uso real.</summary>
    public const float ConfiancaPadrao = 0.9f;

    /// <summary>Acima desta sobreposição, a caixa menos confiante é o mesmo rosto.</summary>
    public const float SobreposicaoMaxima = 0.3f;

    private const int Divisor = 32;

    private readonly InferenceSession _sessao;

    private readonly float _confiancaMinima;

    public DetectorDeRostos(string caminhoDoModelo, float confiancaMinima = ConfiancaPadrao, int threads = 0)
    {
        _sessao = new InferenceSession(caminhoDoModelo, Sessoes.Opcoes(threads));
        _confiancaMinima = confiancaMinima;
    }

    public IReadOnlyList<RostoDetectado> Detectar(Imagem imagem)
    {
        ArgumentNullException.ThrowIfNull(imagem);

        var largura = (((imagem.Largura - 1) / Divisor) + 1) * Divisor;
        var altura = (((imagem.Altura - 1) / Divisor) + 1) * Divisor;

        // Plano a plano — azul, verde, vermelho —; o que sobra além da imagem fica em zero.
        var entrada = new float[3 * largura * altura];
        var plano = largura * altura;

        for (int y = 0, origem = 0; y < imagem.Altura; y++)
        {
            for (var x = 0; x < imagem.Largura; x++, origem += 3)
            {
                var destino = (y * largura) + x;
                entrada[destino] = imagem.Bgr[origem];
                entrada[plano + destino] = imagem.Bgr[origem + 1];
                entrada[(2 * plano) + destino] = imagem.Bgr[origem + 2];
            }
        }

        using var tensor = OrtValue.CreateTensorValueFromMemory(entrada, [1, 3, altura, largura]);
        using var opcoes = new RunOptions();
        using var saidas = _sessao.Run(opcoes, ["input"], [tensor], NomesDasSaidas);

        float[] Saida(int indice) => saidas[indice].GetTensorDataAsSpan<float>().ToArray();

        var porPasso = DecodificacaoDoYuNet.Passos
            .Select((passo, i) => new DecodificacaoDoYuNet.SaidasDoPasso(passo, Saida(i), Saida(i + 3), Saida(i + 6), Saida(i + 9)))
            .ToList();

        return DecodificacaoDoYuNet.Decodificar(porPasso, largura, altura, _confiancaMinima, SobreposicaoMaxima).AsReadOnly();
    }

    public void Dispose() => _sessao.Dispose();

    private static readonly string[] NomesDasSaidas =
    [
        "cls_8", "cls_16", "cls_32",
        "obj_8", "obj_16", "obj_32",
        "bbox_8", "bbox_16", "bbox_32",
        "kps_8", "kps_16", "kps_32",
    ];
}
