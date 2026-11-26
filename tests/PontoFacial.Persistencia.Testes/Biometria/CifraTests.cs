using System.Security.Cryptography;
using System.Text;
using PontoFacial.Persistencia.Biometria;

namespace PontoFacial.Persistencia.Testes.Biometria;

/*
 * O AES-256-GCM como o sistema o usa, sem banco: a cifra abre com a chave e
 * o contexto com que foi feita, e com nada mais.
 */
public sealed class CifraTests
{
    private static readonly byte[] Chave = RandomNumberGenerator.GetBytes(32);

    private static readonly byte[] Aberto = Encoding.UTF8.GetBytes("os 128 números do rosto do João");

    private static readonly byte[] Contexto = Encoding.UTF8.GetBytes("empresa|52998224725|autorização|sface-2021dec|128|1");

    [Fact]
    public void A_cifra_abre_com_a_mesma_chave_e_o_mesmo_contexto()
    {
        var (nonce, cifrado) = Cifra.Cifrar(Chave, Aberto, Contexto);

        Assert.Equal(12, nonce.Length);
        Assert.Equal(Aberto.Length + 16, cifrado.Length);
        Assert.Equal(Aberto, Cifra.Decifrar(Chave, nonce, cifrado, Contexto));
    }

    [Fact]
    public void Cifrar_duas_vezes_o_mesmo_texto_da_duas_cifras_diferentes()
    {
        var primeira = Cifra.Cifrar(Chave, Aberto, Contexto);
        var segunda = Cifra.Cifrar(Chave, Aberto, Contexto);

        Assert.NotEqual(primeira.Nonce, segunda.Nonce);
        Assert.NotEqual(primeira.Cifrado, segunda.Cifrado);
    }

    [Fact]
    public void A_cifra_nao_abre_em_outro_contexto()
    {
        var (nonce, cifrado) = Cifra.Cifrar(Chave, Aberto, Contexto);
        var daMaria = Encoding.UTF8.GetBytes("empresa|11144477735|autorização|sface-2021dec|128|1");

        Assert.Throws<AuthenticationTagMismatchException>(() => Cifra.Decifrar(Chave, nonce, cifrado, daMaria));
    }

    [Fact]
    public void A_cifra_nao_abre_com_outra_chave()
    {
        var (nonce, cifrado) = Cifra.Cifrar(Chave, Aberto, Contexto);

        Assert.Throws<AuthenticationTagMismatchException>(() => Cifra.Decifrar(RandomNumberGenerator.GetBytes(32), nonce, cifrado, Contexto));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(17)]
    [InlineData(-1)]
    public void Um_bit_trocado_em_qualquer_lugar_e_a_cifra_nao_abre(int posicao)
    {
        var (nonce, cifrado) = Cifra.Cifrar(Chave, Aberto, Contexto);
        cifrado[posicao < 0 ? cifrado.Length + posicao : posicao] ^= 0x01;

        Assert.Throws<AuthenticationTagMismatchException>(() => Cifra.Decifrar(Chave, nonce, cifrado, Contexto));
    }

    [Fact]
    public void O_nonce_trocado_ou_cortado_nao_abre()
    {
        var (nonce, cifrado) = Cifra.Cifrar(Chave, Aberto, Contexto);
        var outro = (byte[])nonce.Clone();
        outro[0] ^= 0x80;

        Assert.Throws<AuthenticationTagMismatchException>(() => Cifra.Decifrar(Chave, outro, cifrado, Contexto));
        Assert.Throws<AuthenticationTagMismatchException>(() => Cifra.Decifrar(Chave, nonce.AsSpan(0, 11), cifrado, Contexto));
        Assert.Throws<AuthenticationTagMismatchException>(() => Cifra.Decifrar(Chave, nonce, cifrado.AsSpan(0, 15), Contexto));
    }

    [Fact]
    public async Task A_chave_mestra_embrulha_e_desembrulha_a_chave_da_empresa_so_no_mesmo_contexto()
    {
        var mestra = ChaveMestraLocal.Sortear("mestra-2026-11");

        var embrulhada = await mestra.Embrulhar(Chave, Contexto, TestContext.Current.CancellationToken);

        Assert.Equal(60, embrulhada.Length);
        Assert.Equal(Chave, await mestra.Desembrulhar(embrulhada, Contexto, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => mestra.Desembrulhar(embrulhada, Aberto, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<AuthenticationTagMismatchException>(() => ChaveMestraLocal.Sortear("mestra-2026-11").Desembrulhar(embrulhada, Contexto, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void A_chave_mestra_tem_32_bytes_e_um_nome()
    {
        Assert.Equal(
            "A chave mestra tem 32 bytes; vieram 16. (Parameter 'chave')",
            Assert.Throws<ArgumentException>(() => new ChaveMestraLocal("mestra", new byte[16])).Message);
        Assert.Throws<ArgumentException>(() => new ChaveMestraLocal(" ", new byte[32]));
        Assert.Throws<ArgumentException>(() => new ChaveMestraLocal(new string('m', 101), new byte[32]));
        Assert.Equal("mestra", ChaveMestraLocal.DeBase64("mestra", Convert.ToBase64String(new byte[32])).Id);
    }
}
