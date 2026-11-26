using System.Globalization;
using System.Security.Cryptography;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Biometria.Vivacidade;

/// <summary>
/// Os movimentos que a prova de vida pede, em ordem, sorteados a cada
/// tentativa.
/// </summary>
/// <remarks>
/// <para>
/// O sorteio é o que derruba o vídeo gravado: quem grava o colega virando a
/// cabeça não sabe, ao gravar, em que ordem o coletor vai pedir. Com três
/// movimentos entre quatro, e pelo menos um para o lado, há
/// <see cref="Possibilidades"/> desafios; uma gravação com uma ordem só
/// acerta um deles por tentativa.
/// </para>
/// <para>
/// Pelo menos um movimento é para o lado porque é no giro que o relevo do
/// nariz fica mais à vista. O sorteio usa o gerador criptográfico: um
/// gerador previsível deixaria adivinhar o próximo desafio.
/// </para>
/// </remarks>
public sealed class Desafio
{
    public const int MovimentosPorDesafio = 3;

    private static readonly Movimento[] Todos = [Movimento.Esquerda, Movimento.Direita, Movimento.Cima, Movimento.Baixo];

    private static readonly Movimento[][] Validos = TodosOsDesafios();

    private Desafio(Movimento[] movimentos)
    {
        Movimentos = movimentos.AsReadOnly();
    }

    public IReadOnlyList<Movimento> Movimentos { get; }

    /// <summary>Quantos desafios diferentes o sorteio pode dar.</summary>
    public static int Possibilidades => Validos.Length;

    /// <summary>Sorteia com o gerador criptográfico.</summary>
    public static Desafio Sortear() => Sortear(RandomNumberGenerator.GetInt32);

    /// <summary>
    /// Sorteia com o gerador dado, que recebe <c>n</c> e devolve um número de
    /// 0 a <c>n - 1</c>. Todos os desafios têm a mesma chance.
    /// </summary>
    public static Desafio Sortear(Func<int, int> sorteio)
    {
        ArgumentNullException.ThrowIfNull(sorteio);

        var indice = sorteio(Validos.Length);

        if (indice < 0 || indice >= Validos.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(sorteio), indice, $"O sorteio tem de dar de 0 a {Validos.Length - 1}.");
        }

        return new Desafio([.. Validos[indice]]);
    }

    /// <summary>Um desafio escolhido, para testes e para refazer uma tentativa registrada.</summary>
    public static Desafio De(IReadOnlyList<Movimento> movimentos)
    {
        ArgumentNullException.ThrowIfNull(movimentos);

        if (movimentos.Count != MovimentosPorDesafio)
        {
            throw new ExcecaoDeDominio(string.Create(
                CultureInfo.GetCultureInfo("pt-BR"),
                $"O desafio tem {MovimentosPorDesafio} movimentos; vieram {movimentos.Count}."));
        }

        if (movimentos.Any(movimento => !Enum.IsDefined(movimento)))
        {
            throw new ExcecaoDeDominio("O desafio tem um movimento desconhecido.");
        }

        if (!movimentos.Any(movimento => movimento.EhLateral()))
        {
            throw new ExcecaoDeDominio("O desafio precisa de pelo menos um movimento para o lado.");
        }

        return new Desafio([.. movimentos]);
    }

    public override string ToString() => string.Join(", ", Movimentos);

    private static Movimento[][] TodosOsDesafios()
    {
        var desafios = new List<Movimento[]>();

        foreach (var primeiro in Todos)
        {
            foreach (var segundo in Todos)
            {
                foreach (var terceiro in Todos)
                {
                    Movimento[] desafio = [primeiro, segundo, terceiro];

                    if (desafio.Any(movimento => movimento.EhLateral()))
                    {
                        desafios.Add(desafio);
                    }
                }
            }
        }

        return [.. desafios];
    }
}
