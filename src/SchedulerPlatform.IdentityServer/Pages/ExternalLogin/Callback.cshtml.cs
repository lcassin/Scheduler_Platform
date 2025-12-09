// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Security.Claims;
using Duende.IdentityServer;
using Duende.IdentityServer.Events;
using Duende.IdentityServer.Services;
using Duende.IdentityServer.Test;
using IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SchedulerPlatform.IdentityServer.Services;
using SchedulerPlatform.Core.Domain.Entities;

namespace SchedulerPlatform.IdentityServer.Pages.ExternalLogin;

[AllowAnonymous]
[SecurityHeaders]
public class Callback : PageModel
{
    private readonly TestUserStore? _users;
    private readonly IIdentityServerInteractionService _interaction;
    private readonly ILogger<Callback> _logger;
    private readonly IEventService _events;
    private readonly IUserService _userService;

    public Callback(
        IIdentityServerInteractionService interaction,
        IEventService events,
        ILogger<Callback> logger,
        IUserService userService,
        TestUserStore? users = null)
    {
        _users = users;
        _interaction = interaction;
        _logger = logger;
        _events = events;
        _userService = userService;
    }
        
    public async Task<IActionResult> OnGet()
    {
        // read external identity from the temporary cookie
        var result = await HttpContext.AuthenticateAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);
        if (result.Succeeded != true)
        {
            throw new InvalidOperationException($"External authentication error: { result.Failure }");
        }

