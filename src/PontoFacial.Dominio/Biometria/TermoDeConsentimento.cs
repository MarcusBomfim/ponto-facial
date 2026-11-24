using System.Security.Cryptography;
using System.Text;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Biometria;

/// <summary>
/// O texto que o trabalhador leu antes de autorizar a biometria, com a versão
/// e o hash SHA-256 do texto exato.
/// </summary>
/// <remarks>
/// Na LGPD, cabe ao controlador provar que o consentimento foi obtido como a
/// lei manda (art. 8º, § 2º), e o consentimento para dado sensível é dado
/// para finalidades específicas (art. 11, I). O hash prova qual texto foi
/// aceito: guardar só "versão 3" não prova nada se o texto da versão 3 puder
/// ser trocado depois. Por isso o texto é guardado como foi mostrado — um
/// espaço a mais é outro texto, e outro hash.
/// </remarks>
public sealed class TermoDeConsentimento
{
    /// <summary>A versão cabe em 20 caracteres: <c>2026.1</c>, <c>v3</c>.</summary>
    public const int TamanhoDaVersao = 20;

    private TermoDeConsentimento(string versao, string texto, string hash)
    {
        Versao = versao;
        Texto = texto;
        Hash = hash;
    }

    public string Versao { get; }

    /// <summary>O texto exatamente como foi mostrado ao trabalhador.</summary>
    public string Texto { get; }

    /// <summary>O SHA-256 do texto em UTF-8, em 64 caracteres hexadecimais minúsculos.</summary>
    public string Hash { get; }

    public static TermoDeConsentimento Novo(string versao, string texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            throw new ExcecaoDeDominio("Falta o texto do termo de consentimento.");
        }

        return new TermoDeConsentimento(
            Textos.Obrigatorio(versao, "a versão do termo de consentimento", TamanhoDaVersao),
            texto,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto))));
    }
}
