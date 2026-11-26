using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Testes.Biometria;

/*
 * A biometria guardada volta pelas mesmas regras: a autorização encerrada
 * passa de novo pelo encerramento, e o vetor decifrado é o gravado, bit a
 * bit, sem uma segunda normalização mexendo nos últimos dígitos.
 */
public sealed class ReconstituicaoDaBiometriaTests
{
    [Fact]
    public void O_vetor_guardado_volta_bit_a_bit_sem_normalizar_de_novo()
    {
        var gravado = CadastroFacial.Cadastrar(Autorizacao(), RostosDeTeste.CapturasDoJoao, RostosDeTeste.Limiar, Exemplos.Em(23, 7, 5)).Modelo;

        var lido = ModeloFacial.Reconstituir(gravado.Reconhecedor, gravado.Valores);

        Assert.Equal(gravado.Valores.ToArray(), lido.Valores.ToArray());
        Assert.Equal(1.0, lido.Similaridade(gravado), 6);
    }

    [Fact]
    public void O_vetor_guardado_nao_e_normalizado_de_novo_nem_com_a_norma_um_pouco_fora_de_1()
    {
        // Norma 1,00005, dentro da folga da leitura: normalizar de novo daria 1.
        var lido = ModeloFacial.Reconstituir("teste-4d", [1.00005f, 0f, 0f, 0f]);

        Assert.Equal(1.00005f, lido.Valores[0]);
    }

    [Fact]
    public void O_vetor_sem_norma_1_nao_e_o_que_o_cadastro_gravou()
    {
        var erro = Assert.Throws<ExcecaoDeDominio>(() => ModeloFacial.Reconstituir("teste-4d", [1f, 1f, 0f, 0f]));

        Assert.Equal("O vetor do rosto guardado não tem norma 1: não é o que o cadastro gravou.", erro.Message);
        Assert.Throws<ExcecaoDeDominio>(() => ModeloFacial.Reconstituir("teste-4d", [float.NaN, 0f, 0f, 0f]));
    }

    [Fact]
    public void A_autorizacao_encerrada_volta_encerrada_e_sem_pedido_de_eliminacao_pendente()
    {
        var lida = AutorizacaoBiometrica.Reconstituir(
            Exemplos.Construtora.Documento,
            Exemplos.Joao.Cpf,
            Exemplos.Termo,
            Exemplos.Em(23, 7, 0),
            Exemplos.Em(23, 12, 30),
            MotivoDoEncerramento.RevogadaPeloTrabalhador);

        Assert.False(lida.VigenteEm(Exemplos.Em(23, 12, 30)));
        Assert.True(lida.VigenteEm(Exemplos.Em(23, 12, 29)));
        Assert.Equal(MotivoDoEncerramento.RevogadaPeloTrabalhador, lida.Motivo);
        Assert.Empty(lida.Eventos);
        Assert.Throws<ExcecaoDeDominio>(() => lida.Revogar(Exemplos.Em(23, 13, 0)));
    }

    [Fact]
    public void A_autorizacao_guardada_passa_pelas_regras_do_encerramento()
    {
        Assert.Equal(
            "A autorização biométrica não pode acabar antes de ter sido concedida.",
            Assert.Throws<ExcecaoDeDominio>(() => Reconstituir(Exemplos.Em(23, 6, 0), MotivoDoEncerramento.Desligamento)).Message);
        Assert.Throws<ArgumentException>(() => Reconstituir(Exemplos.Em(23, 12, 30), null));
        Assert.Throws<ArgumentException>(() => Reconstituir(null, MotivoDoEncerramento.Desligamento));
        Assert.Throws<ArgumentOutOfRangeException>(() => Reconstituir(Exemplos.Em(23, 12, 30), (MotivoDoEncerramento)9));

        var vigente = Reconstituir(null, null);
        Assert.True(vigente.VigenteEm(Exemplos.Em(30, 0, 0)));
    }

    [Fact]
    public void O_cadastro_guardado_confere_o_rosto_como_o_original()
    {
        var autorizacao = Autorizacao();
        var original = CadastroFacial.Cadastrar(autorizacao, RostosDeTeste.CapturasDoJoao, RostosDeTeste.Limiar, Exemplos.Em(23, 7, 5));
        var lido = CadastroFacial.Reconstituir(
            original.Empregador,
            original.Trabalhador,
            ModeloFacial.Reconstituir(original.Modelo.Reconhecedor, original.Modelo.Valores),
            original.CadastradoEm);
        var prova = RostosDeTeste.Prova(RostosDeTeste.JoaoNaPortaria, Exemplos.Em(23, 7, 58));

        Assert.Equal(original.Conferir(prova, RostosDeTeste.Limiar).Similaridade, lido.Conferir(prova, RostosDeTeste.Limiar).Similaridade);
    }

    private static AutorizacaoBiometrica Autorizacao() =>
        AutorizacaoBiometrica.Conceder(Exemplos.Construtora, Exemplos.Joao, Exemplos.Termo, Exemplos.Em(23, 7, 0));

    private static AutorizacaoBiometrica Reconstituir(DateTimeOffset? encerradaEm, MotivoDoEncerramento? motivo) =>
        AutorizacaoBiometrica.Reconstituir(Exemplos.Construtora.Documento, Exemplos.Joao.Cpf, Exemplos.Termo, Exemplos.Em(23, 7, 0), encerradaEm, motivo);
}
