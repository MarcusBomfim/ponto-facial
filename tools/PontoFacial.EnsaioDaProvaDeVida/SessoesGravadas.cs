using System.Globalization;
using PontoFacial.Dominio.Biometria.Vivacidade;
using PontoFacial.Rosto;

namespace PontoFacial.EnsaioDaProvaDeVida;

/// <summary>
/// Sessões gravadas com gente de verdade, como apresentações genuínas: uma
/// pasta por sessão, com os quadros numerados (JPEG ou PNG) e um
/// <c>sessao.txt</c> que diz o desafio que a pessoa seguiu e a taxa da
/// câmera:
/// <code>
/// desafio=Esquerda,Cima,Direita
/// quadros-por-segundo=30
/// </code>
/// </summary>
internal static class SessoesGravadas
{
    private static readonly string[] Extensoes = [".jpg", ".jpeg", ".png"];

    /// <summary>Cada sessão da pasta e o resultado da prova de vida nela; nada quando ela nem começou.</summary>
    public static IEnumerable<(string Nome, ProvaDeVida? Resultado)> Rodar(ReconhecedorFacial reconhecedor, string pasta)
    {
        foreach (var sessao in Directory.GetDirectories(pasta).Order(StringComparer.Ordinal))
        {
            var (desafio, quadrosPorSegundo) = LerSessao(Path.Combine(sessao, "sessao.txt"));
            var quadros = Directory.GetFiles(sessao)
                .Where(arquivo => Extensoes.Contains(Path.GetExtension(arquivo).ToLowerInvariant()))
                .Order(StringComparer.Ordinal);
            var camera = new ProvaDeVidaNaCamera(reconhecedor, desafio, Tentativa.ParaMedir);
            var comeco = Tentativa.Instante(0);
            var numero = 0;

            foreach (var arquivo in quadros)
            {
                if (!Imagem.TentarDecodificar(File.ReadAllBytes(arquivo), out var imagem))
                {
                    throw new InvalidDataException($"{arquivo} não é imagem.");
                }

                camera.Processar(imagem!, comeco + TimeSpan.FromSeconds(numero++ / quadrosPorSegundo));

                if (camera.Terminou)
                {
                    break;
                }
            }

            yield return (Path.GetFileName(sessao), camera.Resultado);
        }
    }

    private static (Desafio Desafio, double QuadrosPorSegundo) LerSessao(string arquivo)
    {
        var campos = File.ReadAllLines(arquivo)
            .Where(linha => linha.Contains('=', StringComparison.Ordinal))
            .Select(linha => linha.Split('=', 2, StringSplitOptions.TrimEntries))
            .ToDictionary(par => par[0], par => par[1], StringComparer.OrdinalIgnoreCase);

        var movimentos = campos["desafio"]
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(nome => Enum.Parse<Movimento>(nome, ignoreCase: true))
            .ToList();

        return (Desafio.De(movimentos), double.Parse(campos["quadros-por-segundo"], CultureInfo.InvariantCulture));
    }
}
