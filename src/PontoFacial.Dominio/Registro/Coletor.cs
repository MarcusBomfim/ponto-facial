namespace PontoFacial.Dominio.Registro;

/// <summary>
/// Onde a marcação foi feita, com os códigos do campo "identificador do
/// coletor" do registro de marcação no Arquivo Fonte de Dados.
/// </summary>
public enum Coletor
{
    AplicativoMovel = 1,

    Navegador = 2,

    AplicativoDesktop = 3,

    /// <summary>Um dispositivo eletrônico: o tablet na portaria, em modo quiosque.</summary>
    DispositivoEletronico = 4,

    /// <summary>Outro dispositivo eletrônico, que não os de cima.</summary>
    Outro = 5,
}
