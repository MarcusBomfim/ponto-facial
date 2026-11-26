using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using PontoFacial.Dominio.Biometria.Vivacidade;
using PontoFacial.Dominio.Biometria.Vivacidade.Ensaio;
using PontoFacial.EnsaioDaProvaDeVida;
using PontoFacial.Rosto;

/*
 * dotnet run --project tools/PontoFacial.EnsaioDaProvaDeVida -c Release -- <pasta da base> <relatório.md> [apresentações por espécie] [--genuinas <pasta>]
 *
 * A base tem uma pasta por pessoa, como a LFW. A primeira foto de cada
 * pessoa, quando é de frente, vira instrumento de ataque; as pessoas com
 * quatro fotos ou mais dão a troca de fotos. Cada tentativa é sorteada com
 * semente fixa: duas execuções dão os mesmos números.
 */
var opcoes = args.ToList();
string? genuinas = null;
var indice = opcoes.IndexOf("--genuinas");

if (indice >= 0 && indice + 1 < opcoes.Count)
{
    genuinas = Path.GetFullPath(opcoes[indice + 1]);
    opcoes.RemoveRange(indice, 2);
}

if (opcoes.Count is < 2 or > 3)
{
    Console.Error.WriteLine("Uso: PontoFacial.EnsaioDaProvaDeVida <pasta da base> <relatório.md> [apresentações por espécie, padrão 300] [--genuinas <pasta>]");
    return 2;
}

var pastaDaBase = Path.GetFullPath(opcoes[0]);
var relatorio = Path.GetFullPath(opcoes[1]);
var porEspecie = opcoes.Count == 3 ? int.Parse(opcoes[2], CultureInfo.InvariantCulture) : 300;
var relogio = Stopwatch.StartNew();

using var reconhecedor = new ReconhecedorFacial(ReconhecedorFacial.PastaPadrao, threads: 1);

Foto? Ler(string arquivo)
{
    if (!Imagem.TentarDecodificar(File.ReadAllBytes(arquivo), out var imagem))
    {
        return null;
    }

    var centro = new Ponto(imagem!.Largura / 2f, imagem.Altura / 2f);
    var rosto = reconhecedor.Detectar(imagem).MinBy(r => MathF.Pow(r.Centro.X - centro.X, 2) + MathF.Pow(r.Centro.Y - centro.Y, 2));

    return rosto is null ? null : new Foto(imagem, rosto);
}

// O nariz em relação ao meio dos olhos, ao longo da linha deles, em distâncias entre os olhos.
static double DesvioDoNariz(Foto foto)
{
    var rosto = foto.Rosto;
    double ux = rosto.OlhoEsquerdo.X - rosto.OlhoDireito.X, uy = rosto.OlhoEsquerdo.Y - rosto.OlhoDireito.Y;
    double meioX = (rosto.OlhoDireito.X + rosto.OlhoEsquerdo.X) / 2.0, meioY = (rosto.OlhoDireito.Y + rosto.OlhoEsquerdo.Y) / 2.0;

    return (((rosto.Nariz.X - meioX) * ux) + ((rosto.Nariz.Y - meioY) * uy)) / ((ux * ux) + (uy * uy));
}

PontosDoRosto Pontos(Foto foto) => PontosDoRosto.ParaAcompanhar(
    new(foto.Rosto.OlhoDireito.X, foto.Rosto.OlhoDireito.Y),
    new(foto.Rosto.OlhoEsquerdo.X, foto.Rosto.OlhoEsquerdo.Y),
    new(foto.Rosto.Nariz.X, foto.Rosto.Nariz.Y),
    new(foto.Rosto.BocaDireita.X, foto.Rosto.BocaDireita.Y),
    new(foto.Rosto.BocaEsquerda.X, foto.Rosto.BocaEsquerda.Y));

var pastas = Directory.GetDirectories(pastaDaBase).Order(StringComparer.Ordinal).ToArray();
var arquivosPorPessoa = pastas.Select(pasta => Directory.GetFiles(pasta, "*.jpg").Order(StringComparer.Ordinal).ToArray()).ToArray();

// A primeira foto de cada pessoa, se for de frente.
var primeiras = new Foto?[pastas.Length];
Parallel.For(0, pastas.Length, i => primeiras[i] = Ler(arquivosPorPessoa[i][0]));
var frontais = primeiras.OfType<Foto>().Where(foto => SessaoDaProvaDeVida.EstaDeFrente(Pontos(foto))).ToArray();

// As pessoas com quatro fotos ou mais: a de frente e as outras, com a pose de cada uma em relação a ela.
var grupos = new ConcurrentBag<(int Pessoa, Foto Frente, IReadOnlyList<(Foto Foto, double Giro, double Inclinacao)> Poses)>();
Parallel.For(0, pastas.Length, i =>
{
    if (arquivosPorPessoa[i].Length < 4)
    {
        return;
    }

    var fotos = arquivosPorPessoa[i].Select(Ler).OfType<Foto>().ToList();
    var frente = fotos.Where(foto => SessaoDaProvaDeVida.EstaDeFrente(Pontos(foto))).MinBy(foto => Math.Abs(DesvioDoNariz(foto)));

    if (frente is null)
    {
        return;
    }

    var poses = fotos.Where(foto => foto != frente)
        .Select(foto =>
        {
            var medida = MedidaDoQuadro.Entre(Pontos(frente), Pontos(foto));
            return (foto, medida.Giro, medida.Inclinacao);
        })
        .ToList();

    grupos.Add((i, frente, poses));
});
var pessoasComVariasFotos = grupos.OrderBy(grupo => grupo.Pessoa).ToArray();

