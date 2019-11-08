﻿using IdentityServer4.AccessTokenValidation;
using IdentityServer4.Validation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace RawCMS.Plugins.Core.Handlers
{
    public class RawIdentityServerAuthenticationOptions : IdentityServerAuthenticationOptions
    {
        public string ApiKey { get; set; }
        public string AdminApiKey { get; set; }
    }

    public class RawLocalAccessTokenValidationHandler : AuthenticationHandler<RawIdentityServerAuthenticationOptions>
    {
        private readonly ITokenValidator _tokenValidator;

        public RawLocalAccessTokenValidationHandler(IOptionsMonitor<RawIdentityServerAuthenticationOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, ITokenValidator tokenValidator)
            : base(options, logger, encoder, clock)
        {
            _tokenValidator = tokenValidator;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            string requestToken = null;

            string authorization = Request.Headers["Authorization"];

            if (string.IsNullOrEmpty(authorization))
            {
                return AuthenticateResult.Fail("No Authorization Header is sent.");
            }

            if (authorization.StartsWith("ApiKey ", StringComparison.OrdinalIgnoreCase))
            {
                requestToken = authorization.Substring("ApiKey ".Length).Trim();

                return AuthorizeApiKey(requestToken);
            }

            if (authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                requestToken = authorization.Substring("Bearer ".Length).Trim();
            }

            if (string.IsNullOrEmpty(requestToken))
            {
                return AuthenticateResult.Fail("No Access Token is sent.");
            }

            TokenValidationResult result = await _tokenValidator.ValidateAccessTokenAsync(requestToken);

            if (result.IsError)
            {
                return AuthenticateResult.Fail(result.Error);
            }

            ClaimsIdentity claimsIdentity = new ClaimsIdentity("Bearer", ClaimTypes.Name, ClaimTypes.Role);
            claimsIdentity.AddClaims(result.Claims);

            ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            AuthenticationTicket authenticationTicket = new AuthenticationTicket(claimsPrincipal, Scheme.Name);
            return AuthenticateResult.Success(authenticationTicket);
        }

        private AuthenticateResult AuthorizeApiKey(string requestToken)
        {
            bool isValid = false;
            string username = null;
            string roles = "";
            if (requestToken == Options.ApiKey)
            {
                username = "AdminApiKeyUser";//TODO: ADD IN CONFIG
                roles = "Authenticated";//TODO: ADD IN CONFIG
                isValid = true;
            }

            if (requestToken == Options.AdminApiKey)
            {
                username = "ApiKeyUser";
                roles = "Authenticated,Admin"; //TODO: ADD IN CONFIG
                isValid = true;
            }

            if (isValid)
            {
                ClaimsIdentity claimsIdentity = new ClaimsIdentity("ApiKey", ClaimTypes.Name, ClaimTypes.Role);
                claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, username));
                foreach (string role in roles.Split(","))
                {
                    claimsIdentity.AddClaim(new Claim(ClaimTypes.Role, role));
                }
                ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
                AuthenticationTicket authenticationTicket = new AuthenticationTicket(claimsPrincipal, Scheme.Name);
                return AuthenticateResult.Success(authenticationTicket);
            }

            return AuthenticateResult.Fail("ApiKey not valid");
        }
    }
}