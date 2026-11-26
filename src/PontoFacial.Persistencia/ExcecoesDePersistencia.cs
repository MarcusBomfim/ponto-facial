namespace PontoFacial.Persistencia;

/*
 * O que pode dar errado ao gravar e ler, cada um com o seu tipo. Quem chama
 * — a API, na Parte 6 — decide o que dizer a quem pediu: 404, 409, 500. As
 * regras do negócio continuam saindo como ExcecaoDeDominio, do domínio, com a
 * mensagem dele.
 */

/// <summary>O que se pediu não está no banco — ou é de outra empresa, o que dá no mesmo.</summary>
public sealed class NaoEncontrado : Exception
{
    public NaoEncontrado()
    {
    }

    public NaoEncontrado(string message)
        : base(message)
    {
    }

    public NaoEncontrado(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>O que se quis gravar pela primeira vez já está gravado: o mesmo CNPJ, o registro já aberto.</summary>
public sealed class JaExiste : Exception
{
    public JaExiste()
    {
    }

    public JaExiste(string message)
        : base(message)
    {
    }

    public JaExiste(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// O que está no banco não é o que o sistema gravou: a marcação que não
/// confere com o hash dela, o tratamento que as regras recusam, o vetor que
/// não decifra. Alguém mexeu no banco por fora do sistema.
/// </summary>
public sealed class ConteudoAdulterado : Exception
{
    public ConteudoAdulterado()
    {
    }

    public ConteudoAdulterado(string message)
        : base(message)
    {
    }

    public ConteudoAdulterado(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
