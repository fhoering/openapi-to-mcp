using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Text.Json.Nodes;

namespace OpenApiToMcp.Auth;

public class OpenApiAuthHandler : DelegatingHandler
{
    private readonly OpenApiAuthConfiguration _configuration;
    public OpenApiAuthHandler(OpenApiAuthConfiguration configuration)
    {
        _configuration = configuration;
        InnerHandler = new HttpClientHandler();
    }

    private async Task<string?> GetToken()
    {
        if (_configuration.BearerToken != null)
            return _configuration.BearerToken;
        if (_configuration.Oauth2GrantType != null)
            return await FetchOauth2Token();
        return null;
    }
    
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await GetToken();
        if(token != null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await base.SendAsync(request, cancellationToken);
    }

   #region oauth2 
    private string? _oauth2Token;

    private async Task<string?> FetchOauth2Token()
    {
        if(_oauth2Token != null && !IsJwtTokenExpired(_oauth2Token))
            return _oauth2Token;
        
        var httpClient = new HttpClient();
        var kvs = new List<KeyValuePair<string, string>>();
        kvs.Add(new KeyValuePair<string, string>("grant_type", _configuration.Oauth2GrantType.ToString()!));
        if(_configuration.Oauth2ClientId != null)
            kvs.Add(new KeyValuePair<string, string>("client_id", _configuration.Oauth2ClientId));
        if(_configuration.Oauth2ClientSecret != null)
            kvs.Add(new KeyValuePair<string, string>("client_secret", _configuration.Oauth2ClientSecret));
        if(_configuration.Oauth2RefreshToken != null)
            kvs.Add(new KeyValuePair<string, string>("refresh_token", _configuration.Oauth2RefreshToken));
        if(_configuration.Oauth2Username != null)
            kvs.Add(new KeyValuePair<string, string>("username", _configuration.Oauth2Username));
        if(_configuration.Oauth2Password != null)
            kvs.Add(new KeyValuePair<string, string>("password", _configuration.Oauth2Password));
        
        var response = await httpClient.PostAsync(_configuration.Oauth2TokenUrl, new FormUrlEncodedContent(kvs));
        if(response.StatusCode != HttpStatusCode.OK)
            throw new Exception("Token generation failed with status code " + response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        _oauth2Token =  JsonNode.Parse(body)?["access_token"]?.AsValue().ToString();
        return _oauth2Token;
    }

    private bool IsJwtTokenExpired(string token)
    {
        var jwtToken = new JwtSecurityTokenHandler().ReadJwtToken(token);
        return jwtToken.ValidTo < DateTime.UtcNow;
    }

    #endregion oauth2
    
}