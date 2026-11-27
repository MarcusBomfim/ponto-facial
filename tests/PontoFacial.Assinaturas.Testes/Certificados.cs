using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace PontoFacial.Assinaturas.Testes;

/// <summary>
/// Certificados de teste, feitos na hora e assinados por eles mesmos, com o
/// nome no formato da ICP-Brasil. Em produção, o certificado é ICP-Brasil, da
/// empresa ou do desenvolvedor.
/// </summary>
internal static class Certificados
{
    public static readonly X509Certificate2 DoDesenvolvedor = Novo("Desenvolvedora de Exemplo Ltda.:11444777000161");

    public static readonly X509Certificate2 DeOutro = Novo("Outra Empresa Ltda.:11222333000181");

    public static X509Certificate2 Novo(string nome)
    {
        using var chave = RSA.Create(2048);
        var pedido = new CertificateRequest($"CN={nome}, O=ICP-Brasil de teste, C=BR", chave, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        pedido.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.NonRepudiation, critical: true));

        return pedido.CreateSelfSigned(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2027, 12, 31, 0, 0, 0, TimeSpan.Zero));
    }
}

/// <summary>
/// O openssl, para conferir as assinaturas por um programa que não é o .NET.
/// No CI, ele tem de estar lá; numa máquina sem ele, o teste é pulado.
/// </summary>
internal static class OpenSsl
{
    private static bool EmIntegracaoContinua =>
        string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>O <c>openssl cms -verify</c> da assinatura destacada sobre o conteúdo, sem conferir a cadeia: o certificado de teste não tem uma.</summary>
    public static (int Saida, string Mensagem) ConferirCms(byte[] conteudo, byte[] assinatura)
    {
        var pasta = Directory.CreateTempSubdirectory("ponto-facial-openssl-");

        try
        {
            var arquivoDoConteudo = Path.Combine(pasta.FullName, "conteudo.bin");
            var arquivoDaAssinatura = Path.Combine(pasta.FullName, "assinatura.p7s");
            File.WriteAllBytes(arquivoDoConteudo, conteudo);
            File.WriteAllBytes(arquivoDaAssinatura, assinatura);

            var processo = new ProcessStartInfo("openssl")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };

            foreach (var argumento in new[] { "cms", "-verify", "-binary", "-inform", "DER", "-in", arquivoDaAssinatura, "-content", arquivoDoConteudo, "-noverify", "-out", Path.Combine(pasta.FullName, "saida.bin") })
            {
                processo.ArgumentList.Add(argumento);
            }

            Process? openssl;

            try
            {
                openssl = Process.Start(processo);
            }
            catch (System.ComponentModel.Win32Exception) when (!EmIntegracaoContinua)
            {
                openssl = null;
            }

            Assert.SkipWhen(openssl is null, "O openssl não está nesta máquina; a conferência por ele foi pulada.");

            using (openssl)
            {
                var erro = openssl!.StandardError.ReadToEnd();
                openssl.WaitForExit();

                return (openssl.ExitCode, erro.Trim());
            }
        }
        finally
        {
            pasta.Delete(recursive: true);
        }
    }
}
