using CreatorPlatform.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;

namespace CreatorPlatform.Integration.Tests.Api;

/// <summary>S4: the public page's visitor cookie is Secure everywhere but Development (plain-http dev server).</summary>
public sealed class VisitorCookieTests
{
    [Theory]
    [InlineData("Production", true)]
    [InlineData("Staging", true)]
    [InlineData("Development", false)]
    public void TheVisitorCookie_IsSecure_OutsideDevelopment(string environment, bool secure)
    {
        var options = PublicLandingPagesController.VisitorCookieOptions(new HostingEnvironment { EnvironmentName = environment });

        Assert.Equal(secure, options.Secure);
        Assert.Equal((false, SameSiteMode.Lax, TimeSpan.FromDays(365)), (options.HttpOnly, options.SameSite, options.MaxAge!.Value));
    }
}
