using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Biometria;

/// <summary>
/// O rosto como o reconhecedor o descreve: um vetor de números, guardado com
/// norma 1. É o que se cadastra e o que se compara; a foto não fica.
/// </summary>
/// <remarks>
/// O vetor não é a foto, mas também não é anônimo: ele identifica a pessoa, e
/// há técnicas que reconstroem um rosto aproximado a partir dele. É dado
/// biométrico, e é tratado como tal — com autorização, cifrado ao ser
/// guardado e apagado quando a autorização acaba.
/// </remarks>
public sealed class ModeloFacial
{
    /// <summary>O nome do reconhecedor cabe em 40 caracteres: <c>sface-2021dec</c>.</summary>
    public const int TamanhoDoReconhecedor = 40;

    private readonly float[] _valores;

    private ModeloFacial(string reconhecedor, float[] valores)
    {
        Reconhecedor = reconhecedor;
        _valores = valores;
    }

    /// <summary>
    /// O modelo de reconhecimento que gerou o vetor, com a versão. Vetores de
    /// reconhecedores diferentes não se comparam: os números não querem dizer
    /// a mesma coisa.
    /// </summary>
    public string Reconhecedor { get; }

    public int Dimensao => _valores.Length;

    /// <summary>Os valores do vetor, já com norma 1.</summary>
    public ReadOnlySpan<float> Valores => _valores;

    /// <summary>Lê o vetor que o reconhecedor gerou e o guarda com norma 1.</summary>
    public static ModeloFacial De(string reconhecedor, ReadOnlySpan<float> valores)
    {
        var nome = Textos.Obrigatorio(reconhecedor, "o nome do reconhecedor", TamanhoDoReconhecedor);

        if (valores.IsEmpty)
        {
            throw new ExcecaoDeDominio("O vetor do rosto está vazio.");
        }

        var somaDosQuadrados = 0.0;

        foreach (var valor in valores)
        {
            if (!float.IsFinite(valor))
            {
                throw new ExcecaoDeDominio("O vetor do rosto tem um valor que não é número.");
            }

            somaDosQuadrados += (double)valor * valor;
        }

        if (somaDosQuadrados == 0)
        {
            throw new ExcecaoDeDominio("O vetor do rosto é nulo: não descreve rosto nenhum.");
        }

        var norma = Math.Sqrt(somaDosQuadrados);
        var normalizados = new float[valores.Length];

        for (var i = 0; i < valores.Length; i++)
        {
            normalizados[i] = (float)(valores[i] / norma);
        }

        return new ModeloFacial(nome, normalizados);
    }

    /// <summary>
    /// A similaridade de cosseno entre os dois rostos: 1 é o mesmo vetor, 0 não
    /// tem nada em comum, -1 é o oposto.
    /// </summary>
    public double Similaridade(ModeloFacial outro)
    {
        ArgumentNullException.ThrowIfNull(outro);

        if (outro.Reconhecedor != Reconhecedor || outro.Dimensao != Dimensao)
        {
            throw new ExcecaoDeDominio(
                $"Vetores de reconhecedores diferentes não se comparam: {Reconhecedor} e {outro.Reconhecedor}.");
        }

        var produto = 0.0;

        for (var i = 0; i < _valores.Length; i++)
        {
            produto += (double)_valores[i] * outro._valores[i];
        }

        return Math.Clamp(produto, -1.0, 1.0);
    }

    /// <summary>A média dos vetores, de novo com norma 1: o modelo que o cadastro guarda.</summary>
    internal static ModeloFacial Media(IReadOnlyList<ModeloFacial> modelos)
    {
        var soma = new float[modelos[0].Dimensao];

        foreach (var modelo in modelos)
        {
            for (var i = 0; i < soma.Length; i++)
            {
                soma[i] += modelo._valores[i];
            }
        }

        return De(modelos[0].Reconhecedor, soma);
    }
}
