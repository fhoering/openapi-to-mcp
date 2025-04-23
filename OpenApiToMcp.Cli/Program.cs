using DotMake.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.Protocol.Types;
using OpenApiToMcp.Cli;

try
{

    await Cli.RunAsync<Command>(args);
}
catch (Exception e)
{
    Console.Error.WriteLine("Unmanaged exception: "+e.Message);
}

[CliCommand(Description = "An on-the-fly OpenAPI MCP server")]
public class Command
{
    [CliArgument(Description = "Url of you OpenAPI specification", Required = true)]
    public string OpenApiUrl { get; set; }

    [CliOption(Description =
        "Server URL override. For example this allow to target a different server than the base one specified in the OpenAPI")]
    public string? ServerUrlOverride { get; set; } = null;

    [CliOption(Description = "Authentication method")]
    public AuthenticationMethod? AuthMethodOverride { get; set; } = null;

    [CliOption(Description = "The token endpoint (ex: https://petstore.swagger.io/oauth2/token)")]
    public string? TokenUrl { get; set; } = null;
    
    [CliOption(Description = "Client id (for client credentials auth")]
    public string? ClientId { get; set; } = null;
    
    [CliOption(Description = "Client secret (for client credentials auth")]
    public string? ClientSecret { get; set; } = null;
 
    public async Task RunAsync()
    {
        try
        {
            //scaffold
            var openApiDocument = await new OpenApiParser().Parse(OpenApiUrl);
            var endpoints = new EndpointsExtractor().Extract(openApiDocument);
            var tools = new McpToolsBuilder().BuildFromEndpoints(endpoints);

            //extract server url
            var serverUrl = ServerUrlOverride ?? openApiDocument.Servers.FirstOrDefault()?.Url;
            if (string.IsNullOrEmpty(serverUrl))
            {
                Console.Error.WriteLine($"No server URL specified in the OpenAPI document. Please use the {nameof(ServerUrlOverride)} option");
                return;
            }

            var tokenGenerator = IAuthTokenGenerator.Build(openApiDocument, this);
            var proxy = new McpToolsProxy(endpoints, serverUrl, tokenGenerator);
        
            //MCP server
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.AddConsole(consoleLogOptions =>
            {
                consoleLogOptions.LogToStandardErrorThreshold = LogLevel.Information;
            });
        
            builder.Services.AddMcpServer(serverOptions =>
                {
                    serverOptions.ServerInfo = new Implementation
                    {
                        Name = OpenApiUrl,
                        Version = "1.0",
                    };
                })
                .WithStdioServerTransport()
                .WithListToolsHandler((context, token) => ValueTask.FromResult(new ListToolsResult{Tools = tools}))
                .WithCallToolHandler(async (context, token) => await proxy.CallTool(context));
            var app = builder.Build();

            await app.RunAsync();
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("Unmanaged exception: "+e.Message);
        }
    }
}

public enum AuthenticationMethod
{
    None, ClientCredentials
}