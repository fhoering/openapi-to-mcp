using NUnit.Framework;

namespace OpenApiToMcp.Tests;

public class CliTest
{
    private TextWriter _originalOut;
    
    [SetUp]
    public void MockConsole()
    {
        _originalOut = Console.Out;
    }
    
    [TearDown]
    public void UnmockConsole()
    {
        Console.SetOut(_originalOut);
    }

    [Test]
    public async Task ShouldPrintHelp() 
    {
        //arrange
        var testOut = new StringWriter();
        Console.SetOut(testOut);
        
        //act
        await Program.Main(["--help"]);
        
        //assert
        Assert.That(testOut.ToString(), Does.Contain("An on-the-fly OpenAPI MCP server"));
    }
}