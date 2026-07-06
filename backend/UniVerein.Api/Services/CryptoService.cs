using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Configuration;

namespace UniVerein.Api.Services;

public class CryptoService
{
    private readonly byte[] _key;
    private readonly byte[] _hmacKey;
    private const int DegreeOfParallelism = 2;
    private const int Iterations = 5;
    private const int MemorySize = 65536;

    public CryptoService(IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config["HMAC_KEY"]) || string.IsNullOrWhiteSpace(config["CRYPTO_KEY"]))
            throw new ArgumentException("The configuration HMAC_KEY and CRYPTO_KEY are required.");

        _hmacKey = Convert.FromBase64String(config["HMAC_KEY"]!);
        _key = Convert.FromBase64String(config["CRYPTO_KEY"]!);
    }

    public string Encrypt(string text)
    {
        using Aes aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        byte[] bytes = Encoding.UTF8.GetBytes(text);
        byte[] enc = aes.CreateEncryptor().TransformFinalBlock(bytes, 0, bytes.Length);

        return Convert.ToBase64String(aes.IV.Concat(enc).ToArray());
    }

    public string Encrypt(DateTimeOffset value)
    {
        string plainText = value.ToUniversalTime().ToString("O");
        return Encrypt(plainText);
    }

    public string? Decrypt(string? encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted))
            return encrypted;

        byte[] full = Convert.FromBase64String(encrypted);

        using Aes aes = Aes.Create();
        aes.Key = _key;
        aes.IV = full.Take(16).ToArray();

        byte[] data = full.Skip(16).ToArray();

        byte[] dec = aes.CreateDecryptor()
            .TransformFinalBlock(data, 0, data.Length);

        return Encoding.UTF8.GetString(dec);
    }

    public DateTimeOffset? DecryptDate(string encrypted)
    {
        if (string.IsNullOrWhiteSpace(encrypted))
            return null;
        
        string plainText = Decrypt(encrypted)!;
        return DateTimeOffset.Parse(plainText);
    }

    public string Hash(string input)
    {
        string normalized = input.Replace(" ", "").ToUpperInvariant();
        byte[] inputBytes = Encoding.UTF8.GetBytes(normalized);
        using HMACSHA256 hmac = new(_hmacKey);
        byte[] hashBytes = hmac.ComputeHash(inputBytes);

        return Convert.ToBase64String(hashBytes);
    }

    public static string HashPassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("The password cannot be empty.");

        byte[] salt = new byte[16];
        using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
        {
            randomNumberGenerator.GetBytes(salt);
        }

        Argon2id argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemorySize
        };

        byte[] hash = argon2.GetBytes(32); // 32 Byte = 256 Bit Hash
        byte[] hashBytes = new byte[salt.Length + hash.Length];
        Array.Copy(salt, 0, hashBytes, 0, salt.Length);
        Array.Copy(hash, 0, hashBytes, salt.Length, hash.Length);

        return Convert.ToBase64String(hashBytes);
    }

    public static bool VerifyPassword(string password, string hashedPassword)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashedPassword))
            return false;

        byte[] hashBytes = Convert.FromBase64String(hashedPassword);
        byte[] salt = new byte[16];
        byte[] storedHash = new byte[32];

        Array.Copy(hashBytes, 0, salt, 0, salt.Length);
        Array.Copy(hashBytes, salt.Length, storedHash, 0, storedHash.Length);

        Argon2id argon2 = new(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            DegreeOfParallelism = DegreeOfParallelism,
            Iterations = Iterations,
            MemorySize = MemorySize
        };

        return CryptographicOperations.FixedTimeEquals(storedHash, argon2.GetBytes(32));
    }
}