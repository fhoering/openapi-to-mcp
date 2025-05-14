using System.Net;
using System.Text.Json.Nodes;
using Microsoft.OpenApi.Models;

namespace OpenApiToMcp;

public interface IAuthTokenGenerator
{
    Task<string?> GetToken();

    static IAuthTokenGenerator Build(OpenApiDocument oas, Command command)
    {
        if (command.BearerToken != null)
        {
            Console.Error.WriteLine("Using bearer token authentication");
            return new BearerTokenGenerator(command.BearerToken);
        }

        if (command.Oauth2GrantType == null)
        {
            Console.Error.WriteLine("Using no authentication");
            return new NoAuthTokenGenerator();
        }
        
        //extract the token url based on the selected flow
        var flows = oas.Components.SecuritySchemes.FirstOrDefault(s => s.Value.Type == SecuritySchemeType.OAuth2).Value?.Flows;
        var tokenUrl = command.Oauth2GrantType switch
        {
            OAuth2GrantType.client_credentials => flows?.ClientCredentials?.TokenUrl?.ToString(),
            OAuth2GrantType.refresh_token => flows?.AuthorizationCode?.TokenUrl?.ToString(),
            OAuth2GrantType.password => flows?.Password?.TokenUrl?.ToString(),
            _ => throw new ArgumentException($"Unsupported flow {command.Oauth2GrantType}")
        };
        
        //use the provided token url if there is one
        tokenUrl = command.Oauth2TokenUrl ?? tokenUrl;
        if(tokenUrl == null)
            throw new ArgumentException("No token url specified. Add one in your OpenApi document or provide one as CLI option");

        Console.Error.WriteLine($"Using Oauth2 grant type {command.Oauth2GrantType} with token url: {tokenUrl}");
        return new OAuth2TokenGenerator(tokenUrl, command.Oauth2GrantType.Value, command.Oauth2ClientId, command.Oauth2ClientSecret, command.Oauth2RefreshToken, command.Oauth2Username, command.Oauth2Password);
    }
}


public class NoAuthTokenGenerator : IAuthTokenGenerator
{
    public Task<string?> GetToken() => Task.FromResult<string?>(null);
}


public class BearerTokenGenerator(string token) : IAuthTokenGenerator
{
    public Task<string?> GetToken() => Task.FromResult<string?>(token);
}

public class OAuth2TokenGenerator(string tokenUrl, OAuth2GrantType grantType, string? clientId, string? clientSecret, string? refreshToken, string? username, string? password) : IAuthTokenGenerator
{

    public async Task<string?> GetToken()
    {
        var httpClient = new HttpClient();
        var kvs = new List<KeyValuePair<string, string>>();
        kvs.Add(new KeyValuePair<string, string>("grant_type", grantType.ToString()));
        if(clientId != null)
            kvs.Add(new KeyValuePair<string, string>("client_id", clientId));
        if(clientSecret != null)
            kvs.Add(new KeyValuePair<string, string>("client_secret", clientSecret));
        if(refreshToken != null)
            kvs.Add(new KeyValuePair<string, string>("refresh_token", refreshToken));
        if(username != null)
            kvs.Add(new KeyValuePair<string, string>("username", username));
        if(password != null)
            kvs.Add(new KeyValuePair<string, string>("password", password));
        
        var response = await httpClient.PostAsync(tokenUrl, new FormUrlEncodedContent(kvs));
        if(response.StatusCode != HttpStatusCode.OK)
            throw new Exception("Token generation failed with status code " + response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        return JsonNode.Parse(body)?["access_token"]?.AsValue().ToString();
    }
}