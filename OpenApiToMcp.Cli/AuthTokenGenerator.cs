using System.Net;
using System.Text.Json.Nodes;
using Microsoft.OpenApi.Models;

namespace OpenApiToMcp.Cli;

public interface IAuthTokenGenerator
{
    Task<string?> GetToken();

    static IAuthTokenGenerator Build(OpenApiDocument oas, Command command)
    {
        //if command force auth
        if (command.AuthMethodOverride != null)
        {
            switch (command.AuthMethodOverride)
            {
                case AuthenticationMethod.None:
                    return new NoAuthTokenGenerator();
                case AuthenticationMethod.ClientCredentials:
                    if (string.IsNullOrWhiteSpace(command.TokenUrl) || 
                        string.IsNullOrWhiteSpace(command.ClientId) || 
                        string.IsNullOrWhiteSpace(command.ClientSecret))
                        throw new ArgumentException($"When forcing client credentials, you must set {nameof(Command.TokenUrl)}, {nameof(Command.ClientId)} and {nameof(Command.ClientSecret)}.");
                    return new ClientCredentialsTokenGenerator(command.TokenUrl, command.ClientId, command.ClientSecret);
                default:
                    throw new ArgumentOutOfRangeException($"Unsupported {command.AuthMethodOverride}");
            }
        }
        
        //use the OAS itself
        var oauth2 = oas.Components.SecuritySchemes.FirstOrDefault(s => s.Value.Type == SecuritySchemeType.OAuth2);
        if (oauth2.Value?.Flows.ClientCredentials?.TokenUrl != null)
        {
            if (string.IsNullOrWhiteSpace(command.ClientId) || 
                string.IsNullOrWhiteSpace(command.ClientSecret))
                throw new ArgumentException($"This API uses client credentials, you must set {nameof(Command.ClientId)} and {nameof(Command.ClientSecret)}.");

            var tokenUrl = command.TokenUrl ?? oauth2.Value.Flows.ClientCredentials.TokenUrl.ToString();
            return new ClientCredentialsTokenGenerator(tokenUrl, command.ClientId, command.ClientSecret);
        }
        return new NoAuthTokenGenerator();
    }
}

public class NoAuthTokenGenerator : IAuthTokenGenerator
{
    public Task<string?> GetToken() => Task.FromResult<string?>(null);
}

public class ClientCredentialsTokenGenerator(string tokenUrl, string clientId, string clientSecret) : IAuthTokenGenerator
{
    public async Task<string?> GetToken()
    {
        
        var httpClient = new HttpClient();
        var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("grant_type", "client_credentials"),
            new KeyValuePair<string, string>("client_id", clientId),
            new KeyValuePair<string, string>("client_secret", clientSecret)
        ]));
        if(response.StatusCode != HttpStatusCode.OK)
            throw new Exception("Token generation failed with status code " + response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(body)["access_token"].AsValue().ToString();
    }
}