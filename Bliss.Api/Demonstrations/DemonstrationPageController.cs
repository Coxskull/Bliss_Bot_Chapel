using Bliss.Api.Runtime;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bliss.Api.Demonstrations;

[AllowAnonymous]
public sealed class DemonstrationPageController(IWebHostEnvironment environment) : Controller
{
    [HttpGet("/demonstrations/{slug:regex(^[[a-z0-9-]]+$)}")]
    public IActionResult Prospect(string slug) => Page("prospect.html");

    [HttpGet("/outreach/{slug:regex(^[[a-z0-9-]]+$)}")]
    public IActionResult Outreach(string slug) => Page("outreach.html");

    private IActionResult Page(string fileName)
    {
        var path = Path.Combine(FrontendHost.ResolveRoot(environment), "public", "demonstrations", fileName);
        return System.IO.File.Exists(path)
            ? PhysicalFile(path, "text/html")
            : NotFound();
    }
}
