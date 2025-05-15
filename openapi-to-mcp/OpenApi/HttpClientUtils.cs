using System.Net.Http.Headers;
using System.Reflection;

namespace OpenApiToMcp;

public static class HttpClientUtils
{
    public static HttpClient WithOpenApiToMcpUserAgent(this HttpClient httpClient)
    {
        var version = Assembly.GetAssembly(typeof(HttpClientUtils))?.GetName().Version;
        httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("openapi-to-mcp", version?.ToString()));
        return httpClient;
    }

    public static HttpClient WithBearerToken(this HttpClient httpClient, string? bearerToken)
    {
        if(bearerToken != null){
            httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return httpClient;
    }
}