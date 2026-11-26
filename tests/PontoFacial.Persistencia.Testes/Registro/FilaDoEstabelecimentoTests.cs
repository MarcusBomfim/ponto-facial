using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes;
using PontoFacial.Dominio.Testes.Registro;

namespace PontoFacial.Persistencia.Testes.Registro;

/*
 * A troca de turno: muita gente marcando ao mesmo tempo no mesmo
 * estabelecimento, de vários coletores. Ninguém é recusado — a Portaria não
 * deixa restringir a marcação (art. 74) —, e a numeração e a cadeia saem como
 * se tivessem marcado um de cada vez.
 */
public sealed class FilaDoEstabelecimentoTests(BancoDeTestes banco)
{
    private const int Marcacoes = 40;

    [Fact]
    public async Task Marcacoes_ao_mesmo_tempo_entram_todas_com_a_numeracao_e_a_cadeia_inteiras()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var trabalhadores = new[] { Exemplos.Joao, Exemplos.Maria };
        var origem = RegistroDePonto.Abrir(Obras.De(empregador), RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 0));

        foreach (var trabalhador in trabalhadores)
        {
            origem.IncluirTrabalhador(trabalhador, RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 1));
        }

        var obra = await Copia.DoRegistro(empresa, origem, trabalhadores);

        // Todas no mesmo minuto, para o relógio não recusar nenhuma pela ordem de chegada.
        var marcacoes = await Task.WhenAll(Enumerable.Range(0, Marcacoes).Select(i => Task.Run(
            () => empresa.Registros.Gravar(
                obra,
                registro => registro.MarcarOnLine(
                    IdentificacaoDoTrabalhador.PorCodigoPessoal(empregador, trabalhadores[i % 2], Exemplos.Em(23, 7, 58, i % 60)),
                    (Coletor)((i % 5) + 1)),
                TestContext.Current.CancellationToken),
            TestContext.Current.CancellationToken)));

        var gravadas = (await empresa.Registros.Operacoes(obra, 4, cancelamento: TestContext.Current.CancellationToken)).Cast<MarcacaoDePonto>().ToList();

        Assert.Equal(Enumerable.Range(4, Marcacoes).Select(nsr => (long)nsr), marcacoes.Select(marcacao => marcacao.Nsr).Order());
        Assert.Equal(Enumerable.Range(4, Marcacoes).Select(nsr => (long)nsr), gravadas.Select(marcacao => marcacao.Nsr));
        Assert.Null(gravadas[0].HashAnterior);

        for (var i = 1; i < gravadas.Count; i++)
        {
            Assert.Equal(gravadas[i - 1].Hash, gravadas[i].HashAnterior);
        }

        Assert.True(CadeiaDeMarcacoes.Conferir(gravadas.Select(marcacao => marcacao.LinhaDoAfd)).EstaIntegra);
    }

    [Fact]
    public async Task Dois_estabelecimentos_nao_fazem_fila_um_para_o_outro()
    {
        var (empresa, empregador) = await banco.NovaEmpresa();
        var sede = await Copia.DoRegistro(empresa, ComOJoao(empregador), Exemplos.Joao);
        var obra = await Copia.DoRegistro(empresa, ComOJoao(empregador), Exemplos.Joao);

        var marcacoes = await Task.WhenAll(new[] { sede, obra, sede, obra }.Select(estabelecimento => empresa.Registros.Gravar(
            estabelecimento,
            registro => registro.MarcarOnLine(
                IdentificacaoDoTrabalhador.PorCodigoPessoal(empregador, Exemplos.Joao, Exemplos.Em(23, 7, 58)),
                Coletor.DispositivoEletronico),
            TestContext.Current.CancellationToken)));

        // Cada estabelecimento tem a sua numeração: o NSR 3 e o 4 em cada um.
        Assert.Equal([3L, 3L, 4L, 4L], marcacoes.Select(marcacao => marcacao.Nsr).Order());
    }

    private static RegistroDePonto ComOJoao(Empregador empregador)
    {
        var registro = RegistroDePonto.Abrir(Obras.De(empregador), RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 0));
        registro.IncluirTrabalhador(Exemplos.Joao, RegistroDeExemplo.Rh, Exemplos.Em(23, 6, 1));

        return registro;
    }
}
