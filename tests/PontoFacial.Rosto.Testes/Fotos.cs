namespace PontoFacial.Rosto.Testes;

/// <summary>As fotos de teste (ver Imagens/CREDITOS.md) e montagens feitas com elas.</summary>
internal static class Fotos
{
    /// <summary>Um reconhecedor só para os testes todos: carregar os modelos leva tempo.</summary>
    public static readonly ReconhecedorFacial Reconhecedor = new(ReconhecedorFacial.PastaPadrao);

    public static byte[] Arquivo(string nome) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Imagens", nome));

    public static Imagem Imagem(string nome) =>
        Rosto.Imagem.TentarDecodificar(Arquivo(nome), out var imagem) ? imagem! : throw new InvalidOperationException($"{nome} não abriu.");

    /// <summary>As duas imagens lado a lado, alinhadas por cima, com preto onde a mais baixa não chega.</summary>
    public static Imagem LadoALado(Imagem esquerda, Imagem direita)
    {
        var largura = esquerda.Largura + direita.Largura;
        var altura = Math.Max(esquerda.Altura, direita.Altura);
        var bgr = new byte[largura * altura * 3];

        void Copiar(Imagem origem, int deslocamento)
        {
            for (var y = 0; y < origem.Altura; y++)
            {
                Array.Copy(origem.Bgr, y * origem.Largura * 3, bgr, ((y * largura) + deslocamento) * 3, origem.Largura * 3);
            }
        }

        Copiar(esquerda, 0);
        Copiar(direita, esquerda.Largura);

        return Rosto.Imagem.DeBgr(largura, altura, bgr);
    }

    /// <summary>A mesma imagem, com a luz mudada: cada canal somado de <paramref name="quanto"/>.</summary>
    public static Imagem Clareada(Imagem imagem, int quanto) =>
        Rosto.Imagem.DeBgr(imagem.Largura, imagem.Altura, [.. imagem.Bgr.Select(valor => (byte)Math.Clamp(valor + quanto, 0, 255))]);

    /// <summary>Uma imagem de uma cor só.</summary>
    public static Imagem Lisa(int largura, int altura, byte cinza) =>
        Rosto.Imagem.DeBgr(largura, altura, Enumerable.Repeat(cinza, largura * altura * 3).ToArray());
}
