using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Identity.Client;

const string DefaultEnvironmentUrl = "https://maccabihealthcareservicesqa.crm4.dynamics.com";

var config = DynamicsConfig.FromEnvironment();

try
{
    var accessToken = await GetAccessTokenAsync(config);
    var solutionHistoryJson = await GetAllSolutionHistoryAsync(config.EnvironmentUrl, accessToken);

    Console.WriteLine(solutionHistoryJson.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true
    }));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to read Dynamics solution history: {ex.Message}");
    Environment.ExitCode = 1;
}

static async Task<string> GetAccessTokenAsync(DynamicsConfig config)
{
    var app = PublicClientApplicationBuilder
        .Create(config.ClientId)
        .WithAuthority(AzureCloudInstance.AzurePublic, config.TenantId)
        .Build();

    var result = await app
        .AcquireTokenByUsernamePassword(config.Scopes, config.Username, config.Password)
        .ExecuteAsync();

    return result.AccessToken;
}

static async Task<JsonObject> GetAllSolutionHistoryAsync(string environmentUrl, string accessToken)
{
    using var httpClient = new HttpClient();
    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
    httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");
    httpClient.DefaultRequestHeaders.Add("Prefer", "odata.maxpagesize=5000");

    var requestUrl = $"{environmentUrl.TrimEnd('/')}/api/data/v9.0/msdyn_solutionhistories?$orderby=msdyn_starttime%20desc";
    var allRows = new JsonArray();
    JsonObject? firstPage = null;

    while (!string.IsNullOrWhiteSpace(requestUrl))
    {
        using var response = await httpClient.GetAsync(requestUrl);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Dynamics Web API returned {(int)response.StatusCode} {response.ReasonPhrase}: {responseBody}");
        }

        var page = JsonNode.Parse(responseBody)?.AsObject()
            ?? throw new InvalidOperationException("Dynamics Web API returned an empty or invalid JSON response.");

        firstPage ??= new JsonObject();
        foreach (var property in page)
        {
            if (property.Key is not "value" and not "@odata.nextLink")
            {
                firstPage[property.Key] = property.Value?.DeepClone();
            }
        }

        if (page["value"] is JsonArray values)
        {
            foreach (var value in values)
            {
                allRows.Add(value?.DeepClone());
            }
        }

        requestUrl = page["@odata.nextLink"]?.GetValue<string>();
    }

    firstPage ??= new JsonObject();
    firstPage["value"] = allRows;
    firstPage["retrievedCount"] = allRows.Count;

    return firstPage;
}

sealed record DynamicsConfig(
    string TenantId,
    string ClientId,
    string Username,
    string Password,
    string EnvironmentUrl,
    string[] Scopes)
{
    public static DynamicsConfig FromEnvironment()
    {
        var environmentUrl = ReadOptional("D365_ENVIRONMENT_URL", DefaultEnvironmentUrl).TrimEnd('/');
        var scopes = ReadOptional("D365_SCOPES", $"{environmentUrl}/user_impersonation")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new DynamicsConfig(
            TenantId: ReadRequired("D365_TENANT_ID"),
            ClientId: ReadRequired("D365_CLIENT_ID"),
            Username: ReadRequired("D365_USERNAME"),
            Password: ReadRequired("D365_PASSWORD"),
            EnvironmentUrl: environmentUrl,
            Scopes: scopes);
    }

    private static string ReadRequired(string variableName)
    {
        var value = Environment.GetEnvironmentVariable(variableName);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Missing required environment variable: {variableName}");
        }

        return value;
    }

    private static string ReadOptional(string variableName, string defaultValue) =>
        Environment.GetEnvironmentVariable(variableName) is { Length: > 0 } value ? value : defaultValue;
}
