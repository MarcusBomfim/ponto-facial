using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Npgsql;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Persistencia.Biometria;

/// <summary>
/// O modelo do rosto de cada trabalhador, cifrado com a chave da empresa: o
/// banco guarda a cifra, o nonce e a versão da chave, e nunca o vetor
/// (ADR-0014).
/// </summary>
/// <remarks>
/// <para>
/// A cifra é amarrada ao lugar dela pelo contexto do AES-GCM: a empresa, o
/// CPF, a autorização, o reconhecedor, a dimensão e a versão da chave. A
/// cifra do João copiada para a linha da Maria não abre, nem a de uma
/// empresa copiada para outra.
/// </para>
/// <para>
/// O modelo só é gravado com a autorização vigente, e o banco confere de
/// novo: a linha aponta para a autorização pela chave que só existe enquanto
/// ela vale.
/// </para>
/// </remarks>
public sealed class CadastrosFaciais
{
    private const string AutorizacaoVigente = "select id from ponto.autorizacoes_biometricas where cpf = $1 and encerrada_em is null";

    private const string Gravacao = """
        insert into ponto.cadastros_faciais (
            empregador_id, cpf, autorizacao_id, reconhecedor, dimensao, versao_da_chave, nonce, vetor_cifrado, cadastrado_em, cadastrado_em_deslocamento)
        values ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10)
        on conflict (empregador_id, cpf) do update set
            autorizacao_id = excluded.autorizacao_id,
            reconhecedor = excluded.reconhecedor,
            dimensao = excluded.dimensao,
            versao_da_chave = excluded.versao_da_chave,
            nonce = excluded.nonce,
            vetor_cifrado = excluded.vetor_cifrado,
            cadastrado_em = excluded.cadastrado_em,
            cadastrado_em_deslocamento = excluded.cadastrado_em_deslocamento
        """;

    private const string Colunas = "cpf, autorizacao_id, reconhecedor, dimensao, versao_da_chave, nonce, vetor_cifrado, cadastrado_em, cadastrado_em_deslocamento";

    private const string Ler = $"select {Colunas} from ponto.cadastros_faciais where cpf = $1";

    private const string LerDeVersaoAntiga = $"select {Colunas} from ponto.cadastros_faciais where versao_da_chave < $1 order by cpf for update";

    private const string TrocarACifra = "update ponto.cadastros_faciais set versao_da_chave = $2, nonce = $3, vetor_cifrado = $4 where cpf = $1";

    private const string Apagacao = "delete from ponto.cadastros_faciais where cpf = $1";

    private readonly EmpresaNoBanco _empresa;

    private readonly ChavesDaEmpresa _chaves;

    internal CadastrosFaciais(EmpresaNoBanco empresa, ChavesDaEmpresa chaves)
    {
        _empresa = empresa;
        _chaves = chaves;
    }

    /// <summary>Grava o modelo cifrado, no lugar do que o trabalhador tinha, se tinha.</summary>
    public Task Gravar(CadastroFacial cadastro, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(cadastro);

        return _empresa.Transacao(
            async sessao =>
            {
                var empregador = await _empresa.Empregador(sessao);

                if (empregador.Documento != cadastro.Empregador)
                {
                    throw new InvalidOperationException($"O cadastro do rosto é do empregador {cadastro.Empregador}, e esta empresa é o {empregador.Documento}.");
                }

                var vigentes = await sessao.Ler(AutorizacaoVigente, linha => linha.GetGuid(0), cadastro.Trabalhador.Numero);

                if (vigentes.Count == 0)
                {
                    throw new ExcecaoDeDominio("Sem autorização biométrica vigente, o rosto não é cadastrado.");
                }

                using var chave = await _chaves.Atual(sessao);
                var (nonce, cifrado) = Cifrar(chave, cadastro.Trabalhador, vigentes[0], cadastro.Modelo);

                return await sessao.Executar(
                    Gravacao,
                    _empresa.Id,
                    cadastro.Trabalhador.Numero,
                    vigentes[0],
                    cadastro.Modelo.Reconhecedor,
                    (short)cadastro.Modelo.Dimensao,
                    chave.Versao,
                    nonce,
                    cifrado,
                    Instantes.Utc(cadastro.CadastradoEm),
                    Instantes.Deslocamento(cadastro.CadastradoEm));
            },
            cancelamento);
    }

    /// <summary>O cadastro do trabalhador, com o modelo decifrado; nulo se ele não tem.</summary>
    public Task<CadastroFacial?> Obter(Cpf trabalhador, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);

