using PontoFacial.Dominio.Arquivos;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Testes.Arquivos;

/// <summary>Este programa nos testes: um desenvolvedor de exemplo, com o registro no INPI dos exemplos do README.</summary>
internal static class Programas
{
    public static readonly ProgramaDoPonto PontoFacial = ProgramaDoPonto.De(
        "Ponto Facial",
        "0.6.0",
        "BR512026001234-5",
        Cnpj.De("11.444.777/0001-61"),
        "Desenvolvedora de Exemplo Ltda.",
        "contato@exemplo.com.br");
}
