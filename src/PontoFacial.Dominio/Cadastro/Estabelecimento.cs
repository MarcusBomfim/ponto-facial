using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Cadastro;

/// <summary>
/// O lugar onde o trabalho acontece: a sede, a filial, a obra, o terminal.
/// Cada estabelecimento tem o seu registro de ponto, e a sua numeração.
/// </summary>
/// <remarks>
/// <para>
/// O local vai no comprovante de cada marcação (art. 79, IV, da Portaria
/// 671): o endereço onde o serviço é prestado, ou o do estabelecimento ao qual
/// o trabalhador está ligado, quando ele trabalha fora ou em instalação de
/// terceiro.
/// </para>
/// <para>
/// O fuso também: a marcação é gravada com a hora e o fuso do lugar (anexo
/// IX, item 10.4). O Brasil tem quatro fusos, e o Acre, o Amazonas e Fernando
/// de Noronha não estão na hora de Brasília. O fuso é um dos do Brasil, pelo
/// nome da base de fusos (IANA), e a conta do horário de cada data fica com
/// ela.
/// </para>
/// </remarks>
public sealed class Estabelecimento
{
    /// <summary>O endereço cabe em 100 caracteres, o campo de local do Arquivo Fonte de Dados.</summary>
    public const int TamanhoDoLocal = 100;

    /// <summary>Os fusos do Brasil, pelo nome da base IANA.</summary>
    public static readonly IReadOnlySet<string> FusosDoBrasil = new HashSet<string>(StringComparer.Ordinal)
    {
        "America/Noronha", "America/Belem", "America/Fortaleza", "America/Recife", "America/Araguaina",
        "America/Maceio", "America/Bahia", "America/Sao_Paulo", "America/Campo_Grande", "America/Cuiaba",
        "America/Santarem", "America/Porto_Velho", "America/Boa_Vista", "America/Manaus", "America/Eirunepe",
        "America/Rio_Branco",
    };

    private Estabelecimento(Empregador empregador, string local, TimeZoneInfo fuso)
    {
        Empregador = empregador;
        Local = local;
        Fuso = fuso;
    }

    public Empregador Empregador { get; }

    /// <summary>O endereço onde o serviço é prestado.</summary>
    public string Local { get; }

    /// <summary>O fuso do lugar, em que cada marcação é gravada.</summary>
    public TimeZoneInfo Fuso { get; }

    public static Estabelecimento Novo(Empregador empregador, string local, string fuso)
    {
        ArgumentNullException.ThrowIfNull(empregador);
        ArgumentNullException.ThrowIfNull(fuso);

        if (!FusosDoBrasil.Contains(fuso))
        {
            throw new ExcecaoDeDominio(
                $"\"{fuso}\" não é um fuso do Brasil. Use o nome da base IANA, como America/Sao_Paulo ou America/Manaus.");
        }

        return new Estabelecimento(
            empregador,
            Textos.Obrigatorio(local, "o local do estabelecimento", TamanhoDoLocal),
            TimeZoneInfo.FindSystemTimeZoneById(fuso));
    }

    /// <summary>O instante na hora do lugar, com o deslocamento do fuso naquela data.</summary>
    public DateTimeOffset NaHoraLocal(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, Fuso);
}
