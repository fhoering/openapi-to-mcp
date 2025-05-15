using NUnit.Framework;
using OpenApiToMcp.OpenApi;

namespace OpenApiToMcp.Tests.OpenApi;

public class OpenApiToolsExtractorTest
{
    [Test]
    public async Task GetEndpointTools_ShouldReturnValidEndpointsOnly()
    {
        var (openApiDocument, diagnostic) =
            await new OpenApiParser().Parse("resources/invalid_tool_names.oas.yaml", hostOverride: null, bearerToken: null, toolNamingStrategy: default);
        
        var endpointTools = new OpenApiToolsExtractor().ExtractEndpointTools(openApiDocument, toolNamingStrategy: default);
        
        Assert.That(endpointTools, Is.Not.Null);
        var toolNamesAndDescriptions = endpointTools.Select(t => (t.tool.Name, t.tool.Description));
        Assert.That(toolNamesAndDescriptions, Is.EquivalentTo(new[]
        {
          ("validOperationId", "Some description"), 
          ("Post_valid-tool-name_test", "Some description")
        }));
    }
    
    [Test]
    public async Task GetEndpointTools_ShouldUseOpenApiExtensions()
    {
      var (openApiDocument, diagnostic) =
        await new OpenApiParser().Parse("resources/extensions.oas.yaml", hostOverride: null, bearerToken: null,  toolNamingStrategy: default);
      var endpointTools = new OpenApiToolsExtractor().ExtractEndpointTools(openApiDocument, toolNamingStrategy: default);
      Assert.That(endpointTools, Is.Not.Null);
      var toolNamesAndDescriptions = endpointTools.Select(t => (t.tool.Name, t.tool.Description));
      Assert.That(toolNamesAndDescriptions, Is.EquivalentTo(new[]
      {
        ("GreetOld_FromExtension", "From get /greet/old operation description"), 
        ("GreetNew_FromOperationId", "From get /greet/new extension description")
      }));
    }
    
    [TestCase(ToolNamingStrategy.extension_or_operationid_or_verbandpath, "GreetOld_FromExtension", "GreetNew_FromOperationId")]
    [TestCase(ToolNamingStrategy.operationid, "GreetOld_FromOperationId", "GreetNew_FromOperationId")]
    [TestCase(ToolNamingStrategy.verbandpath, "Get_greet_old", "Get_greet_new")]
    [TestCase(ToolNamingStrategy.extension, "GreetOld_FromExtension")]
    public async Task endpointTools_ShouldFollowToolNamingStrategy(ToolNamingStrategy strategy, params string[] expectedToolsNames)
    {
      var (openApiDocument, diagnostic) = await new OpenApiParser().Parse("resources/extensions.oas.yaml", hostOverride: null, bearerToken: null,  toolNamingStrategy: strategy);
      
      var endpointTools = new OpenApiToolsExtractor().ExtractEndpointTools(openApiDocument, toolNamingStrategy: strategy);
      Assert.That(endpointTools, Is.Not.Null);
      var toolNames = endpointTools.Select(t => t.tool.Name);
      Assert.That(toolNames, Is.EquivalentTo(expectedToolsNames));
    }
}