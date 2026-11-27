namespace PontoFacial.Dominio.Arquivos;

/// <summary>
/// O CRC-16 que o leiaute do Arquivo Fonte de Dados manda gravar nos
/// registros dos tipos 1 a 5, no padrão CRC-16/KERMIT (CCITT-TRUE).
/// </summary>
/// <remarks>
/// É o polinômio 0x1021, refletido, começando em zero. O leiaute dá o valor
/// de conferência: os nove caracteres "123456789" geram 0x2189, gravado
/// como "2189".
/// </remarks>
internal static class Crc16
{
    /// <summary>O polinômio 0x1021 com os bits na ordem inversa, como o KERMIT o usa.</summary>
    private const ushort Polinomio = 0x8408;

    public static ushort Kermit(ReadOnlySpan<byte> dados)
    {
        ushort crc = 0;

        foreach (var octeto in dados)
        {
            crc ^= octeto;

            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (ushort)((crc >> 1) ^ Polinomio) : (ushort)(crc >> 1);
            }
        }

        return crc;
    }

    /// <summary>Os quatro caracteres hexadecimais do CRC do texto em ISO 8859-1, sem o "0x".</summary>
    public static string DoRegistro(string registro) => Kermit(Campos.Latin1.GetBytes(registro)).ToString("X4", System.Globalization.CultureInfo.InvariantCulture);
}
