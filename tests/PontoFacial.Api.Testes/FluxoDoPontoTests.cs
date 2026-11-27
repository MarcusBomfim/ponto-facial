using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using PontoFacial.Assinaturas;
using PontoFacial.Dominio.Testes;

namespace PontoFacial.Api.Testes;

/*
 * O dia do João pela API: o RH monta a obra, o João marca a entrada e a
 * saída e recebe os comprovantes em PDF assinados, o espelho mostra o dia, e
 * o AFD e o AEJ saem com o .p7s.
 */
public sealed partial class FluxoDoPontoTests(ApiDeTestes api) : IClassFixture<ApiDeTestes>
{
    [Fact]
    public async Task A_marcacao_pela_api_leva_a_hora_do_servidor_e_o_nsr_seguinte()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);

        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 58, 31));
        var resposta = await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "DispositivoEletronico" }, TestContext.Current.CancellationToken);
        var entrada = await resposta.Ler<JsonElement>();

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.Equal(4, entrada.GetProperty("nsr").GetInt64());
        Assert.Equal("2026-11-23T07:58-03:00", entrada.GetProperty("marcadaEm").GetString());
        Assert.Equal($"/api/estabelecimentos/{cenario.Obra}/marcacoes/4/comprovante", resposta.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task O_comprovante_e_um_pdf_assinado_que_confere()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 58, 31));
        var entrada = await (await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "AplicativoMovel" }, TestContext.Current.CancellationToken)).Ler<JsonElement>();

        var resposta = await cenario.Joao.GetAsync(entrada.GetProperty("comprovante").GetString(), TestContext.Current.CancellationToken);
        var pdf = await resposta.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        var texto = Encoding.Latin1.GetString(pdf);

        Assert.Equal("application/pdf", resposta.Content.Headers.ContentType?.MediaType);
        Assert.Contains("(NSR: 000000004) Tj", texto, StringComparison.Ordinal);
        Assert.Contains($"(Hash \\(SHA-256\\): {entrada.GetProperty("hash").GetString()}) Tj", texto, StringComparison.Ordinal);
        Assert.Equal(api.Certificado.Thumbprint, ConferirPdf(pdf).Thumbprint);
    }

    [Fact]
    public async Task O_espelho_do_dia_mostra_a_entrada_a_saida_e_o_intervalo_pre_assinalado()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 58, 31));
        Cenario.Esperar(HttpStatusCode.Created, await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "DispositivoEletronico" }, TestContext.Current.CancellationToken));
        RelogioDeTeste.Acertar(Exemplos.Em(23, 17, 2));
        Cenario.Esperar(HttpStatusCode.Created, await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "DispositivoEletronico" }, TestContext.Current.CancellationToken));

        RelogioDeTeste.Acertar(Exemplos.Em(23, 18, 0));
        var espelho = await cenario.Joao.GetStringAsync($"/api/estabelecimentos/{cenario.Obra}/trabalhadores/52998224725/espelho?de=2026-11-23&ate=2026-11-23", TestContext.Current.CancellationToken);

        Assert.StartsWith("Espelho de Ponto Eletrônico\n", espelho, StringComparison.Ordinal);
        Assert.Contains("\nseg 23/11  07:58 12:00P 13:00P 17:02 ", espelho, StringComparison.Ordinal);
        Assert.Contains("Tolerância do art. 58", espelho, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_trabalhador_extrai_os_comprovantes_das_ultimas_48_horas()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 58));
        Cenario.Esperar(HttpStatusCode.Created, await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "DispositivoEletronico" }, TestContext.Current.CancellationToken));
        RelogioDeTeste.Acertar(Exemplos.Em(25, 7, 59));
        Cenario.Esperar(HttpStatusCode.Created, await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "DispositivoEletronico" }, TestContext.Current.CancellationToken));

        // Em 25/11 às 8h, a de 23/11 às 7h58 já passou das 48 horas; a da Maria não é do João.
        Cenario.Esperar(HttpStatusCode.Created, await cenario.Maria.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "DispositivoEletronico" }, TestContext.Current.CancellationToken));
        RelogioDeTeste.Acertar(Exemplos.Em(25, 8, 0));
        var comprovantes = await (await cenario.Joao.GetAsync($"/api/estabelecimentos/{cenario.Obra}/comprovantes", TestContext.Current.CancellationToken)).Ler<JsonElement>();

        Assert.Equal([5L], comprovantes.EnumerateArray().Select(comprovante => comprovante.GetProperty("nsr").GetInt64()));
    }

    [Fact]
    public async Task O_afd_sai_num_zip_com_o_p7s_que_assina_exatamente_o_txt()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 58));
        Cenario.Esperar(HttpStatusCode.Created, await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "DispositivoEletronico" }, TestContext.Current.CancellationToken));
        RelogioDeTeste.Acertar(Exemplos.Em(23, 18, 0));

        var resposta = await cenario.RhCliente.GetAsync($"/api/estabelecimentos/{cenario.Obra}/afd?de=2026-11-23&ate=2026-11-23", TestContext.Current.CancellationToken);
        var (nomes, txt, p7s) = Abrir(await resposta.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var linhas = Encoding.Latin1.GetString(txt).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("application/zip", resposta.Content.Headers.ContentType?.MediaType);
        Assert.Matches(@"^AFD5120260012345\d{14}REP_P\.txt$", nomes[0]);
        Assert.Equal(Path.ChangeExtension(nomes[0], ".p7s"), nomes[1]);
        Assert.Equal(api.Certificado.Thumbprint, AssinaturaCades.Conferir(txt, p7s).Thumbprint);
        Assert.Equal(["000000000", "000000001", "000000002", "000000003", "000000004", "999999999", "ASSINATUR"], linhas.Select(linha => linha[..9]));
    }

    [Fact]
    public async Task O_aej_sai_num_zip_com_o_p7s_e_a_marcacao_do_dia()
    {
        var cenario = await Cenario.Montar(api, RelogioDeTeste.Acertar);
        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 58));
        Cenario.Esperar(HttpStatusCode.Created, await cenario.Joao.PostAsJsonAsync($"/api/estabelecimentos/{cenario.Obra}/marcacoes", new { coletor = "DispositivoEletronico" }, TestContext.Current.CancellationToken));
        RelogioDeTeste.Acertar(Exemplos.Em(24, 9, 0));

        var (nomes, txt, p7s) = Abrir(await cenario.RhCliente.GetByteArrayAsync($"/api/estabelecimentos/{cenario.Obra}/aej?de=2026-11-23&ate=2026-11-23", TestContext.Current.CancellationToken));
        var linhas = Encoding.Latin1.GetString(txt).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Matches(@"^AEJ_\d{14}_20261123_20261123\.txt$", nomes[0]);
        AssinaturaCades.Conferir(txt, p7s);
        Assert.Contains("05|1|2026-11-23T07:58:00-0300|1|E|001|O|1|", linhas);
        Assert.Equal(["03|1|52998224725|João da Silva", "03|2|11144477735|Maria Souza"], linhas.Where(linha => linha.StartsWith("03|", StringComparison.Ordinal)));
    }

    private static (string[] Nomes, byte[] Txt, byte[] P7s) Abrir(byte[] zip)
    {
        using var arquivo = new ZipArchive(new MemoryStream(zip), ZipArchiveMode.Read);
        var nomes = arquivo.Entries.Select(entrada => entrada.FullName).ToArray();

        byte[] Ler(int i)
        {
            using var fluxo = arquivo.Entries[i].Open();
            using var memoria = new MemoryStream();
            fluxo.CopyTo(memoria);

            return memoria.ToArray();
        }

        return (nomes, Ler(0), Ler(1));
    }

    /// <summary>Confere a assinatura do PDF como um leitor de PDF: os bytes do /ByteRange contra o CMS do /Contents.</summary>
    private static System.Security.Cryptography.X509Certificates.X509Certificate2 ConferirPdf(byte[] pdf)
    {
        var texto = Encoding.Latin1.GetString(pdf);
        var faixa = FaixaDaAssinatura().Match(texto).Groups.Values.Skip(1).Select(grupo => int.Parse(grupo.Value, CultureInfo.InvariantCulture)).ToArray();
        var assinado = pdf[faixa[0]..faixa[1]].Concat(pdf[faixa[2]..(faixa[2] + faixa[3])]).ToArray();
        var der = Convert.FromHexString(texto[(faixa[1] + 1)..(faixa[2] - 1)]);
        var cms = new SignedCms(new ContentInfo(assinado), detached: true);
        cms.Decode(new System.Formats.Asn1.AsnReader(der, System.Formats.Asn1.AsnEncodingRules.BER).ReadEncodedValue().Span);
        cms.CheckSignature(verifySignatureOnly: true);

        return cms.SignerInfos[0].Certificate!;
    }

    [GeneratedRegex(@"/ByteRange \[(\d+) (\d+) (\d+) (\d+)\]")]
    private static partial Regex FaixaDaAssinatura();
}
