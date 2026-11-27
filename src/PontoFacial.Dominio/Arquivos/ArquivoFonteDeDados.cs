using System.Globalization;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Registro;

namespace PontoFacial.Dominio.Arquivos;

/// <summary>
/// O Arquivo Fonte de Dados (art. 81 da Portaria 671): o registro de ponto
/// de um estabelecimento num período, no leiaute 004 publicado no gov.br,
/// para o Auditor-Fiscal do Trabalho.
/// </summary>
/// <remarks>
/// <para>
/// Uma linha por registro, de tamanho fixo, em ISO 8859-1, terminada por CR
/// e LF: o cabeçalho (tipo 1), as operações do período em ordem de NSR
/// (tipos 2, 4, 5, 6 e 7), o trailer (tipo 9) com as quantidades, e a linha
/// que avisa que a assinatura está no arquivo .p7s ao lado.
/// </para>
/// <para>
/// Os registros dos tipos 1 a 5 levam o CRC-16/KERMIT deles. O tipo 7 leva o
/// hash da marcação, que é a própria linha que a marcação já escreve; o
/// arquivo não o recalcula (ADR-0003).
/// </para>
/// </remarks>
public sealed class ArquivoFonteDeDados
{
    /// <summary>A versão do leiaute do AFD, que o cabeçalho informa.</summary>
    public const string VersaoDoLeiaute = "004";

    private readonly string[] _linhas;

    private ArquivoFonteDeDados(string nome, string[] linhas)
    {
        NomeDoArquivo = nome;
        _linhas = linhas;
    }

    /// <summary>"AFD", o registro no INPI, o CNPJ ou CPF do empregador e "REP_P" (item 10.3 do leiaute).</summary>
    public string NomeDoArquivo { get; }

    /// <summary>As linhas, sem o CR e o LF do fim.</summary>
    public IReadOnlyList<string> Linhas => _linhas.AsReadOnly();

    /// <summary>
    /// O arquivo das operações gravadas no período, pela data da gravação na
    /// hora do estabelecimento.
    /// </summary>
    /// <param name="estabelecimento">O estabelecimento, com o empregador como está agora, que vai no cabeçalho.</param>
    /// <param name="programa">Este programa: o registro no INPI e o desenvolvedor.</param>
    /// <param name="operacoes">As operações do registro, em ordem de NSR; as de fora do período ficam de fora.</param>
    public static ArquivoFonteDeDados Gerar(
        Estabelecimento estabelecimento,
        ProgramaDoPonto programa,
        IEnumerable<Operacao> operacoes,
        DateOnly de,
        DateOnly ate,
        DateTimeOffset geradoEm)
    {
        ArgumentNullException.ThrowIfNull(estabelecimento);
        ArgumentNullException.ThrowIfNull(programa);
        ArgumentNullException.ThrowIfNull(operacoes);

        if (ate < de)
        {
            throw new ExcecaoDeDominio("O período do arquivo termina antes de começar.");
        }

        var doPeriodo = operacoes
            .Where(operacao => DateOnly.FromDateTime(operacao.GravadaEm.DateTime) is var dia && dia >= de && dia <= ate)
            .ToList();

        for (var i = 1; i < doPeriodo.Count; i++)
        {
            if (doPeriodo[i].Nsr <= doPeriodo[i - 1].Nsr)
            {
                throw new ArgumentException("As operações têm de vir em ordem de NSR, sem repetir.", nameof(operacoes));
            }
        }

        var empregador = estabelecimento.Empregador;
        var linhas = new List<string>(doPeriodo.Count + 3)
        {
            ComCrc(string.Concat(
                "000000000",
                "1",
                Campos.TipoDoDocumento(empregador.Documento),
                Campos.Alfa(empregador.Documento.Numero, 14),
                Campos.Numero(CnoOuCaepf(empregador), 14),
                Campos.Alfa(empregador.Nome, 150),
                Campos.Numero(programa.NumeroNoInpi, ProgramaDoPonto.DigitosDoRegistroNoInpi),
                Campos.Data(de),
                Campos.Data(ate),
                CamposDoAfd.DataHora(estabelecimento.NaHoraLocal(geradoEm)),
                VersaoDoLeiaute,
                Campos.TipoDoDocumento(programa.Desenvolvedor),
                Campos.Alfa(programa.Desenvolvedor.Numero, 14),
                Campos.Alfa(null, 30))),
        };

        linhas.AddRange(doPeriodo.Select(Linha));
        linhas.Add(Trailer(doPeriodo));
        linhas.Add(Campos.Alfa(Campos.AssinaturaEmArquivoP7s, 100));

        return new ArquivoFonteDeDados($"AFD{programa.NumeroNoInpi}{empregador.Documento.Numero}REP_P.txt", [.. linhas]);
    }

