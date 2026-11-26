namespace PontoFacial.EnsaioDaProvaDeVida;

/// <summary>Um vetor no espaço, em milímetros.</summary>
internal readonly record struct Vetor(double X, double Y, double Z)
{
    public static Vetor operator +(Vetor a, Vetor b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static Vetor operator *(double k, Vetor a) => new(k * a.X, k * a.Y, k * a.Z);
}

/// <summary>
/// A forma que a foto tem diante da câmera, no espaço da foto: X para a
/// direita, Y para baixo, Z para longe da câmera, em milímetros, com a
/// origem no centro do rosto. Papel não estica: a coordenada na foto é o
/// comprimento medido sobre a superfície.
/// </summary>
internal abstract class Superficie
{
    /// <summary>
    /// Onde o raio, dado no espaço da foto, encontra a superfície, e em que
    /// ponto da foto (U, V, em milímetros); falso se não encontra.
    /// </summary>
    public abstract bool Intersectar(Vetor origem, Vetor direcao, out double u, out double v);
}

/// <summary>A foto plana, impressa ou na tela de um celular.</summary>
internal sealed class Plana : Superficie
{
    public override bool Intersectar(Vetor origem, Vetor direcao, out double u, out double v)
    {
        var t = -origem.Z / direcao.Z;
        var ponto = origem + (t * direcao);
        u = ponto.X;
        v = ponto.Y;

        return t > 0;
    }
}

/// <summary>
/// Uma superfície que se curva de um lado para o outro em torno de um eixo
/// no plano da foto. O eixo é dado em graus a partir da horizontal: 90 é o
/// eixo de pé (o papel enrolado de um lado para o outro); 0, deitado; 45, na
/// diagonal. As contas são feitas num espaço girado em que o eixo fica de pé.
/// </summary>
internal abstract class EmTornoDeUmEixo(double eixoGraus) : Superficie
{
    private readonly double _cosseno = Math.Cos((eixoGraus - 90) * Math.PI / 180);

    private readonly double _seno = Math.Sin((eixoGraus - 90) * Math.PI / 180);

    public sealed override bool Intersectar(Vetor origem, Vetor direcao, out double u, out double v)
    {
        u = v = 0;

        if (!IntersectarDePe(ParaDePe(origem), ParaDePe(direcao), out var uDePe, out var vDePe))
        {
            return false;
        }

        u = (uDePe * _cosseno) - (vDePe * _seno);
        v = (uDePe * _seno) + (vDePe * _cosseno);

        return true;
    }

    /// <summary>A interseção com o eixo de pé, em que a curva está em X e Z.</summary>
    protected abstract bool IntersectarDePe(Vetor origem, Vetor direcao, out double u, out double v);

    private Vetor ParaDePe(Vetor vetor) =>
        new((vetor.X * _cosseno) + (vetor.Y * _seno), (-vetor.X * _seno) + (vetor.Y * _cosseno), vetor.Z);
}

/// <summary>A foto curvada num cilindro, com o meio para a câmera.</summary>
internal sealed class Curvada(double raio, double eixoGraus) : EmTornoDeUmEixo(eixoGraus)
{
    protected override bool IntersectarDePe(Vetor origem, Vetor direcao, out double u, out double v)
    {
        u = v = 0;
        var oz = origem.Z - raio;
        var a = (direcao.X * direcao.X) + (direcao.Z * direcao.Z);
        var b = 2 * ((origem.X * direcao.X) + (oz * direcao.Z));
        var c = (origem.X * origem.X) + (oz * oz) - (raio * raio);
        var discriminante = (b * b) - (4 * a * c);

        if (discriminante < 0)
        {
            return false;
        }

        var t = (-b - Math.Sqrt(discriminante)) / (2 * a);
        var ponto = origem + (t * direcao);

        if (t <= 0 || ponto.Z > raio)
        {
            return false;
        }

        u = raio * Math.Atan2(ponto.X, raio - ponto.Z);
        v = ponto.Y;

        return true;
    }
}

/// <summary>A foto dobrada em V pelo eixo, com a dobra para a câmera.</summary>
internal sealed class Dobrada(double meioAnguloGraus, double eixoGraus) : EmTornoDeUmEixo(eixoGraus)
{
    private readonly double _tangente = Math.Tan(meioAnguloGraus * Math.PI / 180);

    private readonly double _cosseno = Math.Cos(meioAnguloGraus * Math.PI / 180);

    protected override bool IntersectarDePe(Vetor origem, Vetor direcao, out double u, out double v)
    {
        u = v = 0;
        var melhor = double.PositiveInfinity;

        foreach (var lado in new[] { 1.0, -1.0 })
        {
            // A metade do lado: Z = lado · X · tangente, com X do mesmo sinal que o lado.
            var denominador = direcao.Z - (lado * _tangente * direcao.X);

            if (Math.Abs(denominador) < 1e-12)
            {
                continue;
            }

            var t = ((lado * _tangente * origem.X) - origem.Z) / denominador;
            var ponto = origem + (t * direcao);

            if (t > 0 && t < melhor && ponto.X * lado >= 0)
            {
                melhor = t;
                u = ponto.X / _cosseno;
                v = ponto.Y;
            }
        }

        return !double.IsInfinity(melhor);
    }
}

/// <summary>
/// Um relevo de mentira: a foto projetada numa superfície com um calombo no
/// lugar do nariz e as bordas para trás. Não é rosto nem ataque — serve
/// para conferir que a medida enxerga relevo quando ele existe.
/// </summary>
internal sealed class ComRelevo(double alturaDoNarizY) : Superficie
{
    public override bool Intersectar(Vetor origem, Vetor direcao, out double u, out double v)
    {
        // Ponto fixo: o raio encontra a altura da superfície no ponto em que
        // a encontrou na volta anterior. Converge porque o relevo é suave.
        var t = -origem.Z / direcao.Z;

        for (var i = 0; i < 20; i++)
        {
            var ponto = origem + (t * direcao);
            t = (Profundidade(ponto.X, ponto.Y) - origem.Z) / direcao.Z;
        }

        var final = origem + (t * direcao);
        u = final.X;
        v = final.Y;

        return t > 0;
    }

    private double Profundidade(double x, double y)
    {
        var nariz = 24 * Math.Exp(-((x * x / (2 * 9 * 9)) + ((y - alturaDoNarizY) * (y - alturaDoNarizY) / (2 * 13 * 13))));

        return -nariz + (((x * x) + (y * y)) / (2 * 110));
    }
}
