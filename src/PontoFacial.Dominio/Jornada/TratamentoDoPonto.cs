using System.Globalization;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Registro;

namespace PontoFacial.Dominio.Jornada;

/// <summary>O que o tratamento fez com uma marcação, com as letras do espelho de ponto.</summary>
public enum TipoDeTratamento
{
    /// <summary>A marcação que faltou, acrescentada.</summary>
    Incluida = 'I',

    /// <summary>A marcação indevida do registro, que fica lá mas não conta.</summary>
    Desconsiderada = 'D',
}

/// <summary>Uma marcação tratada: o quê, quando, por quê, por quem e em que momento.</summary>
public sealed class MarcacaoTratada
{
    internal MarcacaoTratada(TipoDeTratamento tipo, DateTimeOffset marcadaEm, long? nsr, string motivo, Cpf responsavel, DateTimeOffset em)
    {
        Tipo = tipo;
        MarcadaEm = marcadaEm;
        Nsr = nsr;
        Motivo = motivo;
        Responsavel = responsavel;
        Em = em;
    }

    public TipoDeTratamento Tipo { get; }

    /// <summary>A hora da marcação incluída, ou da desconsiderada.</summary>
    public DateTimeOffset MarcadaEm { get; }

    /// <summary>O NSR da marcação desconsiderada; nada na incluída, que não está no registro.</summary>
    public long? Nsr { get; }

    public string Motivo { get; }

    /// <summary>Quem tratou.</summary>
    public Cpf Responsavel { get; }

    /// <summary>Quando tratou.</summary>
    public DateTimeOffset Em { get; }
}

/// <summary>Um dia de ausência justificada — atestado, licença —, com o motivo e quem abonou.</summary>
public sealed class AbonoDoDia
{
    internal AbonoDoDia(DateOnly dia, string motivo, Cpf responsavel, DateTimeOffset em)
    {
        Dia = dia;
        Motivo = motivo;
        Responsavel = responsavel;
        Em = em;
    }

    public DateOnly Dia { get; }

    public string Motivo { get; }

    public Cpf Responsavel { get; }

    public DateTimeOffset Em { get; }
}

/// <summary>
/// O tratamento do ponto de um trabalhador num registro: as marcações
/// incluídas, as desconsideradas e os dias abonados, cada um com motivo,
/// responsável e momento.
/// </summary>
/// <remarks>
/// <para>
/// A Portaria 671 limita o tratamento a "acrescentar informações para
/// complementar eventuais omissões no registro de ponto, inclusive
/// ausências", ou "indicar marcações indevidas" (art. 82, parágrafo único).
/// É o que este tipo deixa fazer, e nada mais.
/// </para>
/// <para>
/// O registro não muda. A marcação desconsiderada continua no registro, com
/// o NSR e o hash dela, e aparece no espelho com a letra D; a incluída
/// aparece com a letra I e não ganha NSR, porque não foi marcada no
/// coletor. Tratamento também não se apaga: cada um fica, com quem o fez e
/// quando (ADR-0011).
/// </para>
/// </remarks>
public sealed class TratamentoDoPonto
{
    /// <summary>O motivo cabe em 150 caracteres.</summary>
    public const int TamanhoDoMotivo = 150;

    private const string NaoIncluido = "O trabalhador não foi incluído neste registro de ponto.";

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly List<MarcacaoTratada> _marcacoes = [];

    private readonly Dictionary<DateOnly, AbonoDoDia> _abonos = [];

    private readonly Func<IReadOnlyList<MarcacaoDePonto>> _registradas;

    private TratamentoDoPonto(Estabelecimento estabelecimento, Cpf trabalhador, Func<IReadOnlyList<MarcacaoDePonto>> registradas)
    {
        Estabelecimento = estabelecimento;
        Trabalhador = trabalhador;
        _registradas = registradas;
    }

    /// <summary>O estabelecimento do registro de ponto, com o fuso em que as marcações são lidas.</summary>
    public Estabelecimento Estabelecimento { get; }

    public Cpf Trabalhador { get; }

    /// <summary>As marcações tratadas, na ordem do tratamento.</summary>
    public IReadOnlyList<MarcacaoTratada> Marcacoes => _marcacoes.AsReadOnly();

    /// <summary>Os dias abonados, por dia.</summary>
    public IReadOnlyDictionary<DateOnly, AbonoDoDia> Abonos => _abonos.AsReadOnly();

    /// <summary>O tratamento do trabalhador, que tem de ter sido incluído no registro.</summary>
    /// <remarks>
    /// O registro tem de estar inteiro em memória: o tratamento lê dele as
    /// marcações do trabalhador, inclusive as que forem feitas depois. O
    /// registro retomado do banco só pela ponta não tem as de antes, e o
    /// tratamento, nesse caso, se lê do banco.
    /// </remarks>
    public static TratamentoDoPonto Do(RegistroDePonto registro, Cpf trabalhador)
    {
        ArgumentNullException.ThrowIfNull(registro);
        ArgumentNullException.ThrowIfNull(trabalhador);

        if (!registro.Inteiro)
        {
            throw new InvalidOperationException(
                "Este registro foi retomado do banco pela ponta, sem as operações já gravadas: o tratamento do trabalhador se lê do banco, com as marcações dele.");
        }

        if (!registro.Operacoes.OfType<MudancaNoTrabalhador>().Any(mudanca => mudanca.Cpf == trabalhador && mudanca.Mudanca == TipoDeMudanca.Inclusao))
        {
            throw new ExcecaoDeDominio(NaoIncluido);
        }

        return new TratamentoDoPonto(registro.Estabelecimento, trabalhador, () => [.. registro.Marcacoes.Where(marcacao => marcacao.Cpf == trabalhador)]);
    }

