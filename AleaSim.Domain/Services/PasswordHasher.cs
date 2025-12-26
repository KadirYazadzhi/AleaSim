using System.Security.Cryptography;
using System.Text;

namespace AleaSim.Domain.Services;

public interface IPasswordHasher {
    string HashPassword(string password);
    bool VerifyPassword(string hashedPassword, string providedPassword);
}

public class PasswordHasher : IPasswordHasher {
    private const int SaltSize = 16; // 128 bit
    private const int KeySize = 32;  // 256 bit
    private const int Iterations = 100000;
    private static readonly HashAlgorithmName _hashAlgorithm = HashAlgorithmName.SHA256;

    public string HashPassword(string password) {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            Iterations,
            _hashAlgorithm,
            KeySize);

        return $"{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool VerifyPassword(string hashedPassword, string providedPassword) {
        var parts = hashedPassword.Split('.', 2);
        if (parts.Length != 2) return false;

        byte[] salt = Convert.FromBase64String(parts[0]);
        byte[] hash = Convert.FromBase64String(parts[1]);

        byte[] testHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(providedPassword),
            salt,
            Iterations,
            _hashAlgorithm,
            KeySize);

        return CryptographicOperations.FixedTimeEquals(hash, testHash);
    }
}