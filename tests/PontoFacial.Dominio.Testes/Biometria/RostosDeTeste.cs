using PontoFacial.Dominio.Biometria;

namespace PontoFacial.Dominio.Testes.Biometria;

/// <summary>
/// Rostos de mentira, em quatro dimensões, para os testes do domínio: o do
/// João aponta para um lado, o da Maria para outro. Na vida real, os vetores
/// saem do reconhecedor.
/// </summary>
internal static class RostosDeTeste
{
    public static readonly LimiarDeAceitacao Limiar = LimiarDeAceitacao.De("teste-4d", 0.5, "valor de teste");

    public static readonly ModeloFacial[] CapturasDoJoao =
    [
        ModeloFacial.De("teste-4d", [1f, 0.2f, 0f, 0f]),
        ModeloFacial.De("teste-4d", [1f, 0f, 0.2f, 0f]),
        ModeloFacial.De("teste-4d", [1f, 0f, 0f, 0.2f]),
    ];

    public static readonly ModeloFacial JoaoNaPortaria = ModeloFacial.De("teste-4d", [1f, 0.1f, 0.1f, 0f]);

    public static readonly ModeloFacial MariaNaPortaria = ModeloFacial.De("teste-4d", [0f, 1f, 0f, 0f]);

    /// <summary>O cadastro do João, feito com a autorização dada.</summary>
    public static CadastroFacial CadastroDoJoao(AutorizacaoBiometrica autorizacao) =>
        CadastroFacial.Cadastrar(autorizacao, CapturasDoJoao, Limiar, autorizacao.ConcedidaEm);

    /// <summary>O rosto do João conferido com o cadastro dele, no instante dado.</summary>
    public static ConferenciaFacial JoaoConferido(AutorizacaoBiometrica autorizacao, DateTimeOffset em) =>
        CadastroDoJoao(autorizacao).Conferir(JoaoNaPortaria, Limiar, em);
}
