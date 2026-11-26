using System.Buffers.Binary;
using Npgsql;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes;
using PontoFacial.Dominio.Testes.Biometria;
using PontoFacial.Dominio.Testes.Registro;
using PontoFacial.Persistencia.Biometria;

namespace PontoFacial.Persistencia.Testes.Biometria;

public sealed class BiometriaNoBancoTests(BancoDeTestes banco)
{
    private static readonly IChaveMestra Mestra = ChaveMestraLocal.Sortear("mestra-dos-testes");

    [Fact]
    public async Task O_rosto_cadastrado_volta_decifrado_e_identifica_o_trabalhador_na_marcacao()
    {
        var (empresa, empregador, biometria) = await EmpresaComJoaoEMaria();
        var obra = await Copia.DoRegistro(empresa, ComOJoao(empregador), Exemplos.Joao);
        var cadastro = await Cadastrar(biometria, empregador, Exemplos.Joao, Rostos.CapturasDoJoao);

        // Outro dia, outra instância: tudo lido do banco.
        var autorizacao = await biometria.Autorizacoes.Vigente(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken);
        var lido = await biometria.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken);
        var conferencia = lido!.Conferir(RostosDeTeste.Prova(Rostos.JoaoNaPortaria, Exemplos.Em(23, 7, 58, 31)), Rostos.Limiar);
        var entrada = await empresa.Registros.Gravar(
            obra,
            registro => registro.MarcarOnLine(IdentificacaoDoTrabalhador.PorRosto(autorizacao!, conferencia), Coletor.DispositivoEletronico),
            TestContext.Current.CancellationToken);

