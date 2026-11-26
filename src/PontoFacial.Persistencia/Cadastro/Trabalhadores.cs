using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Persistencia.Cadastro;

/// <summary>
/// Os trabalhadores da empresa, pelo CPF. O nome, a admissão e o cargo
/// mudam; o CPF, não. Toda operação do registro com um CPF é de um
/// trabalhador cadastrado aqui.
/// </summary>
public sealed class Trabalhadores
{
    private const string Gravacao = """
        insert into ponto.trabalhadores (empregador_id, cpf, nome, admissao, cargo)
        values ($1, $2, $3, $4, $5)
        on conflict (empregador_id, cpf) do update
            set nome = excluded.nome, admissao = excluded.admissao, cargo = excluded.cargo, atualizado_em = now()
        """;

    private const string Ler = "select nome, admissao, cargo from ponto.trabalhadores where cpf = $1";

    private readonly EmpresaNoBanco _empresa;

    internal Trabalhadores(EmpresaNoBanco empresa) => _empresa = empresa;

    /// <summary>Cadastra o trabalhador, ou atualiza o cadastro de quem já tem o mesmo CPF.</summary>
    public Task Gravar(Trabalhador trabalhador, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);

        return _empresa.Transacao(
            sessao => sessao.Executar(Gravacao, _empresa.Id, trabalhador.Cpf.Numero, trabalhador.Nome, trabalhador.Admissao, trabalhador.Cargo),
            cancelamento);
    }

    public Task<Trabalhador> Obter(Cpf cpf, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(cpf);

        return _empresa.Transacao(
            async sessao => await sessao.LerUma(
                Ler,
                linha => Leitura.Validada(
                    () => Trabalhador.Novo(linha.GetString(0), cpf, linha.GetFieldValue<DateOnly>(1), linha.GetString(2)),
                    $"O cadastro do trabalhador {cpf}"),
                cpf.Numero)
                ?? throw new NaoEncontrado($"O trabalhador {cpf} não está cadastrado nesta empresa."),
            cancelamento);
    }
}
