using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Biometria.Vivacidade;

/// <summary>
/// Uma tentativa de prova de vida, quadro a quadro: do rosto de frente,
/// pelos movimentos sorteados, de volta à frente.
/// </summary>
/// <remarks>
/// <para>
/// Cada movimento tem três tempos: ir para o lado pedido, ficar virado o
/// bastante para o relevo aparecer, voltar para a frente. O relevo de cada
/// movimento é a mediana dos quadros em que o rosto estava virado; o da
/// prova, o menor entre os movimentos — quem tenta com uma foto tem de
/// enganar todos.
/// </para>
/// <para>
/// A sessão recusa sem esperar o fim quando o tempo acaba, quando o rosto vai
/// para outro lado antes do pedido, quando o acompanhamento se perde e quando
/// a câmera deixa um buraco entre dois quadros: o acompanhamento só garante
/// que é o mesmo rosto se os quadros vierem seguidos. O relevo é julgado no
/// fim, contra o <see cref="CriterioDaProvaDeVida"/>.
/// </para>
/// </remarks>
public sealed class SessaoDaProvaDeVida
{
    /// <summary>O giro que conta como virar para o lado, em distâncias entre os olhos.</summary>
    public const double GiroMinimo = 0.12;

    /// <summary>A inclinação que conta como olhar para cima ou para baixo, em frações da distância dos olhos à boca.</summary>
    public const double InclinacaoMinima = 0.07;

    /// <summary>O quanto o plano do rosto tem de se inclinar para o relevo ser medido.</summary>
    public const double EncurtamentoMinimo = 0.2;

    /// <summary>O maior desvio do nariz no quadro de referência para ele contar como de frente.</summary>
    public const double GiroDeFrente = 0.1;

    /// <summary>
    /// Quanto tempo o rosto fica virado antes de a tela mandar voltar: com
    /// vários quadros no auge, o relevo é a mediana deles, e não um quadro só.
    /// </summary>
    public static readonly TimeSpan TempoNoAuge = TimeSpan.FromMilliseconds(300);

    public static readonly TimeSpan TempoPorMovimento = TimeSpan.FromSeconds(6);

    public static readonly TimeSpan TempoParaTerminar = TimeSpan.FromSeconds(4);

    public static readonly TimeSpan IntervaloMaximoEntreQuadros = TimeSpan.FromMilliseconds(250);

    private static readonly Movimento[] TodosOsMovimentos = [Movimento.Esquerda, Movimento.Direita, Movimento.Cima, Movimento.Baixo];

    private readonly CriterioDaProvaDeVida _criterio;

    private readonly PontosDoRosto _referencia;

    private readonly ModeloFacial _modeloInicial;

    private readonly DateTimeOffset _inicio;

    private readonly List<MovimentoFeito> _feitos = [];

    private readonly List<double> _relevos = [];

    private DateTimeOffset _ultimo;

    private DateTimeOffset _inicioDoPasso;

    private int _indice;

    private DateTimeOffset? _noAugeDesde;

    private Fase _fase = Fase.Indo;

    private SessaoDaProvaDeVida(Desafio desafio, CriterioDaProvaDeVida criterio, PontosDoRosto referencia, ModeloFacial modeloInicial, DateTimeOffset inicio)
    {
        Desafio = desafio;
        _criterio = criterio;
        _referencia = referencia;
        _modeloInicial = modeloInicial;
        _inicio = inicio;
        _ultimo = inicio;
        _inicioDoPasso = inicio;
    }

    private enum Fase
    {
        Indo,
        Voltando,
        Terminando,
    }

    public Desafio Desafio { get; }

    /// <summary>O movimento que a pessoa deve fazer agora; nada quando é para voltar à frente ou quando terminou.</summary>
    public Movimento? MovimentoPedido => !Terminou && _fase == Fase.Indo ? Desafio.Movimentos[_indice] : null;

    /// <summary>O que mostrar na tela agora; nada quando terminou.</summary>
    public string? Instrucao => Terminou ? null : _fase switch
    {
        Fase.Indo => Instrucoes.Do(Desafio.Movimentos[_indice]),
        Fase.Voltando => Instrucoes.VolteParaAFrente,
        _ => Instrucoes.OlheParaACamera,
    };

    /// <summary>A sessão espera um quadro com o vetor do rosto: o de frente, do fim.</summary>
    public bool PedeModelo => !Terminou && _fase == Fase.Terminando;

    public ProvaDeVida? Resultado { get; private set; }

    public bool Terminou => Resultado is not null;

    /// <summary>
    /// O nariz no meio dos olhos, com a folga de <see cref="GiroDeFrente"/>:
    /// o rosto serve de referência.
    /// </summary>
    public static bool EstaDeFrente(PontosDoRosto pontos)
    {
        ArgumentNullException.ThrowIfNull(pontos);

        return Math.Abs(MedidaDoQuadro.Pose(pontos).Lado) <= GiroDeFrente;
    }

    /// <summary>
    /// Começa pelo quadro de referência: o rosto de frente, com o vetor dele,
    /// que é o que a conferência compara com o cadastro.
    /// </summary>
    public static SessaoDaProvaDeVida Iniciar(Desafio desafio, CriterioDaProvaDeVida criterio, QuadroDaCamera referencia)
    {
        ArgumentNullException.ThrowIfNull(desafio);
        ArgumentNullException.ThrowIfNull(criterio);
        ArgumentNullException.ThrowIfNull(referencia);

        if (referencia.Pontos is null)
        {
            throw new ExcecaoDeDominio("A prova de vida começa com o rosto na imagem.");
        }

        if (referencia.Modelo is null)
        {
            throw new ExcecaoDeDominio("A prova de vida começa com o vetor do rosto de frente: é ele que diz de quem é a prova.");
        }

        if (!EstaDeFrente(referencia.Pontos))
        {
            throw new ExcecaoDeDominio("A prova de vida começa com o rosto de frente para a câmera.");
        }

        return new SessaoDaProvaDeVida(desafio, criterio, referencia.Pontos, referencia.Modelo, referencia.Em);
    }

