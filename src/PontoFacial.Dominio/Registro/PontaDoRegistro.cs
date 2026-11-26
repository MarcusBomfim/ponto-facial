using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Registro;

/// <summary>
/// O que o registro de ponto precisa saber para continuar a sequência: o
/// último NSR, a hora do relógio, o hash da última marcação e quem está
/// incluído. É o que o banco devolve para retomar o registro sem ler as
/// operações já gravadas (ADR-0013).
/// </summary>
/// <param name="UltimoNsr">O NSR da última operação gravada.</param>
/// <param name="Relogio">A hora do relógio do registro depois dela: a da gravação, ou a acertada, se ela foi um ajuste.</param>
/// <param name="UltimoHash">O hash da última marcação; nulo se ainda não houve marcação.</param>
/// <param name="Incluidos">Quem está incluído agora, com o nome atual.</param>
internal sealed record PontaDoRegistro(long UltimoNsr, DateTimeOffset Relogio, string? UltimoHash, IReadOnlyList<VinculoNaPonta> Incluidos);

/// <summary>Um trabalhador incluído no registro: o nome como está e quando foi incluído pela última vez.</summary>
internal sealed record VinculoNaPonta(Cpf Cpf, string Nome, DateTimeOffset IncluidoEm);
