using Npgsql;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Testes;
using PontoFacial.Persistencia.Biometria;

namespace PontoFacial.Persistencia.Testes.Biometria;

/*
 * A chave de cada empresa nasce sozinha, gira, é embrulhada de novo quando
 * muda a chave mestra e é descartada quando nada mais a usa — sem que nenhum
 * modelo do rosto deixe de abrir no caminho (ADR-0014).
 */
public sealed class ChavesDaEmpresaTests(BancoDeTestes banco)
{
    private static readonly IChaveMestra DeNovembro = ChaveMestraLocal.Sortear("mestra-2026-11");

    private static readonly IChaveMestra DeMaio = ChaveMestraLocal.Sortear("mestra-2027-05");

    [Fact]
    public async Task A_primeira_chave_nasce_no_primeiro_cadastro_embrulhada_pela_chave_mestra()
    {
        var (empresa, empregador) = await EmpresaComJoaoEMaria();
        var biometria = empresa.Biometria(DeNovembro);
        Assert.Empty(await Chaves(empresa.Id));

        await Cadastrar(biometria, empregador, Exemplos.Joao, Rostos.CapturasDoJoao);

        var chave = Assert.Single(await Chaves(empresa.Id));
        Assert.Equal((1, "mestra-2026-11", 60), chave);
    }

