using System.CommandLine;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LicenseKeyGenerator;

/// <summary>
/// Payload for license key
/// </summary>
record LicensePayload(
    int Tier, // 0=Free, 1=Pro, 2=Enterprise
    string CustomerEmail,
    DateTime? ExpiresAt,
    string IssuedAt
);

class Program
{
    static async Task<int> Main(string[] args)
    {
        var privateKeyOption = new Option<FileInfo?>(
            name: "--private-key",
            description: "Path to RSA private key PEM file")
        { IsRequired = true };

        var tierOption = new Option<int>(
            name: "--tier",
            description: "License tier: 0=Free, 1=Pro, 2=Enterprise")
        { IsRequired = true };

        var emailOption = new Option<string>(
            name: "--email",
            description: "Customer email address")
        { IsRequired = true };

        var expiresOption = new Option<DateTime?>(
            name: "--expires",
            description: "Expiration date (ISO 8601), omit for 1 year",
            getDefaultValue: () => DateTime.UtcNow.AddYears(1));

        var outputOption = new Option<FileInfo?>(
            name: "--output",
            description: "Output file path (optional, prints to stdout if omitted)");

        var generateKeyPairOption = new Option<bool>(
            name: "--generate-keypair",
            description: "Generate new RSA key pair and exit");

        var keyPairOutputOption = new Option<DirectoryInfo?>(
            name: "--keypair-output",
            description: "Directory to save generated key pair (default: current directory)");

        var rootCommand = new RootCommand("MediaClippingMcpServer License Key Generator")
        {
            privateKeyOption,
            tierOption,
            emailOption,
            expiresOption,
            outputOption,
            generateKeyPairOption,
            keyPairOutputOption
        };

        rootCommand.SetHandler(async (privateKey, tier, email, expires, output, generateKeypair, keypairOutput) =>
        {
            if (generateKeypair)
            {
                await GenerateKeyPairAsync(keypairOutput ?? new DirectoryInfo("."));
                return;
            }

            if (privateKey == null || !privateKey.Exists)
            {
                Console.Error.WriteLine("Error: Private key file not found");
                return;
            }

            if (tier < 0 || tier > 2)
            {
                Console.Error.WriteLine("Error: Tier must be 0 (Free), 1 (Pro), or 2 (Enterprise)");
                return;
            }

            var licenseKey = await GenerateLicenseKeyAsync(privateKey, tier, email, expires ?? DateTime.UtcNow.AddYears(1));

            if (output != null)
            {
                await File.WriteAllTextAsync(output.FullName, licenseKey);
                Console.WriteLine($"License key written to {output.FullName}");
            }
            else
            {
                Console.WriteLine(licenseKey);
            }
        }, privateKeyOption, tierOption, emailOption, expiresOption, outputOption, generateKeyPairOption, keyPairOutputOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task GenerateKeyPairAsync(DirectoryInfo outputDir)
    {
        using var rsa = RSA.Create(2048);

        // Export private key
        var privateKeyPem = rsa.ExportRSAPrivateKeyPem();
        var privateKeyPath = Path.Combine(outputDir.FullName, "license_private_key.pem");
        await File.WriteAllTextAsync(privateKeyPath, privateKeyPem);

        // Export public key
        var publicKeyPem = rsa.ExportRSAPublicKeyPem();
        var publicKeyPath = Path.Combine(outputDir.FullName, "license_public_key.pem");
        await File.WriteAllTextAsync(publicKeyPath, publicKeyPem);

        Console.WriteLine($"Generated key pair:");
        Console.WriteLine($"  Private key: {privateKeyPath}");
        Console.WriteLine($"  Public key:  {publicKeyPath}");
        Console.WriteLine();
        Console.WriteLine("IMPORTANT: Keep the private key secure! Add the public key to your appsettings.json:");
        Console.WriteLine($"  \"LicensePublicKey\": \"{EscapeForJson(publicKeyPem)}\"");
    }

    static async Task<string> GenerateLicenseKeyAsync(FileInfo privateKeyFile, int tier, string email, DateTime expiresAt)
    {
        var privateKeyPem = await File.ReadAllTextAsync(privateKeyFile.FullName);
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);

        var payload = new LicensePayload(
            Tier: tier,
            CustomerEmail: email,
            ExpiresAt: expiresAt,
            IssuedAt: DateTime.UtcNow.ToString("O")
        );

        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
        var payloadBase64 = Convert.ToBase64String(payloadBytes);

        // Sign the payload
        var signatureBytes = rsa.SignData(payloadBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var signatureBase64 = Convert.ToBase64String(signatureBytes);

        // Combine: payload.signature
        return $"{payloadBase64}.{signatureBase64}";
    }

    static string EscapeForJson(string input)
    {
        return input.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }
}