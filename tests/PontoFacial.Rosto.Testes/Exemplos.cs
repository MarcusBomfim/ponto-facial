using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Rosto.Testes;

/// <summary>O cadastro mínimo do domínio para os testes que levam o rosto até a autorização.</summary>
internal static class Exemplos
{
    public static readonly Empregador Empregador = Empregador.Novo(Cnpj.De("11.222.333/0001-81"), "Construtora Horizonte Ltda.");

    public static readonly Trabalhador Trabalhador = Trabalhador.Novo("Marcos Pontes", Cpf.De("529.982.247-25"), new DateOnly(2026, 3, 2), "Engenheiro");

    public static readonly TermoDeConsentimento Termo = TermoDeConsentimento.Novo("2026.1", "Autorizo o uso do modelo do meu rosto só para marcar o ponto.");

    public static DateTimeOffset Em(int hora, int minuto) => new(2026, 11, 24, hora, minuto, 0, TimeSpan.FromHours(-3));
}
