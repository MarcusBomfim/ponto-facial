using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;

namespace PontoFacial.Dominio.Testes.Biometria;

public sealed class AutorizacaoBiometricaTests
{
    private static readonly DateTimeOffset Concedida = Exemplos.Em(23, 8, 0);

    private static AutorizacaoBiometrica Autorizacao() =>
        AutorizacaoBiometrica.Conceder(Exemplos.Construtora, Exemplos.Joao, Exemplos.Termo, Concedida);

    [Fact]
    public void Guarda_quem_autorizou_para_quem_e_o_termo_que_leu()
    {
        var autorizacao = Autorizacao();

        Assert.Equal(Exemplos.Construtora.Documento, autorizacao.Empregador);
        Assert.Equal(Exemplos.Joao.Cpf, autorizacao.Trabalhador);
        Assert.Equal(Exemplos.Termo.Hash, autorizacao.Termo.Hash);
        Assert.Null(autorizacao.EncerradaEm);
        Assert.Empty(autorizacao.Eventos);
    }

    [Fact]
    public void Vale_a_partir_da_concessao()
    {
        var autorizacao = Autorizacao();

        Assert.False(autorizacao.VigenteEm(Concedida.AddMinutes(-1)));
        Assert.True(autorizacao.VigenteEm(Concedida));
        Assert.True(autorizacao.VigenteEm(Concedida.AddYears(1)));
    }

    [Fact]
    public void A_revogacao_nao_pede_motivo_nem_aprovacao_e_vale_dali_em_diante()
    {
        var autorizacao = Autorizacao();
        var revogada = Exemplos.Em(24, 18, 0);

        autorizacao.Revogar(revogada);

        Assert.True(autorizacao.VigenteEm(revogada.AddMinutes(-1)));
        Assert.False(autorizacao.VigenteEm(revogada));
        Assert.Equal(revogada, autorizacao.EncerradaEm);
        Assert.Equal(MotivoDoEncerramento.RevogadaPeloTrabalhador, autorizacao.Motivo);
    }

    [Fact]
    public void Ao_ser_revogada_o_modelo_do_rosto_deve_ser_eliminado()
    {
        var autorizacao = Autorizacao();

        autorizacao.Revogar(Exemplos.Em(24, 18, 0));

        var evento = Assert.IsType<ModeloFacialDeveSerEliminado>(Assert.Single(autorizacao.Eventos));
        Assert.Equal(
            new ModeloFacialDeveSerEliminado(
                Exemplos.Construtora.Documento, Exemplos.Joao.Cpf, MotivoDoEncerramento.RevogadaPeloTrabalhador, Exemplos.Em(24, 18, 0)),
            evento);
    }

    [Fact]
    public void O_desligamento_tambem_elimina_o_modelo_do_rosto()
    {
        var autorizacao = Autorizacao();

        autorizacao.EncerrarPeloDesligamento(Exemplos.Em(30, 17, 0));

        var evento = Assert.IsType<ModeloFacialDeveSerEliminado>(Assert.Single(autorizacao.Eventos));
        Assert.Equal(MotivoDoEncerramento.Desligamento, evento.Motivo);
        Assert.False(autorizacao.VigenteEm(Exemplos.Em(30, 17, 0)));
    }

    [Fact]
    public void O_que_ja_acabou_nao_acaba_de_novo()
    {
        var autorizacao = Autorizacao();
        autorizacao.Revogar(Exemplos.Em(24, 18, 0));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => autorizacao.EncerrarPeloDesligamento(Exemplos.Em(30, 17, 0)));

        Assert.Equal("A autorização biométrica já foi encerrada em 24/11/2026 18:00.", erro.Message);
        Assert.Single(autorizacao.Eventos);
    }

    [Fact]
    public void Nao_acaba_antes_de_ter_comecado()
    {
        var autorizacao = Autorizacao();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => autorizacao.Revogar(Concedida.AddMinutes(-1)));

        Assert.Equal("A autorização biométrica não pode acabar antes de ter sido concedida.", erro.Message);
    }

    [Fact]
    public void Autorizar_de_novo_e_outra_autorizacao_com_o_termo_da_vez()
    {
        var primeira = Autorizacao();
        primeira.Revogar(Exemplos.Em(24, 18, 0));
        var termoNovo = TermoDeConsentimento.Novo("2026.2", Exemplos.TextoDoTermo + " Os dados ficam no Brasil.");

        var segunda = AutorizacaoBiometrica.Conceder(Exemplos.Construtora, Exemplos.Joao, termoNovo, Exemplos.Em(26, 8, 0));

        Assert.False(primeira.VigenteEm(Exemplos.Em(26, 9, 0)));
        Assert.True(segunda.VigenteEm(Exemplos.Em(26, 9, 0)));
        Assert.Equal("2026.2", segunda.Termo.Versao);
    }

    [Fact]
    public void Os_eventos_nao_podem_ser_mexidos_por_fora()
    {
        var autorizacao = Autorizacao();
        autorizacao.Revogar(Exemplos.Em(24, 18, 0));

        Assert.Throws<NotSupportedException>(() => ((IList<EventoDeDominio>)autorizacao.Eventos).Clear());
    }
}
