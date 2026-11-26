using PontoFacial.Dominio.Biometria.Vivacidade;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Biometria;

/// <summary>
/// O rosto capturado no coletor comparado com o cadastro de um trabalhador:
/// a similaridade, o limiar usado, o instante e a prova de vida de onde o
/// rosto saiu. É uma verificação um para um: o trabalhador diz quem é, e o
/// rosto confirma.
/// </summary>
public sealed class ConferenciaFacial
{
    internal ConferenciaFacial(Documento empregador, Cpf trabalhador, double similaridade, LimiarDeAceitacao limiar, DateTimeOffset em, ProvaDeVida? prova)
    {
        Empregador = empregador;
        Trabalhador = trabalhador;
        Similaridade = similaridade;
        Limiar = limiar;
        Em = em;
        Prova = prova;
    }

    public Documento Empregador { get; }

    /// <summary>O trabalhador cujo cadastro foi comparado.</summary>
    public Cpf Trabalhador { get; }

    public double Similaridade { get; }

    public LimiarDeAceitacao Limiar { get; }

    /// <summary>O instante da captura.</summary>
    public DateTimeOffset Em { get; }

    /// <summary>
    /// A prova de vida de onde o rosto saiu; nada quando foi comparado um
    /// vetor solto, como na calibração.
    /// </summary>
    public ProvaDeVida? Prova { get; }

    /// <summary>A similaridade alcançou o limiar.</summary>
    public bool Conferiu => Limiar.Aceita(Similaridade);
}
