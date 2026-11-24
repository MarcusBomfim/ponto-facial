namespace PontoFacial.Dominio.Comum;

/// <summary>
/// Algo que aconteceu no domínio e que outra parte do sistema precisa saber
/// para agir — o domínio decide, quem está fora executa.
/// </summary>
public abstract record EventoDeDominio(DateTimeOffset OcorreuEm);
