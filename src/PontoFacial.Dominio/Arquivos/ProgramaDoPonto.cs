using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Arquivos;

/// <summary>
/// Este programa, como os arquivos da Portaria o identificam: o REP-P, pelo
/// número de registro no INPI, e o programa de tratamento, pelo nome, pela
/// versão e por quem o desenvolve.
/// </summary>
/// <remarks>
/// O mesmo programa é as duas coisas: registra o ponto (REP-P, art. 78) e
/// trata o registro (art. 82). O AFD leva o registro no INPI e o documento do
/// desenvolvedor; o AEJ, o nome, a versão e o contato dele.
/// </remarks>
public sealed class ProgramaDoPonto
{
    public const int TamanhoDoNome = 150;

    public const int TamanhoDaVersao = 8;

    public const int TamanhoDoEmail = 50;

    /// <summary>O número no INPI cabe nos 17 dígitos do campo do AFD e do AEJ.</summary>
    public const int DigitosDoRegistroNoInpi = 17;

    private ProgramaDoPonto(string nome, string versao, string registroNoInpi, Documento desenvolvedor, string razaoSocial, string email)
    {
        Nome = nome;
        Versao = versao;
        RegistroNoInpi = registroNoInpi;
        Desenvolvedor = desenvolvedor;
        RazaoSocialDoDesenvolvedor = razaoSocial;
        EmailDoDesenvolvedor = email;
    }

    public string Nome { get; }

    public string Versao { get; }

    /// <summary>O registro no INPI como o certificado o escreve: <c>BR512026001234-5</c>.</summary>
    public string RegistroNoInpi { get; }

    /// <summary>O CNPJ ou o CPF de quem desenvolve o programa.</summary>
    public Documento Desenvolvedor { get; }

    public string RazaoSocialDoDesenvolvedor { get; }

    public string EmailDoDesenvolvedor { get; }

    /// <summary>Só os dígitos do registro no INPI, como os campos numéricos dos arquivos o pedem.</summary>
    internal string NumeroNoInpi => string.Concat(RegistroNoInpi.Where(char.IsAsciiDigit));

    public static ProgramaDoPonto De(string nome, string versao, string registroNoInpi, Documento desenvolvedor, string razaoSocial, string email)
    {
        ArgumentNullException.ThrowIfNull(desenvolvedor);

        var inpi = Textos.Obrigatorio(registroNoInpi, "o número de registro do programa no INPI", 30);
        var digitos = inpi.Count(char.IsAsciiDigit);

        if (digitos == 0 || digitos > DigitosDoRegistroNoInpi)
        {
            throw new ExcecaoDeDominio($"O registro no INPI \"{inpi}\" tem {digitos} dígitos; o campo dos arquivos tem até {DigitosDoRegistroNoInpi}.");
        }

        var contato = Textos.Obrigatorio(email, "o e-mail do desenvolvedor", TamanhoDoEmail);

        if (!contato.Contains('@', StringComparison.Ordinal) || contato.Contains(' ', StringComparison.Ordinal))
        {
            throw new ExcecaoDeDominio($"\"{contato}\" não é um e-mail.");
        }

        return new ProgramaDoPonto(
            Textos.Obrigatorio(nome, "o nome do programa", TamanhoDoNome),
            Textos.Obrigatorio(versao, "a versão do programa", TamanhoDaVersao),
            inpi,
            desenvolvedor,
            Textos.Obrigatorio(razaoSocial, "a razão social ou o nome do desenvolvedor", TamanhoDoNome),
            contato);
    }
}
