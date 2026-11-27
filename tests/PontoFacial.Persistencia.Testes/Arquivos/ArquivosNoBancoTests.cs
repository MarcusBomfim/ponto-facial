using PontoFacial.Dominio.Arquivos;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes;
using PontoFacial.Dominio.Testes.Arquivos;
using PontoFacial.Dominio.Testes.Jornada;
using PontoFacial.Dominio.Testes.Registro;
using PontoFacial.Persistencia.Arquivos;
using PontoFacial.Persistencia.Testes.Registro;

namespace PontoFacial.Persistencia.Testes.Arquivos;

/*
 * O AFD e o AEJ gerados do banco são os mesmos gerados do registro e do
 * tratamento em memória: o banco não perde nem acrescenta nada.
 */
public sealed class ArquivosNoBancoTests(BancoDeTestes banco)
{
    private static readonly DateOnly Dia23 = new(2026, 11, 23);

    [Fact]
    public async Task O_afd_do_banco_e_o_do_registro_em_memoria()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var origem = RegistroVariado.Montar(empregador);
        var obra = await Copia.DoRegistro(empresa, origem, Exemplos.Joao, Exemplos.Maria);

        var doBanco = await empresa.Arquivos.Afd(obra, Dia23, Dia23, Programas.PontoFacial, Exemplos.Em(23, 19, 0), TestContext.Current.CancellationToken);
        var daMemoria = ArquivoFonteDeDados.Gerar(origem.Estabelecimento, Programas.PontoFacial, origem.Operacoes, Dia23, Dia23, Exemplos.Em(23, 19, 0));

