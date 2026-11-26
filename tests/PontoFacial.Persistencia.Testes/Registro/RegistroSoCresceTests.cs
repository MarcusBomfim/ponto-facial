using System.Security.Cryptography;
using System.Text;
using Npgsql;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes;
using PontoFacial.Dominio.Testes.Registro;
using PontoFacial.Persistencia.Registro;

namespace PontoFacial.Persistencia.Testes.Registro;

/*
 * O que o banco garante sozinho, sem a aplicação: cada teste tenta gravar,
 * com o dono do banco — o superusuário —, uma operação que o domínio nunca
 * gravaria, e o PostgreSQL a recusa pela restrição certa. O primeiro teste
 * grava a mesma operação sem mexer em nada: é o controle, que prova que a
 * recusa dos outros vem do que foi mexido.
 */
public sealed class RegistroSoCresceTests(BancoDeTestes banco)
{
    [Fact]
    public async Task A_proxima_operacao_sem_mexer_em_nada_entra()
    {
        var (empresa, obra, origem) = await RegistroComDuasMarcacoes();
        var proxima = Marcar(origem, Exemplos.Em(23, 13, 0));

        Assert.Equal(1, await InserirComoDono(empresa, obra, proxima, _ => { }));
    }

    [Fact]
    public async Task O_banco_recusa_buraco_na_numeracao()
    {
        var (empresa, obra, origem) = await RegistroComDuasMarcacoes();
        var evento = origem.RegistrarEvento(EventoSensivel.RetornoDeEnergia, Exemplos.Em(23, 13, 0));

        var erro = await Recusada(empresa, obra, evento, valores => valores[2] = 6L);

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_operacoes_nsr_anterior"), erro);
    }

