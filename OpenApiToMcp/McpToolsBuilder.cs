using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.Protocol.Types;

namespace OpenApiToMcp;

public class McpToolsBuilder
{
    public const string BodyParameterName = "body";
    public List<Tool> BuildFromEndpoints(IEnumerable<Endpoint> endpoints)
    {
        return endpoints.Select(endpoint => new Tool
        {
            Description = endpoint.operation.Description ?? endpoint.pathItem.Description,
            Name = endpoint.toolName,
            InputSchema = BuildToolParameters(endpoint),
            Annotations = new ToolAnnotations()
            {
                Title = endpoint.httpMethod.ToString().ToUpper()+" "+endpoint.path
            }
        }).ToList();
    }
    
    private static JsonElement BuildToolParameters(Endpoint endpoint)
    {
        var required = new JsonArray();
        var properties = new JsonObject();
        
        
        //request body
        if (endpoint.operation.RequestBody != null)
        {
            var body = endpoint.operation.RequestBody;
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
        foreach (var param in endpoint.pathItem.Parameters)
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
        foreach (var param in endpoint.operation.Parameters)
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
}