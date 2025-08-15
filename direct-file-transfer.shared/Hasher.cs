using System.Security.Cryptography;

namespace direct_file_transfer.shared
{
    public static class Hasher
    {
        public static string GetHash(byte[] data)
        {
            using var sha256 = SHA256.Create();
            return Convert.ToHexString(sha256.ComputeHash(data));
        }
    }
}
