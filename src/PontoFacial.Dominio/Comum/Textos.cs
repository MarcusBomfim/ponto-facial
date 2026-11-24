namespace PontoFacial.Dominio.Comum;

/// <summary>As regras de texto que se repetem no domínio: obrigatório, sem sobra de espaço e com tamanho máximo.</summary>
internal static class Textos
{
    /// <summary>
    /// O texto sem os espaços das pontas e sem os repetidos no meio, recusado
    /// se ficar vazio ou passar do tamanho.
    /// </summary>
    public static string Obrigatorio(string? texto, string campo, int tamanhoMaximo)
    {
        var limpo = string.Join(' ', (texto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

        if (limpo.Length == 0)
        {
            throw new ExcecaoDeDominio($"Falta {campo}.");
        }

        if (limpo.Length > tamanhoMaximo)
        {
            throw new ExcecaoDeDominio($"{Maiuscula(campo)} tem {limpo.Length} caracteres; o máximo é {tamanhoMaximo}.");
        }

        return limpo;
    }

    private static string Maiuscula(string texto) => texto.Length == 0 ? texto : char.ToUpperInvariant(texto[0]) + texto[1..];
}