    public void Registrar(QuadroDaCamera quadro)
    {
        ArgumentNullException.ThrowIfNull(quadro);

        if (Terminou)
        {
            throw new InvalidOperationException("A prova de vida já terminou.");
        }

        if (quadro.Em <= _ultimo)
        {
            throw new ArgumentException("Os quadros chegam em ordem: este não é posterior ao anterior.", nameof(quadro));
        }

        var intervalo = quadro.Em - _ultimo;
        _ultimo = quadro.Em;

        if (intervalo > IntervaloMaximoEntreQuadros)
        {
            Recusar(MotivoDaRecusa.QuadrosEspacados);
            return;
        }

        if (quadro.Pontos is null)
        {
            Recusar(MotivoDaRecusa.RostoPerdido);
            return;
        }

        var medida = MedidaDoQuadro.Entre(_referencia, quadro.Pontos);
        var limite = _fase == Fase.Terminando ? TempoParaTerminar : TempoPorMovimento;

        if (quadro.Em - _inicioDoPasso > limite)
        {
            Recusar(MotivoDaRecusa.TempoEsgotado);
            return;
        }

        switch (_fase)
        {
            case Fase.Indo:
                Ir(medida, quadro.Em);
                break;
            case Fase.Voltando:
                Voltar(medida, quadro.Em);
                break;
            case Fase.Terminando:
                if (DeFrente(medida) && quadro.Modelo is not null)
                {
                    Concluir(quadro.Modelo);
                }

                break;
        }
    }

    /// <summary>Quanto do movimento foi feito: 1 é o mínimo que conta.</summary>
    internal static double Avanco(MedidaDoQuadro medida, Movimento movimento) => movimento switch
    {
        Movimento.Esquerda => medida.Giro / GiroMinimo,
        Movimento.Direita => -medida.Giro / GiroMinimo,
        Movimento.Cima => -medida.Inclinacao / InclinacaoMinima,
        Movimento.Baixo => medida.Inclinacao / InclinacaoMinima,
        _ => throw new ArgumentOutOfRangeException(nameof(movimento), movimento, "Movimento desconhecido."),
    };

    private static bool DeFrente(MedidaDoQuadro medida) =>
        Math.Abs(medida.Giro) <= GiroMinimo / 2 && Math.Abs(medida.Inclinacao) <= InclinacaoMinima / 2;

    private static bool Mede(MedidaDoQuadro medida, Movimento movimento) =>
        Avanco(medida, movimento) >= 1 && medida.Encurtamento >= EncurtamentoMinimo && !double.IsNaN(medida.Relevo);

    private void Ir(MedidaDoQuadro medida, DateTimeOffset em)
    {
        var pedido = Desafio.Movimentos[_indice];
        var avanco = Avanco(medida, pedido);
        var outro = TodosOsMovimentos.Where(movimento => movimento != pedido).Max(movimento => Avanco(medida, movimento));

        if (outro >= 1 && outro > avanco)
        {
            Recusar(MotivoDaRecusa.MovimentoTrocado);
            return;
        }

        if (!Mede(medida, pedido))
        {
            // Saiu do auge antes do tempo: a contagem recomeça.
            _noAugeDesde = null;
            _relevos.Clear();
            return;
        }

        _noAugeDesde ??= em;
        _relevos.Add(medida.Relevo);

        if (em - _noAugeDesde >= TempoNoAuge)
        {
            _fase = Fase.Voltando;
            _noAugeDesde = null;
        }
    }

    private void Voltar(MedidaDoQuadro medida, DateTimeOffset em)
    {
        var pedido = Desafio.Movimentos[_indice];

        if (Mede(medida, pedido))
        {
            _relevos.Add(medida.Relevo);
        }

        if (!DeFrente(medida))
        {
            return;
        }

        _feitos.Add(new MovimentoFeito(pedido, Mediana(_relevos), _relevos.Count, em - _inicioDoPasso));
        _indice++;
        _inicioDoPasso = em;
        _fase = _indice == Desafio.Movimentos.Count ? Fase.Terminando : Fase.Indo;
    }

    private void Concluir(ModeloFacial modeloFinal)
    {
        var menor = _feitos.Min(feito => feito.Relevo);

        Resultado = _criterio.Aceita(menor)
            ? new ProvaDeVida(Desafio, _criterio, _inicio, _ultimo, _feitos.AsReadOnly(), menor, null, _modeloInicial, modeloFinal)
            : new ProvaDeVida(Desafio, _criterio, _inicio, _ultimo, _feitos.AsReadOnly(), menor, MotivoDaRecusa.SemRelevo, null, null);
    }

    private void Recusar(MotivoDaRecusa motivo) =>
        Resultado = new ProvaDeVida(Desafio, _criterio, _inicio, _ultimo, _feitos.AsReadOnly(), null, motivo, null, null);

    private static double Mediana(List<double> valores)
    {
        var ordenados = valores.Order().ToArray();
        var meio = ordenados.Length / 2;

        return ordenados.Length % 2 == 1 ? ordenados[meio] : (ordenados[meio - 1] + ordenados[meio]) / 2;
    }
}
