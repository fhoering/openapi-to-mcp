using NUnit.Framework;

namespace openapi_to_mcp.Tests;

public class CliTest
{
    private TextWriter originalOut;
    
    [SetUp]
    public void MockConsole()
    {
        originalOut = Console.Out;
    }
    
    [TearDown]
    public void UnmockConsole()
    {
        Console.SetOut(originalOut);
    }

    [Test]
    public async Task Test() 
    { 
        var testOut = new StringWriter();
        Console.SetOut(testOut);
        
        //act
        await Program.Main(["--help"]);
        
        var stdOut = testOut.ToString() ?? "";
        Assert.That(stdOut, Does.Contain("An on-the-fly OpenAPI MCP server"));
    }
}