        Assert.Equal(cadastro.Modelo.Valores.ToArray(), lido.Modelo.Valores.ToArray());
        Assert.Equal(cadastro.CadastradoEm.ToString("O"), lido.CadastradoEm.ToString("O"));
        Assert.Equal(Rostos.Reconhecedor, lido.Modelo.Reconhecedor);
        Assert.True(conferencia.Conferiu);
        Assert.Equal(MeioDeIdentificacao.Rosto, entrada.Meio);
    }

    [Fact]
    public async Task O_banco_so_guarda_a_cifra_e_nenhum_pedaco_do_vetor_aparece_nela()
    {
        var (empresa, empregador, biometria) = await EmpresaComJoaoEMaria();
        var cadastro = await Cadastrar(biometria, empregador, Exemplos.Joao, Rostos.CapturasDoJoao);
        var vetor = Bytes(cadastro.Modelo);

        var (nonce, cifra) = await Cifra(empresa.Id, Exemplos.Joao.Cpf.Numero);

        Assert.Equal(12, nonce.Length);
        Assert.Equal((128 * 4) + 16, cifra.Length);

        // Nenhum trecho de 8 bytes do vetor — dois números seguidos — está na cifra.
        for (var inicio = 0; inicio + 8 <= vetor.Length; inicio += 4)
        {
            Assert.Equal(-1, cifra.AsSpan().IndexOf(vetor.AsSpan(inicio, 8)));
        }
    }

    [Fact]
    public async Task Gravar_o_mesmo_rosto_de_novo_da_outra_cifra()
    {
        var (empresa, empregador, biometria) = await EmpresaComJoaoEMaria();
        var cadastro = await Cadastrar(biometria, empregador, Exemplos.Joao, Rostos.CapturasDoJoao);
        var antes = await Cifra(empresa.Id, Exemplos.Joao.Cpf.Numero);

        await biometria.Cadastros.Gravar(cadastro, TestContext.Current.CancellationToken);
        var depois = await Cifra(empresa.Id, Exemplos.Joao.Cpf.Numero);

        Assert.NotEqual(antes.Nonce, depois.Nonce);
        Assert.NotEqual(antes.Cifrado, depois.Cifrado);
        Assert.Equal(cadastro.Modelo.Valores.ToArray(), (await biometria.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken))!.Modelo.Valores.ToArray());
    }

    [Fact]
    public async Task A_revogacao_apaga_o_modelo_na_mesma_transacao_e_a_autorizacao_fica_como_prova()
    {
        var (empresa, empregador, biometria) = await EmpresaComJoaoEMaria();
        await Cadastrar(biometria, empregador, Exemplos.Joao, Rostos.CapturasDoJoao);
        await Cadastrar(biometria, empregador, Exemplos.Maria, Rostos.CapturasDaMaria);

        var eliminacoes = await biometria.Autorizacoes.Encerrar(Exemplos.Joao.Cpf, autorizacao => autorizacao.Revogar(Exemplos.Em(23, 12, 30)), TestContext.Current.CancellationToken);

        var eliminacao = Assert.Single(eliminacoes);
        Assert.Equal(Exemplos.Joao.Cpf, eliminacao.Trabalhador);
        Assert.Equal(MotivoDoEncerramento.RevogadaPeloTrabalhador, eliminacao.Motivo);
        Assert.Null(await biometria.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        Assert.Equal(0L, await banco.ComoAplicacao(empresa.Id, "select count(*) from ponto.cadastros_faciais where cpf = $1", Exemplos.Joao.Cpf.Numero));
        Assert.Null(await biometria.Autorizacoes.Vigente(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));

        var historico = Assert.Single(await biometria.Autorizacoes.DoTrabalhador(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        Assert.Equal(Exemplos.Em(23, 12, 30), historico.EncerradaEm);
        Assert.Equal(MotivoDoEncerramento.RevogadaPeloTrabalhador, historico.Motivo);
        Assert.Empty(historico.Eventos);

        // O da Maria fica.
        Assert.NotNull(await biometria.Cadastros.Obter(Exemplos.Maria.Cpf, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task O_desligamento_tambem_apaga_o_modelo()
    {
        var (_, empregador, biometria) = await EmpresaComJoaoEMaria();
        await Cadastrar(biometria, empregador, Exemplos.Joao, Rostos.CapturasDoJoao);

        var eliminacao = Assert.Single(await biometria.Autorizacoes.Encerrar(
            Exemplos.Joao.Cpf,
            autorizacao => autorizacao.EncerrarPeloDesligamento(Exemplos.Em(30, 17, 0)),
            TestContext.Current.CancellationToken));

        Assert.Equal(MotivoDoEncerramento.Desligamento, eliminacao.Motivo);
        Assert.Null(await biometria.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task O_banco_nao_deixa_a_autorizacao_acabar_com_o_modelo_ainda_gravado()
    {
        var (empresa, empregador, biometria) = await EmpresaComJoaoEMaria();
        await Cadastrar(biometria, empregador, Exemplos.Joao, Rostos.CapturasDoJoao);

        var erro = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoAplicacao(
            empresa.Id,
            "update ponto.autorizacoes_biometricas set encerrada_em = now(), encerrada_em_deslocamento = -180, motivo = 1 where cpf = $1",
            Exemplos.Joao.Cpf.Numero));

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_cadastros_faciais_autorizacao"), (erro.SqlState, erro.ConstraintName));
    }

    [Fact]
    public async Task Sem_autorizacao_vigente_o_rosto_nao_e_cadastrado_nem_pelo_repositorio_nem_por_fora()
    {
        var (empresa, empregador, biometria) = await EmpresaComJoaoEMaria();
        var cadastro = await Cadastrar(biometria, empregador, Exemplos.Joao, Rostos.CapturasDoJoao);
        await biometria.Autorizacoes.Encerrar(Exemplos.Joao.Cpf, autorizacao => autorizacao.Revogar(Exemplos.Em(23, 12, 30)), TestContext.Current.CancellationToken);

        var peloRepositorio = await Assert.ThrowsAsync<ExcecaoDeDominio>(() => biometria.Cadastros.Gravar(cadastro, TestContext.Current.CancellationToken));
        var porFora = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoAplicacao(
            empresa.Id,
            """
            insert into ponto.cadastros_faciais (empregador_id, cpf, autorizacao_id, reconhecedor, dimensao, versao_da_chave, nonce, vetor_cifrado, cadastrado_em, cadastrado_em_deslocamento)
            select empregador_id, cpf, id, 'sface-2021dec', 1, 1, '\x000000000000000000000000'::bytea, '\x0000000000000000000000000000000000000000'::bytea, now(), -180
            from ponto.autorizacoes_biometricas where cpf = $1
            """,
            Exemplos.Joao.Cpf.Numero));

        Assert.Equal("Sem autorização biométrica vigente, o rosto não é cadastrado.", peloRepositorio.Message);
        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_cadastros_faciais_autorizacao"), (porFora.SqlState, porFora.ConstraintName));
    }

    [Fact]
    public async Task A_autorizacao_encerrada_nao_reabre_nem_se_apaga()
    {
        var (empresa, empregador, biometria) = await EmpresaComJoaoEMaria();
        await biometria.Autorizacoes.Conceder(Autorizacao(empregador, Exemplos.Joao), TestContext.Current.CancellationToken);
        await biometria.Autorizacoes.Encerrar(Exemplos.Joao.Cpf, autorizacao => autorizacao.Revogar(Exemplos.Em(23, 12, 30)), TestContext.Current.CancellationToken);

        var reabrir = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoAplicacao(
            empresa.Id,
            "update ponto.autorizacoes_biometricas set encerrada_em = null, encerrada_em_deslocamento = null, motivo = null where cpf = $1",
            Exemplos.Joao.Cpf.Numero));
        var apagar = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoDono("delete from ponto.autorizacoes_biometricas where empregador_id = $1", empresa.Id));
        var trocarOTermo = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoAplicacao(empresa.Id, "update ponto.autorizacoes_biometricas set termo_hash = termo_hash"));

        Assert.Equal("PF002", reabrir.SqlState);
        Assert.Equal("PF001", apagar.SqlState);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, trocarOTermo.SqlState);
    }

    [Fact]
    public async Task Uma_autorizacao_vigente_de_cada_vez_e_um_novo_consentimento_e_outra_autorizacao()
    {
        var (_, empregador, biometria) = await EmpresaComJoaoEMaria();
        await biometria.Autorizacoes.Conceder(Autorizacao(empregador, Exemplos.Joao), TestContext.Current.CancellationToken);

        var duas = await Assert.ThrowsAsync<JaExiste>(() => biometria.Autorizacoes.Conceder(Autorizacao(empregador, Exemplos.Joao), TestContext.Current.CancellationToken));
        Assert.Equal("O trabalhador 529.982.247-25 já tem uma autorização biométrica vigente.", duas.Message);

        await biometria.Autorizacoes.Encerrar(Exemplos.Joao.Cpf, autorizacao => autorizacao.Revogar(Exemplos.Em(23, 12, 30)), TestContext.Current.CancellationToken);
        await biometria.Autorizacoes.Conceder(
            AutorizacaoBiometrica.Conceder(empregador, Exemplos.Joao, Exemplos.Termo, Exemplos.Em(24, 7, 0)),
            TestContext.Current.CancellationToken);

        var historico = await biometria.Autorizacoes.DoTrabalhador(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken);
        Assert.Equal([Exemplos.Em(23, 7, 0), Exemplos.Em(24, 7, 0)], historico.Select(autorizacao => autorizacao.ConcedidaEm));
        Assert.Equal([true, false], historico.Select(autorizacao => autorizacao.EncerradaEm.HasValue));
    }

    [Fact]
    public async Task O_termo_volta_com_o_texto_que_o_trabalhador_leu_e_o_banco_recusa_hash_que_nao_e_o_do_texto()
    {
        var (empresa, empregador, biometria) = await EmpresaComJoaoEMaria();
        await biometria.Autorizacoes.Conceder(Autorizacao(empregador, Exemplos.Joao), TestContext.Current.CancellationToken);

        var termo = (await biometria.Autorizacoes.Vigente(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken))!.Termo;
        var erro = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoAplicacao(
            empresa.Id,
            "insert into ponto.termos_de_consentimento (empregador_id, hash, versao, texto) values ($1, $2, '2026.2', 'Outro texto.')",
            empresa.Id,
            Exemplos.Termo.Hash));

        Assert.Equal(Exemplos.TextoDoTermo, termo.Texto);
        Assert.Equal(Exemplos.Termo.Hash, termo.Hash);
        Assert.Equal("2026.1", termo.Versao);
        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_termos_de_consentimento_hash"), (erro.SqlState, erro.ConstraintName));
    }

    [Fact]
    public async Task A_cifra_de_um_trabalhador_colada_na_linha_de_outro_nao_abre()
    {
        var (empresa, empregador, biometria) = await EmpresaComJoaoEMaria();
        await Cadastrar(biometria, empregador, Exemplos.Joao, Rostos.CapturasDoJoao);
        await Cadastrar(biometria, empregador, Exemplos.Maria, Rostos.CapturasDaMaria);

        // A mesma chave, a mesma empresa: só o contexto — o CPF, a autorização — denuncia a troca.
        await banco.ComoDono(
            """
            update ponto.cadastros_faciais destino
            set nonce = origem.nonce, vetor_cifrado = origem.vetor_cifrado
            from ponto.cadastros_faciais origem
            where origem.empregador_id = $1 and origem.cpf = $2 and destino.empregador_id = $1 and destino.cpf = $3
            """,
            empresa.Id,
            Exemplos.Joao.Cpf.Numero,
            Exemplos.Maria.Cpf.Numero);

        var erro = await Assert.ThrowsAsync<ConteudoAdulterado>(() => biometria.Cadastros.Obter(Exemplos.Maria.Cpf, TestContext.Current.CancellationToken));

        Assert.Equal("O modelo do rosto do trabalhador 111.444.777-35 não decifra: a cifra foi mexida, ou é de outro trabalhador ou de outra empresa.", erro.Message);
    }

    [Fact]
    public async Task A_cifra_de_uma_empresa_copiada_para_outra_nao_abre()
    {
        var (horizonte, empregadorDaHorizonte, biometriaDaHorizonte) = await EmpresaComJoaoEMaria();
        var (aurora, empregadorDaAurora, biometriaDaAurora) = await EmpresaComJoaoEMaria();
        await Cadastrar(biometriaDaHorizonte, empregadorDaHorizonte, Exemplos.Joao, Rostos.CapturasDoJoao);
        await Cadastrar(biometriaDaAurora, empregadorDaAurora, Exemplos.Joao, Rostos.CapturasDoJoao);

        await banco.ComoDono(
            """
            update ponto.cadastros_faciais destino
            set nonce = origem.nonce, vetor_cifrado = origem.vetor_cifrado
            from ponto.cadastros_faciais origem
            where origem.empregador_id = $1 and destino.empregador_id = $2 and origem.cpf = destino.cpf
            """,
            horizonte.Id,
            aurora.Id);

        await Assert.ThrowsAsync<ConteudoAdulterado>(() => biometriaDaAurora.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        Assert.NotNull(await biometriaDaHorizonte.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Um_bit_mexido_na_cifra_e_o_modelo_nao_volta()
    {
        var (empresa, empregador, biometria) = await EmpresaComJoaoEMaria();
        await Cadastrar(biometria, empregador, Exemplos.Joao, Rostos.CapturasDoJoao);

        await banco.ComoDono(
            "update ponto.cadastros_faciais set vetor_cifrado = set_byte(vetor_cifrado, 100, get_byte(vetor_cifrado, 100) # 1) where empregador_id = $1",
            empresa.Id);

        await Assert.ThrowsAsync<ConteudoAdulterado>(() => biometria.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Outra_empresa_nao_ve_a_autorizacao_nem_o_modelo()
    {
        var (_, empregadorDaHorizonte, biometriaDaHorizonte) = await EmpresaComJoaoEMaria();
        var (aurora, _, _) = await EmpresaComJoaoEMaria();
        await Cadastrar(biometriaDaHorizonte, empregadorDaHorizonte, Exemplos.Joao, Rostos.CapturasDoJoao);

        // A mesma chave mestra: o que separa as empresas é o banco, e a chave de cada uma.
        var biometriaDaAurora = aurora.Biometria(Mestra);

        Assert.Null(await biometriaDaAurora.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        Assert.Null(await biometriaDaAurora.Autorizacoes.Vigente(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        Assert.Equal(0L, await banco.ComoAplicacao(aurora.Id, "select count(*) from ponto.cadastros_faciais"));
        Assert.Equal(0L, await banco.ComoAplicacao(aurora.Id, "select count(*) from ponto.chaves_das_empresas"));
    }

    [Fact]
    public async Task O_modelo_se_apaga_a_pedido_sem_encerrar_a_autorizacao()
    {
        var (_, empregador, biometria) = await EmpresaComJoaoEMaria();
        await Cadastrar(biometria, empregador, Exemplos.Joao, Rostos.CapturasDoJoao);

        Assert.True(await biometria.Cadastros.Apagar(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        Assert.False(await biometria.Cadastros.Apagar(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        Assert.Null(await biometria.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        Assert.NotNull(await biometria.Autorizacoes.Vigente(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
    }

    private async Task<(EmpresaNoBanco Empresa, Empregador Empregador, BiometriaDaEmpresa Biometria)> EmpresaComJoaoEMaria()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        await empresa.Trabalhadores.Gravar(Exemplos.Joao, TestContext.Current.CancellationToken);
        await empresa.Trabalhadores.Gravar(Exemplos.Maria, TestContext.Current.CancellationToken);

        return (empresa, empregador, empresa.Biometria(Mestra));
    }

    private static AutorizacaoBiometrica Autorizacao(Empregador empregador, Trabalhador trabalhador) =>
        AutorizacaoBiometrica.Conceder(empregador, trabalhador, Exemplos.Termo, Exemplos.Em(23, 7, 0));

    /// <summary>A autorização concedida e o cadastro do rosto feito com ela, os dois gravados.</summary>
    private static async Task<CadastroFacial> Cadastrar(BiometriaDaEmpresa biometria, Empregador empregador, Trabalhador trabalhador, ModeloFacial[] capturas)
    {
        var autorizacao = Autorizacao(empregador, trabalhador);
        await biometria.Autorizacoes.Conceder(autorizacao, TestContext.Current.CancellationToken);

        var cadastro = CadastroFacial.Cadastrar(autorizacao, capturas, Rostos.Limiar, Exemplos.Em(23, 7, 5));
        await biometria.Cadastros.Gravar(cadastro, TestContext.Current.CancellationToken);

        return cadastro;
    }

    private static RegistroDePonto ComOJoao(Empregador empregador)
    {
        var registro = RegistroDePonto.Abrir(Obras.De(empregador), RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 1));

        return registro;
    }

    private static byte[] Bytes(ModeloFacial modelo)
    {
        var bytes = new byte[modelo.Dimensao * 4];

        for (var i = 0; i < modelo.Dimensao; i++)
        {
            BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(i * 4), modelo.Valores[i]);
        }

        return bytes;
    }

    /// <summary>O nonce e a cifra como estão na linha, lidos pelo dono do banco.</summary>
    private async Task<(byte[] Nonce, byte[] Cifrado)> Cifra(Guid empresa, string cpf)
    {
        await using var comando = banco.Dono.CreateCommand("select nonce, vetor_cifrado from ponto.cadastros_faciais where empregador_id = $1 and cpf = $2");
        comando.Parameters.Add(new NpgsqlParameter { Value = empresa });
        comando.Parameters.Add(new NpgsqlParameter { Value = cpf });
        await using var leitor = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await leitor.ReadAsync(TestContext.Current.CancellationToken));

        return (leitor.GetFieldValue<byte[]>(0), leitor.GetFieldValue<byte[]>(1));
    }
}