        Assert.Equal(daMemoria.Linhas, doBanco.Linhas);
        Assert.Equal(15, doBanco.Linhas.Count);
        Assert.Equal(daMemoria.NomeDoArquivo, doBanco.NomeDoArquivo);
    }

    [Fact]
    public async Task O_afd_de_um_dia_so_traz_as_operacoes_gravadas_nele_na_hora_do_estabelecimento()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var origem = RegistroVariado.Montar(empregador);

        // 23h59 de 23/11 em Santos já é 24/11 em UTC; 00h01 de 24/11, já é o dia seguinte.
        origem.MarcarOnLine(IdentificacaoDoTrabalhador.PorCodigoPessoal(empregador, Exemplos.Joao, Exemplos.Em(23, 23, 59)), Coletor.DispositivoEletronico);
        origem.MarcarOnLine(IdentificacaoDoTrabalhador.PorCodigoPessoal(empregador, Exemplos.Joao, Exemplos.Em(24, 0, 1)), Coletor.DispositivoEletronico);
        var obra = await Copia.DoRegistro(empresa, origem, Exemplos.Joao, Exemplos.Maria);

        var de23 = await empresa.Arquivos.Afd(obra, Dia23, Dia23, Programas.PontoFacial, Exemplos.Em(24, 8, 0), TestContext.Current.CancellationToken);
        var de24 = await empresa.Arquivos.Afd(obra, Dia23.AddDays(1), Dia23.AddDays(1), Programas.PontoFacial, Exemplos.Em(24, 8, 0), TestContext.Current.CancellationToken);
        var vazio = await empresa.Arquivos.Afd(obra, Dia23.AddDays(5), Dia23.AddDays(5), Programas.PontoFacial, Exemplos.Em(24, 8, 0), TestContext.Current.CancellationToken);

        Assert.Equal("000000013", de23.Linhas[^3][..9]);
        Assert.Equal(["000000000", "000000014", "999999999"], de24.Linhas.Take(3).Select(linha => linha[..9]));
        Assert.Equal(3, vazio.Linhas.Count);
    }

    [Fact]
    public async Task O_aej_do_banco_e_o_da_semana_do_joao_em_memoria()
    {
        var (empresa, _) = await banco.NovaEmpresa();
        var (registro, tratamento, _) = SemanaDoJoao.Montar();
        var obra = await Copia.DoRegistro(empresa, registro, Exemplos.Joao);
        await Copia.DoTratamento(empresa, obra, tratamento);
        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, new DateOnly(2026, 3, 2), Horarios.DaObraComIntervaloPreAssinalado, TestContext.Current.CancellationToken);
        var semana = (De: new DateOnly(2026, 11, 16), Ate: new DateOnly(2026, 11, 22));

        var doBanco = await empresa.Arquivos.Aej(obra, semana.De, semana.Ate, Programas.PontoFacial, Exemplos.Em(23, 8, 0), TestContext.Current.CancellationToken);
        var daMemoria = ArquivoEletronicoDeJornada.Gerar(
            Exemplos.Construtora,
            Programas.PontoFacial,
            [new JornadaDoVinculo(Exemplos.Joao, tratamento, Horarios.DaObraComIntervaloPreAssinalado, CalendarioDeFeriados.Nacionais, new DateOnly(2026, 11, 16))],
            semana.De,
            semana.Ate,
            Exemplos.Em(23, 8, 0));

        // O cabeçalho tem o CNPJ da empresa do teste; o resto é o mesmo, linha por linha.
        Assert.Equal(daMemoria.Linhas.Skip(1), doBanco.Linhas.Skip(1));
        Assert.Equal(28, doBanco.Linhas.Count);
    }

    [Fact]
    public async Task O_feriado_local_cadastrado_entra_no_aej_e_tira_a_falta()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var registro = RegistroDePonto.Abrir(Obras.De(empregador), RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(16, 5, 1));
        var obra = await Copia.DoRegistro(empresa, registro, Exemplos.Joao);
        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, new DateOnly(2026, 3, 2), Horarios.DaObra, TestContext.Current.CancellationToken);
        var segunda = new DateOnly(2026, 11, 16);

        var antes = await Ausencias(empresa, obra, segunda);
        await empresa.Feriados.Cadastrar(obra, segunda, "Feriado municipal", TestContext.Current.CancellationToken);
        var depois = await Ausencias(empresa, obra, segunda);

        Assert.Equal(["07|1|2|2026-11-16||"], antes);
        Assert.Empty(depois);
    }

    [Fact]
    public async Task Quem_saiu_antes_do_periodo_nao_entra_e_quem_saiu_no_meio_so_tem_falta_ate_sair()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var registro = RegistroDePonto.Abrir(Obras.De(empregador), RegistroDeExemplo.Rh, Exemplos.Em(9, 5, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(9, 5, 1));
        registro.IncluirTrabalhador(Exemplos.Maria, RegistroDeExemplo.Rh, Exemplos.Em(9, 5, 2));
        registro.ExcluirTrabalhador(Exemplos.Maria.Cpf, RegistroDeExemplo.Rh, Exemplos.Em(13, 18, 0));
        registro.ExcluirTrabalhador(Exemplos.Joao.Cpf, RegistroDeExemplo.Rh, Exemplos.Em(17, 18, 0));
        var obra = await Copia.DoRegistro(empresa, registro, Exemplos.Joao, Exemplos.Maria);
        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, new DateOnly(2026, 3, 2), Horarios.DaObra, TestContext.Current.CancellationToken);

        var aej = await empresa.Arquivos.Aej(obra, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 20), Programas.PontoFacial, Exemplos.Em(23, 8, 0), TestContext.Current.CancellationToken);

        // A Maria saiu na sexta, 13/11, e não tem horário: nem entra. O João saiu na terça, 17.
        Assert.Equal(["03|1|52998224725|João da Silva"], aej.Linhas.Where(linha => linha.StartsWith("03|", StringComparison.Ordinal)));
        Assert.Equal(["07|1|2|2026-11-16||", "07|1|2|2026-11-17||"], aej.Linhas.Where(linha => linha.StartsWith("07|", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Sem_horario_contratual_o_aej_e_recusado_com_o_nome_de_quem_falta()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await Copia.DoRegistro(empresa, RegistroVariado.Montar(empregador), Exemplos.Joao, Exemplos.Maria);
        await empresa.Horarios.Gravar(Exemplos.Joao.Cpf, new DateOnly(2026, 3, 2), Horarios.DaObra, TestContext.Current.CancellationToken);

        var erro = await Assert.ThrowsAsync<ExcecaoDeDominio>(() => empresa.Arquivos.Aej(obra, Dia23, Dia23, Programas.PontoFacial, Exemplos.Em(23, 19, 0), TestContext.Current.CancellationToken));

        Assert.Equal("Maria Souza (111.444.777-35) não tem horário contratual em 23/11/2026: cadastre o horário antes de gerar o AEJ.", erro.Message);
    }

    [Fact]
    public async Task O_arquivo_nao_sai_de_um_registro_adulterado()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await Copia.DoRegistro(empresa, RegistroVariado.Montar(empregador), Exemplos.Joao, Exemplos.Maria);
        await using (var conexao = await banco.Dono.OpenConnectionAsync(TestContext.Current.CancellationToken))
        await using (var transacao = await conexao.BeginTransactionAsync(TestContext.Current.CancellationToken))
        {
            await using var replica = new Npgsql.NpgsqlCommand("set local session_replication_role = replica", conexao, transacao);
            await replica.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await using var mexer = new Npgsql.NpgsqlCommand("update ponto.operacoes set marcada_em = marcada_em - interval '8 minutes' where estabelecimento_id = $1 and nsr = 5", conexao, transacao);
            mexer.Parameters.Add(new Npgsql.NpgsqlParameter { Value = obra });
            await mexer.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
            await transacao.CommitAsync(TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<ConteudoAdulterado>(() => empresa.Arquivos.Afd(obra, Dia23, Dia23, Programas.PontoFacial, Exemplos.Em(23, 19, 0), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("I:2026-11-01", "2026-11-01", null)]
    [InlineData("I:2026-11-01 E:2026-11-17", "2026-11-01", "2026-11-17")]
    [InlineData("I:2026-11-01 E:2026-11-13", null, null)]
    [InlineData("I:2026-11-25", null, null)]
    [InlineData("I:2026-11-01 E:2026-11-17 I:2026-11-19", "2026-11-01", null)]
    [InlineData("I:2026-11-01 A:2026-11-10 E:2026-11-18", "2026-11-01", "2026-11-18")]
    public void O_vinculo_no_periodo_sai_das_inclusoes_e_exclusoes(string mudancas, string? desde, string? ate)
    {
        var lidas = mudancas.Split(' ').Select(mudanca => (mudanca[0], DateOnly.Parse(mudanca[2..], System.Globalization.CultureInfo.InvariantCulture)));

        var vinculo = ArquivosDaPortaria.Vinculo(lidas, new DateOnly(2026, 11, 16), new DateOnly(2026, 11, 20));

        Assert.Equal(desde, vinculo?.Desde.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(ate, vinculo?.Ate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
    }

    private static async Task<string[]> Ausencias(EmpresaNoBanco empresa, Guid obra, DateOnly dia) =>
        [.. (await empresa.Arquivos.Aej(obra, dia, dia, Programas.PontoFacial, Exemplos.Em(23, 8, 0), TestContext.Current.CancellationToken))
            .Linhas.Where(linha => linha.StartsWith("07|", StringComparison.Ordinal))];
}
