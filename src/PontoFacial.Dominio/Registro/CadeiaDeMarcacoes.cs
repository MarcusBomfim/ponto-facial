using System.Globalization;

namespace PontoFacial.Dominio.Registro;

/// <summary>
/// Confere as marcações de um registro, lidas como linhas do tipo 7 do
/// Arquivo Fonte de Dados: cada hash tem de ser o SHA-256 dos campos da linha
/// e do hash da linha anterior.
/// </summary>
/// <remarks>
/// <para>
/// A cadeia denuncia a alteração de uma marcação, e também a inclusão ou a
/// remoção de uma no meio: a seguinte deixa de conferir, porque o hash dela
/// foi calculado com o da anterior.
/// </para>
/// <para>
/// O que a cadeia sozinha não denuncia é quem reescreve tudo — altera uma
/// marcação e refaz todos os hashes dali em diante —, porque o cálculo é
/// público e não usa segredo. Contra isso servem os comprovantes: cada
/// trabalhador tem o hash das próprias marcações, e um comprovante que não
/// confere com o registro aponta a reescrita (ADR-0003).
/// </para>
/// </remarks>
public static class CadeiaDeMarcacoes
{
    public static ConferenciaDaCadeia Conferir(IEnumerable<string> linhas) => Conferir(linhas, []);

    /// <summary>Confere a cadeia e, depois, se cada comprovante tem o hash que o registro tem para o mesmo NSR.</summary>
    public static ConferenciaDaCadeia Conferir(IEnumerable<string> linhas, IEnumerable<ComprovanteDeRegistro> comprovantes)
    {
        ArgumentNullException.ThrowIfNull(linhas);
        ArgumentNullException.ThrowIfNull(comprovantes);

        var hashes = new Dictionary<long, string>();
        string? anterior = null;
        long nsrAnterior = 0;
        var numero = 0;

        foreach (var linha in linhas)
        {
            numero++;

            if (linha.Length != MarcacaoDePonto.TamanhoDaLinha
                || linha[9] != '7'
                || !long.TryParse(linha.AsSpan(0, 9), NumberStyles.None, CultureInfo.InvariantCulture, out var nsr))
            {
                return ConferenciaDaCadeia.Quebrada(numero, string.Create(
                    CultureInfo.InvariantCulture,
                    $"A linha {numero} não é uma marcação do tipo 7: a marcação tem {MarcacaoDePonto.TamanhoDaLinha} caracteres e o tipo na posição 10."));
            }

            if (nsr <= nsrAnterior)
            {
                return ConferenciaDaCadeia.Quebrada(numero, string.Create(
                    CultureInfo.InvariantCulture,
                    $"O NSR {nsr} vem depois do NSR {nsrAnterior}: as marcações têm de estar em ordem de NSR."));
            }

            var hash = linha[MarcacaoDePonto.TamanhoDosCampos..];

            if (MarcacaoDePonto.CalcularHash(linha[..MarcacaoDePonto.TamanhoDosCampos], anterior) != hash)
            {
                return ConferenciaDaCadeia.Quebrada(numero, string.Create(
                    CultureInfo.InvariantCulture,
                    $"A marcação do NSR {nsr} não confere com o hash dela: ela foi alterada, ou alguma antes dela foi alterada, incluída ou removida."));
            }

            hashes[nsr] = hash;
            anterior = hash;
            nsrAnterior = nsr;
        }

        foreach (var comprovante in comprovantes)
        {
            if (!hashes.TryGetValue(comprovante.Nsr, out var hash) || hash != comprovante.Hash)
            {
                return ConferenciaDaCadeia.Quebrada(null, string.Create(
                    CultureInfo.InvariantCulture,
                    $"O comprovante do NSR {comprovante.Nsr} não confere com o registro: a marcação que o trabalhador recebeu não está mais lá como foi feita."));
            }
        }

        return ConferenciaDaCadeia.Integra;
    }
}
