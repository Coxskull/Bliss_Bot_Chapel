using Microsoft.Extensions.FileProviders;

namespace Bliss.Api.Runtime;

public static class FrontendHost
{
    public static string ResolveRoot(IWebHostEnvironment environment)
    {
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", "frontend")),
            Path.GetFullPath(Path.Combine(environment.ContentRootPath, "frontend")),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "frontend"))
        };

        var root = candidates.FirstOrDefault(Directory.Exists);
        if (root is null)
        {
            throw new InvalidOperationException(
                "The frontend folder was not found. Expected a `frontend` directory next to Bliss.Api or copied beside the published output.");
        }

        return root;
    }
}
