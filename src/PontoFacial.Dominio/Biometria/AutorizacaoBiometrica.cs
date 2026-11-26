using System.Globalization;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Biometria;

/// <summary>
/// O consentimento do trabalhador para que o empregador use o rosto dele, e
/// só para identificá-lo na marcação de ponto.
/// </summary>
/// <remarks>
/// <para>
/// Biometria é dado pessoal sensível (LGPD, art. 5º, II). A autorização é de
/// um trabalhador para um empregador, com o termo que ele leu, e vale da
/// concessão até ser encerrada. Não há outra finalidade: o mesmo modelo do
/// rosto não serve para controle de acesso, nem para nada além do ponto.
/// </para>
/// <para>
/// Ela é opcional. Quem não autoriza, ou revoga, marca o ponto pelo código
/// pessoal, e nada muda no registro dele (ADR-0002). A revogação não pede
/// motivo nem aprovação: a lei manda que seja gratuita e facilitada (art. 8º,
/// § 5º).
/// </para>
/// </remarks>
public sealed class AutorizacaoBiometrica
{
    /// <summary>A única finalidade da biometria neste sistema.</summary>
    public const string Finalidade = "Identificar o trabalhador na marcação de ponto.";

    private readonly List<EventoDeDominio> _eventos = [];

    private AutorizacaoBiometrica(Documento empregador, Cpf trabalhador, TermoDeConsentimento termo, DateTimeOffset concedidaEm)
    {
        Empregador = empregador;
        Trabalhador = trabalhador;
        Termo = termo;
        ConcedidaEm = concedidaEm;
    }

    public Documento Empregador { get; }

    public Cpf Trabalhador { get; }

    /// <summary>O termo que o trabalhador leu ao autorizar.</summary>
    public TermoDeConsentimento Termo { get; }

    public DateTimeOffset ConcedidaEm { get; }

    /// <summary>Quando a autorização acabou; nulo enquanto vale.</summary>
    public DateTimeOffset? EncerradaEm { get; private set; }

    public MotivoDoEncerramento? Motivo { get; private set; }

    /// <summary>O que aconteceu e ainda não foi tratado por quem está fora do domínio.</summary>
    public IReadOnlyList<EventoDeDominio> Eventos => _eventos.AsReadOnly();

    public static AutorizacaoBiometrica Conceder(Empregador empregador, Trabalhador trabalhador, TermoDeConsentimento termo, DateTimeOffset em)
    {
        ArgumentNullException.ThrowIfNull(empregador);
        ArgumentNullException.ThrowIfNull(trabalhador);
        ArgumentNullException.ThrowIfNull(termo);

        return new AutorizacaoBiometrica(empregador.Documento, trabalhador.Cpf, termo, em);
    }

    /// <summary>
    /// A autorização guardada. A encerrada passa de novo pelo encerramento,
    /// com as mesmas regras, mas sem o pedido de eliminação: ele foi atendido
    /// quando ela acabou.
    /// </summary>
    internal static AutorizacaoBiometrica Reconstituir(
        Documento empregador,
        Cpf trabalhador,
        TermoDeConsentimento termo,
        DateTimeOffset concedidaEm,
        DateTimeOffset? encerradaEm,
        MotivoDoEncerramento? motivo)
    {
        ArgumentNullException.ThrowIfNull(empregador);
        ArgumentNullException.ThrowIfNull(trabalhador);
        ArgumentNullException.ThrowIfNull(termo);

        var autorizacao = new AutorizacaoBiometrica(empregador, trabalhador, termo, concedidaEm);

        if (encerradaEm.HasValue != motivo.HasValue)
        {
            throw new ArgumentException("A autorização encerrada tem a data e o motivo do encerramento; a vigente, nenhum dos dois.", nameof(motivo));
        }

        if (encerradaEm is { } em && motivo is { } porque)
        {
            if (!Enum.IsDefined(porque))
            {
                throw new ArgumentOutOfRangeException(nameof(motivo), porque, "Motivo de encerramento que não existe.");
            }

            autorizacao.Encerrar(porque, em);
            autorizacao._eventos.Clear();
        }

        return autorizacao;
    }

    /// <summary>Vale da concessão, inclusive, até o encerramento, exclusive.</summary>
    public bool VigenteEm(DateTimeOffset instante) =>
        instante >= ConcedidaEm && (EncerradaEm is not { } fim || instante < fim);

    /// <summary>O trabalhador retira o consentimento. A partir daí, marca pelo código pessoal.</summary>
    public void Revogar(DateTimeOffset em) => Encerrar(MotivoDoEncerramento.RevogadaPeloTrabalhador, em);

    /// <summary>O contrato acabou: o modelo do rosto não tem mais para que existir.</summary>
    public void EncerrarPeloDesligamento(DateTimeOffset em) => Encerrar(MotivoDoEncerramento.Desligamento, em);

    private void Encerrar(MotivoDoEncerramento motivo, DateTimeOffset em)
    {
        if (EncerradaEm is { } encerrada)
        {
            throw new ExcecaoDeDominio(string.Create(
                CultureInfo.InvariantCulture,
                $"A autorização biométrica já foi encerrada em {encerrada:dd/MM/yyyy HH:mm}."));
        }

        if (em < ConcedidaEm)
        {
            throw new ExcecaoDeDominio("A autorização biométrica não pode acabar antes de ter sido concedida.");
        }

        EncerradaEm = em;
        Motivo = motivo;
        _eventos.Add(new ModeloFacialDeveSerEliminado(Empregador, Trabalhador, motivo, em));
    }
}
