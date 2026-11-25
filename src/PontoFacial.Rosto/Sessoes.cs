using Microsoft.ML.OnnxRuntime;

namespace PontoFacial.Rosto;

/// <summary>As opções comuns às sessões do ONNX Runtime.</summary>
internal static class Sessoes
{
    /// <param name="threads">
    /// Quantos núcleos cada inferência usa; zero deixa o ONNX Runtime decidir.
    /// Quem roda muitas imagens em paralelo, como a calibração, usa um por
    /// sessão e paraleliza por imagem.
    /// </param>
    public static SessionOptions Opcoes(int threads)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(threads);

        return new SessionOptions
        {
            // O SFace foi exportado com os pesos listados como entradas do
            // grafo; o ONNX Runtime avisa disso a cada carga. Só os erros
            // interessam.
            LogSeverityLevel = OrtLoggingLevel.ORT_LOGGING_LEVEL_ERROR,
            IntraOpNumThreads = threads,
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
        };
    }
}
