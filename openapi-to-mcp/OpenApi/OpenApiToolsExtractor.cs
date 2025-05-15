using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.Protocol.Types;

namespace OpenApiToMcp.OpenApi;

public record EndpointTool(Tool tool, OperationType httpMethod, string absolutePath);

public class OpenApiToolsExtractor
{
    public const string BodyParameterName = "body";
    
    public List<EndpointTool> ExtractEndpointTools(OpenApiDocument openApiDocument, ToolNamingStrategy toolNamingStrategy)
    {
        var endpointTools = new List<EndpointTool>();
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

                var absolutePath = openApiDocument.Servers?.FirstOrDefault()?.Url + path.Key;
                endpointTools.Add(new EndpointTool(tool, operationType, absolutePath));
            }
        }
        
        return endpointTools;
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
}