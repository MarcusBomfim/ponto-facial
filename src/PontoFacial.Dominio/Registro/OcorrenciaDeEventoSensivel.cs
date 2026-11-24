namespace PontoFacial.Dominio.Registro;

/// <summary>
/// O serviço de registro ficou disponível ou indisponível, ou a energia
/// voltou (anexo IX, item 6.4). Explica, para o fiscal, um intervalo sem
/// marcações on-line.
/// </summary>
public sealed class OcorrenciaDeEventoSensivel : Operacao
{
    internal OcorrenciaDeEventoSensivel(long nsr, DateTimeOffset gravadaEm, EventoSensivel evento)
        : base(nsr, gravadaEm) => Evento = evento;

    public override int TipoDoRegistro => 6;

    public EventoSensivel Evento { get; }
}
