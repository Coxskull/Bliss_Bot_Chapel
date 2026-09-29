using System.Security.Cryptography.X509Certificates;

namespace Bliss.Api.Runtime;

public static class DataProtectionCertificateLoader
{
    public static X509Certificate2 Load(string path, string password)
    {
        try
        {
            var certificate = new X509Certificate2(
                path,
                password,
                X509KeyStorageFlags.EphemeralKeySet | X509KeyStorageFlags.Exportable);
            if (!certificate.HasPrivateKey)
            {
                certificate.Dispose();
                throw new InvalidOperationException(
                    "The data-protection certificate does not include a private key.");
            }

            return certificate;
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new InvalidOperationException(
                "The data-protection certificate could not be loaded. Supply a PKCS#12 file and its password through the secret store.");
        }
    }
}
