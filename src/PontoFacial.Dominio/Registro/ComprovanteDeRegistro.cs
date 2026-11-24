using System.Globalization;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Comum;
using PontoFacial.Dominio.Documentos;

namespace PontoFacial.Dominio.Registro;

/// <summary>
/// O Comprovante de Registro de Ponto do Trabalhador, com o que o art. 79 da
/// Portaria 671 pede, na ordem dos incisos.
/// </summary>
/// <remarks>
/// O trabalhador recebe um a cada marcação (art. 80, II). Como o comprovante
/// traz o hash da marcação, cada trabalhador guarda uma âncora da cadeia: quem
/// reescrevesse o registro inteiro, refazendo todos os hashes, ainda teria de
/// explicar os comprovantes que não conferem (ADR-0003). O PDF assinado, que
/// é a forma eletrônica do comprovante (art. 80, parágrafo único, I), é feito
/// fora do domínio a partir destes campos.
/// </remarks>
public sealed class ComprovanteDeRegistro
{
    /// <summary>O cabeçalho que o art. 79, I, manda usar.</summary>
    public const string Titulo = "Comprovante de Registro de Ponto do Trabalhador";

    public const int TamanhoDoRegistroNoInpi = 30;

    internal ComprovanteDeRegistro(Estabelecimento estabelecimento, string nomeDoTrabalhador, MarcacaoDePonto marcacao, string registroNoInpi)
    {
        Nsr = marcacao.Nsr;
        NomeDoEmpregador = estabelecimento.Empregador.Nome;
        DocumentoDoEmpregador = estabelecimento.Empregador.Documento;
        CeiCaepfOuCno = estabelecimento.Empregador.CeiCaepfOuCno;
        Local = estabelecimento.Local;
        NomeDoTrabalhador = nomeDoTrabalhador;
        CpfDoTrabalhador = marcacao.Cpf;
        MarcadaEm = marcacao.MarcadaEm;
        RegistroNoInpi = Textos.Obrigatorio(registroNoInpi, "o número de registro do programa no INPI", TamanhoDoRegistroNoInpi);
        Hash = marcacao.Hash;
    }

    public long Nsr { get; }

    public string NomeDoEmpregador { get; }

    public Documento DocumentoDoEmpregador { get; }

    public string? CeiCaepfOuCno { get; }

    public string Local { get; }

    public string NomeDoTrabalhador { get; }

    public Cpf CpfDoTrabalhador { get; }

    /// <summary>A data e a hora da marcação, na hora local do estabelecimento.</summary>
    public DateTimeOffset MarcadaEm { get; }

    public string RegistroNoInpi { get; }

    public string Hash { get; }

    /// <summary>O comprovante em texto, uma informação por linha, na ordem do art. 79.</summary>
    public IReadOnlyList<string> Linhas()
    {
        var documento = DocumentoDoEmpregador is Cnpj ? "CNPJ" : "CPF";
        var fuso = MarcadaEm.ToString("zzz", CultureInfo.InvariantCulture);

        List<string> linhas =
        [
            Titulo,
            string.Create(CultureInfo.InvariantCulture, $"NSR: {Nsr:D9}"),
            $"Empregador: {NomeDoEmpregador}",
            $"{documento}: {DocumentoDoEmpregador.Formatado}",
        ];

        if (CeiCaepfOuCno is not null)
        {
            linhas.Add($"CEI/CAEPF/CNO: {CeiCaepfOuCno}");
        }

        linhas.AddRange(
        [
            $"Local: {Local}",
            $"Trabalhador: {NomeDoTrabalhador}",
            $"CPF: {CpfDoTrabalhador.Formatado}",
            string.Create(CultureInfo.InvariantCulture, $"Data e horário: {MarcadaEm:dd/MM/yyyy HH:mm} (UTC{fuso})"),
            $"Registro no INPI: {RegistroNoInpi}",
            $"Hash (SHA-256): {Hash}",
        ]);

        return linhas.AsReadOnly();
    }
}
