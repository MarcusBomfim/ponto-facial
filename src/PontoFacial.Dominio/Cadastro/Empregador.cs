using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Cadastro;

/// <summary>
/// Quem emprega: a empresa, pelo CNPJ, ou a pessoa física, pelo CPF — o
/// produtor rural, o empregador doméstico.
/// </summary>
/// <remarks>
/// O nome, o documento e o CEI/CAEPF/CNO, quando existe, são o que a Portaria
/// 671 pede para identificar o empregador no comprovante de cada marcação
/// (art. 79, III) e no espelho de ponto (art. 84, I).
/// </remarks>
public sealed class Empregador
{
    /// <summary>A razão social ou o nome cabem em 150 caracteres, o campo do Arquivo Fonte de Dados.</summary>
    public const int TamanhoDoNome = 150;

    private Empregador(Documento documento, string nome, string? ceiCaepfOuCno)
    {
        Documento = documento;
        Nome = nome;
        CeiCaepfOuCno = ceiCaepfOuCno;
    }

    /// <summary>O CNPJ, numérico ou alfanumérico, ou o CPF.</summary>
    public Documento Documento { get; }

    /// <summary>A razão social da empresa ou o nome da pessoa.</summary>
    public string Nome { get; }

    /// <summary>
    /// O CEI, o CAEPF ou o CNO, quando o empregador tem: só dígitos, 12 no CEI
    /// e no CNO, 14 no CAEPF. Nulo quando não existe.
    /// </summary>
    public string? CeiCaepfOuCno { get; }

    public static Empregador Novo(Documento documento, string nome, string? ceiCaepfOuCno = null)
    {
        ArgumentNullException.ThrowIfNull(documento);

        return new Empregador(
            documento,
            Textos.Obrigatorio(nome, "a razão social ou o nome do empregador", TamanhoDoNome),
            CadastroComplementar(ceiCaepfOuCno));
    }

    private static string? CadastroComplementar(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        var numero = string.Concat(texto.Where(caractere => caractere is not ('.' or '/' or '-' or ' ')));

        if (!numero.All(char.IsAsciiDigit) || numero.Length is not (12 or 14))
        {
            throw new ExcecaoDeDominio(
                $"\"{texto.Trim()}\" não é CEI, CAEPF nem CNO: o CEI e o CNO têm 12 dígitos, e o CAEPF, 14.");
        }

        return numero;
    }
}
