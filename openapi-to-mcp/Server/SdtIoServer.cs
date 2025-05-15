using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ModelContextProtocol.Protocol.Types;
using OpenApiToMcp.Auth;
using OpenApiToMcp.OpenApi;

namespace OpenApiToMcp.Server;

public class SdtIoServer(OpenApiDocument openApiDocument, List<EndpointTool> endpointTools, Command command)
{
    public IHost Build()
    {
        var appBuilder = Host.CreateApplicationBuilder();
        appBuilder.Logging
            .SetMinimumLevel(command.Verbose ? LogLevel.Debug : LogLevel.Information)
            .AddConsole();

        appBuilder.Services
            .AddSingleton<OpenApiDocument>(_ => openApiDocument)
            .AddSingleton<Command>(_ => command)
            .AddSingleton<List<EndpointTool>>(_ => endpointTools)
            .AddSingleton<OpenApiAuthConfiguration>()
            .AddSingleton<OpenApiToolsProxy>()
            .AddMcpServer(serverOptions =>
            {
                serverOptions.ServerInfo = new Implementation
                {
                    Name = openApiDocument.Info?.Title ?? "API",
                    Version = openApiDocument.Info?.Version ?? "0.0",
                };
                serverOptions.ServerInstructions = command.Instructions ?? openApiDocument.Info?.McpInstructions();
            })
            .WithListToolsHandler((context, token) => context.Services!.GetService<OpenApiToolsProxy>()!.ListTools(context,token))
            .WithCallToolHandler(async (context, token) => await context.Services!.GetService<OpenApiToolsProxy>()!.CallTool(context, token))
            .WithStdioServerTransport();
        
        return appBuilder.Build();
    }
}