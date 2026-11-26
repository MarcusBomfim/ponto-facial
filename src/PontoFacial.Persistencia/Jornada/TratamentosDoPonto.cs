using System.Globalization;
using Npgsql;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Persistencia.Registro;

namespace PontoFacial.Persistencia.Jornada;

/// <summary>
/// O tratamento do ponto de cada trabalhador (art. 82 da Portaria 671), ao
/// lado do registro: as marcações incluídas, as desconsideradas e os dias
/// abonados, em sequência. Como o registro, só cresce (ADR-0013).
/// </summary>
/// <remarks>
/// A leitura refaz o tratamento pelo domínio: as marcações do trabalhador
/// vêm do registro, e cada tratamento gravado é repetido na ordem, pelos
/// métodos de sempre. Um tratamento que as regras recusariam não volta do
/// banco.
/// </remarks>
public sealed class TratamentosDoPonto
{
    private const string Ler = """
        select sequencia, tipo, marcada_em, marcada_em_deslocamento, nsr, dia, motivo, responsavel, tratado_em, tratado_em_deslocamento
        from ponto.tratamentos
        where estabelecimento_id = $1 and cpf = $2
        order by sequencia
        """;

    private const string Inserir = """
        insert into ponto.tratamentos (
            empregador_id, estabelecimento_id, cpf, sequencia, tipo,
            marcada_em, marcada_em_deslocamento, nsr, dia, motivo, responsavel, tratado_em, tratado_em_deslocamento)
        values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13)
        """;

    /// <summary>A letra do abono na coluna <c>tipo</c>; as das marcações são as do <see cref="TipoDeTratamento"/>.</summary>
    private const string Abono = "A";

    private readonly EmpresaNoBanco _empresa;

    internal TratamentosDoPonto(EmpresaNoBanco empresa) => _empresa = empresa;

    /// <summary>O tratamento do trabalhador no registro do estabelecimento, com tudo o que já foi tratado.</summary>
    public Task<TratamentoDoPonto> Obter(Guid estabelecimento, Cpf trabalhador, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);

        return _empresa.Transacao(async sessao => (await Refazer(sessao, estabelecimento, trabalhador)).Tratamento, cancelamento);
    }

    /// <summary>
    /// Faz o tratamento — incluir, desconsiderar, abonar — e grava o que ele
    /// acrescentou, na fila do estabelecimento. Se o domínio recusa, nada se
    /// grava.
    /// </summary>
    public Task<T> Tratar<T>(Guid estabelecimento, Cpf trabalhador, Func<TratamentoDoPonto, T> tratar, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);
        ArgumentNullException.ThrowIfNull(tratar);

        return _empresa.Transacao(
            async sessao =>
            {
                await sessao.EntrarNaFila(estabelecimento);
                var (tratamento, gravados) = await Refazer(sessao, estabelecimento, trabalhador);
                var marcacoesAntes = tratamento.Marcacoes.Count;
                var abonosAntes = tratamento.Abonos.Keys.ToHashSet();

                var resultado = tratar(tratamento);

                var sequencia = gravados;

                foreach (var marcacao in tratamento.Marcacoes.Skip(marcacoesAntes))
                {
                    await sessao.Executar(
                        Inserir,
                        _empresa.Id,
                        estabelecimento,
                        trabalhador.Numero,
                        ++sequencia,
                        ((char)marcacao.Tipo).ToString(),
                        marcacao.Tipo == TipoDeTratamento.Incluida ? Instantes.Utc(marcacao.MarcadaEm) : null,
                        marcacao.Tipo == TipoDeTratamento.Incluida ? Instantes.Deslocamento(marcacao.MarcadaEm) : null,
                        marcacao.Nsr,
                        null,
                        marcacao.Motivo,
                        marcacao.Responsavel.Numero,
                        Instantes.Utc(marcacao.Em),
                        Instantes.Deslocamento(marcacao.Em));
                }

                foreach (var abono in tratamento.Abonos.Values.Where(abono => !abonosAntes.Contains(abono.Dia)).OrderBy(abono => abono.Dia))
                {
                    await sessao.Executar(
                        Inserir,
                        _empresa.Id,
                        estabelecimento,
                        trabalhador.Numero,
                        ++sequencia,
                        Abono,
                        null,
                        null,
                        null,
                        abono.Dia,
                        abono.Motivo,
                        abono.Responsavel.Numero,
                        Instantes.Utc(abono.Em),
                        Instantes.Deslocamento(abono.Em));
                }

                return resultado;
            },
            cancelamento);
    }

    private async Task<(TratamentoDoPonto Tratamento, int Gravados)> Refazer(Sessao sessao, Guid estabelecimento, Cpf trabalhador)
    {
        var local = await _empresa.Estabelecimentos.Obter(sessao, estabelecimento);
        var (incluido, marcacoes) = await RegistrosDePonto.DoTrabalhador(sessao, estabelecimento, trabalhador);
        var tratamento = TratamentoDoPonto.Reconstituir(local, trabalhador, incluido, marcacoes);
        var gravados = await sessao.Ler(Ler, Passo.De, estabelecimento, trabalhador.Numero);

        foreach (var passo in gravados)
        {
            Leitura.Validada(
                () => passo.Tipo switch
                {
                    'I' => tratamento.Incluir(passo.MarcadaEm!.Value, passo.Motivo, passo.Responsavel, passo.Em),
                    'D' => tratamento.Desconsiderar(passo.Nsr!.Value, passo.Motivo, passo.Responsavel, passo.Em),
                    'A' => (object)tratamento.Abonar(passo.Dia!.Value, passo.Motivo, passo.Responsavel, passo.Em),
                    _ => throw new ArgumentException($"O tipo de tratamento {passo.Tipo} não existe."),
                },
                string.Create(CultureInfo.InvariantCulture, $"O tratamento {passo.Sequencia} do trabalhador {trabalhador}"));
        }

        return (tratamento, gravados.Count);
    }

    /// <summary>Um tratamento gravado, como a linha o traz.</summary>
    private sealed record Passo(int Sequencia, char Tipo, DateTimeOffset? MarcadaEm, long? Nsr, DateOnly? Dia, string Motivo, Cpf Responsavel, DateTimeOffset Em)
    {
        public static Passo De(NpgsqlDataReader linha) => Leitura.Validada(
            () => new Passo(
                linha.GetInt32(0),
                linha.GetString(1)[0],
                Instantes.LerOuNulo(linha, 2, 3),
                linha.IsDBNull(4) ? null : linha.GetInt64(4),
                linha.IsDBNull(5) ? null : linha.GetFieldValue<DateOnly>(5),
                linha.GetString(6),
                Cpf.De(linha.GetString(7)),
                Instantes.Ler(linha, 8, 9)),
            "Um tratamento do ponto");
    }
}
