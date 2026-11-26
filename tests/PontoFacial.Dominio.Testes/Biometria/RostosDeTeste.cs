using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Biometria.Vivacidade;
using PontoFacial.Dominio.Testes.Biometria.Vivacidade;

namespace PontoFacial.Dominio.Testes.Biometria;

/// <summary>
/// Rostos de mentira, em quatro dimensões, para os testes do domínio: o do
/// João aponta para um lado, o da Maria para outro. Na vida real, os vetores
/// saem do reconhecedor, e a prova de vida, da câmera.
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

    /// <summary>
    /// Uma prova de vida aprovada que termina no instante dado: a cabeça de
    /// teste segue o desafio, com o rosto dado no começo e no fim.
    /// </summary>
    public static ProvaDeVida Prova(ModeloFacial rosto, DateTimeOffset em) => Prova(rosto, rosto, em);

    /// <summary>A prova com um rosto no começo e outro no fim.</summary>
    public static ProvaDeVida Prova(ModeloFacial noComeco, ModeloFacial noFim, DateTimeOffset em)
    {
        var desafio = Desafio.De([Movimento.Esquerda, Movimento.Cima, Movimento.Direita]);
        var quadros = Cenas.Seguindo(Cenas.Cabeca, desafio, Cenas.Obedecendo);
        var deslocamento = em - quadros[^1].Em;
        var sessao = SessaoDaProvaDeVida.Iniciar(desafio, Cenas.Criterio, new QuadroDaCamera(Cenas.Quadro(0) + deslocamento, Cenas.Ver(Cenas.Cabeca), noComeco));

        foreach (var quadro in quadros)
        {
            sessao.Registrar(quadro with { Em = quadro.Em + deslocamento, Modelo = quadro.Modelo is null ? null : noFim });
        }

        return sessao.Resultado!;
    }

    /// <summary>O rosto do João, numa prova de vida, conferido com o cadastro dele no instante dado.</summary>
    public static ConferenciaFacial JoaoConferido(AutorizacaoBiometrica autorizacao, DateTimeOffset em) =>
        CadastroDoJoao(autorizacao).Conferir(Prova(JoaoNaPortaria, em), Limiar);
}
