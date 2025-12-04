using System;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public BasicAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        System.Text.Encodings.Web.UrlEncoder encoder,
        ISystemClock clock)
        : base(options, logger, encoder, clock)
    { }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // no Authorization header -> not our concern (other handlers may handle)
        if (!Request.Headers.ContainsKey("Authorization"))
            return AuthenticateResult.NoResult();

        var authHeader = Request.Headers["Authorization"].ToString();
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return AuthenticateResult.NoResult();

        try
        {
            var encoded = authHeader.Substring("Basic ".Length).Trim();
            var decodedBytes = Convert.FromBase64String(encoded);
            var decoded = Encoding.UTF8.GetString(decodedBytes);
            var parts = decoded.Split(':', 2);
            if (parts.Length != 2)
                return AuthenticateResult.Fail("Invalid Basic auth header format");

            var userName = parts[0];
            var password = parts[1];

            // call /api/Auth/login on the same host
            var httpFactory = Context.RequestServices.GetRequiredService<IHttpClientFactory>();
            var client = httpFactory.CreateClient();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";
            client.BaseAddress = new Uri(baseUrl);

            var loginPayload = new { UserName = userName, Password = password };
            var json = JsonSerializer.Serialize(loginPayload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await client.PostAsync("/api/Auth/login", content);
            if (!resp.IsSuccessStatusCode)
                return AuthenticateResult.Fail("Invalid username or password");

            var respString = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(respString);

            // your AuthController returns "Token" (capital T)
            string token = null;
            if (doc.RootElement.TryGetProperty("Token", out var tokenProp))
                token = tokenProp.GetString();

            // fallback checks
            if (string.IsNullOrEmpty(token))
            {
                if (doc.RootElement.TryGetProperty("token", out var tokenProp2))
                    token = tokenProp2.GetString();
                else if (doc.RootElement.TryGetProperty("accessToken", out var tokenProp3))
                    token = tokenProp3.GetString();
            }

            if (string.IsNullOrEmpty(token))
                return AuthenticateResult.Fail("Login response did not contain a token");

            // Validate token with app settings
            var config = Context.RequestServices.GetRequiredService<IConfiguration>();
            var jwtKey = config["Jwt:Key"];
            var jwtIssuer = config["Jwt:Issuer"];
            var jwtAudience = config["Jwt:Audience"];

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtIssuer,
                ValidateAudience = true,
                ValidAudience = jwtAudience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateLifetime = true,
                NameClaimType = ClaimTypes.Name,
                RoleClaimType = ClaimTypes.Role
            };

            var handler = new JwtSecurityTokenHandler();
            ClaimsPrincipal principal;
            try
            {
                principal = handler.ValidateToken(token, validationParameters, out var validatedToken);
            }
            catch (SecurityTokenException ste)
            {
                Logger.LogError(ste, "Token validation failed");
                return AuthenticateResult.Fail("Invalid token from login endpoint");
            }

            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Basic authentication error");
            return AuthenticateResult.Fail("Error processing Basic authentication");
        }
    }
}
