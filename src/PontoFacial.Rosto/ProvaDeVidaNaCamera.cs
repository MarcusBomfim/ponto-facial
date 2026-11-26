using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Biometria.Vivacidade;

namespace PontoFacial.Rosto;

/// <summary>
/// A prova de vida com os quadros da câmera: espera o rosto de frente, tira
/// o vetor dele, acompanha os pontos a cada quadro e entrega à sessão do
/// domínio, que decide.
/// </summary>
/// <remarks>
/// <para>
/// Enquanto a prova não começou, cada quadro é uma tentativa de captura: um
/// rosto só, perto, inteiro e de frente. O primeiro que serve é a
/// referência.
/// </para>
/// <para>
/// Depois, o detector continua rodando a cada quadro, mas só para conferir o
/// acompanhamento: se ele acha rosto e nenhum tem os pontos acompanhados
/// dentro da caixa, os pontos se soltaram do rosto e a prova se perde. A
/// conferência é pela caixa, e não pelos pontos do detector: com o rosto
/// virado, eles erram até meia distância entre os olhos. Quadro sem rosto
/// detectado — o detector falha com o rosto muito virado — não derruba
/// nada; o acompanhamento segue.
/// </para>
/// </remarks>
public sealed class ProvaDeVidaNaCamera
{
    /// <summary>
    /// Quanto a caixa do rosto detectado cresce, de cada lado, em frações do
    /// tamanho dela, para conter os pontos acompanhados.
    /// </summary>
    public const double FolgaDaCaixa = 0.15;

    private readonly ReconhecedorFacial _reconhecedor;

    private readonly Desafio _desafio;

    private readonly CriterioDaProvaDeVida _criterio;

    private AcompanhamentoDoRosto? _acompanhamento;

    private SessaoDaProvaDeVida? _sessao;

    public ProvaDeVidaNaCamera(ReconhecedorFacial reconhecedor, Desafio desafio, CriterioDaProvaDeVida criterio)
    {
        ArgumentNullException.ThrowIfNull(reconhecedor);
        ArgumentNullException.ThrowIfNull(desafio);
        ArgumentNullException.ThrowIfNull(criterio);

        _reconhecedor = reconhecedor;
        _desafio = desafio;
        _criterio = criterio;
    }

    public bool Comecou => _sessao is not null;

    /// <summary>O que mostrar na tela agora; nada quando terminou.</summary>
    public string? Instrucao => _sessao is null ? Instrucoes.OlheParaACamera : _sessao.Instrucao;

    /// <summary>O movimento que a pessoa deve fazer agora, para a tela mostrar a seta; nada quando é para olhar de frente.</summary>
    public Movimento? MovimentoPedido => _sessao?.MovimentoPedido;

    public ProvaDeVida? Resultado => _sessao?.Resultado;

    public bool Terminou => Resultado is not null;

    public void Processar(Imagem quadro, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(quadro);

        if (Terminou)
        {
            throw new InvalidOperationException("A prova de vida já terminou.");
        }

        if (_sessao is null)
        {
            TentarComecar(quadro, em);
            return;
        }

        var pontos = _acompanhamento!.Avancar(quadro);
        ModeloFacial? modelo = null;

        if (pontos is not null)
        {
            var rostos = _reconhecedor.Detectar(quadro);
            var proprio = rostos
                .Where(rosto => ContemOsPontos(rosto, pontos))
                .MinBy(rosto => Distancia(rosto.OlhoDireito, pontos.OlhoDireito) + Distancia(rosto.OlhoEsquerdo, pontos.OlhoEsquerdo));

            if (rostos.Count > 0 && proprio is null)
            {
                pontos = null;
            }
            else if (_sessao.PedeModelo && proprio is not null)
            {
                modelo = _reconhecedor.Extrair(quadro, proprio);
            }
        }

        _sessao.Registrar(new QuadroDaCamera(em, pontos, modelo));
    }

    private void TentarComecar(Imagem quadro, DateTimeOffset em)
    {
        var captura = _reconhecedor.Capturar(quadro);

        if (!captura.Capturou)
        {
            return;
        }

        var rosto = captura.Rosto;
        var pontos = PontosDoRosto.ParaAcompanhar(
            Ponto(rosto.OlhoDireito),
            Ponto(rosto.OlhoEsquerdo),
            Ponto(rosto.Nariz),
            Ponto(rosto.BocaDireita),
            Ponto(rosto.BocaEsquerda));

        if (!SessaoDaProvaDeVida.EstaDeFrente(pontos))
        {
            return;
        }

        _sessao = SessaoDaProvaDeVida.Iniciar(_desafio, _criterio, new QuadroDaCamera(em, pontos, captura.Modelo));
        _acompanhamento = new AcompanhamentoDoRosto(quadro, pontos);
    }

    /// <summary>Os cinco pontos do detector, acompanhados, caem dentro da caixa do rosto detectado, com a folga.</summary>
    internal static bool ContemOsPontos(RostoDetectado rosto, PontosDoRosto pontos)
    {
        var folgaX = FolgaDaCaixa * rosto.Largura;
        var folgaY = FolgaDaCaixa * rosto.Altura;

        bool Dentro(PontoNaImagem ponto) =>
            ponto.X >= rosto.X - folgaX && ponto.X <= rosto.X + rosto.Largura + folgaX
            && ponto.Y >= rosto.Y - folgaY && ponto.Y <= rosto.Y + rosto.Altura + folgaY;

        return Dentro(pontos.OlhoDireito) && Dentro(pontos.OlhoEsquerdo) && Dentro(pontos.Nariz)
            && Dentro(pontos.BocaDireita) && Dentro(pontos.BocaEsquerda);
    }

    private static double Distancia(Ponto detectado, PontoNaImagem acompanhado) =>
        Math.Sqrt(((detectado.X - acompanhado.X) * (detectado.X - acompanhado.X)) + ((detectado.Y - acompanhado.Y) * (detectado.Y - acompanhado.Y)));

    private static PontoNaImagem Ponto(Ponto ponto) => new(ponto.X, ponto.Y);
}
