using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Registro;

/// <summary>
/// A primeira operação do registro: os dados do empregador e do local
/// (anexo IX, item 6.1). É ela que leva o NSR 1.
/// </summary>
public sealed class InclusaoDoEmpregador : Operacao
{
    internal InclusaoDoEmpregador(long nsr, DateTimeOffset gravadaEm, Estabelecimento estabelecimento, Cpf responsavel)
        : base(nsr, gravadaEm)
    {
        Estabelecimento = estabelecimento;
        Responsavel = responsavel;
    }

    public override int TipoDoRegistro => 2;

    public Estabelecimento Estabelecimento { get; }

    /// <summary>Quem fez a inclusão.</summary>
    public Cpf Responsavel { get; }
}
