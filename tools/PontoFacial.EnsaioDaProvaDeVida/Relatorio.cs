using System.Globalization;
using System.Text;
using PontoFacial.Dominio.Biometria.Calibracao;
using PontoFacial.Dominio.Biometria.Vivacidade;
using PontoFacial.Dominio.Biometria.Vivacidade.Ensaio;

namespace PontoFacial.EnsaioDaProvaDeVida;

internal sealed record DadosDoRelatorio(
    string Base,
    DateOnly Data,
    TimeSpan Duracao,
    int PorEspecie,
    int FotosDeFrente,
    int PessoasComVariasFotos,
    double ApcerAlvo,
    AvaliacaoDoEnsaio Avaliacao,
    IReadOnlyDictionary<EspecieDeAtaque, ProvaDeVida[]> Resultados,
    IReadOnlyDictionary<EspecieDeAtaque, int> NaoComecaram,
    IReadOnlyList<(string Nome, ProvaDeVida? Resultado)> Genuinas);

/// <summary>O relatório do ensaio em Markdown, com as taxas da ISO/IEC 30107-3 e números como se escreve no Brasil.</summary>
internal static class Relatorio
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    public static string Escrever(DadosDoRelatorio dados)
    {
        var avaliacao = dados.Avaliacao;
        var criterio = avaliacao.Criterio;
        var texto = new StringBuilder();

        void Linha(FormattableString? linha = null) =>
            texto.Append(linha is null ? string.Empty : linha.ToString(PtBr)).Append('\n');

        Linha($"# Ensaio da prova de vida na {dados.Base}");
        Linha();
        Linha($"Gerado por `tools/PontoFacial.EnsaioDaProvaDeVida` em {dados.Data:yyyy-MM-dd}, em {dados.Duracao.TotalMinutes:0} minutos. As fotos e os quadros não estão no repositório; só este relatório.");
        Linha();
        Linha($"As taxas são as da ISO/IEC 30107-3: o **APCER** é a fração dos ataques de uma espécie que a prova de vida aceitou; o **BPCER**, a das pessoas de verdade que ela recusou. A norma manda informar o APCER de cada espécie e o da pior, e não a média.");
        Linha();
        Linha($"## Como os ataques foram feitos");
        Linha();
        Linha($"- {dados.PorEspecie:N0} apresentações de ataque por espécie, {dados.Resultados.Count} espécies, {dados.PorEspecie * dados.Resultados.Count:N0} ao todo.");
        Linha($"- As fotos são da {dados.Base}: a primeira de cada pessoa, quando é de frente ({dados.FotosDeFrente:N0} fotos). A troca de fotos usa as {dados.PessoasComVariasFotos:N0} pessoas com quatro fotos ou mais.");
        Linha($"- Cada foto é impressa em tamanho real, com os olhos a 63 mm, e vista por uma câmera simulada de 640×480 pixels a 35–45 cm, a 15 quadros por segundo, com o tremor da mão e o ruído do sensor.");
        Linha($"- O atacante segue o desafio sorteado na tela e faz o melhor que cada espécie permite. Cada apresentação passa pela prova de vida inteira: o detector, o acompanhamento dos nove pontos e a sessão do domínio.");
        Linha($"- Quando a foto não passava por rosto de frente no começo, foi trocada por outra, como faria quem ataca ({dados.NaoComecaram.Values.Sum():N0} trocas).");
        Linha();
        Linha($"| Espécie | Como |");
        Linha($"|---|---|");

        foreach (var especie in dados.Resultados.Keys)
        {
            Linha($"| {especie.Nome} | {especie.Descricao} |");
        }

        Linha();
        Linha($"## O critério");
        Linha();
        Linha($"Para APCER de até **{dados.ApcerAlvo:0.##%}** na pior espécie, o relevo mínimo é **{criterio.RelevoMinimo:0.000}** meia distância entre os olhos — uns {criterio.RelevoMinimo * 31.5:0.0} mm num rosto de olhos a 63 mm.");
        Linha();
        Linha($"| Espécie | Fizeram os movimentos | Maior relevo | APCER | Intervalo de 95% |");
        Linha($"|---|--:|--:|--:|---|");

        foreach (var erro in avaliacao.PorEspecie)
        {
            var resultados = dados.Resultados[erro.Especie];
            var completos = resultados.Where(resultado => resultado.MenorRelevo is not null).ToList();
            var maior = completos.Count == 0 ? "—" : completos.Max(resultado => resultado.MenorRelevo!.Value).ToString("0.000", PtBr);

            Linha($"| {erro.Especie.Nome} | {completos.Count:N0} de {resultados.Length:N0} | {maior} | {Taxa(erro.Apcer)} | {Intervalo(erro.Apcer)} |");
        }

        Linha();
        var pior = avaliacao.PiorEspecie;
        var semErro = avaliacao.PorEspecie.Where(erro => erro.Apcer.Erros == 0).ToList();

        Linha($"A pior espécie é a **{pior.Especie.Nome}**, com APCER de {Taxa(pior.Apcer)} ({pior.Apcer.Erros:N0} de {pior.Apcer.Total:N0}), e é ela que decide o critério. A maior parte das tentativas dela termina antes: o relevo só é julgado quando a foto consegue fazer os três movimentos sem perder o acompanhamento.");

        if (semErro.Count > 0)
        {
            Linha();
            Linha($"Nas {semErro.Count} espécies sem nenhum ataque aceito, zero em {dados.PorEspecie:N0} não quer dizer zero: o APCER verdadeiro ainda pode chegar a {semErro[0].Apcer.Superior:0.00%}, o limite de cima do intervalo. É o que {dados.PorEspecie:N0} apresentações permitem afirmar.");
        }
        Linha();
        Linha($"## Onde cada ataque parou");
        Linha();
        Linha($"| Espécie | Tempo esgotado | Lado errado | Rosto perdido | Câmera falhou | Sem relevo | Aceitos |");
        Linha($"|---|--:|--:|--:|--:|--:|--:|");

        foreach (var (especie, resultados) in dados.Resultados)
        {
            int Contar(MotivoDaRecusa motivo) => resultados.Count(resultado => resultado.Motivo == motivo);
            var completos = resultados.Where(resultado => resultado.MenorRelevo is not null).ToList();
            var aceitos = completos.Count(resultado => criterio.Aceita(resultado.MenorRelevo!.Value));

            Linha($"| {especie.Nome} | {Contar(MotivoDaRecusa.TempoEsgotado):N0} | {Contar(MotivoDaRecusa.MovimentoTrocado):N0} | {Contar(MotivoDaRecusa.RostoPerdido):N0} | {Contar(MotivoDaRecusa.QuadrosEspacados):N0} | {completos.Count - aceitos:N0} | {aceitos:N0} |");
        }

        Linha();
        Linha($"## O vídeo gravado");
        Linha();
        Linha($"Não foi simulado: a gravação de uma pessoa de verdade tem relevo, e a cena só sabe mostrar fotos. O que derruba a gravação é o sorteio. Uma gravação com três movimentos numa ordem acerta 1 dos {Desafio.Possibilidades} desafios: {1.0 / Desafio.Possibilidades:0.00%} por tentativa. Limitar as tentativas de cada trabalhador fica com a API, na Parte 6.");
        Linha();
        Linha($"## As apresentações genuínas");
        Linha();

        if (avaliacao.Bpcer is { } bpcer)
        {
            Linha($"{dados.Genuinas.Count:N0} sessões gravadas com gente de verdade. BPCER de {Taxa(bpcer)} ({bpcer.Erros:N0} de {bpcer.Total:N0}), com intervalo de 95% de {Intervalo(bpcer)}.");
            Linha();
            Linha($"| Sessão | Resultado | Menor relevo |");
            Linha($"|---|---|--:|");

            foreach (var (nome, resultado) in dados.Genuinas)
            {
                var situacao = resultado is null ? "não começou" : resultado.MenorRelevo is { } relevo && (resultado.Aprovada || resultado.Motivo == MotivoDaRecusa.SemRelevo)
                    ? (criterio.Aceita(relevo) ? "aceita" : "recusada sem relevo")
                    : $"recusada: {resultado.Motivo}";
                var relevoMedido = resultado?.MenorRelevo is { } valor ? valor.ToString("0.000", PtBr) : "—";

                Linha($"| {nome} | {situacao} | {relevoMedido} |");
            }
        }
        else
        {
            Linha($"Nenhuma. **A falsa rejeição (BPCER) não foi medida.** A medida pede gente de verdade fazendo o desafio diante de uma câmera, e as bases públicas de vídeo com o rosto virando são de uso só em pesquisa ou não têm o movimento. O critério acima saiu só do lado do ataque. A ferramenta mede o BPCER com sessões gravadas (`--genuinas`), e a medida com o pessoal da empresa fica para a Parte 8 (ADR-0008).");
        }

        Linha();
        Linha($"## O que o ensaio não mede");
        Linha();
        Linha($"- **A luz.** A geometria da cena é exata — a perspectiva, a curva, a dobra —, mas a aparência é a da foto: sem o brilho do papel e da tela, sem o moiré, sem o borrão do movimento.");
        Linha($"- **Máscaras e rostos injetados.** Uma máscara rígida tem relevo, e um vídeo feito por computador entregue no lugar da câmera também; a prova pelo relevo não os pega. Contra eles servem uma câmera com profundidade ou infravermelho e um coletor que não aceite imagem de fora.");
        Linha($"- **O relevo de gente de verdade**, que muda de rosto para rosto.");
        Linha();
        Linha($"## Como reproduzir");
        Linha();
        Linha($"```powershell");
        Linha($"dotnet run --project tools/PontoFacial.EnsaioDaProvaDeVida -c Release -- <pasta da base> docs/ensaio/prova-de-vida-{dados.Base}.md");
        Linha($"```");
        Linha();
        Linha($"Cada apresentação é sorteada com semente fixa: a mesma base dá os mesmos números.");

        return texto.ToString();
    }

    private static string Taxa(TaxaDeErro taxa) => taxa.Valor.ToString("0.00%", PtBr);

    private static string Intervalo(TaxaDeErro taxa) =>
        string.Create(PtBr, $"{taxa.Inferior:0.00%} a {taxa.Superior:0.00%}");
}
