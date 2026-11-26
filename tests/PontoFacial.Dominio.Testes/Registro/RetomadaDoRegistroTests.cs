using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Dominio.Registro;

namespace PontoFacial.Dominio.Testes.Registro;

/*
 * O registro retomado pela ponta continua exatamente como o que nunca saiu
 * da memória: o mesmo NSR, o mesmo relógio, a mesma cadeia, as mesmas
 * recusas. É o que deixa o banco guardar as operações e devolver só a ponta
 * (ADR-0013).
 */
public sealed class RetomadaDoRegistroTests
{
    private static readonly Trabalhador MariaCasada = Trabalhador.Novo("Maria Souza Lima", Exemplos.Maria.Cpf, Exemplos.Maria.Admissao, Exemplos.Maria.Cargo);

    [Fact]
    public void O_registro_retomado_pela_ponta_continua_igual_ao_que_nao_saiu_da_memoria()
    {
        var original = Manha();
        var retomado = RegistroDePonto.Retomar(Exemplos.Obra, original.Ponta());

        var continuacao = new Func<RegistroDePonto, Operacao>[]
        {
            registro => registro.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 12, 0)), Coletor.DispositivoEletronico),
            registro => registro.MarcarOffLine(RegistroDeExemplo.PeloCodigo(Exemplos.Maria, Exemplos.Em(23, 11, 50)), Coletor.AplicativoMovel, Exemplos.Em(23, 12, 5)),
            registro => registro.AjustarRelogio(Exemplos.Em(23, 12, 30), Exemplos.Em(23, 12, 20), RegistroDeExemplo.Rh),
            registro => registro.AlterarTrabalhador(MariaCasada, RegistroDeExemplo.Rh, Exemplos.Em(23, 12, 21)),
            registro => registro.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 13, 0)), Coletor.Navegador),
            registro => registro.ExcluirTrabalhador(Exemplos.Joao.Cpf, RegistroDeExemplo.Rh, Exemplos.Em(23, 17, 30)),
        };

        foreach (var operacao in continuacao)
        {
            var doOriginal = operacao(original);
            var doRetomado = operacao(retomado);

            Assert.Equal(doOriginal.Nsr, doRetomado.Nsr);
            Assert.Equal(doOriginal.GravadaEm, doRetomado.GravadaEm);

            if (doOriginal is MarcacaoDePonto marcacao)
            {
                Assert.Equal(marcacao.LinhaDoAfd, ((MarcacaoDePonto)doRetomado).LinhaDoAfd);
                Assert.Equal(marcacao.HashAnterior, ((MarcacaoDePonto)doRetomado).HashAnterior);
            }
        }

        Assert.Equal(Descrever(original.Ponta()), Descrever(retomado.Ponta()));
        Assert.Equal(original.Operacoes.Skip(5).Select(operacao => operacao.Nsr), retomado.Operacoes.Select(operacao => operacao.Nsr));
    }

    [Fact]
    public void O_retomado_recusa_o_que_o_original_recusaria()
    {
        var retomado = RegistroDePonto.Retomar(Exemplos.Obra, Manha().Ponta());

        // O relógio já marca 8h05, da marcação off-line da Maria.
        Assert.StartsWith(
            "O relógio do registro já marca 23/11/2026 08:05",
            Assert.Throws<ExcecaoDeDominio>(() => retomado.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 8, 0)), Coletor.DispositivoEletronico)).Message,
            StringComparison.Ordinal);
        Assert.Equal(
            "O trabalhador já está incluído neste registro de ponto.",
            Assert.Throws<ExcecaoDeDominio>(() => retomado.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0))).Message);
        Assert.Equal(
            "A marcação é anterior à inclusão do trabalhador neste registro de ponto.",
            Assert.Throws<ExcecaoDeDominio>(() => retomado.MarcarOffLine(RegistroDeExemplo.PeloCodigo(Exemplos.Maria, Exemplos.Em(23, 5, 0)), Coletor.AplicativoMovel, Exemplos.Em(23, 9, 0))).Message);

        retomado.ExcluirTrabalhador(Exemplos.Joao.Cpf, RegistroDeExemplo.Rh, Exemplos.Em(23, 9, 0));
        Assert.Equal(
            "O trabalhador não está incluído neste registro de ponto.",
            Assert.Throws<ExcecaoDeDominio>(() => retomado.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 9, 1)), Coletor.DispositivoEletronico)).Message);
    }

    [Fact]
    public void O_comprovante_do_retomado_traz_o_nome_de_quando_o_trabalhador_marcou()
    {
        var retomado = RegistroDePonto.Retomar(Exemplos.Obra, Manha().Ponta());

        var antes = retomado.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Maria, Exemplos.Em(23, 12, 0)), Coletor.DispositivoEletronico);
        retomado.AlterarTrabalhador(MariaCasada, RegistroDeExemplo.Rh, Exemplos.Em(23, 12, 1));
        var depois = retomado.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Maria, Exemplos.Em(23, 13, 0)), Coletor.DispositivoEletronico);

        Assert.Equal("Maria Souza", retomado.Comprovante(antes.Nsr, RegistroDeExemplo.RegistroNoInpi).NomeDoTrabalhador);
        Assert.Equal("Maria Souza Lima", retomado.Comprovante(depois.Nsr, RegistroDeExemplo.RegistroNoInpi).NomeDoTrabalhador);

        // O que foi gravado antes da retomada está no banco, e não aqui.
        Assert.Equal(
            "O NSR 4 não é de uma marcação deste registro de ponto.",
            Assert.Throws<ExcecaoDeDominio>(() => retomado.Comprovante(4, RegistroDeExemplo.RegistroNoInpi)).Message);
    }

    [Fact]
    public void O_retomado_so_tem_em_memoria_o_que_fez_e_nao_serve_ao_tratamento()
    {
        var original = Manha();
        var retomado = RegistroDePonto.Retomar(Exemplos.Obra, original.Ponta());

        Assert.True(original.Inteiro);
        Assert.False(retomado.Inteiro);
        Assert.Empty(retomado.Operacoes);

        var erro = Assert.Throws<InvalidOperationException>(() => TratamentoDoPonto.Do(retomado, Exemplos.Joao.Cpf));
        Assert.StartsWith("Este registro foi retomado do banco pela ponta", erro.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_ponta_que_nao_pode_ser_de_um_registro_e_recusada()
    {
        var ponta = Manha().Ponta();

        Assert.Throws<ArgumentOutOfRangeException>(() => RegistroDePonto.Retomar(Exemplos.Obra, ponta with { UltimoNsr = 0 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => RegistroDePonto.Retomar(Exemplos.Obra, ponta with { UltimoNsr = 1_000_000_000 }));
        Assert.Throws<ArgumentException>(() => RegistroDePonto.Retomar(Exemplos.Obra, ponta with { UltimoHash = ponta.UltimoHash!.ToUpperInvariant() }));
        Assert.Throws<ArgumentException>(() => RegistroDePonto.Retomar(Exemplos.Obra, ponta with { UltimoHash = "abc" }));
        Assert.Throws<ArgumentException>(() => RegistroDePonto.Retomar(Exemplos.Obra, ponta with { Incluidos = [.. ponta.Incluidos, ponta.Incluidos[0]] }));
    }

    [Fact]
    public void O_registro_sem_marcacao_retoma_sem_hash_e_a_primeira_marcacao_nao_tem_anterior()
    {
        var registro = RegistroDeExemplo.ComJoaoEMaria();
        var retomado = RegistroDePonto.Retomar(Exemplos.Obra, registro.Ponta());

        var primeira = retomado.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 7, 58)), Coletor.DispositivoEletronico);

        Assert.Null(registro.Ponta().UltimoHash);
        Assert.Equal(4, primeira.Nsr);
        Assert.Null(primeira.HashAnterior);
    }

    /// <summary>A abertura e as inclusões (NSR 1 a 3), a entrada do João e a da Maria, off-line: NSR 4 e 5.</summary>
    private static RegistroDePonto Manha()
    {
        var registro = RegistroDeExemplo.ComJoaoEMaria();
        registro.MarcarOnLine(RegistroDeExemplo.PeloCodigo(Exemplos.Joao, Exemplos.Em(23, 7, 58)), Coletor.DispositivoEletronico);
        registro.MarcarOffLine(RegistroDeExemplo.PeloCodigo(Exemplos.Maria, Exemplos.Em(23, 7, 40)), Coletor.AplicativoMovel, Exemplos.Em(23, 8, 5));

        return registro;
    }

    private static string Descrever(PontaDoRegistro ponta) => FormattableString.Invariant(
        $"{ponta.UltimoNsr} {ponta.Relogio:O} {ponta.UltimoHash} {string.Join(", ", ponta.Incluidos.OrderBy(vinculo => vinculo.Cpf.Numero, StringComparer.Ordinal).Select(vinculo => $"{vinculo.Cpf.Numero} {vinculo.Nome} {vinculo.IncluidoEm:O}"))}");
}
