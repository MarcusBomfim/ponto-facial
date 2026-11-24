namespace PontoFacial.Dominio.Biometria;

/// <summary>Por que a autorização biométrica acabou. Os dois levam à eliminação do modelo do rosto.</summary>
public enum MotivoDoEncerramento
{
    /// <summary>O trabalhador revogou o consentimento (LGPD, art. 8º, § 5º, e art. 18, IX).</summary>
    RevogadaPeloTrabalhador = 1,

    /// <summary>O contrato acabou, e com ele a finalidade (LGPD, art. 15, I).</summary>
    Desligamento = 2,
}
