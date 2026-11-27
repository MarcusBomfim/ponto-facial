using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace PontoFacial.Assinaturas;

/// <summary>
/// Os atributos assinados que fazem de uma assinatura CMS uma assinatura
/// CAdES: o certificado de quem assina, pelo hash dele, e a política de
/// assinatura, quando há uma (ETSI EN 319 122-1 e RFC 5035).
/// </summary>
internal static class AtributosDaAssinatura
{
    /// <summary>id-aa-signingCertificateV2, da RFC 5035.</summary>
    public const string CertificadoDeQuemAssina = "1.2.840.113549.1.9.16.2.47";

    /// <summary>id-aa-ets-sigPolicyId, da RFC 5126.</summary>
    public const string PoliticaDeAssinatura = "1.2.840.113549.1.9.16.2.15";

    /// <summary>id-spq-ets-uri: onde a política pode ser lida.</summary>
    private const string EnderecoDaPolitica = "1.2.840.113549.1.9.16.5.1";

    /// <summary>
    /// O signing-certificate-v2: o SHA-256 do certificado de quem assina, e o
    /// emissor e o número de série dele. Com o SHA-256, o algoritmo é o padrão
    /// do campo e não se escreve.
    /// </summary>
    public static CryptographicAttributeObject Certificado(X509Certificate2 certificado)
    {
        var escritor = new AsnWriter(AsnEncodingRules.DER);

        using (escritor.PushSequence())
        {
            using (escritor.PushSequence())
            {
                using (escritor.PushSequence())
                {
                    escritor.WriteOctetString(SHA256.HashData(certificado.RawData));

                    using (escritor.PushSequence())
                    {
                        using (escritor.PushSequence())
                        {
                            // GeneralName, directoryName [4]: o nome do emissor.
                            using (escritor.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 4, isConstructed: true)))
                            {
                                escritor.WriteEncodedValue(certificado.IssuerName.RawData);
                            }
                        }

                        escritor.WriteInteger(certificado.SerialNumberBytes.Span);
                    }
                }
            }
        }

        return Atributo(CertificadoDeQuemAssina, escritor.Encode());
    }

    /// <summary>
    /// O signature-policy-identifier: o identificador da política, o hash do
    /// documento dela e, se há, o endereço onde ela pode ser lida.
    /// </summary>
    public static CryptographicAttributeObject Politica(PoliticaDeAssinatura politica)
    {
        var escritor = new AsnWriter(AsnEncodingRules.DER);

        using (escritor.PushSequence())
        {
            escritor.WriteObjectIdentifier(politica.Identificador);

            using (escritor.PushSequence())
            {
                using (escritor.PushSequence())
                {
                    escritor.WriteObjectIdentifier(politica.AlgoritmoDoHash);
                }

                escritor.WriteOctetString(politica.Hash.Span);
            }

            if (politica.Endereco is { } endereco)
            {
                using (escritor.PushSequence())
                {
                    using (escritor.PushSequence())
                    {
                        escritor.WriteObjectIdentifier(EnderecoDaPolitica);
                        escritor.WriteCharacterString(UniversalTagNumber.IA5String, endereco.AbsoluteUri);
                    }
                }
            }
        }

        return Atributo(PoliticaDeAssinatura, escritor.Encode());
    }

    private static CryptographicAttributeObject Atributo(string identificador, byte[] valor) =>
        new(new Oid(identificador), [new AsnEncodedData(identificador, valor)]);
}
