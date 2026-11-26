using PontoFacial.Dominio.Biometria.Vivacidade;

namespace PontoFacial.Rosto;

/// <summary>
/// Os nove pontos da prova de vida acompanhados quadro a quadro pelo fluxo
/// óptico, a partir do quadro de referência.
/// </summary>
/// <remarks>
/// <para>
/// O detector acha o rosto uma vez; dali em diante, cada ponto é o mesmo
/// pedaço da imagem, achado de novo no quadro seguinte. É o que dá a posição
/// de verdade do nariz numa foto inclinada — o detector, treinado em rostos,
/// o poria onde um rosto teria nariz (ADR-0007).
/// </para>
/// <para>
/// Basta um ponto se perder para o acompanhamento inteiro se perder: com
/// pontos faltando, o relevo não se mede, e um rosto trocado no meio quebra
/// a ida e volta de todos.
/// </para>
/// </remarks>
public sealed class AcompanhamentoDoRosto
{
    /// <summary>A meia janela de cada ponto, em distâncias entre os olhos.</summary>
    public const double MeiaJanela = 0.12;

    private readonly int _meiaJanela;

    private Piramide _anterior;

    public AcompanhamentoDoRosto(Imagem referencia, PontosDoRosto pontos)
    {
        ArgumentNullException.ThrowIfNull(referencia);
        ArgumentNullException.ThrowIfNull(pontos);

        _anterior = Piramide.De(referencia);
        _meiaJanela = Math.Max(6, (int)Math.Round(MeiaJanela * pontos.DistanciaEntreOlhos));
        Pontos = pontos;
    }

    /// <summary>Os pontos no último quadro; nada depois que o acompanhamento se perdeu.</summary>
    public PontosDoRosto? Pontos { get; private set; }

    public bool Perdido => Pontos is null;

    /// <summary>Os pontos no quadro seguinte, ou nada se algum se perdeu.</summary>
    public PontosDoRosto? Avancar(Imagem quadro)
    {
        ArgumentNullException.ThrowIfNull(quadro);

        if (Pontos is null)
        {
            return null;
        }

        var seguinte = Piramide.De(quadro);
        var novos = new PontoNaImagem[PontosDoRosto.Quantidade];

        for (var i = 0; i < novos.Length; i++)
        {
            var ponto = Pontos.Todos[i];

            if (FluxoOptico.Acompanhar(_anterior, seguinte, ponto.X, ponto.Y, _meiaJanela) is not { } destino)
            {
                Pontos = null;
                return null;
            }

            novos[i] = new PontoNaImagem(destino.X, destino.Y);
        }

        // Os olhos colados um no outro não são mais rosto.
        if (Math.Abs(novos[0].X - novos[1].X) + Math.Abs(novos[0].Y - novos[1].Y) < 2)
        {
            Pontos = null;
            return null;
        }

        _anterior = seguinte;
        Pontos = PontosDoRosto.De(novos);

        return Pontos;
    }
}
