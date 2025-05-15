using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol.Transport;
using NUnit.Framework;

namespace OpenApiToMcp.Tests;

public class HttpE2ETest
{
    private Uri _petStoreUri;
    private Uri _echoUri;
    
    [OneTimeSetUp]
    public async Task StartServers()
    {
        var port = FindAvailablePort();
        _ = Program.Main(["https://petstore3.swagger.io/api/v3/openapi.json", "http", "--server-port", $"{port}"]);
        _petStoreUri = new Uri($"http://localhost:{port}");
        
        port = FindAvailablePort();
        _ = Program.Main(["resources/postmanecho.oas.yaml", "http", "--server-port", $"{port}", "--bearer-token", "token_from_cli_option"]);
        _echoUri = new Uri($"http://localhost:{port}");
        
        await Task.Delay(5000);
    }
    
    [Test]
    public async Task ShouldLoadRemoteOpenApi_WithPetStore() 
    { 
        //create an http mcp client
        var client = await McpClientFactory.CreateAsync(new SseClientTransport(new SseClientTransportOptions
        {
            Endpoint = _petStoreUri,
            UseStreamableHttp = true,
        }));

        //list tools
        var tools = await client.ListToolsAsync();
        Assert.That(tools.Count, Is.GreaterThan(0));
        
        var getPetByIdTool = tools.FirstOrDefault(t => t.Name == "getPetById");
        Assert.That(getPetByIdTool, Is.Not.Null);
        Assert.That(getPetByIdTool.Description, Is.EqualTo("Returns a single pet."));
    }
    
    [TestCase("Authorization", "Bearer token_from_aut_header", "token_from_aut_header")]
    [TestCase("mcp-bearer-token", "token_from_mcp_header", "token_from_mcp_header")]
    [TestCase("Unrelated", "Unrelated", "token_from_cli_option")]
    public async Task ShouldProxyBearerToken_WithPostmanEcho(string headerKey, string headerValue, string expectedProxiedToken) 
    { 
       //create an http mcp client
       var client = await McpClientFactory.CreateAsync(new SseClientTransport(new SseClientTransportOptions
       {
           Endpoint = _echoUri,
           UseStreamableHttp = true,
           AdditionalHeaders = new Dictionary<string, string> { {headerKey, headerValue} }
       }));

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
           Assert.That(headers.GetProperty("authorization").ToString(), Is.EqualTo("Bearer "+expectedProxiedToken));
       });
   }

   private static int FindAvailablePort()
   {
       var listener = new TcpListener(IPAddress.Loopback, 0);
       listener.Start();
       var port = ((IPEndPoint)listener.LocalEndpoint).Port;
       listener.Stop();
       return port;
   }
}
