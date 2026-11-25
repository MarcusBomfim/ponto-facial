using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Biometria;

/// <summary>
/// O trabalhador identificado no coletor, num instante: de qual empregador,
/// qual CPF e por qual meio. É o que a marcação de ponto recebe.
/// </summary>
/// <remarks>
/// A Portaria 671 pede que o registrador receba "de forma inequívoca a
/// identificação do trabalhador" (anexo IX, item 8.1), sem dizer como. Aqui
/// há dois meios, e só um depende de biometria. A conferência em si — o rosto
/// contra o modelo guardado, o código contra o que foi cadastrado — é feita
/// por quem guarda esses dados, fora do domínio; o domínio garante que o
/// rosto só identifica quem autorizou, enquanto a autorização vale.
/// </remarks>
public sealed class IdentificacaoDoTrabalhador
{
    private IdentificacaoDoTrabalhador(Documento empregador, Cpf trabalhador, MeioDeIdentificacao meio, DateTimeOffset em)
    {
        Empregador = empregador;
        Trabalhador = trabalhador;
        Meio = meio;
        Em = em;
    }

    public Documento Empregador { get; }

    public Cpf Trabalhador { get; }

    public MeioDeIdentificacao Meio { get; }

    /// <summary>O instante da identificação, que é o da marcação.</summary>
    public DateTimeOffset Em { get; }

    /// <summary>Pelo código pessoal: não depende de autorização nenhuma.</summary>
    public static IdentificacaoDoTrabalhador PorCodigoPessoal(Empregador empregador, Trabalhador trabalhador, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(empregador);
        ArgumentNullException.ThrowIfNull(trabalhador);

        return new IdentificacaoDoTrabalhador(empregador.Documento, trabalhador.Cpf, MeioDeIdentificacao.CodigoPessoal, em);
    }

    /// <summary>
    /// Pelo rosto: a conferência com o cadastro tem de ter alcançado o
    /// limiar, ser do trabalhador e do empregador da autorização, e a
    /// autorização tem de valer no instante da captura, que é o da marcação.
    /// </summary>
    public static IdentificacaoDoTrabalhador PorRosto(AutorizacaoBiometrica autorizacao, ConferenciaFacial conferencia)
    {
        ArgumentNullException.ThrowIfNull(autorizacao);
        ArgumentNullException.ThrowIfNull(conferencia);

        if (!autorizacao.VigenteEm(conferencia.Em))
        {
            throw new ExcecaoDeDominio(
                "Sem autorização biométrica vigente, o rosto não identifica o trabalhador. O código pessoal continua valendo.");
        }

        if (conferencia.Empregador != autorizacao.Empregador || conferencia.Trabalhador != autorizacao.Trabalhador)
        {
            throw new ExcecaoDeDominio("A conferência do rosto é de outro cadastro que não o da autorização.");
        }

        if (!conferencia.Conferiu)
        {
            throw new ExcecaoDeDominio(
                "O rosto não conferiu com o cadastro do trabalhador. Tente de novo ou marque pelo código pessoal.");
        }

        return new IdentificacaoDoTrabalhador(autorizacao.Empregador, autorizacao.Trabalhador, MeioDeIdentificacao.Rosto, conferencia.Em);
    }
}
