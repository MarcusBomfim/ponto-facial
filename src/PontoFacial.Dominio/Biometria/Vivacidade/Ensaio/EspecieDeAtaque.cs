using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Biometria.Vivacidade.Ensaio;

/// <summary>
/// Uma espécie de instrumento de ataque, como a ISO/IEC 30107-3 chama: o
/// tipo de coisa que alguém põe diante da câmera no lugar do próprio rosto —
/// a foto parada, a foto inclinada, a foto curvada, a dobrada.
/// </summary>
/// <remarks>
/// A norma pede o erro de cada espécie em separado, e não a média entre
/// elas: um sistema que recusa toda foto plana e aceita metade das curvadas
/// não tem 25% de erro; tem uma porta aberta.
/// </remarks>
public sealed class EspecieDeAtaque
{
    public const int TamanhoDoNome = 80;

    public const int TamanhoDaDescricao = 300;

    private EspecieDeAtaque(string nome, string descricao)
    {
        Nome = nome;
        Descricao = descricao;
    }

    public string Nome { get; }

    /// <summary>Como o ataque é feito.</summary>
    public string Descricao { get; }

    public static EspecieDeAtaque De(string nome, string descricao) => new(
        Textos.Obrigatorio(nome, "o nome da espécie de ataque", TamanhoDoNome),
        Textos.Obrigatorio(descricao, "a descrição da espécie de ataque", TamanhoDaDescricao));

    public override string ToString() => Nome;
}
