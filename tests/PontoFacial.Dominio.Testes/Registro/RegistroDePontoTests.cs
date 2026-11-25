using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes.Biometria;
using static PontoFacial.Dominio.Testes.Exemplos;
using static PontoFacial.Dominio.Testes.Registro.RegistroDeExemplo;

namespace PontoFacial.Dominio.Testes.Registro;

public sealed class RegistroDePontoTests
{
    private const string NaoIncluido = "O trabalhador não está incluído neste registro de ponto.";

    [Fact]
    public void O_nsr_comeca_em_1_na_abertura_e_conta_todas_as_operacoes()
    {
        var registro = RegistroDePonto.Abrir(Obra, Rh, Em(23, 6, 0));
        registro.IncluirTrabalhador(Joao, Rh, Em(23, 6, 1));
        registro.RegistrarEvento(EventoSensivel.DisponibilidadeDeServico, Em(23, 6, 2));
        registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.DispositivoEletronico);
        registro.AjustarRelogio(Em(23, 8, 0), Em(23, 8, 1), Rh);
        registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 12, 0)), Coletor.AplicativoMovel);

        (long Nsr, int Tipo)[] esperado = [(1, 2), (2, 5), (3, 6), (4, 7), (5, 4), (6, 7)];

        Assert.Equal(esperado, registro.Operacoes.Select(operacao => (operacao.Nsr, operacao.TipoDoRegistro)));
    }

    [Fact]
    public void Cada_estabelecimento_tem_a_sua_numeracao_e_a_sua_cadeia()
    {
        var sede = Estabelecimento.Novo(Construtora, "Av. Ana Costa, 500 - Santos/SP", "America/Sao_Paulo");
        var naObra = ComJoaoEMaria();
        var naSede = RegistroDePonto.Abrir(sede, Rh, Em(23, 6, 0));
        naSede.IncluirTrabalhador(Joao, Rh, Em(23, 6, 1));

        naObra.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.DispositivoEletronico);
        var primeiraNaSede = naSede.MarcarOnLine(PeloCodigo(Joao, Em(23, 14, 0)), Coletor.AplicativoMovel);

        Assert.Equal(3, primeiraNaSede.Nsr);
        Assert.Null(primeiraNaSede.HashAnterior);
    }

    [Fact]
    public void So_quem_esta_incluido_marca()
    {
        var registro = RegistroDePonto.Abrir(Obra, Rh, Em(23, 6, 0));

        var erro = Assert.Throws<ExcecaoDeDominio>(() => registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.AplicativoMovel));

        Assert.Equal(NaoIncluido, erro.Message);
        Assert.Single(registro.Operacoes);
    }

    [Fact]
    public void O_excluido_nao_marca_mais_as_marcacoes_dele_ficam_e_ele_pode_voltar()
    {
        var registro = ComJoaoEMaria();
        registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.AplicativoMovel);

        var exclusao = registro.ExcluirTrabalhador(Joao.Cpf, Rh, Em(23, 17, 0));
        var erro = Assert.Throws<ExcecaoDeDominio>(() => registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 17, 5)), Coletor.AplicativoMovel));
        registro.IncluirTrabalhador(Joao, Rh, Em(24, 7, 0));
        registro.MarcarOnLine(PeloCodigo(Joao, Em(24, 7, 58)), Coletor.AplicativoMovel);

        Assert.Equal(TipoDeMudanca.Exclusao, exclusao.Mudanca);
        Assert.Equal("João da Silva", exclusao.Nome);
        Assert.Equal(NaoIncluido, erro.Message);
        Assert.Equal(2, registro.Marcacoes.Count);
    }

    [Fact]
    public void Incluir_quem_ja_esta_incluido_e_recusado()
    {
        var registro = ComJoaoEMaria();

        var erro = Assert.Throws<ExcecaoDeDominio>(() => registro.IncluirTrabalhador(Joao, Rh, Em(23, 7, 0)));

        Assert.Equal("O trabalhador já está incluído neste registro de ponto.", erro.Message);
    }

    [Fact]
    public void Alterar_ou_excluir_quem_nao_esta_incluido_e_recusado()
    {
        var registro = RegistroDePonto.Abrir(Obra, Rh, Em(23, 6, 0));

        var alterar = Assert.Throws<ExcecaoDeDominio>(() => registro.AlterarTrabalhador(Joao, Rh, Em(23, 7, 0)));
        var excluir = Assert.Throws<ExcecaoDeDominio>(() => registro.ExcluirTrabalhador(Joao.Cpf, Rh, Em(23, 7, 0)));

        Assert.Equal(NaoIncluido, alterar.Message);
        Assert.Equal(NaoIncluido, excluir.Message);
    }

    [Fact]
    public void A_identificacao_de_outro_empregador_e_recusada()
    {
        var registro = ComJoaoEMaria();
        var outro = Empregador.Novo(Cnpj.De("AB12CD34000184"), "Outra Construtora S.A.");

        var erro = Assert.Throws<ExcecaoDeDominio>(() =>
            registro.MarcarOnLine(IdentificacaoDoTrabalhador.PorCodigoPessoal(outro, Joao, Em(23, 7, 58)), Coletor.AplicativoMovel));

        Assert.Equal("A identificação é de outro empregador: a marcação não pertence a este registro de ponto.", erro.Message);
    }

    [Fact]
    public void A_marcacao_on_line_e_gravada_na_hora_do_registro_ao_minuto()
    {
        var registro = ComJoaoEMaria();

        var marcacao = registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58, 42)), Coletor.DispositivoEletronico);

        Assert.True(marcacao.OnLine);
        Assert.Equal(Em(23, 7, 58), marcacao.MarcadaEm);
        Assert.Equal(Em(23, 7, 58), marcacao.GravadaEm);
        Assert.Equal(Joao.Cpf, marcacao.Cpf);
    }

    [Fact]
    public void A_hora_e_a_do_fuso_do_estabelecimento()
    {
        var acre = Estabelecimento.Novo(Construtora, "Av. Brasil, 200 - Rio Branco/AC", "America/Rio_Branco");
        var registro = RegistroDePonto.Abrir(acre, Rh, new DateTimeOffset(2026, 11, 23, 11, 0, 0, TimeSpan.Zero));
        registro.IncluirTrabalhador(Joao, Rh, new DateTimeOffset(2026, 11, 23, 11, 1, 0, TimeSpan.Zero));

        var marcacao = registro.MarcarOnLine(PeloCodigo(Joao, new DateTimeOffset(2026, 11, 23, 12, 0, 0, TimeSpan.Zero)), Coletor.AplicativoMovel);

        Assert.Equal(new DateTimeOffset(2026, 11, 23, 7, 0, 0, TimeSpan.FromHours(-5)), marcacao.MarcadaEm);
        Assert.Equal(TimeSpan.FromHours(-5), marcacao.MarcadaEm.Offset);
    }

    [Fact]
    public void A_marcacao_off_line_guarda_a_hora_em_que_foi_feita_e_a_em_que_chegou()
    {
        var registro = ComJoaoEMaria();
        registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.DispositivoEletronico);

        // Maria marcou às 7h50 no celular, sem sinal; a marcação chegou às 9h30.
        var marcacao = registro.MarcarOffLine(PeloCodigo(Maria, Em(23, 7, 50)), Coletor.AplicativoMovel, Em(23, 9, 30));

        Assert.False(marcacao.OnLine);
        Assert.Equal(Em(23, 7, 50), marcacao.MarcadaEm);
        Assert.Equal(Em(23, 9, 30), marcacao.GravadaEm);
        Assert.Equal(5, marcacao.Nsr);
    }

    [Fact]
    public void A_marcacao_off_line_nao_e_gravada_antes_de_ser_feita()
    {
        var registro = ComJoaoEMaria();

        var erro = Assert.Throws<ExcecaoDeDominio>(() =>
            registro.MarcarOffLine(PeloCodigo(Maria, Em(23, 9, 0)), Coletor.AplicativoMovel, Em(23, 8, 59)));

        Assert.Equal("A marcação off-line é gravada depois de feita: a gravação não pode ser anterior à marcação.", erro.Message);
    }

    [Fact]
    public void A_marcacao_off_line_nao_e_anterior_a_inclusao_do_trabalhador()
    {
        var registro = ComJoaoEMaria();

        var erro = Assert.Throws<ExcecaoDeDominio>(() =>
            registro.MarcarOffLine(PeloCodigo(Maria, Em(23, 6, 0)), Coletor.AplicativoMovel, Em(23, 9, 0)));

        Assert.Equal("A marcação é anterior à inclusão do trabalhador neste registro de ponto.", erro.Message);
    }

    [Fact]
    public void O_relogio_do_registro_nao_volta_sem_ajuste()
    {
        var registro = ComJoaoEMaria();
        registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 8, 0)), Coletor.DispositivoEletronico);

        var erro = Assert.Throws<ExcecaoDeDominio>(() => registro.MarcarOnLine(PeloCodigo(Maria, Em(23, 7, 59)), Coletor.DispositivoEletronico));

        Assert.Equal(
            "O relógio do registro já marca 23/11/2026 08:00; gravar às 23/11/2026 07:59 seria voltar no tempo. "
            + "Para acertar a hora, registre um ajuste do relógio.",
            erro.Message);
    }

    [Fact]
    public void Com_o_ajuste_o_relogio_volta_e_o_ajuste_fica_registrado_com_o_responsavel()
    {
        var registro = ComJoaoEMaria();
        registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 8, 10)), Coletor.DispositivoEletronico);

        var ajuste = registro.AjustarRelogio(Em(23, 8, 10), Em(23, 8, 0), Rh);
        var depois = registro.MarcarOnLine(PeloCodigo(Maria, Em(23, 8, 5)), Coletor.DispositivoEletronico);

        Assert.Equal(Em(23, 8, 10), ajuste.Antes);
        Assert.Equal(Em(23, 8, 0), ajuste.Ajustado);
        Assert.Equal(Rh, ajuste.Responsavel);
        Assert.Equal(Em(23, 8, 5), depois.MarcadaEm);
    }

    [Fact]
    public void O_registro_nao_restringe_a_marcacao()
    {
        // Portaria 671, art. 74: nada de recusar marcação repetida ou fora do
        // horário. Quem trata o ponto pode indicar a indevida, não apagá-la.
        var registro = ComJoaoEMaria();

        registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58, 10)), Coletor.DispositivoEletronico);
        registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58, 40)), Coletor.DispositivoEletronico);
        registro.MarcarOnLine(PeloCodigo(Joao, Em(24, 3, 0)), Coletor.AplicativoMovel);

        Assert.Equal(3, registro.Marcacoes.Count);
    }

    [Fact]
    public void As_operacoes_nao_podem_ser_apagadas_nem_trocadas_por_fora()
    {
        var registro = ComJoaoEMaria();
        var marcacao = registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.DispositivoEletronico);

        Assert.Throws<NotSupportedException>(() => ((IList<Operacao>)registro.Operacoes).RemoveAt(0));
        Assert.Throws<NotSupportedException>(() => ((IList<Operacao>)registro.Operacoes)[3] = marcacao);
        Assert.Throws<NotSupportedException>(() => ((IList<MarcacaoDePonto>)registro.Marcacoes).Clear());
    }

    [Fact]
    public void Pelo_rosto_ou_pelo_codigo_a_marcacao_e_a_mesma_para_a_portaria()
    {
        var autorizacao = AutorizacaoBiometrica.Conceder(Construtora, Joao, Termo, Em(23, 7, 0));
        var umRegistro = ComJoaoEMaria();
        var outroRegistro = ComJoaoEMaria();

        var peloRosto = umRegistro.MarcarOnLine(
            IdentificacaoDoTrabalhador.PorRosto(autorizacao, RostosDeTeste.JoaoConferido(autorizacao, Em(23, 7, 58))), Coletor.DispositivoEletronico);
        var peloCodigo = outroRegistro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), Coletor.DispositivoEletronico);

        Assert.Equal(MeioDeIdentificacao.Rosto, peloRosto.Meio);
        Assert.Equal(MeioDeIdentificacao.CodigoPessoal, peloCodigo.Meio);
        Assert.Equal(peloRosto.LinhaDoAfd, peloCodigo.LinhaDoAfd);
    }

    [Fact]
    public void Coletor_sem_codigo_no_arquivo_e_recusado()
    {
        var registro = ComJoaoEMaria();

        Assert.Throws<ArgumentOutOfRangeException>(() => registro.MarcarOnLine(PeloCodigo(Joao, Em(23, 7, 58)), (Coletor)9));
    }
}
