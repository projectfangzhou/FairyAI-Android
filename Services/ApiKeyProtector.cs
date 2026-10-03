using System.IO;
using System.Text.Json;

namespace FairyAI_Android.Services;

/// <summary>
/// Android API key encryption using AES-256-GCM + PBKDF2.
/// Replaces plaintext storage with secure encryption.
/// </summary>
public class ApiKeyProtector
{
    private const int MaxUnlockAttempts = 5;
    private const int LockoutMinutes = 30;
    private static readonly string LockPath = Path.Combine(FileSystem.AppDataDirectory, "keylock.dat");

    /// <summary>Encrypt API key using AES-256-GCM.</summary>
    public static string Protect(string plaintext)
    {
        if (string.IsNullOrEmpty(plaintext)) return "";
        try
        {
            var key = GetDeviceKey();
            var nonce = System.Security.Cryptography.RandomNumberGenerator.GetBytes(12);
            var tag = new byte[16];
            var plainBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
            var encrypted = new byte[plainBytes.Length];

            using var aes = new System.Security.Cryptography.AesGcm(key, tag.Length);
            aes.Encrypt(nonce, plainBytes, encrypted, tag);

            var result = new byte[nonce.Length + tag.Length + encrypted.Length];
            Buffer.BlockCopy(nonce, 0, result, 0, nonce.Length);
            Buffer.BlockCopy(tag, 0, result, nonce.Length, tag.Length);
            Buffer.BlockCopy(encrypted, 0, result, nonce.Length + tag.Length, encrypted.Length);
            return Convert.ToBase64String(result);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to protect API key", ex);
        }
    }

    /// <summary>Decrypt API key.</summary>
    public static string Unprotect(string ciphertext)
    {
        if (string.IsNullOrEmpty(ciphertext)) return "";
        if (IsLocked()) return "";
        try
        {
            var key = GetDeviceKey();
            var data = Convert.FromBase64String(ciphertext);
            if (data.Length < 28) return "";

            var nonce = new byte[12];
            var tag = new byte[16];
            Buffer.BlockCopy(data, 0, nonce, 0, 12);
            Buffer.BlockCopy(data, 12, tag, 0, 16);
            var encrypted = new byte[data.Length - 28];
            Buffer.BlockCopy(data, 28, encrypted, 0, encrypted.Length);
            var decrypted = new byte[encrypted.Length];

            using var aes = new System.Security.Cryptography.AesGcm(key, tag.Length);
            aes.Decrypt(nonce, encrypted, tag, decrypted);
            return System.Text.Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            RecordFailedAttempt();
            return "";
        }
    }

    public static bool IsLocked()
    {
        try
        {
            if (!File.Exists(LockPath)) return false;
            var data = File.ReadAllBytes(LockPath);
            if (data.Length < 12) return false;
            int failed = BitConverter.ToInt32(data, 0);
            long lockUntil = BitConverter.ToInt64(data, 4);
            return failed >= MaxUnlockAttempts && DateTime.UtcNow.Ticks < lockUntil;
        }
        catch { return false; }
    }

    private static void RecordFailedAttempt()
    {
        try
        {
            int failed = 0;
            if (File.Exists(LockPath))
            {
                var data = File.ReadAllBytes(LockPath);
                if (data.Length >= 4) failed = BitConverter.ToInt32(data, 0);
            }
            failed++;
            var buf = new byte[12];
            BitConverter.GetBytes(failed).CopyTo(buf, 0);
            BitConverter.GetBytes(DateTime.UtcNow.AddMinutes(LockoutMinutes).Ticks).CopyTo(buf, 4);
            File.WriteAllBytes(LockPath, buf);
        }
        catch { }
    }

    /// <summary>Derive device key using PBKDF2.</summary>
    private static byte[] GetDeviceKey()
    {
        var androidId = Android.Provider.Settings.Secure.GetString(
            Android.App.Application.Context.ContentResolver,
            Android.Provider.Settings.Secure.AndroidId);
        if (string.IsNullOrEmpty(androidId))
            throw new InvalidOperationException("Cannot get Android ID");

        using var pbkdf2 = new System.Security.Cryptography.Rfc2898DeriveBytes(
            "FairyAI_AE256_" + androidId,
            System.Text.Encoding.UTF8.GetBytes("FairyAI_Salt_v2"),
            100000,
            System.Security.Cryptography.HashAlgorithmName.SHA256);
        return pbkdf2.GetBytes(32);
    }
}
