using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Testes;

namespace PontoFacial.Persistencia.Testes;

/// <summary>A obra dos exemplos, no empregador da empresa do teste: o mesmo endereço e o mesmo fuso, com o CNPJ dela.</summary>
internal static class Obras
{
    public static Estabelecimento De(Empregador empregador) => Estabelecimento.Novo(empregador, Exemplos.Obra.Local, "America/Sao_Paulo");
}
