using Microsoft.OpenApi.Models;

namespace OpenApiToMcp;

public class EndpointsExtractor
{
    public IEnumerable<Endpoint> Extract(OpenApiDocument openApiDocument)
    {
        var commonPrefix = LongestCommonPrefix(openApiDocument.Paths.Keys);
        
        var endpoints = from path in openApiDocument.Paths
            from operation in path.Value.Operations
            select new Endpoint(
                BuildToolName(operation, path, commonPrefix),
                path.Key, path.Value, operation.Key, operation.Value);

        return endpoints.Where(e => e.toolName != "" && e.toolName.Length <= 64);
    }

    private string BuildToolName(
        KeyValuePair<OperationType, OpenApiOperation> operation,
        KeyValuePair<string, OpenApiPathItem> path,
        string commonPrefix) => 
        operation.Value.OperationId ?? operation.Key+path.Key.Remove(0, commonPrefix.Length).Replace("{", "").Replace("}", "").Replace("/", "_");

    private string LongestCommonPrefix(IEnumerable<string> strings)
    {
        if (!strings.Any())
            return string.Empty;

        var prefix = strings.First();
        foreach (var str in strings)
            while (!str.StartsWith(prefix))
                prefix = prefix.Substring(0, prefix.Length-1);

        return prefix;
    }
}
public record Endpoint(
    string toolName,
    string path,
    OpenApiPathItem pathItem,
    OperationType httpMethod,
    OpenApiOperation operation);