        return _empresa.Transacao(
            async sessao =>
            {
                var linha = await sessao.LerUma(Ler, Linha.De, trabalhador.Numero);

                if (linha is null)
                {
                    return null;
                }

                var empregador = await _empresa.Empregador(sessao);
                using var chave = await _chaves.DaVersao(sessao, linha.VersaoDaChave);
                var modelo = Decifrar(chave, linha);

                return Leitura.Validada(
                    () => CadastroFacial.Reconstituir(empregador.Documento, trabalhador, modelo, linha.CadastradoEm),
                    $"O cadastro do rosto do trabalhador {trabalhador}");
            },
            cancelamento);
    }

    /// <summary>Apaga o modelo do trabalhador (LGPD, art. 18, VI). Devolve se havia um.</summary>
    public Task<bool> Apagar(Cpf trabalhador, CancellationToken cancelamento = default)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);

        return _empresa.Transacao(async sessao => await Apagar(sessao, trabalhador), cancelamento);
    }

    /// <summary>
    /// Cifra de novo, com a versão atual da chave, os modelos cifrados por uma
    /// anterior: o passo entre girar a chave e descartar a antiga. Devolve
    /// quantos modelos foram cifrados de novo.
    /// </summary>
    public Task<int> CifrarDeNovo(CancellationToken cancelamento = default) =>
        _empresa.Transacao(
            async sessao =>
            {
                using var atual = await _chaves.Atual(sessao);
                var antigas = await sessao.Ler(LerDeVersaoAntiga, Linha.De, atual.Versao);

                foreach (var antiga in antigas)
                {
                    ModeloFacial modelo;

                    using (var chave = await _chaves.DaVersao(sessao, antiga.VersaoDaChave))
                    {
                        modelo = Decifrar(chave, antiga);
                    }

                    var (nonce, cifrado) = Cifrar(atual, antiga.Trabalhador, antiga.Autorizacao, modelo);
                    await sessao.Executar(TrocarACifra, antiga.Trabalhador.Numero, atual.Versao, nonce, cifrado);
                }

                return antigas.Count;
            },
            cancelamento);

    internal static async Task<bool> Apagar(Sessao sessao, Cpf trabalhador) => await sessao.Executar(Apagacao, trabalhador.Numero) > 0;

    private (byte[] Nonce, byte[] Cifrado) Cifrar(ChaveAberta chave, Cpf trabalhador, Guid autorizacao, ModeloFacial modelo)
    {
        var aberto = new byte[modelo.Dimensao * sizeof(float)];

        try
        {
            for (var i = 0; i < modelo.Dimensao; i++)
            {
                BinaryPrimitives.WriteSingleLittleEndian(aberto.AsSpan(i * sizeof(float)), modelo.Valores[i]);
            }

            return Cifra.Cifrar(chave.Bytes, aberto, Contexto(trabalhador, autorizacao, modelo.Reconhecedor, modelo.Dimensao, chave.Versao));
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aberto);
        }
    }

    private ModeloFacial Decifrar(ChaveAberta chave, Linha linha)
    {
        byte[] aberto;

        try
        {
            aberto = Cifra.Decifrar(
                chave.Bytes,
                linha.Nonce,
                linha.Cifrado,
                Contexto(linha.Trabalhador, linha.Autorizacao, linha.Reconhecedor, linha.Dimensao, linha.VersaoDaChave));
        }
        catch (AuthenticationTagMismatchException erro)
        {
            throw new ConteudoAdulterado(
                $"O modelo do rosto do trabalhador {linha.Trabalhador} não decifra: a cifra foi mexida, ou é de outro trabalhador ou de outra empresa.",
                erro);
        }

        try
        {
            var valores = new float[linha.Dimensao];

            for (var i = 0; i < valores.Length; i++)
            {
                valores[i] = BinaryPrimitives.ReadSingleLittleEndian(aberto.AsSpan(i * sizeof(float)));
            }

            return Leitura.Validada(() => ModeloFacial.Reconstituir(linha.Reconhecedor, valores), $"O modelo do rosto do trabalhador {linha.Trabalhador}");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(aberto);
        }
    }

    /// <summary>O contexto da cifra: ela só abre na linha em que foi gravada.</summary>
    private byte[] Contexto(Cpf trabalhador, Guid autorizacao, string reconhecedor, int dimensao, int versaoDaChave) =>
        Encoding.UTF8.GetBytes(string.Create(
            CultureInfo.InvariantCulture,
            $"ponto-facial/modelo-facial/v1|{_empresa.Id:D}|{trabalhador.Numero}|{autorizacao:D}|{reconhecedor}|{dimensao}|{versaoDaChave}"));

    private sealed record Linha(Cpf Trabalhador, Guid Autorizacao, string Reconhecedor, int Dimensao, int VersaoDaChave, byte[] Nonce, byte[] Cifrado, DateTimeOffset CadastradoEm)
    {
        public static Linha De(NpgsqlDataReader linha) => Leitura.Validada(
            () => new Linha(
                Cpf.De(linha.GetString(0)),
                linha.GetGuid(1),
                linha.GetString(2),
                linha.GetInt16(3),
                linha.GetInt32(4),
                linha.GetFieldValue<byte[]>(5),
                linha.GetFieldValue<byte[]>(6),
                Instantes.Ler(linha, 7, 8)),
            "Um cadastro do rosto");
    }
}