    /// <summary>O arquivo como vai para o fiscal: ISO 8859-1, cada linha com CR e LF.</summary>
    public byte[] Conteudo() => Campos.Conteudo(_linhas);

    /// <summary>O CNO tem 12 dígitos; o CAEPF, 14. O CEI, extinto, tinha 12, e o CNO o substituiu.</summary>
    internal static string? CnoOuCaepf(Empregador empregador) => empregador.CeiCaepfOuCno;

    private static string Linha(Operacao operacao) => operacao switch
    {
        InclusaoDoEmpregador inclusao => ComCrc(string.Concat(
            CamposDoAfd.Nsr(inclusao.Nsr),
            "2",
            CamposDoAfd.DataHora(inclusao.GravadaEm),
            Campos.Numero(inclusao.Responsavel.Numero, 14),
            Campos.TipoDoDocumento(inclusao.Estabelecimento.Empregador.Documento),
            Campos.Alfa(inclusao.Estabelecimento.Empregador.Documento.Numero, 14),
            Campos.Numero(CnoOuCaepf(inclusao.Estabelecimento.Empregador), 14),
            Campos.Alfa(inclusao.Estabelecimento.Empregador.Nome, 150),
            Campos.Alfa(inclusao.Estabelecimento.Local, 100))),
        AjusteDoRelogio ajuste => ComCrc(string.Concat(
            CamposDoAfd.Nsr(ajuste.Nsr),
            "4",
            CamposDoAfd.DataHora(ajuste.Antes),
            CamposDoAfd.DataHora(ajuste.Ajustado),
            Campos.Numero(ajuste.Responsavel.Numero, 11))),
        MudancaNoTrabalhador mudanca => ComCrc(string.Concat(
            CamposDoAfd.Nsr(mudanca.Nsr),
            "5",
            CamposDoAfd.DataHora(mudanca.GravadaEm),
            ((char)mudanca.Mudanca).ToString(),
            CamposDoAfd.Cpf(mudanca.Cpf),
            Campos.Alfa(mudanca.Nome, Trabalhador.TamanhoDoNome),
            Campos.Alfa(null, 4),
            Campos.Numero(mudanca.Responsavel.Numero, 11))),
        OcorrenciaDeEventoSensivel ocorrencia => string.Concat(
            CamposDoAfd.Nsr(ocorrencia.Nsr),
            "6",
            CamposDoAfd.DataHora(ocorrencia.GravadaEm),
            ((int)ocorrencia.Evento).ToString("D2", CultureInfo.InvariantCulture)),
        MarcacaoDePonto marcacao => marcacao.LinhaDoAfd,
        _ => throw new InvalidOperationException($"Operação de tipo {operacao.TipoDoRegistro} sem registro no AFD."),
    };

    private static string Trailer(List<Operacao> operacoes)
    {
        string Quantos(int tipo) => operacoes.Count(operacao => operacao.TipoDoRegistro == tipo).ToString("D9", CultureInfo.InvariantCulture);

        // O tipo 3 é a marcação do REP-C e do REP-A: no REP-P, sempre zero.
        return string.Concat("999999999", Quantos(2), Quantos(3), Quantos(4), Quantos(5), Quantos(6), Quantos(7), "9");
    }

    private static string ComCrc(string registro) => registro + Crc16.DoRegistro(registro);
}
