using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Registro;

/// <summary>
/// O relógio do registro foi acertado: a hora de antes, a hora acertada e
/// quem acertou (anexo IX, item 6.2).
/// </summary>
/// <remarks>
/// É o único jeito de o relógio voltar. Sem o ajuste, uma operação gravada
/// antes da anterior é recusada; com ele, a correção fica registrada, com
/// nome, para o fiscal ver.
/// </remarks>
public sealed class AjusteDoRelogio : Operacao
{
    internal AjusteDoRelogio(long nsr, DateTimeOffset antes, DateTimeOffset ajustado, Cpf responsavel)
        : base(nsr, antes)
    {
        Antes = antes;
        Ajustado = ajustado;
        Responsavel = responsavel;
    }

    public override int TipoDoRegistro => 4;

    /// <summary>A hora que o relógio mostrava antes do ajuste.</summary>
    public DateTimeOffset Antes { get; }

    /// <summary>A hora certa, depois do ajuste.</summary>
    public DateTimeOffset Ajustado { get; }

    public Cpf Responsavel { get; }
}
