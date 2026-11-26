using System.Globalization;
using PontoFacial.Dominio.Biometria.Calibracao;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Biometria.Vivacidade.Ensaio;

/// <summary>O APCER de uma espécie de ataque: os ataques dela que a prova de vida aceitou.</summary>
public sealed record ErroPorEspecie(EspecieDeAtaque Especie, TaxaDeErro Apcer);

/// <summary>
/// As taxas da ISO/IEC 30107-3 num critério: o APCER de cada espécie de
/// ataque, a pior delas, e o BPCER, quando houve apresentações genuínas.
/// </summary>
public sealed class AvaliacaoDoEnsaio
{
    internal AvaliacaoDoEnsaio(CriterioDaProvaDeVida criterio, IReadOnlyList<ErroPorEspecie> porEspecie, TaxaDeErro? bpcer)
    {
        Criterio = criterio;
        PorEspecie = porEspecie;
        Bpcer = bpcer;
    }

    public CriterioDaProvaDeVida Criterio { get; }

    /// <summary>APCER: a fração dos ataques de cada espécie aceitos como rosto de verdade.</summary>
    public IReadOnlyList<ErroPorEspecie> PorEspecie { get; }

    /// <summary>A espécie com o maior APCER, que é o que a norma manda informar como o do sistema.</summary>
    public ErroPorEspecie PiorEspecie => PorEspecie.MaxBy(erro => erro.Apcer.Valor)!;

    /// <summary>BPCER: a fração das apresentações genuínas recusadas; nada quando o ensaio não teve nenhuma.</summary>
    public TaxaDeErro? Bpcer { get; }
}

/// <summary>
/// Um ensaio da prova de vida: cada apresentação de ataque, por espécie, e
/// cada apresentação genuína, com o relevo que a sessão mediu.
/// </summary>
/// <remarks>
/// <para>
/// Cada apresentação guarda o menor relevo da sessão, ou nada quando ela foi
/// recusada antes de o relevo ser julgado — o movimento não foi feito, o
/// rosto se perdeu. Assim o mesmo ensaio vale para qualquer critério: um
/// ataque recusado por tempo é recusado em todos; um ataque que chegou ao
/// fim passa ou não conforme o relevo mínimo.
/// </para>
/// <para>
/// A calibração segue a mesma regra do limiar do rosto (ADR-0004): escolhe-se
/// o erro de ataque tolerado, e o critério é o menor relevo que o respeita na
/// pior espécie. Pela regra de três, cada espécie precisa de pelo menos 3
/// sobre o erro tolerado em apresentações.
/// </para>
/// </remarks>
public sealed class EnsaioDaProvaDeVida
{
    /// <summary>O relevo mínimo calibrado é múltiplo de 0,001.</summary>
    public const double Passo = 0.001;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly List<EspecieDeAtaque> _especies = [];

    private readonly Dictionary<string, List<double?>> _ataques = new(StringComparer.Ordinal);

    private readonly List<double?> _genuinas = [];

    /// <summary>As espécies, na ordem em que apareceram.</summary>
    public IReadOnlyList<EspecieDeAtaque> Especies => _especies.AsReadOnly();

    public int Genuinas => _genuinas.Count;

    public int AtaquesDa(EspecieDeAtaque especie)
    {
        ArgumentNullException.ThrowIfNull(especie);

        return _ataques.TryGetValue(especie.Nome, out var relevos) ? relevos.Count : 0;
    }

    /// <param name="especie">O instrumento usado.</param>
    /// <param name="relevo">O menor relevo da sessão, ou nada se ela foi recusada antes de julgar o relevo.</param>
    public void RegistrarAtaque(EspecieDeAtaque especie, double? relevo)
    {
        ArgumentNullException.ThrowIfNull(especie);
        Validar(relevo);

        if (!_ataques.TryGetValue(especie.Nome, out var relevos))
        {
            relevos = [];
            _ataques.Add(especie.Nome, relevos);
            _especies.Add(especie);
        }

        relevos.Add(relevo);
    }

