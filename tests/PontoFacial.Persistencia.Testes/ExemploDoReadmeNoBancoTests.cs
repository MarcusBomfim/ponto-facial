using Npgsql;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes;
using PontoFacial.Dominio.Testes.Biometria;
using PontoFacial.Persistencia.Biometria;
using PontoFacial.Persistencia.Migracoes;
using PontoFacial.Persistencia.Testes.Biometria;

namespace PontoFacial.Persistencia.Testes;

/*
 * A seção "O banco" do README, passo a passo, num PostgreSQL de verdade. Se o
 * código mudar e a página deixar de ser verdade, o build quebra.
 */
public sealed class ExemploDoReadmeNoBancoTests(BancoDeTestes banco)
{
    [Fact]
    public async Task Da_empresa_cadastrada_a_revogacao_com_o_banco_conferindo()
    {
        var cancelamento = TestContext.Current.CancellationToken;

        // O esquema, com o dono dele; a aplicação entra com o papel ponto_aplicacao.
        Assert.Empty(await Migrador.Aplicar(banco.Dono, cancelamento));
        var pontoNoBanco = new BancoDoPonto(banco.Aplicacao);

        var construtora = Empregador.Novo(Documento.De("11.222.333/0001-81"), "Construtora Horizonte Ltda.");
        var joao = Trabalhador.Novo("João da Silva", Cpf.De("529.982.247-25"), new DateOnly(2026, 3, 2), "Pedreiro");
        var rh = Cpf.De("123.456.789-09");

        // A construtora vira uma empresa no banco; a obra, o João e o registro, dela.
        var horizonte = await pontoNoBanco.Cadastrar(construtora, cancelamento);
        var obra = await horizonte.Estabelecimentos.Cadastrar(
            Estabelecimento.Novo(construtora, "Rua das Palmeiras, 100 - Santos/SP", "America/Sao_Paulo"),
            cancelamento);
        await horizonte.Trabalhadores.Gravar(joao, cancelamento);
        await horizonte.Registros.Abrir(obra, rh, Hora(6, 0), cancelamento);
        await horizonte.Registros.Gravar(obra, registro => registro.IncluirTrabalhador(joao, rh, Hora(6, 1)), cancelamento);

        // A biometria precisa da chave mestra, que não fica no banco.
        var biometria = horizonte.Biometria(ChaveMestraLocal.Sortear("mestra-2026-11"));
        var autorizacao = AutorizacaoBiometrica.Conceder(construtora, joao, TermoDeConsentimento.Novo("2026.1", Exemplos.TextoDoTermo), Hora(7, 0));
        await biometria.Autorizacoes.Conceder(autorizacao, cancelamento);
        await biometria.Cadastros.Gravar(CadastroFacial.Cadastrar(autorizacao, Rostos.CapturasDoJoao, Rostos.Limiar, Hora(7, 5)), cancelamento);

        // No banco, só a cifra: o nonce de 12 bytes e 528 de cifra para os 128 números, com a versão 1 da chave da empresa.
        Assert.Equal((12, 528, 1), await ComoEstaNoBanco(horizonte.Id));

        // Às 7h58, o rosto e a autorização lidos do banco identificam o João. A entrada é o NSR 3, com o hash de sempre.
        var cadastro = await biometria.Cadastros.Obter(joao.Cpf, cancelamento);
        var vigente = await biometria.Autorizacoes.Vigente(joao.Cpf, cancelamento);
        var conferencia = cadastro!.Conferir(RostosDeTeste.Prova(Rostos.JoaoNaPortaria, Hora(7, 58, 31)), Rostos.Limiar);
        var entrada = await horizonte.Registros.Gravar(
            obra,
            registro => registro.MarcarOnLine(IdentificacaoDoTrabalhador.PorRosto(vigente!, conferencia), Coletor.DispositivoEletronico),
            cancelamento);

        Assert.Equal(3, entrada.Nsr);
        Assert.Equal("7221a9802299bd7a0cd9b4b9813d9872d5a6a0c07311eab15ce5123e913a41bd", entrada.Hash);

        // No almoço, o João revoga. O modelo sai do banco na mesma transação; a autorização fica, encerrada, como prova.
        var eliminados = await biometria.Autorizacoes.Encerrar(joao.Cpf, autorizacao => autorizacao.Revogar(Hora(12, 30)), cancelamento);

        Assert.Equal(MotivoDoEncerramento.RevogadaPeloTrabalhador, Assert.Single(eliminados).Motivo);
        Assert.Null(await biometria.Cadastros.Obter(joao.Cpf, cancelamento));
        Assert.Equal(Hora(12, 30), Assert.Single(await biometria.Autorizacoes.DoTrabalhador(joao.Cpf, cancelamento)).EncerradaEm);

        // Nem o dono do banco altera a entrada.
        var alterar = await Assert.ThrowsAsync<PostgresException>(() => banco.ComoDono(
            "update ponto.operacoes set marcada_em = marcada_em - interval '8 minutes' where estabelecimento_id = $1 and nsr = 3",
            obra));

        Assert.Equal("A tabela operacoes só cresce: o que foi gravado não se altera nem se apaga (Portaria 671, anexo IX, item 7).", alterar.MessageText);

        // E outra empresa não vê nada da Horizonte.
        var aurora = await pontoNoBanco.Cadastrar(Empregador.Novo(BancoDeTestes.CnpjNovo(), "Construtora Aurora Ltda."), cancelamento);
        var naoVe = await Assert.ThrowsAsync<NaoEncontrado>(() => aurora.Registros.Operacoes(obra, cancelamento: cancelamento));

        Assert.Equal($"O estabelecimento {obra} não está cadastrado nesta empresa.", naoVe.Message);
    }

    private static DateTimeOffset Hora(int hora, int minuto, int segundo = 0) =>
        new(2026, 11, 23, hora, minuto, segundo, TimeSpan.FromHours(-3));

    private async Task<(int Nonce, int Cifra, int VersaoDaChave)> ComoEstaNoBanco(Guid empresa)
    {
        await using var comando = banco.Dono.CreateCommand(
            "select octet_length(nonce), octet_length(vetor_cifrado), versao_da_chave from ponto.cadastros_faciais where empregador_id = $1");
        comando.Parameters.Add(new NpgsqlParameter { Value = empresa });
        await using var leitor = await comando.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await leitor.ReadAsync(TestContext.Current.CancellationToken));

        return (leitor.GetInt32(0), leitor.GetInt32(1), leitor.GetInt32(2));
    }
}
