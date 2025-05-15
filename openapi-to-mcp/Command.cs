using DotMake.CommandLine;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Microsoft.OpenApi.Readers;
using OpenApiToMcp.OpenApi;
using OpenApiToMcp.Server;

namespace OpenApiToMcp;

[CliCommand(Description = "An on-the-fly OpenAPI MCP server", ShortFormAutoGenerate = false)]
public class Command
{
    [CliArgument(Description = "You OpenAPI specification (URL or file)")]
    public required string OpenApi { get; set; }

    [CliArgument(Description = "The MCP server transport")]
    public required Transport Transport { get; set; } = Transport.sdtio;
    
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
    
    [CliOption(Aliases = ["-i"], Description = "MCP instruction to be advertised by the server")]
    public string? Instructions { get; set; } = null;
    
    [CliOption(Aliases = ["-sp"], Description = "(http transport only)")]
    public int? ServerPort { get; set; }
    
    [CliOption(Description = "Log more info (in sdterr)")]
    public bool Verbose { get; set; } = false;
 
    public async Task RunAsync()
    {
        //Load openapi document and check integrity of parameters
        var (openApiDocument, diagnostic) = await new OpenApiParser().Parse(OpenApi, HostOverride, BearerToken, ToolNamingStrategy);
        if (!AreParametersAndOpenApiOk(openApiDocument, diagnostic))
            return;
        
        //extract tools
        var endpointTools = new OpenApiToolsExtractor().ExtractEndpointTools(openApiDocument, ToolNamingStrategy);

        //start MCP server
        var app = Transport switch
        {
            Transport.sdtio => new SdtIoServer(openApiDocument, endpointTools, this).Build(),
            Transport.http => new HttpServer(openApiDocument, endpointTools, this).Build(),
            _ => throw new ArgumentOutOfRangeException(nameof(OpenApiToMcp.Transport))
        };

        await app.RunAsync();
    }

    private bool AreParametersAndOpenApiOk(OpenApiDocument openApiDocument, OpenApiDiagnostic diagnostic)
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder
            .SetMinimumLevel(Verbose ? LogLevel.Debug : LogLevel.Information)
            .AddConsole(options => {
                options.LogToStandardErrorThreshold = Transport == Transport.sdtio ? LogLevel.Debug : LogLevel.Error;
            }));
        var logger = loggerFactory.CreateLogger<Command>();
        
        diagnostic.Errors?.ToList().ForEach(e => logger.Log(LogLevel.Error, e.Message));
        diagnostic.Warnings?.ToList().ForEach(e => logger.Log(LogLevel.Debug, e.Message));
        var serverUrl = openApiDocument.Servers?.FirstOrDefault()?.Url;
        if (string.IsNullOrEmpty(serverUrl) || !Uri.TryCreate(serverUrl, UriKind.Absolute, out _))
        {
            logger.Log(LogLevel.Error, $"The server URL cannot be inferred or is not absolute. Please use the {nameof(HostOverride)} option");
            return false;
        }
        
        //TODO: check consistency of auth params
        return true;
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

public enum Transport
{
    sdtio, http
}