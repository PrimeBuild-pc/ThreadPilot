/*
 * ThreadPilot - best-effort Authenticode signature detection.
 */
namespace ThreadPilot.Services
{
    using System;
    using System.IO;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;

    public sealed class AuthenticodeSignatureVerifier : IUpdateSignatureVerifier
    {
        public UpdateSignatureStatus Verify(string installerPath)
        {
            if (!File.Exists(installerPath))
            {
                return UpdateSignatureStatus.Invalid;
            }

            try
            {
                using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(installerPath));
                using var chain = new X509Chain
                {
                    ChainPolicy =
                    {
                        RevocationMode = X509RevocationMode.Online,
                        RevocationFlag = X509RevocationFlag.ExcludeRoot,
                    },
                };

                return chain.Build(certificate)
                    ? UpdateSignatureStatus.Valid
                    : UpdateSignatureStatus.Unknown;
            }
            catch (CryptographicException)
            {
                return UpdateSignatureStatus.Unknown;
            }
            catch (PlatformNotSupportedException)
            {
                return UpdateSignatureStatus.Unknown;
            }
        }
    }
}
