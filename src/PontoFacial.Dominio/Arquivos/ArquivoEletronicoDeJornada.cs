using System.Globalization;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Jornada;
using PontoFacial.Dominio.Registro;

namespace PontoFacial.Dominio.Arquivos;

/// <summary>
/// A jornada de um trabalhador para o AEJ: quem ele é, o tratamento do
/// ponto dele, o horário do contrato e os feriados do estabelecimento.
/// </summary>
/// <param name="Desde">O primeiro dia do vínculo dentro do período, se ele foi incluído no meio; antes dele, não há falta.</param>
/// <param name="Ate">O último dia do vínculo dentro do período, se ele foi excluído no meio; depois dele, também não.</param>
public sealed record JornadaDoVinculo(
    Trabalhador Trabalhador,
    TratamentoDoPonto Tratamento,
    HorarioContratual Horario,
    CalendarioDeFeriados Calendario,
    DateOnly? Desde = null,
    DateOnly? Ate = null);

/// <summary>
/// O Arquivo Eletrônico de Jornada (art. 83, I, da Portaria 671): o que o
/// programa de tratamento fez com as marcações de cada trabalhador num
/// período, no leiaute 002 publicado no gov.br.
/// </summary>
/// <remarks>
/// <para>
/// Uma linha por registro, com os campos separados por "|", em ISO 8859-1,
/// terminada por CR e LF. Os registros vêm na ordem do leiaute: o cabeçalho
/// (01), o REP (02), os vínculos (03), os horários contratuais (04), as
/// marcações (05), as ausências (07), a identificação deste programa (08), o
/// trailer (99) e a linha da assinatura, que fica no arquivo .p7s ao lado.
/// </para>
/// <para>
/// As marcações são as do espelho de ponto, com outras letras. A do registro
/// é "O", a incluída no tratamento é "I", e a pré-assinalada pelo horário é
/// "P". As que contam se alternam em entrada e saída, em pares numerados a
/// cada dia de trabalho. A desconsiderada é "D", com o motivo, e não entra
/// na contagem dos pares (ADR-0015).
/// </para>
/// </remarks>
public sealed class ArquivoEletronicoDeJornada
{
    /// <summary>A versão do leiaute do AEJ, que o cabeçalho informa.</summary>
    public const string VersaoDoLeiaute = "002";

    /// <summary>O único REP do arquivo: este programa, o REP-P.</summary>
    private const string IdDoRep = "1";

    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    /// <summary>Os tipos de registro que o trailer conta, na ordem dos campos dele.</summary>
    private static readonly string[] TiposContados = ["01", "02", "03", "04", "05", "06", "07", "08"];

    private readonly string[] _linhas;

    private ArquivoEletronicoDeJornada(string nome, string[] linhas)
    {
        NomeDoArquivo = nome;
        _linhas = linhas;
    }

    /// <summary>"AEJ", o CNPJ ou CPF do empregador e o período: <c>AEJ_11222333000181_20261116_20261122.txt</c>.</summary>
    public string NomeDoArquivo { get; }

    /// <summary>As linhas, sem o CR e o LF do fim.</summary>
    public IReadOnlyList<string> Linhas => _linhas.AsReadOnly();

