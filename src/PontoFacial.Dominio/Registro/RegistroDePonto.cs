using System.Globalization;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Registro;

/// <summary>
/// O registro de ponto de um estabelecimento: a sequência das operações, que
/// só cresce. É o Armazenamento de Registro de Ponto da Portaria 671 (anexo
/// IX, item 6).
/// </summary>
/// <remarks>
/// <para>
/// Cada estabelecimento tem a sua numeração, que começa em 1 na abertura e
/// conta todas as operações — a inclusão do empregador, os ajustes do
/// relógio, as mudanças nos trabalhadores, os eventos sensíveis e as
/// marcações. Nenhuma operação muda ou sai depois de gravada (item 7), e não
/// há método para isso.
/// </para>
/// <para>
/// O registro não filtra marcação. A Portaria proíbe restringir a marcação,
/// marcar automaticamente e qualquer coisa que altere o que o trabalhador
/// registrou (art. 74). Duas marcações no mesmo minuto, ou fora do horário,
/// são gravadas as duas; quem trata o ponto pode indicar a indevida, mas não
/// apagá-la (art. 82).
/// </para>
/// <para>
/// As horas são as do relógio do registro, na hora local do estabelecimento,
/// ao minuto — a resolução do Arquivo Fonte de Dados. O relógio só anda para
/// frente; para voltar, há o ajuste, que fica registrado com o responsável.
/// </para>
/// </remarks>
public sealed class RegistroDePonto
{
    private const string NaoIncluido = "O trabalhador não está incluído neste registro de ponto.";

    private readonly List<Operacao> _operacoes = [];

    private readonly Dictionary<Cpf, Vinculo> _incluidos = [];

    private DateTimeOffset _relogio;

    private string? _ultimoHash;

    private RegistroDePonto(Estabelecimento estabelecimento) => Estabelecimento = estabelecimento;

    public Estabelecimento Estabelecimento { get; }

    /// <summary>Todas as operações, em ordem de NSR. Só leitura.</summary>
    public IReadOnlyList<Operacao> Operacoes => _operacoes.AsReadOnly();

    /// <summary>Só as marcações, em ordem de NSR.</summary>
    public IReadOnlyList<MarcacaoDePonto> Marcacoes => [.. _operacoes.OfType<MarcacaoDePonto>()];

    /// <summary>Abre o registro do estabelecimento com a inclusão do empregador, que leva o NSR 1.</summary>
    public static RegistroDePonto Abrir(Estabelecimento estabelecimento, Cpf responsavel, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(estabelecimento);
        ArgumentNullException.ThrowIfNull(responsavel);

        var registro = new RegistroDePonto(estabelecimento);
        var gravadaEm = registro.NaHoraDoRegistro(em);
        registro.Gravar(new InclusaoDoEmpregador(registro.ProximoNsr(), gravadaEm, estabelecimento, responsavel), gravadaEm);

        return registro;
    }

    public MudancaNoTrabalhador IncluirTrabalhador(Trabalhador trabalhador, Cpf responsavel, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);
        ArgumentNullException.ThrowIfNull(responsavel);

        var gravadaEm = HoraDaGravacao(em);

        if (_incluidos.ContainsKey(trabalhador.Cpf))
        {
            throw new ExcecaoDeDominio("O trabalhador já está incluído neste registro de ponto.");
        }

        _incluidos[trabalhador.Cpf] = new Vinculo(trabalhador.Nome, gravadaEm);

