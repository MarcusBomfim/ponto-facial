using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Registro;

/// <summary>
/// Um trabalhador entrou no registro, teve os dados alterados ou saiu dele
/// (anexo IX, item 6.3). Só quem está incluído marca o ponto.
/// </summary>
public sealed class MudancaNoTrabalhador : Operacao
{
    internal MudancaNoTrabalhador(long nsr, DateTimeOffset gravadaEm, TipoDeMudanca mudanca, Cpf cpf, string nome, Cpf responsavel)
        : base(nsr, gravadaEm)
    {
        Mudanca = mudanca;
        Cpf = cpf;
        Nome = nome;
        Responsavel = responsavel;
    }

    public override int TipoDoRegistro => 5;

    public TipoDeMudanca Mudanca { get; }

    public Cpf Cpf { get; }

    /// <summary>O nome do trabalhador como ficou depois da mudança.</summary>
    public string Nome { get; }

    public Cpf Responsavel { get; }
}