    /// <param name="empregador">O empregador, como está cadastrado agora.</param>
    /// <param name="programa">Este programa: o REP-P e o programa de tratamento.</param>
    /// <param name="vinculos">Os trabalhadores do arquivo, cada um com a jornada dele, na ordem em que vão no arquivo.</param>
    public static ArquivoEletronicoDeJornada Gerar(
        Empregador empregador,
        ProgramaDoPonto programa,
        IReadOnlyList<JornadaDoVinculo> vinculos,
        DateOnly de,
        DateOnly ate,
        DateTimeOffset geradoEm)
    {
        ArgumentNullException.ThrowIfNull(empregador);
        ArgumentNullException.ThrowIfNull(programa);
        ArgumentNullException.ThrowIfNull(vinculos);

        if (ate < de)
        {
            throw new ExcecaoDeDominio("O período do arquivo termina antes de começar.");
        }

        foreach (var vinculo in vinculos)
        {
            if (vinculo.Tratamento.Estabelecimento.Empregador.Documento != empregador.Documento)
            {
                throw new ExcecaoDeDominio($"O ponto de {vinculo.Trabalhador.Nome} é de outro empregador.");
            }
        }

        var cno = ArquivoFonteDeDados.CnoOuCaepf(empregador);
        var horarios = new Horarios();
        var marcacoes = new List<string>();
        var ausencias = new List<string>();

        for (var i = 0; i < vinculos.Count; i++)
        {
            Apurar(vinculos[i], (i + 1).ToString(Invariante), de, ate, geradoEm, horarios, marcacoes, ausencias);
        }

        var linhas = new List<string>
        {
            Registro(
                "01",
                Campos.TipoDoDocumento(empregador.Documento),
                empregador.Documento.Numero,
                cno?.Length == 14 ? cno : string.Empty,
                cno?.Length == 12 ? cno : string.Empty,
                empregador.Nome,
                Campos.Data(de),
                Campos.Data(ate),
                CamposDoAfd.DataHora(geradoEm),
                VersaoDoLeiaute),
            Registro("02", IdDoRep, "3", Campos.Numero(programa.NumeroNoInpi, ProgramaDoPonto.DigitosDoRegistroNoInpi)),
        };

        linhas.AddRange(vinculos.Select((vinculo, i) => Registro("03", (i + 1).ToString(Invariante), vinculo.Trabalhador.Cpf.Numero, vinculo.Trabalhador.Nome)));
        linhas.AddRange(horarios.Linhas());
        linhas.AddRange(marcacoes);
        linhas.AddRange(ausencias);
        linhas.Add(Registro(
            "08",
            programa.Nome,
            programa.Versao,
            Campos.TipoDoDocumento(programa.Desenvolvedor),
            programa.Desenvolvedor.Numero,
            programa.RazaoSocialDoDesenvolvedor,
            programa.EmailDoDesenvolvedor));

        int Quantos(string tipo) => linhas.Count(linha => linha.StartsWith(tipo + "|", StringComparison.Ordinal));

        linhas.Add(Registro(["99", .. TiposContados.Select(tipo => Quantos(tipo).ToString(Invariante))]));
        linhas.Add(Campos.Alfa(Campos.AssinaturaEmArquivoP7s, 100));

        return new ArquivoEletronicoDeJornada(
            string.Create(Invariante, $"AEJ_{empregador.Documento.Numero}_{de:yyyyMMdd}_{ate:yyyyMMdd}.txt"),
            [.. linhas]);
    }

    /// <summary>O arquivo como vai para o fiscal: ISO 8859-1, cada linha com CR e LF.</summary>
    public byte[] Conteudo() => Campos.Conteudo(_linhas);

