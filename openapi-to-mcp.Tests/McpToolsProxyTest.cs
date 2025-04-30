using System.Text.Json;
using ModelContextProtocol.Protocol.Messages;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using NUnit.Framework;
using OpenApiToMcp;

namespace openapi_to_mcp.Tests;

public class McpToolsProxyTest
{
    [Test]
    public async Task ListTools_ShouldListValidEndpointsOnly()
    {
        var (openApiDocument, diagnostic) =
            await new OpenApiParser().Parse("resources/invalid_tool_names.oas.yaml", hostOverride: null, bearerToken: null);
        var proxy = new McpToolsProxy(openApiDocument, "https://example.com", new NoAuthTokenGenerator());
        var tools = await proxy.ListTools();
        Assert.That(tools, Is.Not.Null);
        var toolNamesAndDescriptions = tools.Tools.Select(t => (t.Name, t.Description));
        Assert.That(toolNamesAndDescriptions, Is.EquivalentTo(new[]
        {
          ("validOperationId", "Some description"), 
          ("Post_valid-tool-name_test", "Some description")
        }));
    }
    
    [Test]
    public async Task ListTools_ShouldUseOpenApiExtensions()
    {
      var (openApiDocument, diagnostic) =
        await new OpenApiParser().Parse("resources/extensions.oas.yaml", hostOverride: null, bearerToken: null);
      var proxy = new McpToolsProxy(openApiDocument, "https://example.com", new NoAuthTokenGenerator());
      var tools = await proxy.ListTools();
      Assert.That(tools, Is.Not.Null);
      var toolNamesAndDescriptions = tools.Tools.Select(t => (t.Name, t.Description));
      Assert.That(toolNamesAndDescriptions, Is.EquivalentTo(new[]
      {
        ("GreetdOld", "Greet users"), 
        ("GreetNew", "Happily greet users")
      }));
    }