    /// <param name="relevo">O menor relevo da sessão, ou nada se ela foi recusada antes de julgar o relevo.</param>
    public void RegistrarGenuina(double? relevo)
    {
        Validar(relevo);
        _genuinas.Add(relevo);
    }

    public AvaliacaoDoEnsaio Avaliar(CriterioDaProvaDeVida criterio)
    {
        ArgumentNullException.ThrowIfNull(criterio);

        if (_especies.Count == 0)
        {
            throw new ExcecaoDeDominio("O ensaio não tem apresentação de ataque nenhuma.");
        }

        var porEspecie = _especies
            .Select(especie =>
            {
                var relevos = _ataques[especie.Nome];
                var aceitos = relevos.Count(relevo => relevo is { } valor && criterio.Aceita(valor));

                return new ErroPorEspecie(especie, TaxaDeErro.De(aceitos, relevos.Count));
            })
            .ToList()
            .AsReadOnly();

        var bpcer = _genuinas.Count == 0
            ? null
            : TaxaDeErro.De(_genuinas.Count(relevo => relevo is not { } valor || !criterio.Aceita(valor)), _genuinas.Count);

        return new AvaliacaoDoEnsaio(criterio, porEspecie, bpcer);
    }

    /// <summary>
    /// O menor relevo mínimo, em passos de 0,001, com que nenhuma espécie
    /// passa do erro de ataque tolerado.
    /// </summary>
    public CriterioDaProvaDeVida Calibrar(double apcerAlvo, string origem)
    {
        if (!(apcerAlvo > 0 && apcerAlvo < 0.5))
        {
            throw new ExcecaoDeDominio("O erro de ataque tolerado é uma fração acima de 0 e abaixo de 0,5.");
        }

        if (_especies.Count == 0)
        {
            throw new ExcecaoDeDominio("O ensaio não tem apresentação de ataque nenhuma.");
        }

        var minimo = (int)Math.Ceiling(3 / apcerAlvo);
        var relevoMinimo = Passo;

        foreach (var especie in _especies)
        {
            var relevos = _ataques[especie.Nome];

            if (relevos.Count < minimo)
            {
                throw new ExcecaoDeDominio(string.Create(
                    PtBr,
                    $"Para medir APCER de {apcerAlvo:0.##%} na espécie \"{especie.Nome}\" são precisas pelo menos {minimo:N0} apresentações; vieram {relevos.Count:N0}."));
            }

            // Os que chegaram ao fim, do maior relevo para o menor. Até
            // "tolerados" deles podem passar; o critério fica logo acima do
            // seguinte.
            var chegaram = relevos.OfType<double>().OrderDescending().ToList();
            var tolerados = (int)Math.Floor(apcerAlvo * relevos.Count);

            if (chegaram.Count > tolerados)
            {
                relevoMinimo = Math.Max(relevoMinimo, LogoAcima(chegaram[tolerados]));
            }
        }

        if (relevoMinimo > 2)
        {
            throw new ExcecaoDeDominio("Os ataques do ensaio mostram mais relevo que qualquer critério possível: nenhum relevo mínimo até 2 os recusa.");
        }

        return CriterioDaProvaDeVida.De(relevoMinimo, origem);
    }

    /// <summary>O menor múltiplo de 0,001 maior que o valor.</summary>
    private static double LogoAcima(double valor)
    {
        var acima = Math.Round((Math.Floor(valor / Passo) + 1) * Passo, 3);

        // A divisão em ponto flutuante pode cair uma casa abaixo.
        while (acima <= valor)
        {
            acima = Math.Round(acima + Passo, 3);
        }

        return acima;
    }

    private static void Validar(double? relevo)
    {
        if (relevo is { } valor && !double.IsFinite(valor))
        {
            throw new ArgumentOutOfRangeException(nameof(relevo), relevo, "O relevo é um número.");
        }
    }
}