    /// <summary>As marcações e as ausências do trabalhador no período, e os horários que elas usam.</summary>
    private static void Apurar(
        JornadaDoVinculo vinculo,
        string id,
        DateOnly de,
        DateOnly ate,
        DateTimeOffset geradoEm,
        Horarios horarios,
        List<string> marcacoes,
        List<string> ausencias)
    {
        var inicio = vinculo.Desde is { } desde && desde > de ? desde : de;
        var fim = vinculo.Ate is { } termino && termino < ate ? termino : ate;

        if (fim < inicio)
        {
            return;
        }

        var tratamento = vinculo.Tratamento;
        var fuso = tratamento.Estabelecimento.Fuso;
        var apuracao = EspelhoDePonto.Gerar(tratamento, vinculo.Trabalhador, vinculo.Horario, vinculo.Calendario, inicio, fim, geradoEm).Apuracao;
        var motivos = tratamento.Marcacoes
            .Where(tratada => tratada.Tipo == TipoDeTratamento.Desconsiderada)
            .ToDictionary(tratada => tratada.Nsr!.Value, tratada => tratada.Motivo);

        // Cada marcação do trabalhador com a fonte, a letra e o motivo, para pôr no dia dela.
        var todas = tratamento.Registradas()
            .Select(marcacao => new Marcacao(marcacao.MarcadaEm, "O", motivos.GetValueOrDefault(marcacao.Nsr), motivos.ContainsKey(marcacao.Nsr)))
            .Concat(tratamento.Marcacoes
                .Where(tratada => tratada.Tipo == TipoDeTratamento.Incluida)
                .Select(tratada => new Marcacao(tratada.MarcadaEm, "I", tratada.Motivo, Desconsiderada: false)))
            .GroupBy(marcacao => vinculo.Horario.DiaDeTrabalho(TimeZoneInfo.ConvertTime(marcacao.Em, fuso).DateTime))
            .ToDictionary(grupo => grupo.Key, grupo => grupo.ToList());

        foreach (var dia in apuracao.Dias)
        {
            var doDia = todas.GetValueOrDefault(dia.Data, [])
                .Concat(dia.PreAssinaladas.Select(preAssinalada => new Marcacao(preAssinalada, "P", null, Desconsiderada: false)))
                .OrderBy(marcacao => marcacao.Em)
                .ThenBy(marcacao => marcacao.Desconsiderada)
                .ToList();
            var periodos = vinculo.Horario.Do(dia.Data.DayOfWeek);
            var codigo = periodos.Count > 0 ? horarios.Codigo(periodos, vinculo.Horario, dia.Data, fuso) : string.Empty;
            var contadas = 0;

            foreach (var marcacao in doDia)
            {
                var entrada = contadas % 2 == 0;
                var par = ((marcacao.Desconsiderada ? Math.Max(contadas - 1, 0) : contadas) / 2) + 1;

                marcacoes.Add(Registro(
                    "05",
                    id,
                    CamposDoAfd.DataHora(TimeZoneInfo.ConvertTime(marcacao.Em, fuso)),
                    marcacao.Fonte == "O" ? IdDoRep : string.Empty,
                    marcacao.Desconsiderada ? "D" : entrada ? "E" : "S",
                    par.ToString("D3", Invariante),
                    marcacao.Fonte,
                    !marcacao.Desconsiderada && entrada && contadas == 0 ? codigo : string.Empty,
                    marcacao.Desconsiderada || marcacao.Fonte == "I" ? marcacao.Motivo : string.Empty));

                if (!marcacao.Desconsiderada)
                {
                    contadas++;
                }
            }

            var ausencia = dia.Situacao switch
            {
                SituacaoDoDia.DescansoSemanal => "1",
                SituacaoDoDia.Falta => "2",
                _ => null,
            };

            if (ausencia is not null)
            {
                ausencias.Add(Registro("07", id, ausencia, Campos.Data(dia.Data), string.Empty, string.Empty));
            }
        }
    }

    /// <summary>Os campos separados por "|", cada um sem quebra de linha e sem "|" dentro.</summary>
    private static string Registro(params string?[] campos) => string.Join('|', campos.Select(campo => Campos.Seguro(campo ?? string.Empty)));

    private sealed record Marcacao(DateTimeOffset Em, string Fonte, string? Motivo, bool Desconsiderada);

    /// <summary>
    /// Os horários contratuais do arquivo, um para cada sequência diferente
    /// de entradas e saídas num dia, numerados na ordem em que aparecem.
    /// </summary>
    private sealed class Horarios
    {
        private readonly Dictionary<string, (string Codigo, string Linha)> _porSequencia = [];

        public string Codigo(IReadOnlyList<Periodo> periodos, HorarioContratual horario, DateOnly dia, TimeZoneInfo fuso)
        {
            var trechos = horario.TrechosEm(dia, fuso);
            var duracao = TrabalhoNoturno.Computar(trechos, TrabalhoNoturno.Da(trechos, fuso));
            var pares = periodos.SelectMany(periodo => new[] { Hora(periodo.Entrada), Hora(periodo.Saida) }).ToList();
            var sequencia = string.Join('-', pares) + "/" + ((long)duracao.TotalMinutes).ToString(Invariante);

            if (!_porSequencia.TryGetValue(sequencia, out var horarioNoArquivo))
            {
                var codigo = (_porSequencia.Count + 1).ToString(Invariante);
                horarioNoArquivo = (codigo, Registro([.. new[] { "04", codigo, ((long)duracao.TotalMinutes).ToString(Invariante) }.Concat(pares)]));
                _porSequencia.Add(sequencia, horarioNoArquivo);
            }

            return horarioNoArquivo.Codigo;
        }

        public IEnumerable<string> Linhas() => _porSequencia.Values.Select(horario => horario.Linha);

        private static string Hora(TimeOnly hora) => hora.ToString("HHmm", Invariante);
    }
}