    /// <summary>
    /// O tratamento vazio, sobre as marcações do trabalhador lidas do banco.
    /// Quem o refaz repete cada tratamento gravado, na ordem em que foi feito,
    /// pelos métodos de sempre: o que volta do banco passa pelas mesmas
    /// regras do que entrou.
    /// </summary>
    internal static TratamentoDoPonto Reconstituir(Estabelecimento estabelecimento, Cpf trabalhador, bool incluidoNoRegistro, IReadOnlyList<MarcacaoDePonto> registradas)
    {
        ArgumentNullException.ThrowIfNull(estabelecimento);
        ArgumentNullException.ThrowIfNull(trabalhador);
        ArgumentNullException.ThrowIfNull(registradas);

        if (!incluidoNoRegistro)
        {
            throw new ExcecaoDeDominio(NaoIncluido);
        }

        for (var i = 0; i < registradas.Count; i++)
        {
            if (registradas[i].Cpf != trabalhador || (i > 0 && registradas[i].Nsr <= registradas[i - 1].Nsr))
            {
                throw new ArgumentException("As marcações registradas têm de ser só do trabalhador, em ordem de NSR.", nameof(registradas));
            }
        }

        return new TratamentoDoPonto(estabelecimento, trabalhador, () => registradas);
    }

    /// <summary>As marcações do trabalhador no registro, na ordem do NSR, desconsideradas ou não.</summary>
    public IReadOnlyList<MarcacaoDePonto> Registradas() => _registradas();

    /// <summary>As que contam: as do registro que não foram desconsideradas e as incluídas, em ordem.</summary>
    public IReadOnlyList<DateTimeOffset> Consideradas()
    {
        var desconsideradas = _marcacoes.Where(tratada => tratada.Tipo == TipoDeTratamento.Desconsiderada).Select(tratada => tratada.Nsr).ToHashSet();

        return
        [
            .. Registradas().Where(marcacao => !desconsideradas.Contains(marcacao.Nsr)).Select(marcacao => marcacao.MarcadaEm)
                .Concat(_marcacoes.Where(tratada => tratada.Tipo == TipoDeTratamento.Incluida).Select(tratada => tratada.MarcadaEm))
                .Order(),
        ];
    }

    /// <summary>Acrescenta a marcação que faltou: a saída esquecida, a entrada do coletor sem bateria.</summary>
    public MarcacaoTratada Incluir(DateTimeOffset marcadaEm, string motivo, Cpf responsavel, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(responsavel);

        var limpo = Textos.Obrigatorio(motivo, "o motivo do tratamento", TamanhoDoMotivo);
        var local = AoMinuto(Estabelecimento.NaHoraLocal(marcadaEm));

        if (local > em)
        {
            throw new ExcecaoDeDominio("Não se inclui marcação que ainda não aconteceu.");
        }

        if (Consideradas().Contains(local))
        {
            throw new ExcecaoDeDominio(string.Create(PtBr, $"Já há marcação às {local:HH:mm} de {local:dd/MM/yyyy}."));
        }

        var tratada = new MarcacaoTratada(TipoDeTratamento.Incluida, local, null, limpo, responsavel, em);
        _marcacoes.Add(tratada);

        return tratada;
    }

    /// <summary>Indica a marcação indevida — a repetida por engano, a feita por outro no lugar —, que fica no registro mas não conta.</summary>
    public MarcacaoTratada Desconsiderar(long nsr, string motivo, Cpf responsavel, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(responsavel);

        var limpo = Textos.Obrigatorio(motivo, "o motivo do tratamento", TamanhoDoMotivo);
        var marcacao = Registradas().FirstOrDefault(registrada => registrada.Nsr == nsr)
            ?? throw new ExcecaoDeDominio(string.Create(PtBr, $"O NSR {nsr} não é de uma marcação deste trabalhador."));

        if (_marcacoes.Any(tratada => tratada.Nsr == nsr))
        {
            throw new ExcecaoDeDominio(string.Create(PtBr, $"A marcação do NSR {nsr} já foi desconsiderada."));
        }

        var tratada = new MarcacaoTratada(TipoDeTratamento.Desconsiderada, marcacao.MarcadaEm, nsr, limpo, responsavel, em);
        _marcacoes.Add(tratada);

        return tratada;
    }

    /// <summary>Justifica a ausência do dia: a falta, o atraso ou a saída antecipada deixam de contar como faltantes.</summary>
    public AbonoDoDia Abonar(DateOnly dia, string motivo, Cpf responsavel, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(responsavel);

        var limpo = Textos.Obrigatorio(motivo, "o motivo do abono", TamanhoDoMotivo);

        if (_abonos.TryGetValue(dia, out var anterior))
        {
            throw new ExcecaoDeDominio(string.Create(PtBr, $"O dia {dia:dd/MM/yyyy} já foi abonado: {anterior.Motivo}."));
        }

        var abono = new AbonoDoDia(dia, limpo, responsavel, em);
        _abonos.Add(dia, abono);

        return abono;
    }

    private static DateTimeOffset AoMinuto(DateTimeOffset instante) =>
        new(instante.Year, instante.Month, instante.Day, instante.Hour, instante.Minute, 0, instante.Offset);
}
