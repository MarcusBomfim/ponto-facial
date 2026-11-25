namespace PontoFacial.Rosto;

/// <summary>
/// As saídas do YuNet transformadas em rostos, como o <c>FaceDetectorYN</c>
/// do OpenCV faz: para cada passo da grade (8, 16 e 32 pixels), cada célula
/// tem uma nota de classe e uma de objeto, a caixa e os cinco pontos
/// relativos à célula; a confiança é a média geométrica das duas notas, e a
/// supressão de não máximos tira as caixas repetidas.
/// </summary>
internal static class DecodificacaoDoYuNet
{
    public static readonly int[] Passos = [8, 16, 32];

    /// <summary>As saídas de um passo da grade, linha a linha.</summary>
    public sealed record SaidasDoPasso(int Passo, float[] Classe, float[] Objeto, float[] Caixa, float[] Pontos);

    public static List<RostoDetectado> Decodificar(
        IReadOnlyList<SaidasDoPasso> saidas,
        int larguraDaEntrada,
        int alturaDaEntrada,
        float confiancaMinima,
        float sobreposicaoMaxima)
    {
        var candidatos = new List<RostoDetectado>();

        foreach (var (passo, classe, objeto, caixa, pontos) in saidas)
        {
            var colunas = larguraDaEntrada / passo;
            var linhas = alturaDaEntrada / passo;

            for (var linha = 0; linha < linhas; linha++)
            {
                for (var coluna = 0; coluna < colunas; coluna++)
                {
                    var celula = (linha * colunas) + coluna;
                    var confianca = MathF.Sqrt(Math.Clamp(classe[celula], 0f, 1f) * Math.Clamp(objeto[celula], 0f, 1f));

                    if (confianca < confiancaMinima)
                    {
                        continue;
                    }

                    var centroX = (coluna + caixa[(celula * 4) + 0]) * passo;
                    var centroY = (linha + caixa[(celula * 4) + 1]) * passo;
                    var largura = MathF.Exp(caixa[(celula * 4) + 2]) * passo;
                    var altura = MathF.Exp(caixa[(celula * 4) + 3]) * passo;

                    Ponto Ponto(int n) => new(
                        (pontos[(celula * 10) + (2 * n)] + coluna) * passo,
                        (pontos[(celula * 10) + (2 * n) + 1] + linha) * passo);

                    candidatos.Add(new RostoDetectado(
                        centroX - (largura / 2),
                        centroY - (altura / 2),
                        largura,
                        altura,
                        Ponto(0),
                        Ponto(1),
                        Ponto(2),
                        Ponto(3),
                        Ponto(4),
                        confianca));
                }
            }
        }

        return SuprimirRepetidos(candidatos, sobreposicaoMaxima);
    }

    /// <summary>
    /// Do mais confiante para o menos, fica a caixa que não se sobrepõe demais
    /// a nenhuma que já ficou.
    /// </summary>
    public static List<RostoDetectado> SuprimirRepetidos(List<RostoDetectado> candidatos, float sobreposicaoMaxima)
    {
        var mantidos = new List<RostoDetectado>();

        foreach (var candidato in candidatos.OrderByDescending(rosto => rosto.Confianca))
        {
            if (mantidos.TrueForAll(mantido => Sobreposicao(mantido, candidato) <= sobreposicaoMaxima))
            {
                mantidos.Add(candidato);
            }
        }

        return mantidos;
    }

    /// <summary>A interseção sobre a união das duas caixas.</summary>
    public static float Sobreposicao(RostoDetectado a, RostoDetectado b)
    {
        var largura = Math.Min(a.X + a.Largura, b.X + b.Largura) - Math.Max(a.X, b.X);
        var altura = Math.Min(a.Y + a.Altura, b.Y + b.Altura) - Math.Max(a.Y, b.Y);

        if (largura <= 0 || altura <= 0)
        {
            return 0;
        }

        var intersecao = largura * altura;

        return intersecao / ((a.Largura * a.Altura) + (b.Largura * b.Altura) - intersecao);
    }
}
