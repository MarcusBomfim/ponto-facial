using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Biometria;

/// <summary>
/// O rosto capturado no coletor comparado com o cadastro de um trabalhador:
/// a similaridade, o limiar usado e o instante. É uma verificação um para um:
/// o trabalhador diz quem é, e o rosto confirma.
/// </summary>
public sealed class ConferenciaFacial
{
    internal ConferenciaFacial(Documento empregador, Cpf trabalhador, double similaridade, LimiarDeAceitacao limiar, DateTimeOffset em)
    {
        Empregador = empregador;
        Trabalhador = trabalhador;
        Similaridade = similaridade;
        Limiar = limiar;
        Em = em;
    }

    public Documento Empregador { get; }

    /// <summary>O trabalhador cujo cadastro foi comparado.</summary>
    public Cpf Trabalhador { get; }

    public double Similaridade { get; }

    public LimiarDeAceitacao Limiar { get; }

    /// <summary>O instante da captura.</summary>
    public DateTimeOffset Em { get; }

    /// <summary>A similaridade alcançou o limiar.</summary>
    public bool Conferiu => Limiar.Aceita(Similaridade);
}
