using System.Security.Cryptography;

public class PayloadUtilities
{
    public static string ComputeETag(byte[] payload)
    {
        using (var hasher = SHA256.Create())
        {
            var hash = hasher.ComputeHash(payload);
            return "\"" + Convert.ToBase64String(hash) + "\"";
        }
    }
}
