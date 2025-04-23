using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using Microsoft.OpenApi.Writers;

namespace OpenApiToMcp.Cli;

public class OpenApiParser
{
    public async Task<OpenApiDocument> Parse(string openApiUrl)
    {
        //load and resolve refs
        var httpClient = new HttpClient();
        var stream = await httpClient.GetStreamAsync(openApiUrl);
        var openApiDocumentAsString = await new StreamReader(stream).ReadToEndAsync();
        var openApiDocumentWithRef = new OpenApiStringReader().Read(openApiDocumentAsString, out var diagnostic);
        var openApiDocumentWithoutRefAsString = new StringWriter();
        openApiDocumentWithRef.SerializeAsV3(new OpenApiJsonWriter(
            openApiDocumentWithoutRefAsString,
            new OpenApiWriterSettings() { InlineLocalReferences = true }
        ));
        var openApiDocument = new OpenApiStringReader().Read(openApiDocumentWithoutRefAsString.ToString(), out var _);
        
        //until https://github.com/microsoft/OpenAPI.NET/pull/2278 is released
        openApiDocument.Components.SecuritySchemes = openApiDocumentWithRef.Components.SecuritySchemes;
        
        //handle relative servers
        if (openApiDocument.Servers.Any())
        {
            foreach (var server in openApiDocument.Servers)
            {
                if (string.IsNullOrEmpty(server.Url) || !new Uri(server.Url, UriKind.RelativeOrAbsolute).IsAbsoluteUri)
                {
                    server.Url = new Uri(openApiUrl, UriKind.Absolute).GetLeftPart(UriPartial.Authority) + (server.Url ?? "");
                }
            }
        }
        return openApiDocument;
    }
}