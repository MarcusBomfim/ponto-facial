using PontoFacial.Rosto;

namespace PontoFacial.EnsaioDaProvaDeVida.Testes;

/// <summary>Os retratos da NASA como instrumentos de ataque.</summary>
internal static class Retratos
{
    /// <summary>Um reconhecedor só para os testes todos: carregar os modelos leva tempo.</summary>
    public static readonly ReconhecedorFacial Reconhecedor = new(ReconhecedorFacial.PastaPadrao);

    public static Imagem Imagem(string nome) =>
        Rosto.Imagem.TentarDecodificar(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Imagens", nome)), out var imagem)
            ? imagem!
            : throw new InvalidOperationException($"{nome} não abriu.");

    public static Foto Foto(string nome)
    {
        var imagem = Imagem(nome);

        return new Foto(imagem, Reconhecedor.Capturar(imagem).Rosto ?? throw new InvalidOperationException($"{nome} não tem rosto."));
    }

    /// <summary>A diferença média por canal entre duas imagens do mesmo tamanho.</summary>
    public static double Diferenca(Imagem a, Imagem b)
    {
        var x = a.CopiarBgr();
        var y = b.CopiarBgr();

        return x.Zip(y, (p, q) => Math.Abs(p - q)).Average();
    }
}
