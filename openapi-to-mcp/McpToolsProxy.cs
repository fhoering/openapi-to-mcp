using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Web;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;

namespace OpenApiToMcp;


public record EndpointTool(Tool tool, OperationType httpMethod, string path);

public class McpToolsProxy{
    private IEnumerable<EndpointTool> _endpointTools;
    private string _serverUrl;
    private IAuthTokenGenerator _authTokenGenerator;
    
    private const string BodyParameterName = "body";

    public McpToolsProxy(OpenApiDocument openApiDocument, string serverUrl, IAuthTokenGenerator tokenGenerator, ToolNamingStrategy toolNamingStrategy)
    {
        _endpointTools = ExtractTools(openApiDocument, toolNamingStrategy);
        _serverUrl = serverUrl;
        _authTokenGenerator = tokenGenerator;
    }

    public ValueTask<ListToolsResult> ListTools()
    {     
        var tools = _endpointTools
            .Select(e => e.tool)
            .ToList();

        return new ValueTask<ListToolsResult>(new ListToolsResult()
        {
            Tools = tools
        });
    }

    public async ValueTask<CallToolResponse> CallTool(RequestContext<CallToolRequestParams> context)
    {
        try
        {
            if(context.Params == null)
                throw new Exception("Shouldn't be called without a tool name");

            var token = await _authTokenGenerator.GetToken();
            var httpClient = new HttpClient()
                .WithOpenApiToMcpUserAgent()
                .WithBearerToken(token);
        
            var endpoint = _endpointTools.First(endpoint => endpoint.tool.Name == context.Params.Name);
            var uri = InjectPathParams(_serverUrl + endpoint.path, context.Params.Arguments);
            var method = HttpMethod.Parse(endpoint.httpMethod.ToString());
            var request = new HttpRequestMessage(method, uri);
            if (context.Params.Arguments != null && context.Params.Arguments.TryGetValue("body", out var body))
            {
                request.Content = new StringContent(body.ToString(), new MediaTypeHeaderValue("application/json"));
            }
            var response = await httpClient.SendAsync(request);
            var responseString = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                return CallError($"Called {method} {uri} with status {response.StatusCode}", responseString);

            //ok
            return CallOk($"Called {method} {uri} with status {response.StatusCode}", responseString);
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return CallError(e.Message);
        }
    }

    private CallToolResponse CallError(params string[] errors) =>
        new() { IsError = true, Content = errors.Select(error => new Content { Text = error, Type = "text" }).ToList() };

    private CallToolResponse CallOk(params string[] messages) =>
        new() { IsError = false, Content = messages.Select(message => new Content { Text = message, Type = "text" }).ToList() };
    
    private static IEnumerable<EndpointTool> ExtractTools(OpenApiDocument openApiDocument, ToolNamingStrategy toolNamingStrategy)
    {
        var endpoints = new List<EndpointTool>();
        foreach (var path in openApiDocument.Paths)
        {
            foreach (var (operationType, operation) in path.Value.Operations)
            {
                if(!operation.McpToolEnabled())
                    continue;

                var toolName = operation.McpToolName(path.Key, operationType, toolNamingStrategy);
                if (!OpenApiUtils.ValidToolName.IsMatch(toolName))
                    continue;

                var tool = new Tool
                {
                    Name =  toolName,
                    Description = operation.McpToolDescription(path.Value),
                    InputSchema = BuildToolInputSchema(operation, path.Value),
                    Annotations = new ToolAnnotations {
                        Title = operationType.ToString().ToUpper() + " " + path.Key,
                    }
                };
                
                endpoints.Add(new EndpointTool(tool, operationType, path.Key));
            }
        }

        return endpoints;
    }
    
    private static JsonElement BuildToolInputSchema(OpenApiOperation operation, OpenApiPathItem pathItem)
    {
        var required = new JsonArray();
        var properties = new JsonObject();
        
        //request body
        if (operation.RequestBody != null)
        {
            var body = operation.RequestBody;
            if (body.Content.TryGetValue("application/json", out var content) && content.Schema != null)
            {
                if(content.Schema != null)
                    content.Schema.Description ??= body.Description;
                var schema = content.Schema.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0);
                properties.Add(BodyParameterName, JsonNode.Parse(schema));
            
                if (body.Required)
                    required.Add(BodyParameterName);
            }
        }
        
        //path
        foreach (var param in pathItem.Parameters)
        {
            if (param.In == ParameterLocation.Cookie)
                continue;
            if (param.In == ParameterLocation.Header)
                continue;
            
            if(param.Schema != null)
                param.Schema.Description ??= param.Description;
            var schema = param.Schema.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0) ?? "{}";
            properties.Add(param.Name, JsonNode.Parse(schema));
           
            if(param.Required)
                required.Add(param.Name);
        }
        
        //operation
        foreach (var param in operation.Parameters)
        {
            if (param.In == ParameterLocation.Cookie)
                continue;
            if (param.In == ParameterLocation.Header)
                continue;
            
            if(param.Schema != null)
                param.Schema.Description ??= param.Description;
            var schema = param.Schema.SerializeAsJson(OpenApiSpecVersion.OpenApi3_0) ?? "{}";;
            properties.Add(param.Name, JsonNode.Parse(schema));
            if(param.Required)
                required.Add(param.Name);
        }
        
        
        //operation
        var inputs = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = required
        };
        return JsonDocument.Parse(inputs.ToJsonString()).RootElement;
    }
    
    private static string InjectPathParams(string path, IReadOnlyDictionary<string, JsonElement>? parameters)
    {
        if(parameters == null) return path;
 
        var pathParams = parameters.Where(kv => kv.Key != BodyParameterName && path.Contains($"{{{kv.Key}}}")).ToList();
        var queryParams = parameters.Where(kv => kv.Key != BodyParameterName && !path.Contains($"{{{kv.Key}}}")).ToList();
        
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