using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using NUnit.Framework;

namespace OpenApiToMcp.Tests;

public class SdtIoE2ETest
{
    
    [Test]
    public async Task ShouldLoadRemoteOpenApi_WithPetStore() 
    { 
        //start the http mcp server and client
        var rootDir = Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.FullName;
        var client = await McpClientFactory.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            {
                WorkingDirectory = rootDir+"/openapi-to-mcp",
                Command = "dotnet",
                Arguments = ["run", "--no-build", "https://petstore3.swagger.io/api/v3/openapi.json", "sdtio"]
            }
        ));

        //list tools
        var tools = await client.ListToolsAsync();
        Assert.That(tools.Count, Is.GreaterThan(0));
        
        var getPetByIdTool = tools.FirstOrDefault(t => t.Name == "getPetById");
        Assert.That(getPetByIdTool, Is.Not.Null);
        Assert.That(getPetByIdTool.Description, Is.EqualTo("Returns a single pet."));
    }
    
    [Test]
    public async Task ShouldProxyBearerToken_WithPostmanEcho() 
    { 
        //start the http mcp server and client
        var rootDir = Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.FullName;
        var client = await McpClientFactory.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
            {
                WorkingDirectory = rootDir+"/openapi-to-mcp",
                Command = "dotnet",
                Arguments = ["run", "--no-build", "../openapi-to-mcp.Tests/resources/postmanecho.oas.yaml", "sdtio", "--bearer-token", "token_from_cli_option"]
            }
        ));
        
        //check instructions
        Assert.That(client.ServerInstructions, Is.EqualTo("echo echo echo..."));

        //list tools
        var tools = await client.ListToolsAsync();
        Assert.That(tools.Select(t => t.Name), Is.EquivalentTo(new[]{"Echo"}));

        //call echo tool
        var echoResponse = await client.CallToolAsync("Echo", arguments: new Dictionary<string, object?>()
        {
            { "foo", JsonSerializer.SerializeToElement("bar") }
        });

        Assert.Multiple(() =>
        {
            Assert.That(echoResponse, Is.Not.Null);
            Assert.That(echoResponse.IsError, Is.False);
            Assert.That(echoResponse.Content.Count, Is.EqualTo(2));
            Assert.That(echoResponse.Content[0].Text, Is.EqualTo("Called GET https://postman-echo.com/get?foo=bar with status OK"));
            
            using var jsonDoc = JsonDocument.Parse(echoResponse.Content[1].Text!);
            var headers = jsonDoc.RootElement.GetProperty("headers");
            Assert.That(headers.GetProperty("user-agent").ToString(), Does.StartWith("openapi-to-mcp/"));
            Assert.That(headers.GetProperty("authorization").ToString(), Is.EqualTo("Bearer token_from_cli_option"));
        });
    }
}
