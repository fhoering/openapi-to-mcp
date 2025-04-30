using NUnit.Framework;
using OpenApiToMcp;

namespace openapi_to_mcp.Tests;

public class OpenApiParserTest
{
    [TestCase("https://petstore3.swagger.io/api/v3/openapi.json", 19)] //remote
    [TestCase("resources/petstore3.oas.yaml", 19)] //local YAML v3
    [TestCase("resources/petstore2.oas.json", 20)] //local JSON v2
    public async Task Parse_ShouldParseRemoteOrFileAndJsonOrYaml(string openapiFileOrUrl, int expectedOperations)
    {
        var (openApiDocument, diagnostic) = await new OpenApiParser().Parse(openapiFileOrUrl, hostOverride: null, bearerToken: null);
        
        Assert.That(openApiDocument, Is.Not.Null);
        var operations = openApiDocument.Paths.SelectMany(p => p.Value.Operations.Values);
        Assert.That(operations.Count(), Is.EqualTo(expectedOperations));
        Assert.That(diagnostic.Errors.Count, Is.EqualTo(0));
        Assert.That(diagnostic.Warnings.Count, Is.EqualTo(0));
    }
    
    [Test]
    public async Task Parse_DetectInvalidToolNames()
    {
        var (openApiDocument, diagnostic) = await new OpenApiParser().Parse("resources/invalid_tool_names.oas.yaml", hostOverride: null, bearerToken: null);
        Assert.That(openApiDocument, Is.Not.Null);

        var operations = openApiDocument.Paths.SelectMany(p => p.Value.Operations.Values);
        Assert.That(operations.Count(), Is.EqualTo(5));

        var errorMessages = diagnostic.Errors.Select(e => e.Message);
        var expectedErrors = new[]
        {
            "Operation Post /invalid-tool-name-because-the-path-is-way-to-long/{long-parameter} translate to an invalid tool name: Post_invalid-tool-name-because-the-path-is-way-to-long_long-parameter",
            "Operation Get /invalid-tool-name translate to an invalid tool name: SuperSuperSuperSuperLongOperationIdSoTheToolNameIsLongerThan64Chars",
            "Operation Post /invalid-tool-name translate to an invalid tool name: Invalid tool name from x-mcp-tool-name"
        };
        
        Assert.That(errorMessages, Is.EquivalentTo(expectedErrors));
        Assert.That(diagnostic.Warnings.Count, Is.EqualTo(0));
    }
    
    [TestCase("https://petstore3.swagger.io/api/v3/openapi.json", null, "https://petstore3.swagger.io/api/v3")]
    [TestCase("https://petstore3.swagger.io/api/v3/openapi.json", "https://other-host.com", "https://other-host.com/api/v3")]
    [TestCase("resources/petstore3.oas.yaml", null, "/api/v3")]
    [TestCase("resources/petstore3.oas.yaml", "https://other-host.com", "https://other-host.com/api/v3")]
    [TestCase("resources/no_server_url.oas.yaml", null, null)]
    [TestCase("resources/no_server_url.oas.yaml", "https://other-host.com", "https://other-host.com")]
    [TestCase("resources/absolute_server_url.oas.yaml", null, "https://absolute.com/v1")]
    [TestCase("resources/absolute_server_url.oas.yaml", "https://other-host.com", "https://other-host.com/v1")]
    public async Task Parse_ShouldInferServerHostAndAllowOverride(string openapiFileOrUrl, string? hostOverride, string expectedServerUrl)
    {
        var (openApiDocument, diagnostic) = await new OpenApiParser().Parse(openapiFileOrUrl, hostOverride, bearerToken: null);
        
        Assert.That(openApiDocument, Is.Not.Null);
        var serverUrl = openApiDocument.Servers.First().Url;
        Assert.That(serverUrl, Is.EqualTo(expectedServerUrl));
    }
}