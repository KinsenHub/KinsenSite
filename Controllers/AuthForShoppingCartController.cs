using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Web.Common.Controllers;

[Route("umbraco/api/auth")]
public class AuthApiController : UmbracoApiController
{
    [HttpGet("status")]
    public IActionResult Status()
    {
        // Αν χρησιμοποιείς Umbraco Members:
        if (User?.Identity?.IsAuthenticated == true)
            return Ok(new { loggedIn = true });

        // Αν χρησιμοποιείς custom cookie:
        var hasAuthCookie = HttpContext.Request.Cookies.ContainsKey(".AspNetCore.Identity.Application");
        return Ok(new { loggedIn = hasAuthCookie });
    }
}