        return Gravar(
            new MudancaNoTrabalhador(ProximoNsr(), gravadaEm, TipoDeMudanca.Inclusao, trabalhador.Cpf, trabalhador.Nome, responsavel),
            gravadaEm);
    }

    public MudancaNoTrabalhador AlterarTrabalhador(Trabalhador trabalhador, Cpf responsavel, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(trabalhador);
        ArgumentNullException.ThrowIfNull(responsavel);

        var gravadaEm = HoraDaGravacao(em);

        if (!_incluidos.TryGetValue(trabalhador.Cpf, out var vinculo))
        {
            throw new ExcecaoDeDominio(NaoIncluido);
        }

        _incluidos[trabalhador.Cpf] = vinculo with { Nome = trabalhador.Nome };

        return Gravar(
            new MudancaNoTrabalhador(ProximoNsr(), gravadaEm, TipoDeMudanca.Alteracao, trabalhador.Cpf, trabalhador.Nome, responsavel),
            gravadaEm);
    }

    /// <summary>Tira o trabalhador do registro: ele não marca mais aqui. As marcações dele ficam.</summary>
    public MudancaNoTrabalhador ExcluirTrabalhador(Cpf cpf, Cpf responsavel, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(cpf);
        ArgumentNullException.ThrowIfNull(responsavel);

        var gravadaEm = HoraDaGravacao(em);

        if (!_incluidos.Remove(cpf, out var vinculo))
        {
            throw new ExcecaoDeDominio(NaoIncluido);
        }

        return Gravar(new MudancaNoTrabalhador(ProximoNsr(), gravadaEm, TipoDeMudanca.Exclusao, cpf, vinculo.Nome, responsavel), gravadaEm);
    }

    /// <summary>
    /// Acerta o relógio do registro: dali em diante, ele parte da hora
    /// ajustada, mesmo que ela seja anterior à de alguma operação já gravada.
    /// </summary>
    public AjusteDoRelogio AjustarRelogio(DateTimeOffset antes, DateTimeOffset ajustado, Cpf responsavel)
    {
        ArgumentNullException.ThrowIfNull(responsavel);

        var horaDeAntes = HoraDaGravacao(antes);
        var horaAjustada = NaHoraDoRegistro(ajustado);

        return Gravar(new AjusteDoRelogio(ProximoNsr(), horaDeAntes, horaAjustada, responsavel), horaAjustada);
    }

    public OcorrenciaDeEventoSensivel RegistrarEvento(EventoSensivel evento, DateTimeOffset em)
    {
        if (!Enum.IsDefined(evento))
        {
            throw new ArgumentOutOfRangeException(nameof(evento), evento, "Evento sensível sem código no Arquivo Fonte de Dados.");
        }

        var gravadaEm = HoraDaGravacao(em);

        return Gravar(new OcorrenciaDeEventoSensivel(ProximoNsr(), gravadaEm, evento), gravadaEm);
    }

    /// <summary>
    /// A marcação feita com o coletor ligado ao registro: a hora da marcação é
    /// a da gravação, a do relógio do registro.
    /// </summary>
    public MarcacaoDePonto MarcarOnLine(IdentificacaoDoTrabalhador identificacao, Coletor coletor)
    {
        ArgumentNullException.ThrowIfNull(identificacao);

        var em = HoraDaGravacao(identificacao.Em);

        return Marcar(identificacao, coletor, em, em, online: true);
    }

    /// <summary>
    /// A marcação feita com o coletor desligado, guardada nele e enviada
    /// quando ele voltou a se ligar (anexo IX, itens 4 e 5). A hora da
    /// marcação pode ser anterior à da última operação gravada; a da gravação,
    /// não.
    /// </summary>
    public MarcacaoDePonto MarcarOffLine(IdentificacaoDoTrabalhador identificacao, Coletor coletor, DateTimeOffset gravadaEm)
    {
        ArgumentNullException.ThrowIfNull(identificacao);

        var gravacao = HoraDaGravacao(gravadaEm);
        var marcadaEm = NaHoraDoRegistro(identificacao.Em);

        if (marcadaEm > gravacao)
        {
            throw new ExcecaoDeDominio("A marcação off-line é gravada depois de feita: a gravação não pode ser anterior à marcação.");
        }

        return Marcar(identificacao, coletor, marcadaEm, gravacao, online: false);
    }

    /// <summary>
    /// O comprovante da marcação (art. 79), com o nome do trabalhador como
    /// estava no registro quando ele marcou.
    /// </summary>
    /// <param name="nsr">O NSR da marcação.</param>
    /// <param name="registroNoInpi">O número de registro deste programa no INPI, que o comprovante do REP-P traz.</param>
    public ComprovanteDeRegistro Comprovante(long nsr, string registroNoInpi)
    {
        var marcacao = (nsr >= 1 && nsr <= _operacoes.Count ? _operacoes[(int)(nsr - 1)] : null) as MarcacaoDePonto
            ?? throw new ExcecaoDeDominio(string.Create(
                CultureInfo.InvariantCulture,
                $"O NSR {nsr} não é de uma marcação deste registro de ponto."));

        var nome = _operacoes
            .Take((int)(nsr - 1))
            .OfType<MudancaNoTrabalhador>()
            .Last(mudanca => mudanca.Cpf == marcacao.Cpf)
            .Nome;

        return new ComprovanteDeRegistro(Estabelecimento, nome, marcacao, registroNoInpi);
    }

    private MarcacaoDePonto Marcar(
        IdentificacaoDoTrabalhador identificacao,
        Coletor coletor,
        DateTimeOffset marcadaEm,
        DateTimeOffset gravadaEm,
        bool online)
    {
        if (!Enum.IsDefined(coletor))
        {
            throw new ArgumentOutOfRangeException(nameof(coletor), coletor, "Coletor sem código no Arquivo Fonte de Dados.");
        }

        if (identificacao.Empregador != Estabelecimento.Empregador.Documento)
        {
            throw new ExcecaoDeDominio("A identificação é de outro empregador: a marcação não pertence a este registro de ponto.");
        }

        if (!_incluidos.TryGetValue(identificacao.Trabalhador, out var vinculo))
        {
            throw new ExcecaoDeDominio(NaoIncluido);
        }

        if (marcadaEm < vinculo.IncluidoEm)
        {
            throw new ExcecaoDeDominio("A marcação é anterior à inclusão do trabalhador neste registro de ponto.");
        }

        var marcacao = Gravar(
            new MarcacaoDePonto(ProximoNsr(), marcadaEm, gravadaEm, identificacao.Trabalhador, coletor, online, identificacao.Meio, _ultimoHash),
            gravadaEm);
        _ultimoHash = marcacao.Hash;

        return marcacao;
    }

    /// <summary>A hora de uma nova gravação, recusada se o relógio voltasse sem ajuste.</summary>
    private DateTimeOffset HoraDaGravacao(DateTimeOffset em)
    {
        var gravadaEm = NaHoraDoRegistro(em);

        if (gravadaEm < _relogio)
        {
            throw new ExcecaoDeDominio(string.Create(
                CultureInfo.InvariantCulture,
                $"O relógio do registro já marca {_relogio:dd/MM/yyyy HH:mm}; gravar às {gravadaEm:dd/MM/yyyy HH:mm} seria voltar no tempo. Para acertar a hora, registre um ajuste do relógio."));
        }

        return gravadaEm;
    }

    /// <summary>O instante na hora local do estabelecimento, sem os segundos.</summary>
    private DateTimeOffset NaHoraDoRegistro(DateTimeOffset instante)
    {
        var local = Estabelecimento.NaHoraLocal(instante);

        return new DateTimeOffset(local.Year, local.Month, local.Day, local.Hour, local.Minute, 0, local.Offset);
    }

    private long ProximoNsr()
    {
        var nsr = _operacoes.Count + 1L;

        return nsr <= CamposDoAfd.MaiorNsr
            ? nsr
            : throw new ExcecaoDeDominio("O registro de ponto chegou ao maior NSR que cabe no Arquivo Fonte de Dados.");
    }

    private T Gravar<T>(T operacao, DateTimeOffset relogio)
        where T : Operacao
    {
        _operacoes.Add(operacao);
        _relogio = relogio;

        return operacao;
    }

    private sealed record Vinculo(string Nome, DateTimeOffset IncluidoEm);
}
