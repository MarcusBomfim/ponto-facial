namespace PontoFacial.Dominio.Registro;

/// <summary>
/// Uma operação gravada no registro de ponto, com o seu Número Sequencial de
/// Registro. Depois de gravada, não muda e não sai.
/// </summary>
/// <remarks>
/// A Portaria 671 manda gravar cinco tipos de operação (anexo IX, item 6), e
/// todas dividem a mesma numeração: o NSR conta as operações do
/// estabelecimento, não só as marcações. O tipo é o número do registro no
/// Arquivo Fonte de Dados.
/// </remarks>
public abstract class Operacao
{
    private protected Operacao(long nsr, DateTimeOffset gravadaEm)
    {
        Nsr = nsr;
        GravadaEm = gravadaEm;
    }

    /// <summary>Começa em 1 na primeira operação do estabelecimento e sobe de um em um.</summary>
    public long Nsr { get; }

    /// <summary>A hora do relógio do registro na gravação, na hora local, ao minuto.</summary>
    public DateTimeOffset GravadaEm { get; }

    /// <summary>O tipo do registro no Arquivo Fonte de Dados: 2, 4, 5, 6 ou 7.</summary>
    public abstract int TipoDoRegistro { get; }
}
