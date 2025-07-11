﻿using System.Collections.ObjectModel;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Moq;
using NUnit.Framework;
using OpenApiToMcp.Auth;
using OpenApiToMcp.OpenApi;

namespace OpenApiToMcp.Tests.OpenApi;

public class OpenApiToolsProxyTest
{
    [Test]
    public async Task Petstore_Example_addPet()
    {
        var (openApiDocument, diagnostic) =
            await new OpenApiParser().Parse("resources/petstore3.oas.yaml", hostOverride: "https://petstore3.swagger.io", bearerToken: null,  toolNamingStrategy: default);
        var tools = new OpenApiToolsExtractor().ExtractEndpointTools(openApiDocument, toolNamingStrategy: default);
        var proxy = new OpenApiToolsProxy(tools, OpenApiAuthConfiguration.EmptyForTest, new Logger<OpenApiToolsProxy>(new NullLoggerFactory()));

        //List tools
        var toolsResult = await proxy.ListTools(new RequestContext<ListToolsRequestParams>(new Mock<IMcpServer>().Object), CancellationToken.None);
        Assert.That(tools, Is.Not.Null);
        var addPetTool = toolsResult.Tools.FirstOrDefault(t => t.Name == "addPet");
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
        var getPetByIdTool = toolsResult.Tools.FirstOrDefault(t => t.Name == "getPetById");
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
        var addPetResponse = await proxy.CallTool(new RequestContext<CallToolRequestParams>(new Mock<IMcpServer>().Object)
        {
            Params = new CallToolRequestParams
            {
                Name = "addPet",
                Arguments = new Dictionary<string, JsonElement>
                {
                    { "body", JsonDocument.Parse("""{"id": 111111,"name":"Bob"}""").RootElement }
                }
            }
        }, CancellationToken.None);
        Assert.Multiple(() =>
        {
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
        });
        
        var getPetResponse = await proxy.CallTool(new RequestContext<CallToolRequestParams>(new Mock<IMcpServer>().Object)
        {
            Params = new CallToolRequestParams
            {
                Name = "getPetById",
                Arguments = new Dictionary<string, JsonElement>
                {
                    { "petId", JsonSerializer.SerializeToElement(111111) }
                }
            }
        }, CancellationToken.None);
        Assert.Multiple(() =>
        {
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
        });
    }

    [Test]
    public void InjectPathParams()
    {
        var url = OpenApiToolsProxy.InjectPathParams("http://example.com/endpoint/{urlParam}", new Dictionary<string, JsonElement>
        {
            {"urlParam", JsonDocument.Parse("\"id\"").RootElement},
            {"strValue", JsonDocument.Parse("\"my_string\"").RootElement},
            {"intValue", JsonDocument.Parse("5").RootElement},
            {"list", JsonDocument.Parse("[\"value1,value2\"]").RootElement}
        });
        Assert.That(url, Is.EqualTo("http://example.com/endpoint/id?strValue=my_string&intValue=5&list=value1,value2"));
    }
}

public static class JsonStringExtension
{
    public static string AsMinifiedJson(this string jsonAsString)
    {
        using var doc = JsonDocument.Parse(jsonAsString);
        return JsonSerializer.Serialize(doc.RootElement);
    }
}