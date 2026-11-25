using Microsoft.ML.OnnxRuntime;
using PontoFacial.Dominio.Biometria;

namespace PontoFacial.Rosto;

/// <summary>
/// Transforma o rosto alinhado nos 128 números do SFace: o
/// <see cref="ModeloFacial"/> que o domínio cadastra e compara.
/// </summary>
public sealed class ExtratorDoSFace : IDisposable
{
    public const string ArquivoDoModelo = "face_recognition_sface_2021dec.onnx";

    /// <summary>O nome com que os vetores deste modelo são guardados e comparados.</summary>
    public const string Reconhecedor = "sface-2021dec";

    private readonly InferenceSession _sessao;

    public ExtratorDoSFace(string caminhoDoModelo, int threads = 0) =>
        _sessao = new InferenceSession(caminhoDoModelo, Sessoes.Opcoes(threads));

    public ModeloFacial Extrair(Imagem imagem, RostoDetectado rosto)
    {
        ArgumentNullException.ThrowIfNull(imagem);
        ArgumentNullException.ThrowIfNull(rosto);

        var entrada = Alinhamento.RecortarParaOSFace(imagem, Alinhamento.Estimar(rosto.Pontos));

        using var tensor = OrtValue.CreateTensorValueFromMemory(entrada, [1, 3, Alinhamento.Lado, Alinhamento.Lado]);
        using var opcoes = new RunOptions();
        using var saidas = _sessao.Run(opcoes, ["data"], [tensor], ["fc1"]);

        return ModeloFacial.De(Reconhecedor, saidas[0].GetTensorDataAsSpan<float>());
    }

    public void Dispose() => _sessao.Dispose();
}
