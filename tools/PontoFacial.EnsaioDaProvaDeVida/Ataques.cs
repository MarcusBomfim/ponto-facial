using PontoFacial.Dominio.Biometria.Vivacidade;
using PontoFacial.Dominio.Biometria.Vivacidade.Ensaio;
using PontoFacial.Rosto;

namespace PontoFacial.EnsaioDaProvaDeVida;

/// <summary>As espécies de ataque do ensaio, cada uma com o atacante fazendo o melhor que ela permite.</summary>
internal static class Especies
{
    public static readonly EspecieDeAtaque FotoParada = EspecieDeAtaque.De(
        "foto parada",
        "A foto impressa, ou na tela de um celular, segura diante da câmera, só com o tremor da mão.");

    public static readonly EspecieDeAtaque FotoInclinada = EspecieDeAtaque.De(
        "foto plana inclinada",
        "A foto plana inclinada a cada pedido; nos giros, na diagonal, para o nariz andar para o lado como numa cabeça.");

    public static readonly EspecieDeAtaque CurvadaDeLado = EspecieDeAtaque.De(
        "foto curvada de lado",
        "A foto enrolada de um lado para o outro, com raio de 4 a 10 cm, girada a cada pedido.");

    public static readonly EspecieDeAtaque CurvadaDeCimaABaixo = EspecieDeAtaque.De(
        "foto curvada de cima para baixo",
        "A foto enrolada de cima para baixo, com raio de 4 a 10 cm, girada a cada pedido.");

    public static readonly EspecieDeAtaque CurvadaNaDiagonal = EspecieDeAtaque.De(
        "foto curvada na diagonal",
        "A foto enrolada com o eixo a 45°, com raio de 3 a 8 cm, girada a cada pedido.");

    public static readonly EspecieDeAtaque DobradaDeLado = EspecieDeAtaque.De(
        "foto dobrada na linha do nariz",
        "A foto dobrada em V pela linha do nariz, de 25° a 50° de cada lado, girada a cada pedido.");

    public static readonly EspecieDeAtaque DobradaDeCimaABaixo = EspecieDeAtaque.De(
        "foto dobrada na altura do nariz",
        "A foto dobrada em V de lado a lado, na altura do nariz, de 25° a 50° de cada lado, girada a cada pedido.");

    public static readonly EspecieDeAtaque TrocaDeFotos = EspecieDeAtaque.De(
        "troca de fotos",
        "Fotos da mesma pessoa em poses diferentes, alinhadas, trocadas a cada pedido pela que mais se parece com o movimento.");

    public static IReadOnlyList<EspecieDeAtaque> Todas { get; } =
    [
        FotoParada, FotoInclinada, CurvadaDeLado, CurvadaDeCimaABaixo, CurvadaNaDiagonal, DobradaDeLado, DobradaDeCimaABaixo, TrocaDeFotos,
    ];
}

/// <summary>Um quadro do ataque: o que a câmera vê e como.</summary>
internal sealed record QuadroDoAtaque(Cena Cena, Superficie Superficie, Pose Pose);

/// <summary>
/// Quem ataca, olhando a tela a cada quadro: vai para o movimento pedido,
/// segura enquanto a tela pede, volta para a frente quando ela manda.
/// </summary>
/// <remarks>
/// A mão anda um décimo do ângulo do auge por quadro — a 15 quadros por
/// segundo, uns 50° por segundo. Na troca de fotos, a foto muda de uma vez,
/// como na tela de um celular.
/// </remarks>
internal sealed class Atacante
{
    private readonly Cena _frente;

    private readonly Superficie _superficie;

    private readonly double _distancia;

    private readonly Func<Movimento, (double Giro, double Inclinacao)> _auge;

    private readonly Func<Movimento, Cena>? _troca;

    private double _giro;

    private double _inclinacao;

    /// <param name="auge">A pose que o atacante procura em cada movimento, em graus.</param>
    /// <param name="troca">Na troca de fotos, a foto que ele mostra em cada movimento.</param>
    public Atacante(Cena frente, Superficie superficie, double distancia, Func<Movimento, (double Giro, double Inclinacao)> auge, Func<Movimento, Cena>? troca = null)
    {
        _frente = frente;
        _superficie = superficie;
        _distancia = distancia;
        _auge = auge;
        _troca = troca;
    }

    public QuadroDoAtaque Proximo(Movimento? pedido)
    {
        if (_troca is not null)
        {
            return new QuadroDoAtaque(pedido is { } trocado ? _troca(trocado) : _frente, _superficie, new Pose(0, 0, _distancia));
        }

        var (giro, inclinacao) = pedido is { } movimento ? _auge(movimento) : (0, 0);
        var passo = Math.Max(Math.Abs(giro), Math.Abs(inclinacao)) / 10;
        passo = passo == 0 ? 4 : passo;

        _giro += Math.Clamp(giro - _giro, -passo, passo);
        _inclinacao += Math.Clamp(inclinacao - _inclinacao, -passo, passo);

        return new QuadroDoAtaque(_frente, _superficie, new Pose(_giro, _inclinacao, _distancia));
    }
}

/// <summary>Como cada espécie ataca.</summary>
internal static class Ataques
{
    public const int QuadrosPorSegundo = 15;

    /// <summary>Quadros de frente para a prova começar; se não começa, a foto é trocada.</summary>
    public const int QuadrosDeFrente = 8;

