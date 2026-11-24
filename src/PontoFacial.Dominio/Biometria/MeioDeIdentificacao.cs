namespace PontoFacial.Dominio.Biometria;

/// <summary>Como o trabalhador provou quem é ao marcar o ponto.</summary>
public enum MeioDeIdentificacao
{
    /// <summary>O código que só ele sabe. Está sempre disponível, para todos.</summary>
    CodigoPessoal = 1,

    /// <summary>O rosto, conferido com o modelo guardado. Só com autorização biométrica vigente.</summary>
    Rosto = 2,
}
