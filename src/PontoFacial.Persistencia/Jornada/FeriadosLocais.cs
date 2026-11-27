using Npgsql;
using PontoFacial.Dominio.Jornada;

namespace PontoFacial.Persistencia.Jornada;

/// <summary>
/// Os feriados do estado e do município de cada estabelecimento. Os
/// nacionais não se cadastram: estão no domínio, pela lei federal.
/// </summary>
public sealed class FeriadosLocais
{
    private const string Inserir = "insert into ponto.feriados_locais (empregador_id, estabelecimento_id, data, nome) values ($1, $2, $3, $4)";

    private const string Apagar = "delete from ponto.feriados_locais where estabelecimento_id = $1 and data = $2";

    private const string Ler = "select data, nome from ponto.feriados_locais where estabelecimento_id = $1 order by data";

    private readonly EmpresaNoBanco _empresa;

    internal FeriadosLocais(EmpresaNoBanco empresa) => _empresa = empresa;

    /// <summary>
    /// Cadastra o feriado local, pelas regras do calendário: o dia que já é
    /// feriado nacional, ou que já está no calendário, é recusado.
    /// </summary>
    public Task Cadastrar(Guid estabelecimento, DateOnly data, string nome, CancellationToken cancelamento = default) =>
        _empresa.Transacao(
            async sessao =>
            {
                var calendario = await Calendario(sessao, estabelecimento);
                var comONovo = calendario.ComLocais([(data, nome)]);

                try
                {
                    return await sessao.Executar(Inserir, _empresa.Id, estabelecimento, data, comONovo.Locais[data]);
                }
                catch (PostgresException erro) when (erro is { SqlState: PostgresErrorCodes.UniqueViolation })
                {
                    throw new JaExiste($"{data:dd/MM/yyyy} já está no calendário do estabelecimento.", erro);
                }
            },
            cancelamento);

    /// <summary>Tira do calendário o feriado cadastrado por engano. Devolve se havia um.</summary>
    public Task<bool> Remover(Guid estabelecimento, DateOnly data, CancellationToken cancelamento = default) =>
        _empresa.Transacao(
            async sessao =>
            {
                await _empresa.Estabelecimentos.Obter(sessao, estabelecimento);

                return await sessao.Executar(Apagar, estabelecimento, data) > 0;
            },
            cancelamento);

    /// <summary>Os feriados do estabelecimento: os nacionais e os locais cadastrados.</summary>
    public Task<CalendarioDeFeriados> Calendario(Guid estabelecimento, CancellationToken cancelamento = default) =>
        _empresa.Transacao(sessao => Calendario(sessao, estabelecimento), cancelamento);

    internal async Task<CalendarioDeFeriados> Calendario(Sessao sessao, Guid estabelecimento)
    {
        await _empresa.Estabelecimentos.Obter(sessao, estabelecimento);
        var locais = await sessao.Ler(Ler, linha => (Data: linha.GetFieldValue<DateOnly>(0), Nome: linha.GetString(1)), estabelecimento);

        return Leitura.Validada(() => CalendarioDeFeriados.Nacionais.ComLocais(locais), "O calendário de feriados do estabelecimento");
    }
}