        var externalUser = result.Principal ?? 
            throw new InvalidOperationException("External authentication produced a null Principal");
		
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var externalClaims = externalUser.Claims.Select(c => $"{c.Type}: {c.Value}");
            _logger.ExternalClaims(externalClaims);
        }

        // lookup our user and external provider info
        // try to determine the unique id of the external user (issued by the provider)
        // the most common claim type for that are the sub claim and the NameIdentifier
        // depending on the external provider, some other claim type might be used
        var userIdClaim = externalUser.FindFirst(JwtClaimTypes.Subject) ??
                          externalUser.FindFirst(ClaimTypes.NameIdentifier) ??
                          throw new InvalidOperationException("Unknown userid");

        var provider = result.Properties.Items["scheme"] ?? throw new InvalidOperationException("Null scheme in authentiation properties");
        var providerUserId = userIdClaim.Value;

        TestUser? user = null;
        string subjectId;
        string username;

        if (_users != null)
        {
            user = _users.FindByExternalProvider(provider, providerUserId);
        }

        if (user == null && provider != "entra")
        {
            if (_users != null)
            {
                var claims = externalUser.Claims.ToList();
                claims.Remove(userIdClaim);
                user = _users.AutoProvisionUser(provider, providerUserId, claims.ToList());
            }
        }

        if (user != null)
        {
            subjectId = user.SubjectId;
            username = user.Username;
        }
        else
        {
            var issuer = externalUser.FindFirst("iss")?.Value ?? provider;
            var oid = externalUser.FindFirst("oid")?.Value ?? 
                      externalUser.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value ?? 
                      providerUserId;
            
                        var externalId = $"{issuer}|{oid}";
                        var dbUser = await _userService.GetUserByExternalIdAsync(externalId);

                        // Extract email from claims for lookup/creation
                        var email = externalUser.FindFirst("email")?.Value ?? 
                                   externalUser.FindFirst(ClaimTypes.Email)?.Value ?? 
                                   externalUser.FindFirst("preferred_username")?.Value ?? 
                                   $"{oid}@external.user";

                        if (dbUser == null)
                        {
                            // User not found by external ID - check if user exists by email
                            dbUser = await _userService.GetUserByEmailAsync(email);
                
                            if (dbUser != null)
                            {
                                // Found existing user by email - link to Entra identity
                                dbUser.ExternalUserId = externalId;
                                dbUser.ExternalIssuer = issuer;
                                dbUser.LastLoginDateTime = DateTime.UtcNow;
                                await _userService.UpdateUserAsync(dbUser);
                    
                                _logger.LogInformation("Linked existing user {Email} to external provider {Provider}", email, provider);
                            }
                            else
                            {
                                // No existing user found - create new user
                                var givenName = externalUser.FindFirst("given_name")?.Value ?? 
                                               externalUser.FindFirst(ClaimTypes.GivenName)?.Value ?? "External";
                    
                                var familyName = externalUser.FindFirst("family_name")?.Value ?? 
                                                externalUser.FindFirst(ClaimTypes.Surname)?.Value ?? "User";
                    
                                var name = externalUser.FindFirst("name")?.Value ?? 
                                          externalUser.FindFirst(ClaimTypes.Name)?.Value ?? 
                                          $"{givenName} {familyName}";

                                var userNow = DateTime.UtcNow;
                                dbUser = new User
                                {
                                    Username = email,
                                    Email = email,
                                    FirstName = givenName,
                                    LastName = familyName,
                                    ExternalUserId = externalId,
                                    ExternalIssuer = issuer,
                                    IsActive = true,
                                    ClientId = 1,
                                    CreatedDateTime = userNow,
                                    CreatedBy = "System",
                                    ModifiedDateTime = userNow,
                                    ModifiedBy = "System",
                                    IsDeleted = false,
                                    LastLoginDateTime = userNow
                                };

                                await _userService.CreateUserAsync(dbUser);
                                await _userService.AssignDefaultPermissionsAsync(dbUser.Id);
                    
                                _logger.LogInformation("JIT provisioned new user {Email} from {Provider}", email, provider);
                            }
                        }
                        else
                        {
                            dbUser.LastLoginDateTime = DateTime.UtcNow;
                            await _userService.UpdateUserAsync(dbUser);
                        }

            subjectId = dbUser.Id.ToString();
            username = dbUser.Email;
        }

        // this allows us to collect any additional claims or properties
        // for the specific protocols used and store them in the local auth cookie.
        // this is typically used to store data needed for signout from those protocols.
        var additionalLocalClaims = new List<Claim>();
        var localSignInProps = new AuthenticationProperties();
        CaptureExternalLoginContext(result, additionalLocalClaims, localSignInProps);
            
        // issue authentication cookie for user
        var isuser = new IdentityServerUser(subjectId)
        {
            DisplayName = username,
            IdentityProvider = provider,
            AdditionalClaims = additionalLocalClaims
        };

        await HttpContext.SignInAsync(isuser, localSignInProps);

        // delete temporary cookie used during external authentication
        await HttpContext.SignOutAsync(IdentityServerConstants.ExternalCookieAuthenticationScheme);

        // retrieve return URL
        var returnUrl = result.Properties.Items["returnUrl"] ?? "~/";

        // check if external login is in the context of an OIDC request
        var context = await _interaction.GetAuthorizationContextAsync(returnUrl);
        await _events.RaiseAsync(new UserLoginSuccessEvent(provider, providerUserId, subjectId, username, true, context?.Client.ClientId));
        Telemetry.Metrics.UserLogin(context?.Client.ClientId, provider!);

        if (context != null)
        {
            if (context.IsNativeClient())
            {
                // The client is native, so this change in how to
                // return the response is for better UX for the end user.
                return this.LoadingPage(returnUrl);
            }
        }

        return Redirect(returnUrl);
    }

    // if the external login is OIDC-based, there are certain things we need to preserve to make logout work
    // this will be different for WS-Fed, SAML2p or other protocols
    private static void CaptureExternalLoginContext(AuthenticateResult externalResult, List<Claim> localClaims, AuthenticationProperties localSignInProps)
    {
        ArgumentNullException.ThrowIfNull(externalResult.Principal, nameof(externalResult.Principal));

        // capture the idp used to login, so the session knows where the user came from
        localClaims.Add(new Claim(JwtClaimTypes.IdentityProvider, externalResult.Properties?.Items["scheme"] ?? "unknown identity provider"));

        // if the external system sent a session id claim, copy it over
        // so we can use it for single sign-out
        var sid = externalResult.Principal.Claims.FirstOrDefault(x => x.Type == JwtClaimTypes.SessionId);
        if (sid != null)
        {
            localClaims.Add(new Claim(JwtClaimTypes.SessionId, sid.Value));
        }

        // if the external provider issued an id_token, we'll keep it for signout
        var idToken = externalResult.Properties?.GetTokenValue("id_token");
        if (idToken != null)
        {
            localSignInProps.StoreTokens(new[] { new AuthenticationToken { Name = "id_token", Value = idToken } });
        }
    }
}
