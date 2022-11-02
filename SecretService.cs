using Azure.Security.KeyVault.Secrets;

namespace ContainerLogExporter;

internal class SecretService
{
    private readonly SecretClient secretClient;

    public SecretService(SecretClient secretClient)
    {
        this.secretClient = secretClient;
    }

    public async Task<string?> GetWorkspaceKey(string workspace)
    {
        KeyVaultSecret secret = await secretClient.GetSecretAsync(workspace);
        return secret.Value;
    }
}