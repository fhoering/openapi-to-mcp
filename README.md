# openapi-to-mcp
 An MCP server for your API

## using

(for example on windows, in your Claude/VSCode MCP config)
```json
{
    "mcpServers": {
        "petstore": {
            "command": "OpenApiToMcp.Cli.exe",
            "args": [
                "https://petstore3.swagger.io/api/v3/openapi.json"
            ]
        }
    }
}
```

## building

```
cd OpenApiToMcp.Cli
dotnet publish -c Release -r win-x64 --self-contained true -p:EnableCompressionInSingleFile=true  
dotnet publish -c Release -r osx-arm64 --self-contained true -p:EnableCompressionInSingleFile=true  
```
