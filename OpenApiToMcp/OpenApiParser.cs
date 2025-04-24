using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Writers;

namespace OpenApiToMcp;

public class OpenApiParser
{
    public async Task<OpenApiDocument> Parse(string openapiFileOrUrl, string? hostOverride)
    {
        //if starts with http, treat as url
        string? openApiDocumentAsString;
        string host = null;
        if (openapiFileOrUrl.StartsWith("http"))
        {
            host = new Uri(openapiFileOrUrl, UriKind.Absolute).GetLeftPart(UriPartial.Authority);
            var httpClient = new HttpClient();
            var stream = await httpClient.GetStreamAsync(openapiFileOrUrl);
            openApiDocumentAsString = await new StreamReader(stream).ReadToEndAsync();
        }
        else
        {
            if (!File.Exists(openapiFileOrUrl))
                throw new ArgumentException($"Openapi file does not exist: {openapiFileOrUrl}");
            openApiDocumentAsString =  await File.ReadAllTextAsync(openapiFileOrUrl);
        }
        
        var openApiDocumentWithRef = new OpenApiStringReader().Read(openApiDocumentAsString, out var diagnostic);
        var openApiDocumentWithoutRefAsString = new StringWriter();
        openApiDocumentWithRef.SerializeAsV3(new OpenApiJsonWriter(
            openApiDocumentWithoutRefAsString,
            new OpenApiWriterSettings() { InlineLocalReferences = true }
        ));
        var openApiDocument = new OpenApiStringReader().Read(openApiDocumentWithoutRefAsString.ToString(), out var _);
        
        //until https://github.com/microsoft/OpenAPI.NET/pull/2278 is released
        openApiDocument.Components.SecuritySchemes = openApiDocumentWithRef.Components.SecuritySchemes;
        
        //handle relative servers url and inject host override
        foreach (var server in openApiDocument.Servers)
        {
            if (Uri.TryCreate(server.Url, UriKind.Relative, out var _))
                server.Url = (hostOverride ?? host) + server.Url;
            if (Uri.TryCreate(server.Url, UriKind.Absolute, out var abs) && hostOverride != null)
                server.Url = hostOverride + abs.PathAndQuery+abs.Fragment;
        }
        if(!openApiDocument.Servers.Any())
            openApiDocument.Servers.Add(new OpenApiServer{Url = hostOverride ?? host});

        return openApiDocument;
    }

    private async Task<string> Load(string openapi)
    {
        if (File.Exists(openapi))
            return await File.ReadAllTextAsync("path/to/your/file.txt");
        
        //fallback to http
        var httpClient = new HttpClient();
        var stream = await httpClient.GetStreamAsync(openapi);
        return await new StreamReader(stream).ReadToEndAsync();
    }
}