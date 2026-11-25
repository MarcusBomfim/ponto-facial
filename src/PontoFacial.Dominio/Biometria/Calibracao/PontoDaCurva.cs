namespace PontoFacial.Dominio.Biometria.Calibracao;

/// <summary>Um ponto da curva da calibração: a falsa aceitação tolerada e o limiar e os erros que ela dá.</summary>
public sealed record PontoDaCurva(double FalsaAceitacaoTolerada, PontoDeOperacao Operacao);
