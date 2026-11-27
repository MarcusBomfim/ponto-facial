using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography.Pkcs;
using System.Text;
using System.Text.Json;
using PontoFacial.Assinaturas;
using PontoFacial.Dominio.Cadastro;
using PontoFacial.Dominio.Documentos;
using PontoFacial.Dominio.Testes;

namespace PontoFacial.Api.Testes;

/*
 * A seção "A API e os arquivos da Portaria" do README, passo a passo, pela
 * API de verdade sobre o PostgreSQL. Se o código mudar e a página deixar de
 * ser verdade, o build quebra.
 */
public sealed class ExemploDoReadmeNaApiTests(ApiDeTestes api) : IClassFixture<ApiDeTestes>
{
    [Fact]
    public async Task O_dia_do_joao_pela_api_do_cadastro_ao_afd_assinado()
    {
        var cancelamento = TestContext.Current.CancellationToken;
        var construtora = await api.Banco.Banco.Cadastrar(Empregador.Novo(Documento.De("11.222.333/0001-81"), "Construtora Horizonte Ltda."), cancelamento);
        var rh = api.Cliente("rh", construtora.Id, Cpf.De("123.456.789-09"));
        var joao = api.Cliente("trabalhador", construtora.Id, Cpf.De("529.982.247-25"));

        // O RH cadastra a obra, o João e o horário dele, abre o registro às 6h e inclui o João às 6h01.
        var obra = (await (await rh.PostAsJsonAsync("/api/estabelecimentos", new { local = "Rua das Palmeiras, 100 - Santos/SP", fuso = "America/Sao_Paulo" }, cancelamento)).Ler<JsonElement>()).GetProperty("id").GetGuid();
        Cenario.Esperar(HttpStatusCode.NoContent, await rh.PutAsJsonAsync("/api/trabalhadores/529.982.247-25", new { nome = "João da Silva", admissao = "2026-03-02", cargo = "Pedreiro" }, cancelamento));
        Cenario.Esperar(HttpStatusCode.NoContent, await rh.PutAsJsonAsync("/api/trabalhadores/529.982.247-25/horarios/2026-03-02", Cenario.HorarioDaObra, cancelamento));
        RelogioDeTeste.Acertar(Exemplos.Em(23, 6, 0));
        Cenario.Esperar(HttpStatusCode.Created, await rh.PostAsync($"/api/estabelecimentos/{obra}/registro", null, cancelamento));
        RelogioDeTeste.Acertar(Exemplos.Em(23, 6, 1));
        Cenario.Esperar(HttpStatusCode.Created, await rh.PostAsJsonAsync($"/api/estabelecimentos/{obra}/registro/inclusoes", new { cpf = "529.982.247-25" }, cancelamento));

        // Às 7h58min31s, o João marca pelo aplicativo. A hora é a do servidor; o hash, o do exemplo.
        RelogioDeTeste.Acertar(Exemplos.Em(23, 7, 58, 31));
        var entrada = await (await joao.PostAsJsonAsync($"/api/estabelecimentos/{obra}/marcacoes", new { coletor = "DispositivoEletronico" }, cancelamento)).Ler<JsonElement>();

        Assert.Equal(3, entrada.GetProperty("nsr").GetInt64());
        Assert.Equal("2026-11-23T07:58-03:00", entrada.GetProperty("marcadaEm").GetString());
        Assert.Equal("7221a9802299bd7a0cd9b4b9813d9872d5a6a0c07311eab15ce5123e913a41bd", entrada.GetProperty("hash").GetString());
        Assert.Equal($"/api/estabelecimentos/{obra}/marcacoes/3/comprovante", entrada.GetProperty("comprovante").GetString());

        // O comprovante é um PDF assinado com PAdES pelo certificado do desenvolvedor.
        var pdf = await joao.GetByteArrayAsync(entrada.GetProperty("comprovante").GetString(), cancelamento);
        Assert.Contains("(NSR: 000000003) Tj", Encoding.Latin1.GetString(pdf), StringComparison.Ordinal);
        Assert.Contains("/SubFilter /ETSI.CAdES.detached", Encoding.Latin1.GetString(pdf), StringComparison.Ordinal);

        // A saída, às 17h02, amarrada à entrada.
        RelogioDeTeste.Acertar(Exemplos.Em(23, 17, 2));
        var saida = await (await joao.PostAsJsonAsync($"/api/estabelecimentos/{obra}/marcacoes", new { coletor = "DispositivoEletronico" }, cancelamento)).Ler<JsonElement>();
        Assert.Equal("a3f002c3cd4c91cc0a2cfa30633f03affd5102961137424dc5bada210bfea3bc", saida.GetProperty("hash").GetString());

        // O espelho do dia, pelo próprio João.
        RelogioDeTeste.Acertar(Exemplos.Em(23, 18, 0));
        var espelho = await joao.GetStringAsync($"/api/estabelecimentos/{obra}/trabalhadores/52998224725/espelho?de=2026-11-23&ate=2026-11-23", cancelamento);
        Assert.Contains("\nseg 23/11  07:58 12:00P 13:00P 17:02 ", espelho, StringComparison.Ordinal);

        // O AFD do dia para o fiscal: o .txt e o .p7s que o assina, num .zip.
        using var afd = new ZipArchive(new MemoryStream(await rh.GetByteArrayAsync($"/api/estabelecimentos/{obra}/afd?de=2026-11-23&ate=2026-11-23", cancelamento)));
        Assert.Equal(["AFD512026001234511222333000181REP_P.txt", "AFD512026001234511222333000181REP_P.p7s"], afd.Entries.Select(arquivo => arquivo.FullName));

        var txt = Ler(afd.Entries[0]);
        var linhas = Encoding.Latin1.GetString(txt).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(
            "000000003" + "7" + "2026-11-23T07:58:00-0300" + "052998224725" + "2026-11-23T07:58:00-0300" + "04" + "0" + "7221a9802299bd7a0cd9b4b9813d9872d5a6a0c07311eab15ce5123e913a41bd",
            linhas[3]);
        Assert.Equal("999999999000000001000000000000000000000000001000000000000000002" + "9", linhas[^2]);
        Assert.Equal(api.Certificado.Thumbprint, AssinaturaCades.Conferir(txt, Ler(afd.Entries[1])).Thumbprint);

        var p7s = new SignedCms();
        p7s.Decode(Ler(afd.Entries[1]));
        Assert.Empty(p7s.ContentInfo.Content);

        // E o AEJ da semana, com a entrada do João.
        using var aej = new ZipArchive(new MemoryStream(await rh.GetByteArrayAsync($"/api/estabelecimentos/{obra}/aej?de=2026-11-23&ate=2026-11-23", cancelamento)));
        Assert.Contains("05|1|2026-11-23T07:58:00-0300|1|E|001|O|1|", Encoding.Latin1.GetString(Ler(aej.Entries[0])).Split("\r\n"));
    }

    private static byte[] Ler(ZipArchiveEntry arquivo)
    {
        using var fluxo = arquivo.Open();
        using var memoria = new MemoryStream();
        fluxo.CopyTo(memoria);

        return memoria.ToArray();
    }
}
