using System.Globalization;
using System.Text;
using PontoFacial.Dominio.Biometria.Calibracao;
using PontoFacial.Rosto;

namespace PontoFacial.Calibracao;

internal sealed record DadosDoRelatorio(
    string Base,
    DateOnly Data,
    int Imagens,
    int Pessoas,
    int Capturadas,
    IReadOnlyDictionary<ProblemaNaCaptura, int> Problemas,
    CalibracaoDoLimiar Calibracao,
    double SimilaridadeSuspeita,
    int ImpostorasSuspeitas,
    TimeSpan Duracao);

/// <summary>O relatório da calibração em Markdown, com números como se escreve no Brasil.</summary>
internal static class Relatorio
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Escrever(DadosDoRelatorio dados)
    {
        var c = dados.Calibracao;
        var texto = new StringBuilder();

        void Linha(FormattableString linha = default!) =>
            texto.Append(linha is null ? string.Empty : linha.ToString(PtBr)).Append('\n');

        Linha($"# Calibração do limiar: {ReconhecedorFacial.Reconhecedor} na {dados.Base}");
        Linha();
        Linha($"Gerado por `tools/PontoFacial.Calibracao` em {dados.Data:yyyy-MM-dd}, em {dados.Duracao.TotalMinutes:0.0} minutos. As fotos não estão no repositório; só este relatório.");
        Linha();
        Linha($"## A base");
        Linha();
        Linha($"- {dados.Imagens:N0} imagens de {dados.Pessoas:N0} pessoas, uma pasta por pessoa.");
        Linha($"- Rosto extraído de {dados.Capturadas:N0} imagens ({(double)dados.Capturadas / dados.Imagens:P2}). Quando a foto tem mais de um rosto, vale o mais perto do centro.");

        foreach (var (problema, quantas) in dados.Problemas.OrderBy(par => par.Key))
        {
            Linha($"- Sem vetor por {Nome(problema)}: {quantas:N0}.");
        }

        Linha($"- Comparações da mesma pessoa (genuínas): {c.Operacao.FalsaRejeicao.Total:N0}.");
        Linha($"- Comparações entre pessoas diferentes (impostoras): {c.Operacao.FalsaAceitacao.Total:N0}.");
        Linha();
        Linha($"## O limiar");
        Linha();
        Linha($"Para falsa aceitação de até **1:{1 / c.FalsaAceitacaoAlvo:N0}**, o limiar é **{c.Limiar.Similaridade:0.0000}**.");
        Linha();
        Linha($"| | Medida | Erros | Intervalo de 95% |");
        Linha($"|---|--:|--:|---|");
        Linha($"| Falsa aceitação | {Taxa(c.Operacao.FalsaAceitacao)} | {c.Operacao.FalsaAceitacao.Erros:N0} em {c.Operacao.FalsaAceitacao.Total:N0} | {Intervalo(c.Operacao.FalsaAceitacao)} |");
        Linha($"| Falsa rejeição | {Taxa(c.Operacao.FalsaRejeicao)} | {c.Operacao.FalsaRejeicao.Erros:N0} em {c.Operacao.FalsaRejeicao.Total:N0} | {Intervalo(c.Operacao.FalsaRejeicao)} |");
        Linha();
        Linha($"A taxa de erro igual (EER) é {Taxa(c.ErroIgual.FalsaRejeicao)}, no limiar {c.ErroIgual.Limiar:0.0000}.");
        Linha();
        Linha($"## A curva");
        Linha();
        Linha($"| Falsa aceitação tolerada | Limiar | Falsa aceitação medida | Falsa rejeição |");
        Linha($"|---|--:|--:|--:|");

        foreach (var (alvo, ponto) in c.Curva)
        {
            Linha($"| 1:{1 / alvo:N0} | {ponto.Limiar:0.0000} | {Taxa(ponto.FalsaAceitacao)} | {Taxa(ponto.FalsaRejeicao)} |");
        }

        Linha();
        Linha($"Na ponta da curva, a falsa aceitação medida vem em boa parte de erros da base, e não do reconhecedor: há {dados.ImpostorasSuspeitas:N0} pares de pessoas diferentes com similaridade de {dados.SimilaridadeSuspeita:0.0} ou mais, e os mais parecidos são a mesma foto em duas pastas, a mesma pessoa com o nome escrito de dois jeitos, ou fotos com duas pessoas em que o rosto tomado não é o de quem dá nome à pasta. A ferramenta lista esses pares na tela, para quem calibra conferir.");
        Linha();
        Linha($"## Como reproduzir");
        Linha();
        Linha($"```powershell");
        Linha($"dotnet run --project tools/PontoFacial.Calibracao -c Release -- <pasta da base> docs/calibracao/{dados.Base}-{ReconhecedorFacial.Reconhecedor}.md");
        Linha($"```");
        Linha();
        Linha($"As similaridades são contadas em faixas de 0,0001, e o limiar é a borda de uma faixa (ADR-0004).");

        return texto.ToString();
    }

    private static string Taxa(TaxaDeErro taxa) => Porcentagem(taxa.Valor, CasasPara(taxa.Valor));

    /// <summary>Os dois limites com as casas do menor que não é zero, para se lerem juntos.</summary>
    private static string Intervalo(TaxaDeErro taxa)
    {
        var casas = CasasPara(taxa.Inferior > 0 ? taxa.Inferior : taxa.Superior);

        return $"{Porcentagem(taxa.Inferior, casas)} a {Porcentagem(taxa.Superior, casas)}";
    }

    /// <summary>Casas decimais para mostrar dois algarismos que importam, e pelo menos duas.</summary>
    private static int CasasPara(double fracao) =>
        fracao <= 0 ? 2 : Math.Max(2, (int)Math.Ceiling(-Math.Log10(fracao * 100)) + 1);

    private static string Porcentagem(double fracao, int casas) => fracao.ToString($"P{casas}", PtBr);

    private static string Nome(ProblemaNaCaptura problema) => problema switch
    {
        ProblemaNaCaptura.ImagemIlegivel => "imagem ilegível",
        ProblemaNaCaptura.NenhumRosto => "nenhum rosto achado",
        ProblemaNaCaptura.MaisDeUmRosto => "mais de um rosto",
        ProblemaNaCaptura.RostoLonge => "rosto pequeno demais",
        ProblemaNaCaptura.RostoCortado => "rosto cortado na borda",
        _ => problema.ToString(),
    };
}
