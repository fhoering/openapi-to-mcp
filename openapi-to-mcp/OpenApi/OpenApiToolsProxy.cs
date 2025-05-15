using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using OpenApiToMcp.Auth;

namespace OpenApiToMcp.OpenApi;

public class OpenApiToolsProxy(List<EndpointTool> endpointTools, OpenApiAuthConfiguration authConfig, ILogger<OpenApiToolsProxy> logger)
{
    private readonly HttpClient _httpClient = new HttpClient(new OpenApiAuthHandler(authConfig)).WithOpenApiToMcpUserAgent();
    public ValueTask<ListToolsResult> ListTools(RequestContext<ListToolsRequestParams> requestContext, CancellationToken cancellationToken)
    {
        var tools = endpointTools.Select(e => e.tool).ToList();
        
        logger.Log(LogLevel.Debug, $"ListTools: {tools.Count}");
        tools.ForEach(t => logger.Log(LogLevel.Debug, t.Name));

        return new ValueTask<ListToolsResult>(new ListToolsResult { Tools = tools });
    }

    public async ValueTask<CallToolResponse> CallTool(RequestContext<CallToolRequestParams> context, CancellationToken cancellationToken)
    {
        try
        {
            logger.Log(LogLevel.Debug, $"CallTool: {context.Params?.Name}");
            if(context.Params == null)
                throw new Exception("Shouldn't be called without a tool name");
            
            var endpoint = endpointTools.First(endpoint => endpoint.tool.Name == context.Params.Name);
            var uri = InjectPathParams(endpoint.absolutePath, context.Params.Arguments);
            var method = HttpMethod.Parse(endpoint.httpMethod.ToString());
            var request = new HttpRequestMessage(method, uri);
            if (context.Params.Arguments != null && context.Params.Arguments.TryGetValue("body", out var body))
            {
                request.Content = new StringContent(body.ToString(), new MediaTypeHeaderValue("application/json"));
            }
            
            logger.Log(LogLevel.Debug, $"Calling: {method} {uri}");
            var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseString = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.Log(LogLevel.Debug, $"Response status: {response.StatusCode}");
            logger.Log(LogLevel.Debug, $"Response: {responseString}");
            
            if (!response.IsSuccessStatusCode)
                return CallError($"Called {method} {uri} with status {response.StatusCode}", responseString);

            //ok
            return CallOk($"Called {method} {uri} with status {response.StatusCode}", responseString);
        }
        catch (Exception e)
        {
            logger.Log(LogLevel.Error, e.Message);
            return CallError(e.Message);
        }
    }

    private CallToolResponse CallError(params string[] errors) =>
        new() { IsError = true, Content = errors.Select(error => new Content { Text = error, Type = "text" }).ToList() };

    private CallToolResponse CallOk(params string[] messages) =>
        new() { IsError = false, Content = messages.Select(message => new Content { Text = message, Type = "text" }).ToList() };
    
    private static string InjectPathParams(string path, IReadOnlyDictionary<string, JsonElement>? parameters)
    {
        if(parameters == null) return path;
 
        var pathParams = parameters.Where(kv => kv.Key != OpenApiToolsExtractor.BodyParameterName && path.Contains($"{{{kv.Key}}}")).ToList();
        var queryParams = parameters.Where(kv => kv.Key != OpenApiToolsExtractor.BodyParameterName && !path.Contains($"{{{kv.Key}}}")).ToList();
        
        //path
        foreach (var kvp in pathParams)
        {
            var encodedValue = Uri.EscapeDataString(kvp.Value.ValueKind == JsonValueKind.String ? kvp.Value.GetString() ?? "" : kvp.Value.GetRawText());
            path = path.Replace($"{{{kvp.Key}}}", encodedValue);
        }
        
        //query
        var builder = new UriBuilder(path);
        var query = HttpUtility.ParseQueryString(string.Empty);
        foreach (var kvp in queryParams)
        {
            if(kvp.Value.ValueKind == JsonValueKind.Null)
                continue;
            query[kvp.Key] = Uri.EscapeDataString(kvp.Value.ValueKind == JsonValueKind.String ? kvp.Value.GetString() ?? "" : kvp.Value.GetRawText());
        }

        builder.Query = query.ToString();
        return builder.Uri.ToString();
    }
}