    /// <param name="foto">A foto de frente com que a prova começa.</param>
    /// <param name="poses">Na troca de fotos, as outras fotos da pessoa, com a pose de cada uma em relação à de frente.</param>
    public static Atacante Preparar(EspecieDeAtaque especie, Foto foto, IReadOnlyList<(Foto Foto, double Giro, double Inclinacao)> poses, Random sorteio)
    {
        double Entre(double de, double ate) => de + ((ate - de) * sorteio.NextDouble());

        var distancia = Entre(350, 450);
        var cena = new Cena(foto);

        if (especie == Especies.TrocaDeFotos)
        {
            var cenas = new Dictionary<Movimento, Cena>();

            foreach (var movimento in Enum.GetValues<Movimento>())
            {
                cenas[movimento] = new Cena(MelhorPose(poses, movimento) ?? foto, escala: foto);
            }

            return new Atacante(cena, new Plana(), distancia, _ => (0, 0), movimento => cenas[movimento]);
        }

        Superficie superficie = especie switch
        {
            _ when especie == Especies.CurvadaDeLado => new Curvada(Entre(40, 100), 90),
            _ when especie == Especies.CurvadaDeCimaABaixo => new Curvada(Entre(40, 100), 0),
            _ when especie == Especies.CurvadaNaDiagonal => new Curvada(Entre(30, 80), sorteio.Next(2) == 0 ? 45 : 135),
            _ when especie == Especies.DobradaDeLado => new Dobrada(Entre(25, 50), 90),
            _ when especie == Especies.DobradaDeCimaABaixo => new Dobrada(Entre(25, 50), 0),
            _ => new Plana(),
        };

        // Os ângulos são sorteados uma vez: o atacante tem o seu jeito de segurar a foto.
        var lateral = Entre(35, 55);
        var vertical = Entre(30, 45);
        var diagonal = Entre(30, 40);

        (double, double) Auge(Movimento movimento)
        {
            if (especie == Especies.FotoParada)
            {
                return (0, 0);
            }

            if (especie == Especies.FotoInclinada)
            {
                // Na diagonal, a foto cisalha e o nariz anda para o lado.
                return movimento switch
                {
                    Movimento.Esquerda => (lateral, -diagonal),
                    Movimento.Direita => (-lateral, -diagonal),
                    Movimento.Cima => (0, -vertical),
                    _ => (0, vertical),
                };
            }

            // A foto curvada ou dobrada tem menos relevo que um rosto: para o
            // nariz andar o bastante, quem ataca vira mais que uma pessoa viraria.
            return movimento switch
            {
                Movimento.Esquerda => (lateral, 0),
                Movimento.Direita => (-lateral, 0),
                Movimento.Cima => (0, -vertical),
                _ => (0, vertical),
            };
        }

        return new Atacante(cena, superficie, distancia, Auge);
    }

    /// <summary>A foto da pessoa que mais parece feita no movimento pedido.</summary>
    private static Foto? MelhorPose(IReadOnlyList<(Foto Foto, double Giro, double Inclinacao)> poses, Movimento movimento) =>
        poses.Count == 0
            ? null
            : movimento switch
            {
                Movimento.Esquerda => poses.MaxBy(pose => pose.Giro).Foto,
                Movimento.Direita => poses.MinBy(pose => pose.Giro).Foto,
                Movimento.Cima => poses.MinBy(pose => pose.Inclinacao).Foto,
                _ => poses.MaxBy(pose => pose.Inclinacao).Foto,
            };
}

/// <summary>Uma tentativa de ataque, do primeiro quadro ao resultado.</summary>
internal static class Tentativa
{
    /// <summary>
    /// O critério com que o ensaio roda: o mais alto possível. Toda sessão
    /// que faz os movimentos termina recusada por relevo, com o relevo
    /// medido, e o ensaio julga depois com qualquer critério.
    /// </summary>
    public static readonly CriterioDaProvaDeVida ParaMedir = CriterioDaProvaDeVida.De(2, "ensaio: todo relevo é medido e julgado depois");

    /// <summary>Uns 30 segundos de câmera: toda sessão termina antes, nem que seja por tempo.</summary>
    public const int LimiteDeQuadros = 450;

    private static readonly DateTimeOffset Comeco = new(2026, 11, 25, 8, 0, 0, TimeSpan.FromHours(-3));

    /// <summary>O resultado, ou nada se a prova nem começou: a foto não passou por rosto de frente.</summary>
    public static ProvaDeVida? Rodar(ReconhecedorFacial reconhecedor, Desafio desafio, Atacante atacante, Random sorteio)
    {
        var camera = new ProvaDeVidaNaCamera(reconhecedor, desafio, ParaMedir);

        for (var numero = 0; numero < LimiteDeQuadros && !camera.Terminou; numero++)
        {
            if (!camera.Comecou && numero >= Ataques.QuadrosDeFrente)
            {
                return null;
            }

            var quadro = atacante.Proximo(camera.MovimentoPedido);
            camera.Processar(quadro.Cena.Ver(quadro.Superficie, quadro.Pose.ComTremor(sorteio), sorteio), Instante(numero));
        }

        return camera.Resultado ?? throw new InvalidOperationException("A sessão não terminou em 30 segundos de câmera.");
    }

    public static DateTimeOffset Instante(int numero) => Comeco + TimeSpan.FromSeconds(numero / (double)Ataques.QuadrosPorSegundo);
}
