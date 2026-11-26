namespace PontoFacial.Dominio.Biometria.Vivacidade;

/// <summary>Um movimento do desafio, feito: o relevo medido nele e quanto tempo levou.</summary>
/// <param name="Movimento">O movimento pedido.</param>
/// <param name="Relevo">A mediana do relevo nos quadros em que o rosto estava virado o bastante.</param>
/// <param name="QuadrosMedidos">Em quantos quadros o relevo foi medido.</param>
/// <param name="Duracao">Do pedido à volta para a frente.</param>
public sealed record MovimentoFeito(Movimento Movimento, double Relevo, int QuadrosMedidos, TimeSpan Duracao);

/// <summary>
/// O resultado de uma sessão de prova de vida: aprovada, com os vetores do
/// rosto do começo e do fim, ou recusada, com o motivo.
/// </summary>
/// <remarks>
/// Os dois vetores vêm de quadros da mesma sessão, ligados pelo
/// acompanhamento do rosto: o que se compara com o cadastro é o rosto que fez
/// os movimentos. Eles só existem na prova aprovada.
/// </remarks>
public sealed class ProvaDeVida
{
    internal ProvaDeVida(
        Desafio desafio,
        CriterioDaProvaDeVida criterio,
        DateTimeOffset inicio,
        DateTimeOffset em,
        IReadOnlyList<MovimentoFeito> movimentos,
        double? menorRelevo,
        MotivoDaRecusa? motivo,
        ModeloFacial? modeloInicial,
        ModeloFacial? modeloFinal)
    {
        Desafio = desafio;
        Criterio = criterio;
        Inicio = inicio;
        Em = em;
        Movimentos = movimentos;
        MenorRelevo = menorRelevo;
        Motivo = motivo;
        ModeloInicial = modeloInicial;
        ModeloFinal = modeloFinal;
    }

    public Desafio Desafio { get; }

    public CriterioDaProvaDeVida Criterio { get; }

    /// <summary>O instante do quadro de referência.</summary>
    public DateTimeOffset Inicio { get; }

    /// <summary>O instante do último quadro: o da marcação, quando a prova é aprovada.</summary>
    public DateTimeOffset Em { get; }

    /// <summary>Os movimentos feitos até a sessão terminar.</summary>
    public IReadOnlyList<MovimentoFeito> Movimentos { get; }

    /// <summary>O menor relevo entre os movimentos, quando todos foram feitos; é o que o critério julga.</summary>
    public double? MenorRelevo { get; }

    public MotivoDaRecusa? Motivo { get; }

    public bool Aprovada => Motivo is null;

    /// <summary>O que dizer a quem está diante da câmera, quando a prova não passou.</summary>
    public string? Mensagem => Motivo is { } motivo ? MensagensDaRecusa.Da(motivo) : null;

    /// <summary>O vetor do rosto no quadro de referência, de frente, antes dos movimentos.</summary>
    public ModeloFacial? ModeloInicial { get; }

    /// <summary>O vetor do rosto no último quadro, de frente, depois dos movimentos.</summary>
    public ModeloFacial? ModeloFinal { get; }
}
