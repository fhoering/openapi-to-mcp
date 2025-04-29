[![.NET Build](https://github.com/ouvreboite/openapi-to-mcp/actions/workflows/build_and_test.yml/badge.svg)](https://github.com/ouvreboite/openapi-to-mcp/actions/workflows/build_and_test.yml)

# openapi-to-mcp

Use your OpenAPI specification to expose your API's endpoints as strongly typed tools.

Example for https://petstore3.swagger.io/ 🎉

```json
{
  "mcpServers": {
    "petstore": {
      "command": "openapi-to-mcp",
        "args": [
          "https://petstore3.swagger.io/api/v3/openapi.json"
        ]
    }
  }
}
```

## Install

As a Nuget tool: [openapi-to-mcp](https://www.nuget.org/packages/openapi-to-mcp)
```sh
dotnet tool install --global openapi-to-mcp
```
Or download the executables from the [releases](https://github.com/ouvreboite/openapi-to-mcp/releases)

## Usage


```bash
Usage:
  openapi-to-mcp <open-api> [options]

Arguments:
  <open-api>  You OpenAPI specification (URL or file) [required]

Options:
  -h, --host-override                          Host override
  -b, --bearer-token                           Bearer token
  -o, -o2, --oauth-2-grant-type                OAuth2 flow to be used
  <client_credentials|password|refresh_token>
  -o2.tu, --oauth-2-token-url                  OAuth2 token endpoint URL (override the one defined in your OpenAPI for
                                               your chosen OAuth2 flow)
  -o2.ci, --oauth-2-client-id                  OAuth2 client id (for the client_credentials grant_type)
  -o2.cs, --oauth-2-client-secret              OAuth2 client secret (for the client_credentials grant_type)
  -o2.rt, --oauth-2-refresh-token              OAuth2 refresh token (for the refresh_token grant_type)
  -o2.un, --oauth-2-username                   OAuth2 username (for the password grant_type)
  -o2.pw, --oauth-2-password                   OAuth2 password (for the password grant_type)
  -?, -h, --help                               Show help and usage information
  -v, --version                                Show version information
```

## Features

| Category              | Feature                | Support                                                                                                      | Details                                                                                                            |
|-----------------------|------------------------|--------------------------------------------------------------------------------------------------------------|--------------------------------------------------------------------------------------------------------------------|
| **OpenAPI**           | **Versions**           | v2.0, v3.0                                                                                                   |                                                                                                                    |
|                       | **Formats**            | JSON, YAML                                                                                                   |                                                                                                                    |
|                       | **Sources**            | file, URL                                                                                                    |                                                                                                                    |
|                       | **$refs**              | Local, Remote                                                                                                |                                                                                                                    |
|                       | **Base path**          | Use the first [server](https://swagger.io/docs/specification/v3_0/api-host-and-base-path/) URL               | Prepend with the source URL host if relative                                                                       |
| **MCP**               | **Transport**          | SDTIO                                                                                                        |                                                                                                                    |
|                       | **Tool's name**        | Use the [operationId](https://swagger.io/docs/specification/v3_0/paths-and-operations/#operationid)          | Fallback to `{httpMethod}_{escaped_path}`. ⚠️Tool names >64 chars long are discarded                               |
|                       | **Tool's description** | Use the [operation](https://swagger.io/docs/specification/v3_0/paths-and-operations)'s description           | Fallback to [path](https://swagger.io/docs/specification/v3_0/paths-and-operations)'s description                  |
|                       | **Inputs**             | Path params, Query params, JSON request bodies                                                               | Use the JSONSchema from the OpenAPI specification                                                                  |
|                       | **Outputs**            | Textual responses (json, text, ...)                                                                          |                                                                                                                    |
| **API Authorization** | **Bearer Token**       | As [Authorization](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Authorization) header |                                                                                                                    |
|                       | **OAuth2**             | ClientCredentials, RefreshToken, Password                                                                    | Using the `tokenUrl` from the [securitySchemes](https://swagger.io/docs/specification/v3_0/authentication/oauth2/) |

## How to publish

Create a new tag/release