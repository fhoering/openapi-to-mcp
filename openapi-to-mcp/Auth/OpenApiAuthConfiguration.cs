using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Microsoft.OpenApi.Models;

namespace OpenApiToMcp.Auth;

public class OpenApiAuthConfiguration
{
    public readonly string? BearerToken;
    public readonly OAuth2GrantType? Oauth2GrantType;
    public readonly string? Oauth2TokenUrl;
    public readonly string? Oauth2ClientId;
    public readonly string? Oauth2ClientSecret;
    public readonly string? Oauth2RefreshToken;
    public readonly string? Oauth2Username;
    public readonly string? Oauth2Password;
    
    public OpenApiAuthConfiguration(Command command, OpenApiDocument openApiDocument, HttpContext? httpContext = null)
    {
        BearerToken = command.BearerToken.OverrideByBearerToken(httpContext).OverrideByHeader(httpContext, "mcp-bearer-token");
        Oauth2GrantType = command.Oauth2GrantType.OverrideByHeader(httpContext, "mcp-oauth-2-grant-type");
        Oauth2TokenUrl = command.Oauth2TokenUrl.FallbackToOas(Oauth2GrantType, openApiDocument).OverrideByHeader(httpContext, "mcp-oauth-2-token-url");
        Oauth2ClientId = command.Oauth2ClientId.OverrideByHeader(httpContext, "mcp-oauth-2-client-id");
        Oauth2ClientSecret = command.Oauth2ClientSecret.OverrideByHeader(httpContext, "mcp-oauth-2-client-secret");
        Oauth2RefreshToken = command.Oauth2RefreshToken.OverrideByHeader(httpContext, "mcp-oauth-2-refresh-token");
        Oauth2Username = command.Oauth2Username.OverrideByHeader(httpContext, "mcp-oauth-2-username");
        Oauth2Password = command.Oauth2Password.OverrideByHeader(httpContext, "mcp-oauth-2-password");
    }
    
    
    private OpenApiAuthConfiguration(){}
    public static OpenApiAuthConfiguration EmptyForTest = new();
}


internal static class OpenApiAuthConfigurationExtensions
{
    internal static string? OverrideByHeader(this string? value, HttpContext? httpContext, string headerName)
    {
        if(httpContext?.Request.Headers.TryGetValue(headerName, out var values) ?? false)
            return values.FirstOrDefault();
        return value;
    }
    
    internal static string? OverrideByBearerToken(this string? value, HttpContext? httpContext)
    {
        if(httpContext?.Request.Headers.TryGetValue(HeaderNames.Authorization, out var values) ?? false)
            return values.FirstOrDefault()?.Split(" ")[1];
        return value;
    }

    
    internal static OAuth2GrantType? OverrideByHeader(this OAuth2GrantType? value, HttpContext? httpContext, string headerName)
    {
        if(httpContext?.Request.Headers.TryGetValue(headerName, out var values) ?? false)
            return Enum.Parse<OAuth2GrantType>(values.FirstOrDefault()!);
        return value;
    }
    
    internal static string? FallbackToOas(this string? value, OAuth2GrantType? oauth2GrantType, OpenApiDocument openApiDocument)
    {
        if (value != null)
            return value;
        
        var flows = openApiDocument.Components?.SecuritySchemes?.FirstOrDefault(s => s.Value.Type == SecuritySchemeType.OAuth2).Value?.Flows;
        return oauth2GrantType switch
        {
            null => null, //no grant type = no token url
            OAuth2GrantType.client_credentials => flows?.ClientCredentials?.TokenUrl?.ToString(),
            OAuth2GrantType.refresh_token => flows?.AuthorizationCode?.TokenUrl?.ToString(),
            OAuth2GrantType.password => flows?.Password?.TokenUrl?.ToString(),
            _ => throw new ArgumentException($"Unsupported flow {oauth2GrantType}")
        };
    }
}