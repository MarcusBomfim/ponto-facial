using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Registro;

namespace PontoFacial.Dominio.Testes.Registro;

/// <summary>O registro da obra, aberto às 6h de 23/11/2026, com João e Maria incluídos: NSR 1, 2 e 3.</summary>
internal static class RegistroDeExemplo
{
    public const string RegistroNoInpi = "BR512026001234-5";

    /// <summary>Quem cuida do ponto no RH.</summary>
    public static readonly Cpf Rh = Cpf.De("123.456.789-09");

    public static RegistroDePonto ComJoaoEMaria()
    {
        var registro = RegistroDePonto.Abrir(Exemplos.Obra, Rh, Exemplos.Em(23, 6, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, Rh, Exemplos.Em(23, 6, 1));
        registro.IncluirTrabalhador(Exemplos.Maria, Rh, Exemplos.Em(23, 6, 2));

        return registro;
    }

    public static IdentificacaoDoTrabalhador PeloCodigo(Trabalhador trabalhador, DateTimeOffset em) =>
        IdentificacaoDoTrabalhador.PorCodigoPessoal(Exemplos.Construtora, trabalhador, em);
}
