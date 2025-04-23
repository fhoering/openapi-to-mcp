using System.Net.Http.Headers;
using System.Text.Json;
using System.Web;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace OpenApiToMcp.Cli;

public class McpToolsProxy(IEnumerable<Endpoint> endpoints, string serverUrl, IAuthTokenGenerator authTokenGenerator)
{
    public async ValueTask<CallToolResponse> CallTool(RequestContext<CallToolRequestParams> context)
    {
        try
        {
            if(context.Params == null)
                throw new Exception("Shouldn't be called without a tool name");
            var token = await authTokenGenerator.GetToken();
            var httpClient = new HttpClient();
            if (token != null)
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        
            var endpoint = endpoints.First(endpoint => endpoint.toolName == context.Params.Name);
            var uri = InjectPathParams(serverUrl + endpoint.path, context.Params.Arguments);
            var method = HttpMethod.Parse(endpoint.httpMethod.ToString());
            var request = new HttpRequestMessage(method, uri);
            if (context.Params.Arguments.ContainsKey("body"))
            {
                request.Content = new StringContent(context.Params.Arguments["body"].ToString(), new MediaTypeHeaderValue("application/json"));
            }
            var response = await httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return CallUtils.CallError("Called "+uri, responseString);

            //ok
            return CallUtils.CallOk("Called "+uri, responseString);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return CallUtils.CallError(e.Message);
        }
    }
    
    private static string InjectPathParams(string path, IReadOnlyDictionary<string, JsonElement>? parameters)
    {
        if(parameters == null) return path;
 
        var pathParams = parameters.Where(kv => kv.Key != McpToolsBuilder.BodyParameterName && path.Contains($"{{{kv.Key}}}")).ToList();
        var queryParams = parameters.Where(kv => kv.Key != McpToolsBuilder.BodyParameterName && !path.Contains($"{{{kv.Key}}}")).ToList();
        
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

public static class CallUtils
{
    public static CallToolResponse CallError(params string[] errors) =>
        new() { IsError = true, Content = errors.Select(error => new Content { Text = error, Type = "text" }).ToList() };

    public static CallToolResponse CallOk(params string[] messages) =>
        new() { IsError = false, Content = messages.Select(message => new Content { Text = message, Type = "text" }).ToList() };
}