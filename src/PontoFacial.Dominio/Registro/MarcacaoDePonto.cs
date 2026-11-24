using System.Security.Cryptography;
using System.Text;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Registro;

/// <summary>
/// Uma marcação de ponto, com o hash SHA-256 que a amarra à marcação anterior
/// do mesmo registro (anexo IX, itens 6.5 e 10).
/// </summary>
/// <remarks>
/// <para>
/// O hash é calculado sobre os sete primeiros campos do registro do tipo 7 do
/// Arquivo Fonte de Dados — NSR, tipo, data e hora da marcação, CPF, data e
/// hora da gravação, coletor e on-line —, escritos como o leiaute os escreve,
/// seguidos do hash da marcação anterior, quando existe. Os sete campos
/// ocupam as posições 1 a 73 da linha, e o hash, as 64 seguintes: a linha do
/// arquivo é o que foi assinado e a assinatura.
/// </para>
/// <para>
/// O meio de identificação fica na marcação, mas fora do hash e do arquivo: o
/// leiaute não tem campo para ele, e a marcação pelo código pessoal vale o
/// mesmo que a pelo rosto (ADR-0002).
/// </para>
/// </remarks>
public sealed class MarcacaoDePonto : Operacao
{
    /// <summary>Os campos 1 a 7 do registro do tipo 7, sobre os quais o hash é calculado.</summary>
    public const int TamanhoDosCampos = 73;

    /// <summary>A linha inteira do registro do tipo 7: os campos e o hash.</summary>
    public const int TamanhoDaLinha = 137;

    internal MarcacaoDePonto(
        long nsr,
        DateTimeOffset marcadaEm,
        DateTimeOffset gravadaEm,
        Cpf cpf,
        Coletor coletor,
        bool online,
        MeioDeIdentificacao meio,
        string? hashAnterior)
        : base(nsr, gravadaEm)
    {
        MarcadaEm = marcadaEm;
        Cpf = cpf;
        Coletor = coletor;
        OnLine = online;
        Meio = meio;
        HashAnterior = hashAnterior;
        Campos = string.Concat(
            CamposDoAfd.Nsr(nsr),
            "7",
            CamposDoAfd.DataHora(marcadaEm),
            CamposDoAfd.Cpf(cpf),
            CamposDoAfd.DataHora(gravadaEm),
            CamposDoAfd.Coletor(coletor),
            CamposDoAfd.OnLine(online));
        Hash = CalcularHash(Campos, hashAnterior);
    }

    public override int TipoDoRegistro => 7;

    /// <summary>A hora em que o trabalhador marcou, na hora local do estabelecimento, ao minuto.</summary>
    public DateTimeOffset MarcadaEm { get; }

    public Cpf Cpf { get; }

    public Coletor Coletor { get; }

    /// <summary>Feita com o coletor ligado ao registro; se não, foi guardada no coletor e enviada depois.</summary>
    public bool OnLine { get; }

    public MeioDeIdentificacao Meio { get; }

    /// <summary>Os campos 1 a 7, como o leiaute os escreve: 73 caracteres.</summary>
    public string Campos { get; }

    /// <summary>O hash da marcação anterior deste registro; nulo na primeira.</summary>
    public string? HashAnterior { get; }

    /// <summary>O SHA-256 dos campos e do hash anterior, em 64 caracteres hexadecimais minúsculos.</summary>
    public string Hash { get; }

    /// <summary>A linha do registro do tipo 7 no Arquivo Fonte de Dados: 137 caracteres.</summary>
    public string LinhaDoAfd => Campos + Hash;

    internal static string CalcularHash(string campos, string? hashAnterior) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.Latin1.GetBytes(campos + hashAnterior)));
}
