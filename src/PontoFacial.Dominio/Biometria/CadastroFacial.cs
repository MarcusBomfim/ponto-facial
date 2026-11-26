using System.Globalization;
using PontoFacial.Dominio.Biometria.Vivacidade;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Biometria;

/// <summary>
/// O modelo do rosto de um trabalhador para um empregador, feito de várias
/// capturas, só com a autorização biométrica vigente.
/// </summary>
/// <remarks>
/// <para>
/// Uma captura só não basta: a luz, o ângulo e a expressão mudam o vetor, e um
/// cadastro feito numa foto ruim recusa o trabalhador todo dia. Com três ou
/// mais, o modelo é a média delas, mais estável que qualquer uma.
/// </para>
/// <para>
/// As capturas têm de conferir entre si, com o mesmo limiar da marcação. Se
/// uma delas não é do mesmo rosto — outra pessoa entrou na frente, ou alguém
/// tentou cadastrar o rosto de um colega no meio —, o cadastro é recusado
/// inteiro, e não se faz a média de dois rostos.
/// </para>
/// </remarks>
public sealed class CadastroFacial
{
    public const int CapturasMinimas = 3;

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private CadastroFacial(Documento empregador, Cpf trabalhador, ModeloFacial modelo, DateTimeOffset cadastradoEm)
    {
        Empregador = empregador;
        Trabalhador = trabalhador;
        Modelo = modelo;
        CadastradoEm = cadastradoEm;
    }

    public Documento Empregador { get; }

    public Cpf Trabalhador { get; }

    /// <summary>A média das capturas, com norma 1.</summary>
    public ModeloFacial Modelo { get; }

    public DateTimeOffset CadastradoEm { get; }

    public static CadastroFacial Cadastrar(
        AutorizacaoBiometrica autorizacao,
        IReadOnlyList<ModeloFacial> capturas,
        LimiarDeAceitacao limiar,
        DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(autorizacao);
        ArgumentNullException.ThrowIfNull(capturas);
        ArgumentNullException.ThrowIfNull(limiar);

        if (!autorizacao.VigenteEm(em))
        {
            throw new ExcecaoDeDominio("Sem autorização biométrica vigente, o rosto não é cadastrado.");
        }

        if (capturas.Count < CapturasMinimas)
        {
            throw new ExcecaoDeDominio(string.Create(
                PtBr,
                $"O cadastro do rosto pede pelo menos {CapturasMinimas} capturas; vieram {capturas.Count}."));
        }

        for (var i = 0; i < capturas.Count; i++)
        {
            if (capturas[i].Reconhecedor != limiar.Reconhecedor)
            {
                throw new ExcecaoDeDominio(
                    $"A captura {i + 1} é do reconhecedor {capturas[i].Reconhecedor}, e o limiar, do {limiar.Reconhecedor}.");
            }
        }

        for (var i = 0; i < capturas.Count; i++)
        {
            for (var j = i + 1; j < capturas.Count; j++)
            {
                var similaridade = capturas[i].Similaridade(capturas[j]);

                if (!limiar.Aceita(similaridade))
                {
                    throw new ExcecaoDeDominio(string.Create(
                        PtBr,
                        $"As capturas {i + 1} e {j + 1} não conferem entre si: similaridade {similaridade:0.000}, abaixo do limiar {limiar.Similaridade:0.000}. Refaça o cadastro só com o trabalhador diante da câmera."));
                }
            }
        }

        return new CadastroFacial(autorizacao.Empregador, autorizacao.Trabalhador, ModeloFacial.Media(capturas), em);
    }

    /// <summary>
    /// Compara com o cadastro o rosto de uma prova de vida aprovada: o do
    /// começo e o do fim, que o acompanhamento liga ao rosto que fez os
    /// movimentos. A similaridade é a menor das duas — os dois têm de ser do
    /// trabalhador.
    /// </summary>
    public ConferenciaFacial Conferir(ProvaDeVida prova, LimiarDeAceitacao limiar)
    {
        ArgumentNullException.ThrowIfNull(prova);
        ArgumentNullException.ThrowIfNull(limiar);
        ConferirReconhecedor(limiar);

        if (!prova.Aprovada || prova.ModeloInicial is null || prova.ModeloFinal is null)
        {
            throw new ExcecaoDeDominio("A prova de vida não passou: não há rosto para conferir com o cadastro.");
        }

        var similaridade = Math.Min(Modelo.Similaridade(prova.ModeloInicial), Modelo.Similaridade(prova.ModeloFinal));

        return new ConferenciaFacial(Empregador, Trabalhador, similaridade, limiar, prova.Em, prova);
    }

    /// <summary>
    /// Compara um vetor solto com o cadastro, como a calibração faz. A
    /// conferência resultante não identifica ninguém na marcação: sem prova
    /// de vida, o vetor pode ter saído de uma foto.
    /// </summary>
    public ConferenciaFacial Conferir(ModeloFacial capturado, LimiarDeAceitacao limiar, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(capturado);
        ArgumentNullException.ThrowIfNull(limiar);
        ConferirReconhecedor(limiar);

        return new ConferenciaFacial(Empregador, Trabalhador, Modelo.Similaridade(capturado), limiar, em, null);
    }

    private void ConferirReconhecedor(LimiarDeAceitacao limiar)
    {
        if (limiar.Reconhecedor != Modelo.Reconhecedor)
        {
            throw new ExcecaoDeDominio(
                $"O limiar é do reconhecedor {limiar.Reconhecedor}, e o cadastro, do {Modelo.Reconhecedor}.");
        }
    }
}
