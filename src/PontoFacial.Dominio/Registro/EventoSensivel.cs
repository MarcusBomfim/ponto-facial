namespace PontoFacial.Dominio.Registro;

/// <summary>
/// Os eventos sensíveis que um registrador por programa (REP-P) grava, com os
/// códigos do Arquivo Fonte de Dados. Os outros códigos são de relógio físico.
/// </summary>
public enum EventoSensivel
{
    RetornoDeEnergia = 2,

    DisponibilidadeDeServico = 7,

    IndisponibilidadeDeServico = 8,
}
