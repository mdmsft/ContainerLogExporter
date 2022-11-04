using Azure.Security.KeyVault.Secrets;

namespace ContainerLogExporter;

internal class SecretService
{
    private readonly SecretClient secretClient;

    public SecretService(SecretClient secretClient)
    {
        this.secretClient = secretClient;
    }

    public async Task<string?> GetSecret(string name)
    {
        KeyVaultSecret secret = await secretClient.GetSecretAsync(name);
        return secret.Value;
    }
}