    [Fact]
    public async Task O_NSR_1_e_so_da_inclusao_do_empregador()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var obra = await empresa.Estabelecimentos.Cadastrar(Obras.De(empregador), TestContext.Current.CancellationToken);
        var evento = RegistroDePonto.Abrir(Obras.De(empregador), RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 0))
            .RegistrarEvento(EventoSensivel.RetornoDeEnergia, Exemplos.Em(23, 6, 1));

        var erro = await Recusada(empresa.Id, obra, evento, valores => valores[2] = 1L);

        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_operacoes_abertura"), erro);
    }

    [Fact]
    public async Task O_banco_recusa_a_marcacao_cujo_hash_nao_e_o_dos_campos()
    {
        var (empresa, obra, origem) = await RegistroComDuasMarcacoes();
        var proxima = Marcar(origem, Exemplos.Em(23, 13, 0));

        var erro = await Recusada(empresa, obra, proxima, valores => valores[24] = new string('a', 64));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_operacoes_hash"), erro);
    }

    [Fact]
    public async Task O_banco_recusa_campos_que_nao_sao_os_das_colunas_mesmo_com_o_hash_refeito()
    {
        var (empresa, obra, origem) = await RegistroComDuasMarcacoes();
        var proxima = Marcar(origem, Exemplos.Em(23, 13, 0));

        // Os campos com o CPF da Maria, e o hash refeito sobre eles; a coluna continua com o do João.
        var erro = await Recusada(empresa, obra, proxima, valores =>
        {
            valores[23] = ((string)valores[23]!).Replace("052998224725", "011144477735", StringComparison.Ordinal);
            valores[24] = Sha256((string)valores[23]! + proxima.HashAnterior);
        });

        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_operacoes_campos"), erro);
    }

    [Fact]
    public async Task O_banco_recusa_galho_na_cadeia()
    {
        var (empresa, obra, origem) = await RegistroComDuasMarcacoes();
        var primeira = origem.Marcacoes[0];
        var proxima = Marcar(origem, Exemplos.Em(23, 13, 0));

        // A terceira marcação amarrada à primeira, e não à segunda, com o hash certo para isso.
        var erro = await Recusada(empresa, obra, proxima, valores =>
        {
            valores[25] = primeira.Hash;
            valores[24] = Sha256(proxima.Campos + primeira.Hash);
        });

        Assert.Equal((PostgresErrorCodes.UniqueViolation, "uq_operacoes_hash_anterior"), erro);
    }

    [Fact]
    public async Task O_banco_recusa_uma_segunda_marcacao_sem_anterior()
    {
        var (empresa, obra, origem) = await RegistroComDuasMarcacoes();
        var proxima = Marcar(origem, Exemplos.Em(23, 13, 0));

        var erro = await Recusada(empresa, obra, proxima, valores =>
        {
            valores[25] = null;
            valores[24] = Sha256(proxima.Campos);
        });

        Assert.Equal((PostgresErrorCodes.UniqueViolation, "ux_operacoes_primeira_marcacao"), erro);
    }

    [Fact]
    public async Task O_banco_recusa_amarrar_a_marcacao_a_um_hash_que_nao_esta_no_registro()
    {
        var (empresa, obra, origem) = await RegistroComDuasMarcacoes();
        var proxima = Marcar(origem, Exemplos.Em(23, 13, 0));
        var inventado = Sha256("nenhuma marcação");

        var erro = await Recusada(empresa, obra, proxima, valores =>
        {
            valores[25] = inventado;
            valores[24] = Sha256(proxima.Campos + inventado);
        });

        Assert.Equal((PostgresErrorCodes.ForeignKeyViolation, "fk_operacoes_hash_anterior"), erro);
    }

    [Fact]
    public async Task O_banco_recusa_hora_com_segundos()
    {
        var (empresa, obra, origem) = await RegistroComDuasMarcacoes();
        var proxima = Marcar(origem, Exemplos.Em(23, 13, 0));

        var erro = await Recusada(empresa, obra, proxima, valores => valores[18] = ((DateTimeOffset)valores[18]!).AddSeconds(31));

        Assert.Equal((PostgresErrorCodes.CheckViolation, "ck_operacoes_ao_minuto"), erro);
    }

    [Theory]
    [InlineData("update ponto.operacoes set meio = 1 where estabelecimento_id = $1 and nsr = 3")]
    [InlineData("delete from ponto.operacoes where estabelecimento_id = $1 and nsr = 4")]
    public async Task Nem_o_dono_do_banco_altera_ou_apaga_uma_operacao(string sql)
    {
        var (_, obra, _) = await RegistroComDuasMarcacoes();

        var erro = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoDono(sql, obra));

        Assert.Equal("PF001", erro.SqlState);
        Assert.Equal("A tabela operacoes só cresce: o que foi gravado não se altera nem se apaga (Portaria 671, anexo IX, item 7).", erro.MessageText);
    }

    [Fact]
    public async Task Nem_o_dono_do_banco_esvazia_a_tabela()
    {
        await RegistroComDuasMarcacoes();

        var erro = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoDono("truncate ponto.operacoes cascade"));

        Assert.Equal("PF001", erro.SqlState);
    }

    [Theory]
    [InlineData("update ponto.operacoes set meio = 1 where nsr = 3")]
    [InlineData("delete from ponto.operacoes where nsr = 4")]
    [InlineData("truncate ponto.operacoes")]
    public async Task A_aplicacao_nem_tem_permissao_de_alterar_ou_apagar(string sql)
    {
        var (empresa, _, _) = await RegistroComDuasMarcacoes();

        var erro = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoAplicacao(empresa, sql));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, erro.SqlState);
    }

    /// <summary>
    /// O registro da obra no banco com as duas primeiras marcações do João, às
    /// 7h58 e às 12h: NSR 1 a 4. O registro em memória continua de onde o do
    /// banco parou.
    /// </summary>
    private async Task<(Guid Empresa, Guid Obra, RegistroDePonto Origem)> RegistroComDuasMarcacoes()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var origem = RegistroDePonto.Abrir(Obras.De(empregador), RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 0));
        origem.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 1));
        Marcar(origem, Exemplos.Em(23, 7, 58));
        Marcar(origem, Exemplos.Em(23, 12, 0));

        var obra = await Copia.DoRegistro(empresa, origem, Exemplos.Joao);

        return (empresa.Id, obra, origem);
    }

    private static MarcacaoDePonto Marcar(RegistroDePonto registro, DateTimeOffset em) =>
        registro.MarcarOnLine(
            IdentificacaoDoTrabalhador.PorCodigoPessoal(registro.Estabelecimento.Empregador, Exemplos.Joao, em),
            Coletor.DispositivoEletronico);

    private Task<int> InserirComoDono(Guid empresa, Guid obra, Operacao operacao, Action<object?[]> mexer)
    {
        var valores = OperacoesNoBanco.Valores(empresa, obra, operacao);
        mexer(valores);

        return banco.ComoDono(OperacoesNoBanco.Inserir, valores);
    }

    private async Task<(string SqlState, string? Restricao)> Recusada(Guid empresa, Guid obra, Operacao operacao, Action<object?[]> mexer)
    {
        var erro = await Assert.ThrowsAsync<PostgresException>(() => InserirComoDono(empresa, obra, operacao, mexer));

        return (erro.SqlState, erro.ConstraintName);
    }

    private static string Sha256(string texto) => Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(texto)));
}
