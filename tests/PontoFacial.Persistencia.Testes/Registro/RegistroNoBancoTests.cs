using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes;
using PontoFacial.Dominio.Testes.Biometria;
using PontoFacial.Dominio.Testes.Registro;
using PontoFacial.Persistencia.Registro;

namespace PontoFacial.Persistencia.Testes.Registro;

public sealed class RegistroNoBancoTests(BancoDeTestes banco)
{
    [Fact]
    public async Task O_registro_gravado_volta_do_banco_operacao_por_operacao_com_o_fuso_de_cada_hora()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var origem = RegistroVariado.Montar(empregador);

        var obra = await Copia.DoRegistro(empresa, origem, Exemplos.Joao, Exemplos.Maria);
        var lidas = await empresa.Registros.Operacoes(obra, cancelamento: TestContext.Current.CancellationToken);

        Assert.Equal(origem.Operacoes.Select(RegistroVariado.Descrever), lidas.Select(RegistroVariado.Descrever));
        Assert.Equal(12, lidas.Count);
    }

    [Fact]
    public async Task A_ponta_lida_do_banco_e_a_do_registro_que_nao_saiu_da_memoria()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var origem = RegistroVariado.Montar(empregador);
        var obra = await Copia.DoRegistro(empresa, origem, Exemplos.Joao, Exemplos.Maria);

        var ponta = await empresa.Transacao(sessao => RegistrosDePonto.Ponta(sessao, obra), TestContext.Current.CancellationToken);
        var esperada = origem.Ponta();

        Assert.NotNull(ponta);
        Assert.Equal(esperada.UltimoNsr, ponta.UltimoNsr);
        Assert.Equal(esperada.Relogio.ToString("O"), ponta.Relogio.ToString("O"));
        Assert.Equal(esperada.UltimoHash, ponta.UltimoHash);
        Assert.Equal(
            esperada.Incluidos.OrderBy(vinculo => vinculo.Cpf.Numero, StringComparer.Ordinal).Select(Vinculo),
            ponta.Incluidos.Select(Vinculo));

        // A Maria, com o nome de casada; o João, incluído de novo às 18h.
        Assert.Equal(["11144477735 Maria Souza Lima 2026-11-23T06:02:00.0000000-03:00", "52998224725 João da Silva 2026-11-23T18:00:00.0000000-03:00"], ponta.Incluidos.Select(Vinculo));
    }

    [Fact]
    public async Task A_marcacao_pelo_repositorio_continua_a_cadeia_e_o_comprovante_do_banco_e_o_mesmo_da_hora()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var origem = RegistroVariado.Montar(empregador);
        var obra = await Copia.DoRegistro(empresa, origem, Exemplos.Joao, Exemplos.Maria);
        var ultima = origem.Marcacoes[^1];

        var comprovanteNaHora = await empresa.Registros.Gravar(
            obra,
            registro =>
            {
                var saida = registro.MarcarOnLine(
                    IdentificacaoDoTrabalhador.PorCodigoPessoal(empregador, RegistroVariado.MariaDepoisDoCasamento, Exemplos.Em(23, 18, 10)),
                    Coletor.DispositivoEletronico);

                return registro.Comprovante(saida.Nsr, RegistroDeExemplo.RegistroNoInpi);
            },
            TestContext.Current.CancellationToken);

        var doBanco = await empresa.Registros.Comprovante(obra, 13, RegistroDeExemplo.RegistroNoInpi, TestContext.Current.CancellationToken);
        var gravada = (MarcacaoDePonto)(await empresa.Registros.Operacoes(obra, 13, cancelamento: TestContext.Current.CancellationToken))[0];

        Assert.Equal(13, comprovanteNaHora.Nsr);
        Assert.Equal(ultima.Hash, gravada.HashAnterior);
        Assert.Equal(Comprovante(comprovanteNaHora), Comprovante(doBanco));
        Assert.Equal("Maria Souza Lima", doBanco.NomeDoTrabalhador);
    }

    [Fact]
    public async Task O_comprovante_lido_depois_traz_o_nome_que_o_trabalhador_tinha_quando_marcou()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await Copia.DoRegistro(empresa, RegistroVariado.Montar(empregador), Exemplos.Joao, Exemplos.Maria);

        // A Maria marcou no NSR 6, antes do casamento (NSR 9), e no 11, depois.
        var antes = await empresa.Registros.Comprovante(obra, 6, RegistroDeExemplo.RegistroNoInpi, TestContext.Current.CancellationToken);
        var depois = await empresa.Registros.Comprovante(obra, 11, RegistroDeExemplo.RegistroNoInpi, TestContext.Current.CancellationToken);

        Assert.Equal("Maria Souza", antes.NomeDoTrabalhador);
        Assert.Equal("Maria Souza Lima", depois.NomeDoTrabalhador);

        var erro = await Assert.ThrowsAsync<NaoEncontrado>(() => empresa.Registros.Comprovante(obra, 9, RegistroDeExemplo.RegistroNoInpi, TestContext.Current.CancellationToken));
        Assert.Equal("O NSR 9 não é de uma marcação deste registro de ponto.", erro.Message);
    }

    [Fact]
    public async Task O_que_o_dominio_recusa_nao_vai_para_o_banco()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await Copia.DoRegistro(empresa, RegistroVariado.Montar(empregador), Exemplos.Joao, Exemplos.Maria);

        // O relógio do registro marca 18h; gravar às 17h seria voltar no tempo.
        var erro = await Assert.ThrowsAsync<ExcecaoDeDominio>(() => empresa.Registros.Gravar(
            obra,
            registro => registro.MarcarOnLine(
                IdentificacaoDoTrabalhador.PorCodigoPessoal(empregador, Exemplos.Joao, Exemplos.Em(23, 17, 0)),
                Coletor.DispositivoEletronico),
            TestContext.Current.CancellationToken));

        Assert.StartsWith("O relógio do registro já marca 23/11/2026 18:00", erro.Message, StringComparison.Ordinal);
        Assert.Equal(12, (await empresa.Registros.Operacoes(obra, cancelamento: TestContext.Current.CancellationToken)).Count);
    }

    [Fact]
    public async Task Varias_operacoes_numa_gravacao_so_vao_juntas_ou_nenhuma_vai()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await Copia.DoRegistro(empresa, RegistroVariado.Montar(empregador), Exemplos.Joao, Exemplos.Maria);

        // A segunda operação recusa, e a primeira, que o domínio aceitou, também não fica.
        await Assert.ThrowsAsync<ExcecaoDeDominio>(() => empresa.Registros.Gravar(
            obra,
            registro =>
            {
                registro.RegistrarEvento(EventoSensivel.IndisponibilidadeDeServico, Exemplos.Em(23, 19, 0));

                return registro.ExcluirTrabalhador(Cpf.De("123.456.789-09"), RegistroDeExemplo.Rh, Exemplos.Em(23, 19, 1));
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(12, (await empresa.Registros.Operacoes(obra, cancelamento: TestContext.Current.CancellationToken)).Count);

        await empresa.Registros.Gravar(
            obra,
            registro =>
            {
                registro.RegistrarEvento(EventoSensivel.IndisponibilidadeDeServico, Exemplos.Em(23, 19, 0));

                return registro.RegistrarEvento(EventoSensivel.DisponibilidadeDeServico, Exemplos.Em(23, 19, 30));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal([13L, 14L], (await empresa.Registros.Operacoes(obra, 13, cancelamento: TestContext.Current.CancellationToken)).Select(operacao => operacao.Nsr));
    }

    [Fact]
    public async Task A_marcacao_pelo_rosto_guarda_o_meio_que_fica_fora_do_hash()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await Copia.DoRegistro(empresa, RegistroVariado.Montar(empregador), Exemplos.Joao, Exemplos.Maria);
        var autorizacao = AutorizacaoBiometrica.Conceder(empregador, Exemplos.Joao, Exemplos.Termo, Exemplos.Em(23, 18, 5));

        var pelaFace = await empresa.Registros.Gravar(
            obra,
            registro => registro.MarcarOnLine(
                IdentificacaoDoTrabalhador.PorRosto(autorizacao, RostosDeTeste.JoaoConferido(autorizacao, Exemplos.Em(23, 18, 20))),
                Coletor.DispositivoEletronico),
            TestContext.Current.CancellationToken);

        var lida = (MarcacaoDePonto)(await empresa.Registros.Operacoes(obra, pelaFace.Nsr, cancelamento: TestContext.Current.CancellationToken))[0];

        Assert.Equal(MeioDeIdentificacao.Rosto, lida.Meio);
        Assert.Equal(pelaFace.Hash, lida.Hash);
    }

    [Fact]
    public async Task O_registro_abre_uma_vez_e_so_recebe_operacao_depois_de_aberto()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await empresa.Estabelecimentos.Cadastrar(Obras.De(empregador), TestContext.Current.CancellationToken);

        var semAbrir = await Assert.ThrowsAsync<NaoEncontrado>(() => empresa.Registros.Gravar(
            obra,
            registro => registro.RegistrarEvento(EventoSensivel.RetornoDeEnergia, Exemplos.Em(23, 6, 0)),
            TestContext.Current.CancellationToken));
        Assert.Equal($"O registro de ponto do estabelecimento {obra} ainda não foi aberto.", semAbrir.Message);

        var abertura = await empresa.Registros.Abrir(obra, RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 0, 45), TestContext.Current.CancellationToken);
        Assert.Equal(1, abertura.Nsr);
        Assert.Equal(Exemplos.Em(23, 6, 0), abertura.GravadaEm);

        var deNovo = await Assert.ThrowsAsync<JaExiste>(() => empresa.Registros.Abrir(obra, RegistroDeExemplo.Rh, Exemplos.Em(23, 7, 0), TestContext.Current.CancellationToken));
        Assert.Equal($"O registro de ponto do estabelecimento {obra} já foi aberto.", deNovo.Message);
    }

    [Fact]
    public async Task O_trabalhador_que_nao_esta_cadastrado_na_empresa_nao_entra_no_registro()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await empresa.Estabelecimentos.Cadastrar(Obras.De(empregador), TestContext.Current.CancellationToken);
        await empresa.Registros.Abrir(obra, RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 0), TestContext.Current.CancellationToken);

        var erro = await Assert.ThrowsAsync<NaoEncontrado>(() => empresa.Registros.Gravar(
            obra,
            registro => registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 1)),
            TestContext.Current.CancellationToken));

        Assert.Equal("O trabalhador 529.982.247-25 não está cadastrado nesta empresa: cadastre-o antes de incluí-lo no registro de ponto.", erro.Message);
    }

    [Fact]
    public async Task A_leitura_em_paginas_continua_de_onde_parou()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var origem = RegistroVariado.Montar(empregador);
        var obra = await Copia.DoRegistro(empresa, origem, Exemplos.Joao, Exemplos.Maria);

        var paginas = new List<Operacao>();

        for (var nsr = 1L; nsr <= 12; nsr += 5)
        {
            paginas.AddRange(await empresa.Registros.Operacoes(obra, nsr, 5, TestContext.Current.CancellationToken));
        }

        Assert.Equal(origem.Operacoes.Select(RegistroVariado.Descrever), paginas.Select(RegistroVariado.Descrever));
        Assert.Empty(await empresa.Registros.Operacoes(obra, 13, cancelamento: TestContext.Current.CancellationToken));
    }

    private static string Vinculo(VinculoNaPonta vinculo) => $"{vinculo.Cpf.Numero} {vinculo.Nome} {vinculo.IncluidoEm:O}";

    private static string Comprovante(ComprovanteDeRegistro comprovante) => FormattableString.Invariant(
        $"{comprovante.Nsr} {comprovante.NomeDoEmpregador} {comprovante.DocumentoDoEmpregador} {comprovante.Local} {comprovante.NomeDoTrabalhador} {comprovante.CpfDoTrabalhador} {comprovante.MarcadaEm:O} {comprovante.RegistroNoInpi} {comprovante.Hash}");
}
