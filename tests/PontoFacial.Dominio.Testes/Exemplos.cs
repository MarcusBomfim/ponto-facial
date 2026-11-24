using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Testes;

/// <summary>Um empregador, uma obra e dois trabalhadores, fictícios, para os testes.</summary>
internal static class Exemplos
{
    public const string TextoDoTermo =
        "Autorizo a Construtora Horizonte Ltda. a usar o modelo do meu rosto apenas para me identificar na "
        + "marcação de ponto. Sei que posso marcar pelo código pessoal e revogar esta autorização a qualquer momento.";

    public static readonly Empregador Construtora = Empregador.Novo(Cnpj.De("11.222.333/0001-81"), "Construtora Horizonte Ltda.");

    public static readonly Estabelecimento Obra = Estabelecimento.Novo(Construtora, "Rua das Palmeiras, 100 - Santos/SP", "America/Sao_Paulo");

    public static readonly Trabalhador Joao = Trabalhador.Novo("João da Silva", Cpf.De("529.982.247-25"), new DateOnly(2026, 3, 2), "Pedreiro");

    public static readonly Trabalhador Maria = Trabalhador.Novo("Maria Souza", Cpf.De("111.444.777-35"), new DateOnly(2026, 5, 4), "Mestre de obras");

    public static readonly TermoDeConsentimento Termo = TermoDeConsentimento.Novo("2026.1", TextoDoTermo);

    /// <summary>Um instante de novembro de 2026, na hora de Brasília.</summary>
    public static DateTimeOffset Em(int dia, int hora, int minuto, int segundo = 0) =>
        new(2026, 11, dia, hora, minuto, segundo, TimeSpan.FromHours(-3));
}
