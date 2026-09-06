using System.Security.Cryptography;
using System.Text.Json;
using VolturaWeekNumber.Features.Updates;
using Xunit;

namespace VolturaWeekNumber.Tests;

public sealed class UpdateTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SignedManifestSelectsExactVariantAndRejectsTampering(bool full)
    {
        using var rsa = RSA.Create(2048);
        var name = $"VolturaWeekNumber-Setup-1.1.0-win-x64{(full ? "-full" : "")}.exe";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, version = "1.1.0", assets = new[] { new { name, size = 12, sha256 = new string('a', 64) } } });
        var signature = rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        var verified = UpdateVerifier.Verify(bytes, signature, rsa.ExportSubjectPublicKeyInfoPem(), new(1, 0, 0), full);
        Assert.Equal(name, verified.Asset.Name);
        bytes[0] ^= 1;
        Assert.Throws<InvalidDataException>(() => UpdateVerifier.Verify(bytes, signature, rsa.ExportSubjectPublicKeyInfoPem(), new(1, 0, 0), full));
    }
    [Fact]
    public void OlderOrEqualVersionIsRejected()
    {
        using var rsa = RSA.Create(2048);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { schema = 1, version = "1.0.0", assets = Array.Empty<object>() });
        var signature = rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        Assert.Throws<InvalidDataException>(() => UpdateVerifier.Verify(bytes, signature, rsa.ExportSubjectPublicKeyInfoPem(), new(1, 0, 0), false));
    }
    [Fact]
    public async Task IncorrectInstallerContentIsRejected()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllBytesAsync(path, [1, 2, 3], TestContext.Current.CancellationToken);
            await Assert.ThrowsAsync<InvalidDataException>(() => UpdateVerifier.VerifyFileAsync(path, new("test", 3, new string('a', 64)), TestContext.Current.CancellationToken));
            await Assert.ThrowsAsync<InvalidDataException>(() => UpdateVerifier.VerifyFileAsync(path, new("test", 4, new string('a', 64)), TestContext.Current.CancellationToken));
        }
        finally { File.Delete(path); }
    }
}
