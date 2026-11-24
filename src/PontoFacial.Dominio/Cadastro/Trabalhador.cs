using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Cadastro;

/// <summary>
/// Quem marca o ponto. O CPF o identifica em cada marcação; o nome, a
/// admissão e o cargo vão no espelho de ponto (art. 84, II, da Portaria 671).
/// </summary>
public sealed class Trabalhador
{
    /// <summary>O nome cabe em 52 caracteres, o campo de nome do empregado no Arquivo Fonte de Dados.</summary>
    public const int TamanhoDoNome = 52;

    private Trabalhador(string nome, Cpf cpf, DateOnly admissao, string cargo)
    {
        Nome = nome;
        Cpf = cpf;
        Admissao = admissao;
        Cargo = cargo;
    }

    public string Nome { get; }

    public Cpf Cpf { get; }

    public DateOnly Admissao { get; }

    /// <summary>O cargo ou a função.</summary>
    public string Cargo { get; }

    public static Trabalhador Novo(string nome, Cpf cpf, DateOnly admissao, string cargo)
    {
        ArgumentNullException.ThrowIfNull(cpf);

        return new Trabalhador(
            Textos.Obrigatorio(nome, "o nome do trabalhador", TamanhoDoNome),
            cpf,
            admissao,
            Textos.Obrigatorio(cargo, "o cargo ou a função", 60));
    }
}
