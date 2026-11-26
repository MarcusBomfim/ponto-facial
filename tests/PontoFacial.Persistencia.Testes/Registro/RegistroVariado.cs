using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes;
using PontoFacial.Dominio.Testes.Registro;

namespace PontoFacial.Persistencia.Testes.Registro;

/// <summary>
/// Um registro com uma operação de cada tipo, em 23/11/2026: a abertura,
/// as inclusões, um evento sensível, marcações on-line e off-line, um ajuste
/// que volta o relógio, uma alteração de nome, uma exclusão e uma inclusão de
/// novo. Doze operações.
/// </summary>
internal static class RegistroVariado
{
    public static RegistroDePonto Montar(Empregador empregador)
    {
        var obra = Obras.De(empregador);
        var rh = RegistroDeExemplo.Rh;

        IdentificacaoDoTrabalhador PeloCodigo(Trabalhador trabalhador, DateTimeOffset em) =>
            IdentificacaoDoTrabalhador.PorCodigoPessoal(empregador, trabalhador, em);

        var registro = RegistroDePonto.Abrir(obra, rh, Exemplos.Em(23, 6, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, rh, Exemplos.Em(23, 6, 1));
        registro.IncluirTrabalhador(Exemplos.Maria, rh, Exemplos.Em(23, 6, 2));
        registro.RegistrarEvento(EventoSensivel.DisponibilidadeDeServico, Exemplos.Em(23, 6, 3));
        registro.MarcarOnLine(PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 7, 58, 31)), Coletor.DispositivoEletronico);
        registro.MarcarOffLine(PeloCodigo(Exemplos.Maria, Exemplos.Em(23, 7, 40)), Coletor.AplicativoMovel, Exemplos.Em(23, 8, 5));
        registro.AjustarRelogio(Exemplos.Em(23, 9, 0), Exemplos.Em(23, 8, 57), rh);
        registro.MarcarOnLine(PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 8, 58)), Coletor.Navegador);
        registro.AlterarTrabalhador(MariaDepoisDoCasamento, rh, Exemplos.Em(23, 10, 0));
        registro.ExcluirTrabalhador(Exemplos.Joao.Cpf, rh, Exemplos.Em(23, 17, 30));
        registro.MarcarOnLine(PeloCodigo(Exemplos.Maria, Exemplos.Em(23, 17, 31)), Coletor.DispositivoEletronico);
        registro.IncluirTrabalhador(Exemplos.Joao, rh, Exemplos.Em(23, 18, 0));

        return registro;
    }

    public static readonly Trabalhador MariaDepoisDoCasamento =
        Trabalhador.Novo("Maria Souza Lima", Exemplos.Maria.Cpf, Exemplos.Maria.Admissao, Exemplos.Maria.Cargo);

    /// <summary>Tudo o que a operação guarda, numa linha, com o fuso de cada hora: duas operações iguais dão a mesma linha.</summary>
    public static string Descrever(Operacao operacao) => operacao switch
    {
        InclusaoDoEmpregador inclusao => FormattableString.Invariant(
            $"{inclusao.Nsr} inclusão do empregador {inclusao.GravadaEm:O} {inclusao.Responsavel.Numero} {inclusao.Estabelecimento.Empregador.Documento.Numero} {inclusao.Estabelecimento.Empregador.Nome} | {inclusao.Estabelecimento.Empregador.CeiCaepfOuCno} | {inclusao.Estabelecimento.Local} {inclusao.Estabelecimento.Fuso.Id}"),
        AjusteDoRelogio ajuste => FormattableString.Invariant(
            $"{ajuste.Nsr} ajuste {ajuste.Antes:O} para {ajuste.Ajustado:O} por {ajuste.Responsavel.Numero}"),
        MudancaNoTrabalhador mudanca => FormattableString.Invariant(
            $"{mudanca.Nsr} {mudanca.Mudanca} {mudanca.GravadaEm:O} {mudanca.Cpf.Numero} {mudanca.Nome} por {mudanca.Responsavel.Numero}"),
        OcorrenciaDeEventoSensivel ocorrencia => FormattableString.Invariant(
            $"{ocorrencia.Nsr} evento {ocorrencia.Evento} {ocorrencia.GravadaEm:O}"),
        MarcacaoDePonto marcacao => FormattableString.Invariant(
            $"{marcacao.Nsr} marcação {marcacao.LinhaDoAfd} depois de {marcacao.HashAnterior} pelo {marcacao.Meio}, {marcacao.MarcadaEm:O}, {marcacao.GravadaEm:O}"),
        _ => throw new InvalidOperationException($"Operação de tipo {operacao.TipoDoRegistro} sem descrição."),
    };
}
