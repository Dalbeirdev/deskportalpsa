using System.Security.Cryptography;
using System.Text;
using Desk.Infrastructure.Secrets;
using FluentAssertions;
using Xunit;

namespace Desk.Tests.Unit;

public class SecretCipherTests
{
    // Fixed only for test determinism — never a value used outside this file.
    private const string TestKey = "w+WEoJiLQLVmZzgEm//uVd0YpeTwnhwm2rUyftBqdO8=";

    [Fact]
    public void Round_trips_plaintext()
    {
        var cipher = new SecretCipher(new SecretEncryptionOptions { Key = TestKey });
        var plaintext = Encoding.UTF8.GetBytes("{\"PrivateKey\":\"topsecret\"}");

        var decrypted = cipher.Decrypt(cipher.Encrypt(plaintext));

        decrypted.Should().Equal(plaintext);
    }

    [Fact]
    public void Same_plaintext_encrypts_differently_each_time()
    {
        // A fixed nonce would let two identical secrets be recognised as identical from the
        // ciphertext alone, and reused nonces are how AES-GCM keys get broken. Each call must draw
        // a fresh one.
        var cipher = new SecretCipher(new SecretEncryptionOptions { Key = TestKey });
        var plaintext = Encoding.UTF8.GetBytes("same-secret");

        cipher.Encrypt(plaintext).Should().NotEqual(cipher.Encrypt(plaintext));
    }

    [Fact]
    public void Rejects_a_tampered_ciphertext()
    {
        var cipher = new SecretCipher(new SecretEncryptionOptions { Key = TestKey });
        var combined = cipher.Encrypt(Encoding.UTF8.GetBytes("topsecret"));
        combined[^1] ^= 0xFF; // flip a bit in the authentication tag

        var act = () => cipher.Decrypt(combined);

        act.Should().Throw<CryptographicException>();
    }

    [Fact]
    public void Cannot_decrypt_under_a_different_key()
    {
        var writer = new SecretCipher(new SecretEncryptionOptions { Key = TestKey });
        var reader = new SecretCipher(new SecretEncryptionOptions
        {
            Key = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        });
        var combined = writer.Encrypt(Encoding.UTF8.GetBytes("topsecret"));

        var act = () => reader.Decrypt(combined);

        act.Should().Throw<CryptographicException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-base64!!!")]
    [InlineData("dG9vc2hvcnQ=")] // valid base64, decodes to far fewer than 32 bytes
    public void Refuses_a_key_that_is_not_32_bytes_of_base64(string badKey)
    {
        var act = () => new SecretCipher(new SecretEncryptionOptions { Key = badKey });

        act.Should().Throw<InvalidOperationException>();
    }
}
