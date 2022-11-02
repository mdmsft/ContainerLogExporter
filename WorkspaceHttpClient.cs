using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ContainerLogExporter;

internal class WorkspaceHttpClient : HttpClient
{
    private const string LogName = "ContainerLog";

    private const string AuthorizationScheme = "SharedKey";

    private readonly string workspaceId;

    private readonly string workspaceKey;

    private readonly string dateString;

    public WorkspaceHttpClient(string workspaceId, string workspaceKey)
    {
        this.workspaceId = workspaceId;
        this.workspaceKey = workspaceKey;
        this.dateString = DateTime.UtcNow.ToString("r");

        this.BaseAddress = new Uri($"https://{workspaceId}.ods.opinsights.azure.com/api/logs?api-version=2016-04-01");
        this.DefaultRequestHeaders.Accept.Clear();
        this.DefaultRequestHeaders.Accept.Add(new(MediaTypeNames.Application.Json));
        this.DefaultRequestHeaders.Add("Log-Type", LogName);
        this.DefaultRequestHeaders.Add("x-ms-date", this.dateString);
        this.DefaultRequestHeaders.Add("time-generated-field", nameof(Entity.TimeGenerated));
    }

    public async Task SendLogs(Entity[] entities)
    {
        string json = JsonSerializer.Serialize(entities);
        using HttpContent httpContent = new StringContent(json, Encoding.UTF8);
        httpContent.Headers.ContentType = new(MediaTypeNames.Application.Json);
        this.DefaultRequestHeaders.Authorization = new(AuthorizationScheme, GetAuthorizationHeaderValue(json));
        await this.PostAsync(string.Empty, httpContent);
    }

    private string GetAuthorizationHeaderValue(string jsonPayload)
    {
        byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPayload);
        string signPayload = string.Join("\n", new[] { "POST", jsonBytes.Length.ToString(), MediaTypeNames.Application.Json, $"x-ms-date:{dateString}", "/api/logs" });
        string signature = CreateSignature(signPayload, workspaceKey);
        return $"{workspaceId}:{signature}";
    }

    private static string CreateSignature(string message, string secret)
    {
        ASCIIEncoding encoding = new();
        byte[] keyByte = Convert.FromBase64String(secret);
        byte[] messageBytes = encoding.GetBytes(message);
        using (var hmacsha256 = new HMACSHA256(keyByte))
        {
            byte[] hash = hmacsha256.ComputeHash(messageBytes);
            return Convert.ToBase64String(hash);
        }
    }
}