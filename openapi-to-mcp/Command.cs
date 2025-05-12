using DotMake.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol.Types;

namespace OpenApiToMcp;


[CliCommand(Description = "An on-the-fly OpenAPI MCP server")]
public class Command
{
    [CliArgument(Description = "You OpenAPI specification (URL or file)")]
    public required string OpenApi { get; set; }

    [CliOption(Aliases = ["-t"], Description = "How the tool name should be computed")]
    public required ToolNamingStrategy ToolNamingStrategy { get; set; } = ToolNamingStrategy.extension_or_operationid_or_verbandpath;

    [CliOption(Aliases = ["-h"], Description = "Host override")]
    public string? HostOverride { get; set; } = null;
    
    [CliOption(Aliases = ["-b"], Description = "Bearer token")]
    public string? BearerToken { get; set; } = null;
    
    [CliOption(Aliases = ["-o2"], Description = "OAuth2 flow to be used")]
    public OAuth2GrantType? Oauth2GrantType { get; set; } = null;

    [CliOption(Aliases = ["-o2_tu"], Description = "OAuth2 token endpoint URL (override the one defined in your OpenAPI for your chosen OAuth2 flow)"),]
    public string? Oauth2TokenUrl { get; set; } = null;
    
    [CliOption(Aliases = ["-o2_ci"], Description = $"OAuth2 client id (for the {nameof(OAuth2GrantType.client_credentials)} grant_type)")]
    public string? Oauth2ClientId { get; set; } = null;
    
    [CliOption(Aliases = ["-o2_cs"], Description = $"OAuth2 client secret (for the {nameof(OAuth2GrantType.client_credentials)} grant_type)")]
    public string? Oauth2ClientSecret { get; set; } = null;

    [CliOption(Aliases = ["-o2_rt"], Description = $"OAuth2 refresh token (for the {nameof(OAuth2GrantType.refresh_token)} grant_type)")]
    public string? Oauth2RefreshToken { get; set; } = null;

    [CliOption(Aliases = ["-o2_un"], Description = $"OAuth2 username (for the {nameof(OAuth2GrantType.password)} grant_type)")]
    public string? Oauth2Username { get; set; } = null;
    
    [CliOption(Aliases = ["-o2_pw"], Description = $"OAuth2 password (for the {nameof(OAuth2GrantType.password)} grant_type)")]
    public string? Oauth2Password { get; set; } = null;
    
    [CliOption(Aliases = ["-i"], Description = $"MCP instruction to be advertised by the server")]
    public string? Instructions { get; set; } = null;
 
    public async Task RunAsync()
    {
        try
        {
            //Setup
            var (openApiDocument, diagnostic) = await new OpenApiParser().Parse(OpenApi, HostOverride, BearerToken, ToolNamingStrategy);
            diagnostic.Errors?.ToList().ForEach(e => Console.Error.WriteLine(e));
            diagnostic.Warnings?.ToList().ForEach(e => Console.Error.WriteLine(e));
            var serverUrl = openApiDocument.Servers?.FirstOrDefault()?.Url;
            if (string.IsNullOrEmpty(serverUrl) || !Uri.TryCreate(serverUrl, UriKind.Absolute, out _))
            {
                Console.Error.WriteLine($"The server URL cannot be inferred or is not absolute. Please use the {nameof(HostOverride)} option");
                return;
            }
            var auth = IAuthTokenGenerator.Build(openApiDocument, this);
            var proxy = new McpToolsProxy(openApiDocument, serverUrl, auth, ToolNamingStrategy);
            
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
                    serverOptions.ServerInstructions = Instructions ?? openApiDocument.Info?.McpInstructions();
                })
                .WithStdioServerTransport()
                .WithListToolsHandler((context, token) => proxy.ListTools())
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

public enum OAuth2GrantType
{
    client_credentials,
    refresh_token,
    password
}

public enum ToolNamingStrategy
{
    extension_or_operationid_or_verbandpath,
    extension,
    operationid,
    verbandpath
}