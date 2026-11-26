using PontoFacial.Dominio.Cadastro;

namespace PontoFacial.Persistencia.Cadastro;

/// <summary>Os estabelecimentos da empresa: cada um com o seu registro de ponto.</summary>
public sealed class Estabelecimentos
{
    private const string Inserir = "insert into ponto.estabelecimentos (id, empregador_id, local, fuso) values ($1, $2, $3, $4)";

    private const string Ler = "select local, fuso from ponto.estabelecimentos where id = $1";

    private readonly EmpresaNoBanco _empresa;

    internal Estabelecimentos(EmpresaNoBanco empresa) => _empresa = empresa;

    /// <summary>Cadastra o estabelecimento, que tem de ser do empregador desta empresa, e devolve o id dele.</summary>
    public Task<Guid> Cadastrar(Estabelecimento estabelecimento, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(estabelecimento);

        return _empresa.Transacao(
            async sessao =>
            {
                var empregador = await _empresa.Empregador(sessao);

                if (empregador.Documento != estabelecimento.Empregador.Documento)
                {
                    throw new InvalidOperationException(
                        $"O estabelecimento é do empregador {estabelecimento.Empregador.Documento}, e esta empresa é o {empregador.Documento}.");
                }

                var id = Guid.CreateVersion7();
                await sessao.Executar(Inserir, id, _empresa.Id, estabelecimento.Local, estabelecimento.Fuso.Id);

                return id;
            },
            cancelamento);
    }

    /// <summary>O estabelecimento, com o empregador como está cadastrado.</summary>
    public Task<Estabelecimento> Obter(Guid id, CancellationToken cancelamento = default) =>
        _empresa.Transacao(sessao => Obter(sessao, id), cancelamento);

    internal async Task<Estabelecimento> Obter(Sessao sessao, Guid id)
    {
        var linha = await sessao.LerUma(Ler, linha => new Linha(linha.GetString(0), linha.GetString(1)), id)
            ?? throw new NaoEncontrado($"O estabelecimento {id} não está cadastrado nesta empresa.");

        var empregador = await _empresa.Empregador(sessao);

        return Leitura.Validada(() => Estabelecimento.Novo(empregador, linha.Local, linha.Fuso), $"O estabelecimento {id}");
    }

    private sealed record Linha(string Local, string Fuso);
}
