using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Jcf.NetworkManager.Domain;
using Jcf.NetworkManager.Services;

namespace Jcf.NetworkManager.Connectors;

/// <param name="handlerOverride">Test seam only: supplies a simulated device transport. Never registered in DI.</param>
public sealed class DeviceHttpFactory(SecretResolver secrets, Func<Device,int,HttpMessageHandler>? handlerOverride = null)
{
    public HttpClient Create(Device device, int port)
    {
        var pinned = secrets.PinnedSha256(device.SecretRef)?.Replace(":", "").Replace(" ", "").ToUpperInvariant();
        var handler = new HttpClientHandler { UseCookies = true, CookieContainer = new CookieContainer(), AllowAutoRedirect = false, UseProxy = false };
        if (!string.IsNullOrWhiteSpace(pinned))
        {
            if (pinned.Length != 64 || !pinned.All(Uri.IsHexDigit))
                throw new InvalidOperationException("Device TLS fingerprint must be 64 hexadecimal characters (SHA-256).");
            handler.ServerCertificateCustomValidationCallback = (request, certificate, chain, errors) =>
            {
                if (certificate is null) return false;
                using var sha = SHA256.Create();
                var actual = Convert.ToHexString(sha.ComputeHash(certificate.GetRawCertData()));
                return CryptographicOperations.FixedTimeEquals(Convert.FromHexString(actual), Convert.FromHexString(pinned));
            };
        }
        HttpMessageHandler transport = handlerOverride is null ? handler : handlerOverride(device, port);
        if (handlerOverride is not null) handler.Dispose();
        var client = new HttpClient(transport) { BaseAddress = new Uri($"https://{device.ManagementIp}:{port}/"), Timeout = TimeSpan.FromSeconds(18) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "JCF-NetworkManager/"+AppInfo.Version);
        return client;
    }
}
