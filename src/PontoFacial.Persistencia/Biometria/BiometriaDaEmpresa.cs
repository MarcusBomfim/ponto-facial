namespace PontoFacial.Persistencia.Biometria;

/// <summary>
/// A biometria de uma empresa no banco: as autorizações, os modelos do rosto
/// cifrados e a chave que os cifra. Precisa da chave mestra, que não fica no
/// banco (ADR-0014).
/// </summary>
public sealed class BiometriaDaEmpresa
{
    internal BiometriaDaEmpresa(EmpresaNoBanco empresa, IChaveMestra chaveMestra, IReadOnlyList<IChaveMestra> anteriores)
    {
        Chaves = new ChavesDaEmpresa(empresa, chaveMestra, anteriores);
        Autorizacoes = new AutorizacoesBiometricas(empresa);
        Cadastros = new CadastrosFaciais(empresa, Chaves);
    }

    public AutorizacoesBiometricas Autorizacoes { get; }

    /// <summary>Os modelos do rosto, cifrados.</summary>
    public CadastrosFaciais Cadastros { get; }

    /// <summary>A chave da empresa: girar, embrulhar de novo, descartar a antiga.</summary>
    public ChavesDaEmpresa Chaves { get; }
}

/// <summary>A biometria a partir da empresa: <c>empresa.Biometria(chaveMestra)</c>.</summary>
public static class BiometriaNoBanco
{
    /// <param name="empresa">A empresa.</param>
    /// <param name="chaveMestra">A chave mestra atual, que embrulha as chaves novas.</param>
    /// <param name="anteriores">As chaves mestras de antes, que ainda desembrulham o que embrulharam, até tudo ser embrulhado de novo.</param>
    public static BiometriaDaEmpresa Biometria(this EmpresaNoBanco empresa, IChaveMestra chaveMestra, params IChaveMestra[] anteriores)
    {
        ArgumentNullException.ThrowIfNull(empresa);
        ArgumentNullException.ThrowIfNull(chaveMestra);
        ArgumentNullException.ThrowIfNull(anteriores);

        return new BiometriaDaEmpresa(empresa, chaveMestra, anteriores);
    }
}
