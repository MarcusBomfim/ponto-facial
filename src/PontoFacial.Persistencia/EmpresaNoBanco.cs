using Npgsql;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Persistencia.Arquivos;
using PontoFacial.Persistencia.Cadastro;
using PontoFacial.Persistencia.Jornada;
using PontoFacial.Persistencia.Registro;

namespace PontoFacial.Persistencia;

/// <summary>
/// Uma empresa no banco: o que se lê e grava em nome dela. Cada operação é
/// uma transação própria, com a empresa definida no começo.
/// </summary>
/// <remarks>
/// <para>
/// A empresa vai para o banco num parâmetro da transação
/// (<c>ponto.empregador_id</c>), e cada tabela tem uma política de segurança
/// por linha que só deixa ver e gravar as linhas dela. O filtro não depende
/// de quem escreve o SQL lembrar do <c>where</c>: uma consulta escrita
/// errado, aqui ou na Parte 6, ainda não vê outra empresa (ADR-0012).
/// </para>
/// <para>
/// O parâmetro é local à transação. Ele some no commit e no rollback, e uma
/// conexão que volta ao pool não leva a empresa de um pedido para o outro.
/// </para>
/// </remarks>
public sealed class EmpresaNoBanco
{
    private const string DefinirEmpresa = "select set_config('ponto.empregador_id', $1, true)";

    private const string LerEmpregador = "select documento, nome, cei_caepf_ou_cno from ponto.empregadores where id = $1";

    internal EmpresaNoBanco(BancoDoPonto banco, Guid id)
    {
        Banco = banco;
        Id = id;
        Estabelecimentos = new Estabelecimentos(this);
        Trabalhadores = new Trabalhadores(this);
        Registros = new RegistrosDePonto(this);
        Tratamentos = new TratamentosDoPonto(this);
        Horarios = new HorariosContratuais(this);
        Feriados = new FeriadosLocais(this);
        Arquivos = new ArquivosDaPortaria(this);
    }

    /// <summary>O id do empregador, que é o da empresa.</summary>
    public Guid Id { get; }

    public Estabelecimentos Estabelecimentos { get; }

    public Trabalhadores Trabalhadores { get; }

    /// <summary>O registro de ponto de cada estabelecimento.</summary>
    public RegistrosDePonto Registros { get; }

    /// <summary>O tratamento do ponto de cada trabalhador, em cada estabelecimento.</summary>
    public TratamentosDoPonto Tratamentos { get; }

    /// <summary>O horário contratual de cada trabalhador, com a data em que passou a valer.</summary>
    public HorariosContratuais Horarios { get; }

    /// <summary>Os feriados locais de cada estabelecimento.</summary>
    public FeriadosLocais Feriados { get; }

    /// <summary>O AFD e o AEJ, para o Auditor-Fiscal do Trabalho.</summary>
    public ArquivosDaPortaria Arquivos { get; }

    internal BancoDoPonto Banco { get; }

    /// <summary>O empregador como está cadastrado.</summary>
    public Task<Empregador> Empregador(CancellationToken cancelamento = default) =>
        Transacao(Empregador, cancelamento);

    /// <summary>Abre a transação em nome desta empresa, faz o trabalho e confirma; qualquer exceção desfaz tudo.</summary>
    internal async Task<T> Transacao<T>(Func<Sessao, Task<T>> trabalho, CancellationToken cancelamento)
    {
        await using var conexao = await Banco.Fonte.OpenConnectionAsync(cancelamento);
        await using var transacao = await conexao.BeginTransactionAsync(cancelamento);

        var sessao = new Sessao(conexao, transacao, cancelamento);
        await sessao.Executar(DefinirEmpresa, Id.ToString());

        var resultado = await trabalho(sessao);
        await transacao.CommitAsync(cancelamento);

        return resultado;
    }

    /// <summary>O empregador, lido dentro de uma transação já aberta.</summary>
    internal async Task<Empregador> Empregador(Sessao sessao) =>
        await sessao.LerUma(
            LerEmpregador,
            linha => Leitura.Validada(
                () => Dominio.Cadastro.Empregador.Novo(Documento.De(linha.GetString(0)), linha.GetString(1), linha.IsDBNull(2) ? null : linha.GetString(2)),
                "O cadastro do empregador"),
            Id)
        ?? throw new NaoEncontrado("A empresa não está cadastrada.");
}

/// <summary>Como uma linha lida vira objeto do domínio: pelas mesmas regras de quando ele foi criado.</summary>
internal static class Leitura
{
    /// <summary>
    /// O objeto refeito pela fábrica do domínio. Se ela o recusa, o banco tem
    /// o que o domínio nunca teria gravado, e quem mexeu foi alguém de fora.
    /// </summary>
    public static T Validada<T>(Func<T> fabrica, string oQue)
    {
        try
        {
            return fabrica();
        }
        catch (Exception erro) when (erro is Dominio.Comum.ExcecaoDeDominio or ArgumentException or InvalidCastException)
        {
            throw new ConteudoAdulterado($"{oQue} no banco não passa pelas regras do domínio: {erro.Message}", erro);
        }
    }

    public static string? TextoOuNulo(NpgsqlDataReader linha, int coluna) => linha.IsDBNull(coluna) ? null : linha.GetString(coluna);
}
