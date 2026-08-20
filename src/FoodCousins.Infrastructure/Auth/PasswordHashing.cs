using System.Security.Cryptography;
namespace FoodCousins.Infrastructure.Auth;
internal static class PasswordHashing
{
    private const int Iterations = 150_000;
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }
    public static bool Verify(string password, string encoded)
    {
        var p = encoded.Split('.', 3);
        if (p.Length != 3 || !int.TryParse(p[0], out var iterations)) return false;
        try {
            var salt = Convert.FromBase64String(p[1]); var expected = Convert.FromBase64String(p[2]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        } catch (FormatException) { return false; }
    }
}
