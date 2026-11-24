namespace PontoFacial.Dominio.Comum;

/// <summary>
/// Uma regra do registro de ponto ou da biometria foi violada.
/// </summary>
/// <remarks>
/// A mensagem é escrita para quem usa o sistema — o RH, o trabalhador —, e
/// não para quem depura o código: ela diz o que está errado e, quando dá, o
/// que fazer. As camadas de fora repassam a mensagem como veio.
/// </remarks>
public sealed class ExcecaoDeDominio : Exception
{
    public ExcecaoDeDominio()
    {
    }

    public ExcecaoDeDominio(string mensagem)
        : base(mensagem)
    {
    }

    public ExcecaoDeDominio(string mensagem, Exception causa)
        : base(mensagem, causa)
    {
    }
}
