using System.Diagnostics;
using System.Globalization;
using PontoFacial.Calibracao;
using PontoFacial.Dominio.Biometria;
using PontoFacial.Dominio.Biometria.Calibracao;
using PontoFacial.Rosto;

/*
 * dotnet run --project tools/PontoFacial.Calibracao -c Release -- <pasta da base> <relatório.md> [falsa aceitação alvo]
 *
 * A base tem uma pasta por pessoa e as fotos dentro. Toda foto é comparada
 * com todas as outras: as da mesma pessoa contam como genuínas; as de pessoas
 * diferentes, como impostoras.
 */
if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("Uso: PontoFacial.Calibracao <pasta da base> <relatório.md> [falsa aceitação alvo, padrão 0,0001]");
    return 2;
}

var pastaDaBase = Path.GetFullPath(args[0]);
var relatorio = Path.GetFullPath(args[1]);
var alvo = args.Length == 3 ? double.Parse(args[2], CultureInfo.InvariantCulture) : 1e-4;

var relogio = Stopwatch.StartNew();
var imagens = Directory.GetDirectories(pastaDaBase)
    .Order(StringComparer.Ordinal)
    .SelectMany((pasta, pessoa) => Directory.GetFiles(pasta, "*.jpg")
        .Order(StringComparer.Ordinal)
        .Select(arquivo => (Pessoa: pessoa, Arquivo: arquivo)))
    .ToArray();
var pessoas = imagens.Select(imagem => imagem.Pessoa).Distinct().Count();

Console.WriteLine($"{imagens.Length} imagens de {pessoas} pessoas em {pastaDaBase}");

// Uma sessão por núcleo seria desperdício de memória; uma só, com um núcleo
// por inferência e as imagens em paralelo, ocupa a máquina inteira.
using var reconhecedor = new ReconhecedorFacial(ReconhecedorFacial.PastaPadrao, threads: 1);
var capturas = new ResultadoDaCaptura[imagens.Length];

Parallel.For(0, imagens.Length, i =>
    capturas[i] = reconhecedor.Capturar(File.ReadAllBytes(imagens[i].Arquivo), EscolhaDoRosto.OMaisCentral));

var capturadas = Enumerable.Range(0, imagens.Length).Where(i => capturas[i].Capturou).ToArray();
var problemas = capturas
    .Where(captura => !captura.Capturou)
    .GroupBy(captura => captura.Problema!.Value)
    .ToDictionary(grupo => grupo.Key, grupo => grupo.Count());

Console.WriteLine($"Rosto extraído de {capturadas.Length} imagens em {relogio.Elapsed.TotalSeconds:0} s; comparando todas com todas");

var modelos = capturadas.Select(i => capturas[i].Modelo!).ToArray();
var pessoaDe = capturadas.Select(i => imagens[i].Pessoa).ToArray();
var genuinas = new DistribuicaoDeSimilaridades();
var impostoras = new DistribuicaoDeSimilaridades();
var suspeitas = new List<(double Similaridade, int A, int B)>();
var trava = new object();

Parallel.For(
    0,
    modelos.Length,
    () => (Genuinas: new DistribuicaoDeSimilaridades(), Impostoras: new DistribuicaoDeSimilaridades(), Suspeitas: new List<(double, int, int)>()),
    (i, _, local) =>
    {
        for (var j = i + 1; j < modelos.Length; j++)
        {
            var similaridade = modelos[i].Similaridade(modelos[j]);
            var mesmaPessoa = pessoaDe[i] == pessoaDe[j];
            (mesmaPessoa ? local.Genuinas : local.Impostoras).Adicionar(similaridade);

            if (!mesmaPessoa && similaridade >= SimilaridadeSuspeita)
            {
                local.Suspeitas.Add((similaridade, capturadas[i], capturadas[j]));
            }
        }

        return local;
    },
    local =>
    {
        lock (trava)
        {
            genuinas.Somar(local.Genuinas);
            impostoras.Somar(local.Impostoras);
            suspeitas.AddRange(local.Suspeitas);
        }
    });

// Pessoas diferentes tão parecidas assim costumam ser erro da base — a
// mesma pessoa com dois nomes, a mesma foto em duas pastas. Ficam na tela
// para quem calibra conferir; os nomes não vão para o relatório.
Console.WriteLine($"{suspeitas.Count} pares de pessoas diferentes com similaridade de {SimilaridadeSuspeita} ou mais; os mais parecidos:");

foreach (var (similaridade, a, b) in suspeitas.OrderByDescending(par => par.Similaridade).Take(20))
{
    Console.WriteLine($"  {similaridade:0.000}  {Path.GetRelativePath(pastaDaBase, imagens[a].Arquivo)}  {Path.GetRelativePath(pastaDaBase, imagens[b].Arquivo)}");
}

var data = DateOnly.FromDateTime(DateTime.Now);
var origem = string.Create(
    CultureInfo.GetCultureInfo("pt-BR"),
    $"{Path.GetFileName(pastaDaBase)} ({imagens.Length:N0} imagens), falsa aceitação 1:{1 / alvo:N0}, {data:yyyy-MM-dd}");
var calibracao = CalibracaoDoLimiar.Calibrar(ReconhecedorFacial.Reconhecedor, genuinas, impostoras, alvo, origem);

Directory.CreateDirectory(Path.GetDirectoryName(relatorio)!);
File.WriteAllText(
    relatorio,
    Relatorio.Escrever(new DadosDoRelatorio(
        Path.GetFileName(pastaDaBase),
        data,
        imagens.Length,
        pessoas,
        capturadas.Length,
        problemas,
        calibracao,
        SimilaridadeSuspeita,
        suspeitas.Count,
        relogio.Elapsed)));

Console.WriteLine(string.Create(
    CultureInfo.GetCultureInfo("pt-BR"),
    $"Limiar {calibracao.Limiar.Similaridade:0.0000}: falsa aceitação {calibracao.Operacao.FalsaAceitacao.Valor:P4}, falsa rejeição {calibracao.Operacao.FalsaRejeicao.Valor:P2}. Relatório em {relatorio} ({relogio.Elapsed.TotalSeconds:0} s)."));

return 0;

partial class Program
{
    /// <summary>A partir desta similaridade, um par de pessoas diferentes merece ser conferido à mão.</summary>
    private const double SimilaridadeSuspeita = 0.7;
}
