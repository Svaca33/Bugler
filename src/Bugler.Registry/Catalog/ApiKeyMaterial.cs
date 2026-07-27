using System.Security.Cryptography;
using System.Text;

namespace Bugler.Registry.Catalog;

public static class ApiKeyMaterial
{
    public const string Prefix = "blgr_";

    public static string GeneratePlaintext() =>
        Prefix + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Hash(string plaintext) =>
        SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));
}