    [Test]
    public async Task Petstore_Example_addPet()
    {
        var (openApiDocument, diagnostic) =
            await new OpenApiParser().Parse("resources/petstore3.oas.yaml", hostOverride: null, bearerToken: null);
        var proxy = new McpToolsProxy(openApiDocument, "https://petstore3.swagger.io/api/v3", new NoAuthTokenGenerator());

        //List tools
        var tools = await proxy.ListTools();
        Assert.That(tools, Is.Not.Null);
        var addPetTool = tools.Tools.FirstOrDefault(t => t.Name == "addPet");
        Assert.That(addPetTool, Is.Not.Null);
        Assert.That(addPetTool.Description, Is.EqualTo("Add a new pet to the store."));
        Assert.That(addPetTool.InputSchema.ToString(), Is.EqualTo("""
          {
            "type": "object",
            "properties": {
              "body": {
                "required": [
                  "name",
                  "photoUrls"
                ],
                "type": "object",
                "properties": {
                  "id": {
                    "type": "integer",
                    "format": "int64",
                    "example": 10
                  },
                  "name": {
                    "type": "string",
                    "example": "doggie"
                  },
                  "category": {
                    "type": "object",
                    "properties": {
                      "id": {
                        "type": "integer",
                        "format": "int64",
                        "example": 1
                      },
                      "name": {
                        "type": "string",
                        "example": "Dogs"
                      }
                    },
                    "xml": {
                      "name": "category"
                    }
                  },
                  "photoUrls": {
                    "type": "array",
                    "items": {
                      "type": "string",
                      "xml": {
                        "name": "photoUrl"
                      }
                    },
                    "xml": {
                      "wrapped": true
                    }
                  },
                  "tags": {
                    "type": "array",
                    "items": {
                      "type": "object",
                      "properties": {
                        "id": {
                          "type": "integer",
                          "format": "int64"
                        },
                        "name": {
                          "type": "string"
                        }
                      },
                      "xml": {
                        "name": "tag"
                      }
                    },
                    "xml": {
                      "wrapped": true
                    }
                  },
                  "status": {
                    "enum": [
                      "available",
                      "pending",
                      "sold"
                    ],
                    "type": "string",
                    "description": "pet status in the store"
                  }
                },
                "description": "Create a new pet in the store",
                "xml": {
                  "name": "pet"
                }
              }
            },
            "required": [
              "body"
            ]
          }
          """.AsMinifiedJson()));
        var getPetByIdTool = tools.Tools.FirstOrDefault(t => t.Name == "getPetById");
        Assert.That(getPetByIdTool, Is.Not.Null);
        Assert.That(getPetByIdTool.Description, Is.EqualTo("Returns a single pet."));
        Assert.That(getPetByIdTool.InputSchema.ToString(), Is.EqualTo("""
          {
            "type": "object",
            "properties": {
              "petId": {
                "type": "integer",
                "description": "ID of pet to return",
                "format": "int64"
              }
            },
            "required": [
              "petId"
            ]
          }
          """.AsMinifiedJson()));

        //Create a pet and then fetch it
        var addPetResponse = await proxy.CallTool(new RequestContext<CallToolRequestParams>(new MockServer())
        {
            Params = new CallToolRequestParams
            {
                Name = "addPet",
                Arguments = new Dictionary<string, JsonElement>
                {
                    { "body", JsonDocument.Parse("""{"id": 111111,"name":"Bob"}""").RootElement }
                }
            }
        });
        Assert.That(addPetResponse, Is.Not.Null);
        Assert.That(addPetResponse.IsError, Is.False);
        Assert.That(addPetResponse.Content.Count, Is.EqualTo(2));
        Assert.That(addPetResponse.Content[0].Text,
            Is.EqualTo("Called POST https://petstore3.swagger.io/api/v3/pet with status OK"));
        Assert.That(addPetResponse.Content[1].Text, Is.EqualTo("""
         {
           "id": 111111,
           "name": "Bob",
           "photoUrls": [],
           "tags": []
         }
         """.AsMinifiedJson()));
        var getPetResponse = await proxy.CallTool(new RequestContext<CallToolRequestParams>(new MockServer())
        {
            Params = new CallToolRequestParams
            {
                Name = "getPetById",
                Arguments = new Dictionary<string, JsonElement>
                {
                    { "petId", JsonSerializer.SerializeToElement(111111) }
                }
            }
        });
        Assert.That(getPetResponse, Is.Not.Null);
        Assert.That(getPetResponse.IsError, Is.False);
        Assert.That(getPetResponse.Content.Count, Is.EqualTo(2));
        Assert.That(getPetResponse.Content[0].Text,
            Is.EqualTo("Called GET https://petstore3.swagger.io/api/v3/pet/111111 with status OK"));
        Assert.That(getPetResponse.Content[1].Text, Is.EqualTo("""
         {
           "id": 111111,
           "name": "Bob",
           "photoUrls": [],
           "tags": []
         }
         """.AsMinifiedJson()));
    }
}

class MockServer : IMcpServer
{
    public ValueTask DisposeAsync() => throw new NotImplementedException();
    public Task<JsonRpcResponse> SendRequestAsync(JsonRpcRequest request, CancellationToken ct = new()) => throw new NotImplementedException();
    public Task SendMessageAsync(JsonRpcMessage message, CancellationToken ct = new ()) => throw new NotImplementedException();
    public IAsyncDisposable RegisterNotificationHandler(string method, Func<JsonRpcNotification, CancellationToken, ValueTask> handler) => throw new NotImplementedException();
    public Task RunAsync(CancellationToken ct = new ()) => throw new NotImplementedException();

    public ClientCapabilities? ClientCapabilities { get; }
    public Implementation? ClientInfo { get; }
    public McpServerOptions ServerOptions { get; } = new();
    public IServiceProvider? Services { get; }
    public LoggingLevel? LoggingLevel { get; }
}

public static class JsonStringExtension
{
    public static string AsMinifiedJson(this string jsonAsString)
    {
        using var doc = JsonDocument.Parse(jsonAsString);
        return JsonSerializer.Serialize(doc.RootElement);
    }
}