    [Fact]
    public async Task Dois_cadastros_ao_mesmo_tempo_numa_empresa_nova_ficam_com_a_mesma_chave()
    {
        var (empresa, empregador) = await EmpresaComJoaoEMaria();
        var biometria = empresa.Biometria(DeNovembro);
        await biometria.Autorizacoes.Conceder(AutorizacaoBiometrica.Conceder(empregador, Exemplos.Joao, Exemplos.Termo, Exemplos.Em(23, 7, 0)), TestContext.Current.CancellationToken);
        await biometria.Autorizacoes.Conceder(AutorizacaoBiometrica.Conceder(empregador, Exemplos.Maria, Exemplos.Termo, Exemplos.Em(23, 7, 0)), TestContext.Current.CancellationToken);

        await Task.WhenAll(
            Task.Run(() => biometria.Cadastros.Gravar(Cadastro(empregador, Exemplos.Joao, Rostos.CapturasDoJoao), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken),
            Task.Run(() => biometria.Cadastros.Gravar(Cadastro(empregador, Exemplos.Maria, Rostos.CapturasDaMaria), TestContext.Current.CancellationToken), TestContext.Current.CancellationToken));

        Assert.Single(await Chaves(empresa.Id));
        Assert.NotNull(await biometria.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        Assert.NotNull(await biometria.Cadastros.Obter(Exemplos.Maria.Cpf, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Girar_cifrar_de_novo_e_descartar_a_antiga_sem_que_nenhum_modelo_deixe_de_abrir()
    {
        var (empresa, empregador) = await EmpresaComJoaoEMaria();
        var biometria = empresa.Biometria(DeNovembro);
        var doJoao = await Cadastrar(biometria, empregador, Exemplos.Joao, Rostos.CapturasDoJoao);

        Assert.Equal(2, await biometria.Chaves.Girar(TestContext.Current.CancellationToken));

        // O modelo novo já sai com a versão 2; o antigo continua na 1, e abre.
        var daMaria = await Cadastrar(biometria, empregador, Exemplos.Maria, Rostos.CapturasDaMaria);
        Assert.Equal([1, 2], await VersoesDosModelos(empresa.Id));
        Assert.Equal(0, await biometria.Chaves.DescartarAsAntigas(TestContext.Current.CancellationToken));

        Assert.Equal(1, await biometria.Cadastros.CifrarDeNovo(TestContext.Current.CancellationToken));
        Assert.Equal([2, 2], await VersoesDosModelos(empresa.Id));
        Assert.Equal(1, await biometria.Chaves.DescartarAsAntigas(TestContext.Current.CancellationToken));

        Assert.Equal([2], (await Chaves(empresa.Id)).Select(chave => chave.Versao));
        Assert.Equal(doJoao.Modelo.Valores.ToArray(), (await biometria.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken))!.Modelo.Valores.ToArray());
        Assert.Equal(daMaria.Modelo.Valores.ToArray(), (await biometria.Cadastros.Obter(Exemplos.Maria.Cpf, TestContext.Current.CancellationToken))!.Modelo.Valores.ToArray());
        Assert.Equal(0, await biometria.Cadastros.CifrarDeNovo(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Trocar_a_chave_mestra_embrulha_de_novo_a_chave_da_empresa_sem_tocar_nos_modelos()
    {
        var (empresa, empregador) = await EmpresaComJoaoEMaria();
        var doJoao = await Cadastrar(empresa.Biometria(DeNovembro), empregador, Exemplos.Joao, Rostos.CapturasDoJoao);
        var cifraAntes = await CifraDoJoao(empresa.Id);

        // Na troca, a instalação tem as duas: a nova embrulha, a antiga ainda desembrulha.
        var naTroca = empresa.Biometria(DeMaio, DeNovembro);
        Assert.Equal(1, await naTroca.Chaves.EmbrulharDeNovo(TestContext.Current.CancellationToken));
        Assert.Equal(0, await naTroca.Chaves.EmbrulharDeNovo(TestContext.Current.CancellationToken));

        // Depois, só a nova.
        var depois = empresa.Biometria(DeMaio);

        Assert.Equal((1, "mestra-2027-05", 60), Assert.Single(await Chaves(empresa.Id)));
        Assert.Equal(cifraAntes, await CifraDoJoao(empresa.Id));
        Assert.Equal(doJoao.Modelo.Valores.ToArray(), (await depois.Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken))!.Modelo.Valores.ToArray());
    }

    [Fact]
    public async Task Sem_a_chave_mestra_que_embrulhou_nada_abre()
    {
        var (empresa, empregador) = await EmpresaComJoaoEMaria();
        await Cadastrar(empresa.Biometria(DeNovembro), empregador, Exemplos.Joao, Rostos.CapturasDoJoao);

        // Outra chave mestra, de outro nome: a instalação diz qual falta.
        var semANovembro = await Assert.ThrowsAsync<InvalidOperationException>(() => empresa.Biometria(DeMaio).Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        Assert.Equal(
            "A versão 1 da chave da empresa foi embrulhada pela chave mestra \"mestra-2026-11\", que esta instalação não tem.",
            semANovembro.Message);

        // Uma chave com o mesmo nome e outros bytes: o embrulho não abre.
        var impostora = ChaveMestraLocal.Sortear("mestra-2026-11");
        var erro = await Assert.ThrowsAsync<ConteudoAdulterado>(() => empresa.Biometria(impostora).Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));
        Assert.Equal("A versão 1 da chave da empresa não desembrulha: o embrulho foi mexido, ou é de outra empresa.", erro.Message);
    }

    [Fact]
    public async Task A_chave_embrulhada_de_uma_empresa_copiada_para_outra_nao_desembrulha()
    {
        var (horizonte, empregadorDaHorizonte) = await EmpresaComJoaoEMaria();
        var (aurora, empregadorDaAurora) = await EmpresaComJoaoEMaria();
        await Cadastrar(horizonte.Biometria(DeNovembro), empregadorDaHorizonte, Exemplos.Joao, Rostos.CapturasDoJoao);
        await Cadastrar(aurora.Biometria(DeNovembro), empregadorDaAurora, Exemplos.Joao, Rostos.CapturasDoJoao);

        // A mesma chave mestra nas duas: é o contexto do embrulho, com a empresa, que não deixa abrir.
        await banco.ComoDono(
            """
            update ponto.chaves_das_empresas destino
            set chave_embrulhada = origem.chave_embrulhada
            from ponto.chaves_das_empresas origem
            where origem.empregador_id = $1 and destino.empregador_id = $2 and origem.versao = destino.versao
            """,
            horizonte.Id,
            aurora.Id);

        var erro = await Assert.ThrowsAsync<ConteudoAdulterado>(() => aurora.Biometria(DeNovembro).Cadastros.Obter(Exemplos.Joao.Cpf, TestContext.Current.CancellationToken));

        Assert.Equal("A versão 1 da chave da empresa não desembrulha: o embrulho foi mexido, ou é de outra empresa.", erro.Message);
    }

    private async Task<(EmpresaNoBanco Empresa, Empregador Empregador)> EmpresaComJoaoEMaria()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        await empresa.Trabalhadores.Gravar(Exemplos.Joao, TestContext.Current.CancellationToken);
        await empresa.Trabalhadores.Gravar(Exemplos.Maria, TestContext.Current.CancellationToken);

        return (empresa, empregador);
    }

    private static CadastroFacial Cadastro(Empregador empregador, Trabalhador trabalhador, ModeloFacial[] capturas) =>
        CadastroFacial.Cadastrar(
            AutorizacaoBiometrica.Conceder(empregador, trabalhador, Exemplos.Termo, Exemplos.Em(23, 7, 0)),
            capturas,
            Rostos.Limiar,
            Exemplos.Em(23, 7, 5));

    private static async Task<CadastroFacial> Cadastrar(BiometriaDaEmpresa biometria, Empregador empregador, Trabalhador trabalhador, ModeloFacial[] capturas)
    {
        await biometria.Autorizacoes.Conceder(AutorizacaoBiometrica.Conceder(empregador, trabalhador, Exemplos.Termo, Exemplos.Em(23, 7, 0)), TestContext.Current.CancellationToken);
        var cadastro = Cadastro(empregador, trabalhador, capturas);
        await biometria.Cadastros.Gravar(cadastro, TestContext.Current.CancellationToken);

        return cadastro;
    }

    private async Task<List<(int Versao, string ChaveMestra, int Tamanho)>> Chaves(Guid empresa)
    {
        await using var comando = banco.Dono.CreateCommand(
            "select versao, chave_mestra, octet_length(chave_embrulhada) from ponto.chaves_das_empresas where empregador_id = $1 order by versao");
        comando.Parameters.Add(new NpgsqlParameter { Value = empresa });
        await using var leitor = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var chaves = new List<(int, string, int)>();

        while (await leitor.ReadAsync(TestContext.Current.CancellationToken))
        {
            chaves.Add((leitor.GetInt32(0), leitor.GetString(1), leitor.GetInt32(2)));
        }

        return chaves;
    }

    private async Task<List<int>> VersoesDosModelos(Guid empresa)
    {
        await using var comando = banco.Dono.CreateCommand("select versao_da_chave from ponto.cadastros_faciais where empregador_id = $1 order by cpf desc");
        comando.Parameters.Add(new NpgsqlParameter { Value = empresa });
        await using var leitor = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var versoes = new List<int>();

        while (await leitor.ReadAsync(TestContext.Current.CancellationToken))
        {
            versoes.Add(leitor.GetInt32(0));
        }

        return versoes;
    }

    private async Task<byte[]> CifraDoJoao(Guid empresa)
    {
        await using var comando = banco.Dono.CreateCommand("select vetor_cifrado from ponto.cadastros_faciais where empregador_id = $1 and cpf = $2");
        comando.Parameters.Add(new NpgsqlParameter { Value = empresa });
        comando.Parameters.Add(new NpgsqlParameter { Value = Exemplos.Joao.Cpf.Numero });

        return (byte[])(await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }
}
