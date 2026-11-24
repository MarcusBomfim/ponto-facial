namespace PontoFacial.Dominio.Registro;

/// <summary>O resultado de conferir as marcações de um registro: íntegras, ou onde a cadeia quebrou e por quê.</summary>
public sealed class ConferenciaDaCadeia
{
    public static readonly ConferenciaDaCadeia Integra = new(null, null);

    private ConferenciaDaCadeia(int? linha, string? motivo)
    {
        Linha = linha;
        Motivo = motivo;
    }

    public bool EstaIntegra => Motivo is null;

    /// <summary>A linha, contada a partir de 1, onde a cadeia quebrou; nula se está íntegra ou se o que não confere é um comprovante.</summary>
    public int? Linha { get; }

    public string? Motivo { get; }

    internal static ConferenciaDaCadeia Quebrada(int? linha, string motivo) => new(linha, motivo);
}
