using DotMake.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol.Types;
using OpenApiToMcp;

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
    [CliArgument(Description = "You OpenAPI specification (URL or file)")]
    public required string OpenApi { get; set; }

    [CliOption(Aliases = ["-s"], Description = "Server host override")]
    public string? ServerHostOverride { get; set; } = null;
    
    [CliOption(Aliases = ["-b"], Description = "Bearer token")]
    public string? BearerToken { get; set; } = null;
    
    [CliOption(Aliases = ["-o2"], Description = "OAuth2 flow to be used")]
    public OAuth2Flow? Oauth2Flow { get; set; } = null;

    [CliOption(Aliases = ["-o2.tu"], Description = "OAuth2 token endpoint URL (override the one defined in your OpenAPI for your chosen OAuth2 flow)"),]
    public string? Oauth2TokenUrl { get; set; } = null;
    
    [CliOption(Aliases = ["-o2.ci"], Description = "OAuth2 client id")]
    public string? Oauth2ClientId { get; set; } = null;
    
    [CliOption(Aliases = ["-o2.cs"], Description = "OAuth2 client secret")]
    public string? Oauth2ClientSecret { get; set; } = null;
    
    [CliOption(Aliases = ["-o2.rt"], Description = "OAuth2 refresh token")]
    public string? Oauth2RefreshToken { get; set; } = null;
 
    public async Task RunAsync()
    {
        try
        {
            //scaffold
            var openApiDocument = await new OpenApiParser().Parse(OpenApi, ServerHostOverride);
            var endpoints = new EndpointsExtractor().Extract(openApiDocument);
            var tools = new McpToolsBuilder().BuildFromEndpoints(endpoints);

            //extract server url
            var serverUrl = openApiDocument.Servers.FirstOrDefault()?.Url;
            Console.Error.WriteLine("Server URL: " + serverUrl);
            if (string.IsNullOrEmpty(serverUrl) || !Uri.TryCreate(serverUrl, UriKind.Absolute, out _))
            {
                Console.Error.WriteLine($"The server URL cannot be inferred or is not absolute. Please use the {nameof(ServerHostOverride)} option");
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
                        Name = openApiDocument.Info?.Title ?? "API",
                        Version = openApiDocument.Info?.Version ?? "0.0",
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
            Console.Error.WriteLine("Error: "+e.Message);
        }
    }
}

public enum OAuth2Flow
{
    client_credentials,refresh_token,password,implici
}