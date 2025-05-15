using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.Protocol.Types;
using OpenApiToMcp.Auth;
using OpenApiToMcp.OpenApi;

namespace OpenApiToMcp.Server;

public class HttpServer(OpenApiDocument openApiDocument, List<EndpointTool> endpointTools, Command command)
{
    public IHost Build()
    {
        var appBuilder = WebApplication.CreateBuilder();
        if (command.ServerPort.HasValue) {
            appBuilder.WebHost.ConfigureKestrel(options => { options.ListenAnyIP(command.ServerPort.Value); });
        }
        
        appBuilder.Logging
            .SetMinimumLevel(command.Verbose ? LogLevel.Debug : LogLevel.Information)
            .AddConsole();
        
        appBuilder.Services
            .AddMcpServer(serverOptions =>
            {
                serverOptions.ServerInfo = new Implementation
                {
                    Name = openApiDocument.Info?.Title ?? "API",
                    Version = openApiDocument.Info?.Version ?? "0.0",
                };
                serverOptions.ServerInstructions = command.Instructions ?? openApiDocument.Info?.McpInstructions();
            })
            .WithHttpTransport(options =>
            {
                options.ConfigureSessionOptions = (httpContext, mcpServerOptions, cancellationToken) =>
                {
                    mcpServerOptions.Capabilities ??= new ServerCapabilities();
                    mcpServerOptions.Capabilities.Tools ??= new ToolsCapability();

                    var authConfig = new OpenApiAuthConfiguration(command, openApiDocument, httpContext);
                    var proxy = new OpenApiToolsProxy(endpointTools, authConfig, httpContext.RequestServices.GetService<ILogger<OpenApiToolsProxy>>()!);
                    mcpServerOptions.Capabilities!.Tools!.ListToolsHandler = proxy.ListTools;
                    mcpServerOptions.Capabilities!.Tools!.CallToolHandler = proxy.CallTool;
                    return Task.CompletedTask;
                };
            });
        
        var app = appBuilder.Build();
        app.MapMcp();
        return app;
    }
}