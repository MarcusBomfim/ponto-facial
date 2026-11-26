using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Dominio.Registro;
using PontoFacial.Dominio.Testes.Biometria;

namespace PontoFacial.Dominio.Testes;

/*
 * O exemplo do README, passo a passo. Cada número e cada frase que o README
 * mostra está conferido aqui: se o código mudar e a página deixar de ser
 * verdade, o build quebra.
 */
public sealed class ExemploDoReadmeTests
{
    [Fact]
    public void Do_consentimento_a_conferencia_do_registro()
    {
        // A obra da construtora em Santos, o João, pedreiro, e quem cuida do ponto no RH.
        var construtora = Empregador.Novo(Documento.De("11.222.333/0001-81"), "Construtora Horizonte Ltda.");
        var obra = Estabelecimento.Novo(construtora, "Rua das Palmeiras, 100 - Santos/SP", "America/Sao_Paulo");
        var joao = Trabalhador.Novo("João da Silva", Cpf.De("529.982.247-25"), new DateOnly(2026, 3, 2), "Pedreiro");
        var rh = Cpf.De("123.456.789-09");

        // O registro da obra abre com o empregador, no NSR 1; a inclusão do João é o NSR 2.
        var registro = RegistroDePonto.Abrir(obra, rh, Hora(6, 0));
        registro.IncluirTrabalhador(joao, rh, Hora(6, 1));

        // O João lê o termo, autoriza a biometria e cadastra o rosto com três capturas.
        var termo = TermoDeConsentimento.Novo("2026.1", Exemplos.TextoDoTermo);
        var autorizacao = AutorizacaoBiometrica.Conceder(construtora, joao, termo, Hora(7, 0));
        var limiar = RostosDeTeste.Limiar;
        var cadastro = CadastroFacial.Cadastrar(autorizacao, RostosDeTeste.CapturasDoJoao, limiar, Hora(7, 5));

        // Às 7h58min31s, digita o CPF no tablet da portaria e faz a prova de vida:
        // olha para a câmera, vira o rosto para onde a tela pede e volta.
        var provaNaPortaria = RostosDeTeste.Prova(RostosDeTeste.JoaoNaPortaria, Hora(7, 58, 31));
        Assert.True(provaNaPortaria.Aprovada);
        var conferencia = cadastro.Conferir(provaNaPortaria, limiar);
        Assert.True(conferencia.Conferiu);
        var entrada = registro.MarcarOnLine(IdentificacaoDoTrabalhador.PorRosto(autorizacao, conferencia), Coletor.DispositivoEletronico);
        Assert.Equal(3, entrada.Nsr);
        Assert.Equal("000000003" + "7" + "2026-11-23T07:58:00-0300" + "052998224725" + "2026-11-23T07:58:00-0300" + "04" + "0", entrada.Campos);
        Assert.Equal("7221a9802299bd7a0cd9b4b9813d9872d5a6a0c07311eab15ce5123e913a41bd", entrada.Hash);

        // No almoço, muda de ideia e revoga. O modelo do rosto tem de ser apagado; a entrada fica.
        autorizacao.Revogar(Hora(12, 30));
        var eliminar = Assert.IsType<ModeloFacialDeveSerEliminado>(Assert.Single(autorizacao.Eventos));
        Assert.Equal(MotivoDoEncerramento.RevogadaPeloTrabalhador, eliminar.Motivo);

        // Na saída, o rosto não o identifica mais. O código pessoal, sim, e a saída vale o mesmo.
        var naSaida = cadastro.Conferir(RostosDeTeste.Prova(RostosDeTeste.JoaoNaPortaria, Hora(17, 2)), limiar);
        var recusa = Assert.Throws<ExcecaoDeDominio>(() => IdentificacaoDoTrabalhador.PorRosto(autorizacao, naSaida));
        Assert.Equal(
            "Sem autorização biométrica vigente, o rosto não identifica o trabalhador. O código pessoal continua valendo.",
            recusa.Message);
        var saida = registro.MarcarOnLine(IdentificacaoDoTrabalhador.PorCodigoPessoal(construtora, joao, Hora(17, 2)), Coletor.DispositivoEletronico);
        Assert.Equal(entrada.Hash, saida.HashAnterior);
        Assert.Equal("a3f002c3cd4c91cc0a2cfa30633f03affd5102961137424dc5bada210bfea3bc", saida.Hash);

        // O comprovante da saída, com o que o art. 79 pede.
        Assert.Equal(
            [
                "Comprovante de Registro de Ponto do Trabalhador",
                "NSR: 000000004",
                "Empregador: Construtora Horizonte Ltda.",
                "CNPJ: 11.222.333/0001-81",
                "Local: Rua das Palmeiras, 100 - Santos/SP",
                "Trabalhador: João da Silva",
                "CPF: 529.982.247-25",
                "Data e horário: 23/11/2026 17:02 (UTC-03:00)",
                "Registro no INPI: BR512026001234-5",
                "Hash (SHA-256): a3f002c3cd4c91cc0a2cfa30633f03affd5102961137424dc5bada210bfea3bc",
            ],
            registro.Comprovante(saida.Nsr, "BR512026001234-5").Linhas());

        // O horário da obra, com o intervalo pré-assinalado, e o espelho do art. 84.
        IReadOnlyList<Periodo> util = [new(new(8, 0), new(12, 0)), new(new(13, 0), new(17, 0))];
        var horario = HorarioContratual.De(
            new Dictionary<DayOfWeek, IReadOnlyList<Periodo>>
            {
                [DayOfWeek.Monday] = util,
                [DayOfWeek.Tuesday] = util,
                [DayOfWeek.Wednesday] = util,
                [DayOfWeek.Thursday] = util,
                [DayOfWeek.Friday] = util,
                [DayOfWeek.Saturday] = [new(new(8, 0), new(12, 0))],
            },
            intervaloPreAssinalado: true);
        var tratamento = TratamentoDoPonto.Do(registro, joao.Cpf);
        var espelho = EspelhoDePonto.Gerar(tratamento, joao, horario, CalendarioDeFeriados.Nacionais, new DateOnly(2026, 11, 23), new DateOnly(2026, 11, 23), Hora(18, 0));

        // 7h58 e 17h02: 2 minutos de cada lado, na tolerância. O dia vale as 8 horas.
        Assert.Equal(TimeSpan.FromHours(8), espelho.Apuracao.Realizada);
        Assert.Contains(
            "seg 23/11  07:58 12:00P 13:00P 17:02                   08:00    00:00   00:00        00:00   00:00  Tolerância do art. 58",
            espelho.Linhas());

        // A cadeia confere. Trocada a entrada para 7h50, deixa de conferir — ali.
        var linhas = registro.Marcacoes.Select(marcacao => marcacao.LinhaDoAfd).ToList();
        Assert.True(CadeiaDeMarcacoes.Conferir(linhas).EstaIntegra);

        linhas[0] = linhas[0][..24] + "50" + linhas[0][26..];
        var adulterada = CadeiaDeMarcacoes.Conferir(linhas);
        Assert.Equal(1, adulterada.Linha);
        Assert.Equal(
            "A marcação do NSR 3 não confere com o hash dela: ela foi alterada, ou alguma antes dela foi alterada, incluída ou removida.",
            adulterada.Motivo);
    }

    private static DateTimeOffset Hora(int hora, int minuto, int segundo = 0) =>
        new(2026, 11, 23, hora, minuto, segundo, TimeSpan.FromHours(-3));
}
