using DotMake.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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

    [CliOption(Aliases = ["-s"], Description = "Server URL override")]
    public string? ServerUrlOverride { get; set; } = null;
    
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

public enum OAuth2Flow
{
    client_credentials,refresh_token,password,implici
}