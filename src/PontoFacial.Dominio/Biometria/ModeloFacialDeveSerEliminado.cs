using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Biometria;

/// <summary>
/// A autorização acabou, e o modelo do rosto do trabalhador — o vetor
/// guardado no lugar da foto — tem de ser apagado (LGPD, art. 16).
/// </summary>
/// <remarks>
/// Só o modelo do rosto sai. As marcações feitas com ele ficam: o registro de
/// ponto é guardado pelo prazo legal (Portaria 671, anexo IX, item 7), e a
/// LGPD autoriza conservar o que cumpre obrigação legal (art. 16, I).
/// </remarks>
public sealed record ModeloFacialDeveSerEliminado(
    Documento Empregador,
    Cpf Trabalhador,
    MotivoDoEncerramento Motivo,
    DateTimeOffset OcorreuEm) : EventoDeDominio(OcorreuEm);
