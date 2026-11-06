using System.Text.RegularExpressions;

namespace Jcf.NetworkManager.Services;

public sealed class SecretResolver
{
    public (string User, string Password) Resolve(string reference)
    {
        if (!Regex.IsMatch(reference ?? "", "^[A-Za-z0-9_]{1,40}$"))
            throw new InvalidOperationException("Invalid device secret reference; use letters, numbers and underscores.");
        var prefix = "JCF_DEVICE_" + reference!.ToUpperInvariant();
        var user = Environment.GetEnvironmentVariable(prefix + "_USER");
        var pass = Environment.GetEnvironmentVariable(prefix + "_PASS");
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrEmpty(pass))
            throw new InvalidOperationException("Device credentials not configured in the server's secret environment.");
        return (user, pass);
    }

    public string? PinnedSha256(string reference) => Environment.GetEnvironmentVariable("JCF_DEVICE_" + reference.ToUpperInvariant() + "_CERT_SHA256");
}