Console.WriteLine($"{frontais.Length} fotos de frente e {pessoasComVariasFotos.Length} pessoas com quatro fotos ou mais em {pastaDaBase} ({relogio.Elapsed.TotalSeconds:0} s)");

var resultados = new Dictionary<EspecieDeAtaque, ProvaDeVida[]>();
var naoComecaram = new Dictionary<EspecieDeAtaque, int>();

for (var e = 0; e < Especies.Todas.Count; e++)
{
    var especie = Especies.Todas[e];
    var desta = new ProvaDeVida[porEspecie];
    var semComeco = 0;

    Parallel.For(0, porEspecie, i =>
    {
        var sorteio = new Random((e * 1_000_003) + i);

        // A foto que não passa por rosto de frente na câmera é trocada por
        // outra, como faria quem ataca.
        for (var tentativa = 0; ; tentativa++)
        {
            var desafio = Desafio.Sortear(sorteio.Next);
            Atacante atacante;

            if (especie == Especies.TrocaDeFotos)
            {
                var grupo = pessoasComVariasFotos[sorteio.Next(pessoasComVariasFotos.Length)];
                atacante = Ataques.Preparar(especie, grupo.Frente, grupo.Poses, sorteio);
            }
            else
            {
                atacante = Ataques.Preparar(especie, frontais[sorteio.Next(frontais.Length)], [], sorteio);
            }

            if (Tentativa.Rodar(reconhecedor, desafio, atacante, sorteio) is { } resultado)
            {
                desta[i] = resultado;
                break;
            }

            Interlocked.Increment(ref semComeco);

            if (tentativa > 20)
            {
                throw new InvalidOperationException($"Nenhuma foto começou a prova na espécie {especie.Nome}.");
            }
        }
    });

    resultados[especie] = desta;
    naoComecaram[especie] = semComeco;
    var completos = desta.Where(r => r.MenorRelevo is not null).Select(r => r.MenorRelevo!.Value).ToList();
    var maior = completos.Count == 0 ? "nenhum" : completos.Max().ToString("0.000", CultureInfo.GetCultureInfo("pt-BR"));
    Console.WriteLine($"{especie.Nome}: {completos.Count} de {porEspecie} fizeram os movimentos; maior relevo {maior} ({relogio.Elapsed.TotalMinutes:0.0} min)");
}

var ensaio = new EnsaioDaProvaDeVida();

static double? RelevoJulgavel(ProvaDeVida? resultado) =>
    resultado is { MenorRelevo: { } relevo } && (resultado.Aprovada || resultado.Motivo == MotivoDaRecusa.SemRelevo) ? relevo : null;

foreach (var especie in Especies.Todas)
{
    foreach (var resultado in resultados[especie])
    {
        ensaio.RegistrarAtaque(especie, RelevoJulgavel(resultado));
    }
}

var sessoes = new List<(string Nome, ProvaDeVida? Resultado)>();

if (genuinas is not null)
{
    sessoes.AddRange(SessoesGravadas.Rodar(reconhecedor, genuinas));

    foreach (var (_, resultado) in sessoes)
    {
        ensaio.RegistrarGenuina(RelevoJulgavel(resultado));
    }
}

const double ApcerAlvo = 0.01;
var data = DateOnly.FromDateTime(DateTime.Now);
var origem = string.Create(
    CultureInfo.GetCultureInfo("pt-BR"),
    $"ensaio simulado na {Path.GetFileName(pastaDaBase)} ({porEspecie} por espécie, {Especies.Todas.Count} espécies), APCER 1%, {data:yyyy-MM-dd}");
var criterio = ensaio.Calibrar(ApcerAlvo, origem);
var avaliacao = ensaio.Avaliar(criterio);

Directory.CreateDirectory(Path.GetDirectoryName(relatorio)!);
File.WriteAllText(
    relatorio,
    Relatorio.Escrever(new DadosDoRelatorio(
        Path.GetFileName(pastaDaBase),
        data,
        relogio.Elapsed,
        porEspecie,
        frontais.Length,
        pessoasComVariasFotos.Length,
        ApcerAlvo,
        avaliacao,
        resultados,
        naoComecaram,
        sessoes)));

Console.WriteLine(string.Create(
    CultureInfo.GetCultureInfo("pt-BR"),
    $"Relevo mínimo {criterio.RelevoMinimo:0.000}: pior espécie {avaliacao.PiorEspecie.Especie.Nome}, APCER {avaliacao.PiorEspecie.Apcer.Valor:P2}. Relatório em {relatorio} ({relogio.Elapsed.TotalMinutes:0.0} min)."));

return 0;
