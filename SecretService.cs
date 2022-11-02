using Azure.Security.KeyVault.Secrets;

namespace ContainerLogExporter;

internal class SecretService
{
    private readonly SecretClient secretClient;

    public SecretService(SecretClient secretClient)
    {
        this.secretClient = secretClient;
    }

    public async Task<(string? id, string? key)> GetWorkspaceInfo(string workspace)
    {
        KeyVaultSecret secret = await secretClient.GetSecretAsync(workspace);
        return (secret.Value?.Split(':')[0], secret.Value?.Split(':')[1]);
    }
}