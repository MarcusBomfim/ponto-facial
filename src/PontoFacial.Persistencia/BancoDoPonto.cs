using Npgsql;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Persistencia;

/// <summary>
/// O banco do ponto: a porta de entrada da persistência. Cada empresa é um
/// empregador, e tudo o que se lê e grava passa por uma delas
/// (<see cref="EmpresaNoBanco"/>).
/// </summary>
/// <remarks>
/// A fonte de conexões é a do papel da aplicação, que a segurança por linha
/// alcança; o dono do esquema só aparece para aplicar as migrações
/// (ADR-0012).
/// </remarks>
public sealed class BancoDoPonto(NpgsqlDataSource fonte)
{
    private const string InserirEmpregador = """
        insert into ponto.empregadores (id, documento, nome, cei_caepf_ou_cno)
        values ($1, $2, $3, $4)
        """;

    internal NpgsqlDataSource Fonte { get; } = fonte ?? throw new ArgumentNullException(nameof(fonte));

    /// <summary>Cadastra o empregador como uma empresa nova, com um id novo.</summary>
    public async Task<EmpresaNoBanco> Cadastrar(Empregador empregador, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(empregador);

        var empresa = Empresa(Guid.CreateVersion7());

        try
        {
            await empresa.Transacao(
                sessao => sessao.Executar(InserirEmpregador, empresa.Id, empregador.Documento.Numero, empregador.Nome, empregador.CeiCaepfOuCno),
                cancelamento);
        }
        catch (PostgresException erro) when (erro is { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "uq_empregadores_documento" })
        {
            var documento = empregador.Documento is Cnpj ? "CNPJ" : "CPF";

            throw new JaExiste($"O empregador de {documento} {empregador.Documento.Formatado} já está cadastrado.", erro);
        }

        return empresa;
    }

    /// <summary>
    /// A empresa de id dado, para ler e gravar em nome dela. Não consulta o
    /// banco: uma empresa que não existe simplesmente não encontra nada.
    /// </summary>
    public EmpresaNoBanco Empresa(Guid id) => new(this, id);
}
