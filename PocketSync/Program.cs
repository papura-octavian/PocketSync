using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = null);
var app = builder.Build();

app.UseDefaultFiles(); // index.html
app.UseStaticFiles(); // wwwroot

app.MapGet("/api/status", () => new { ok = true, version = "0.1" });

// Create the path for the transfered files
var basePath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
basePath = Path.Combine(basePath, "PocketSync");
Directory.CreateDirectory(basePath);
// Console.WriteLine(basePath);


// Create POST API
app.MapPost("/api/upload", async (HttpRequest request) =>
{
    var mediaType = MediaTypeHeaderValue.Parse(request.ContentType!);
    var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;

    var reader = new MultipartReader(boundary!, request.Body);
    var section = await reader.ReadNextSectionAsync();
    var fileSection = section?.AsFileSection();

    if (fileSection is null)
        return Results.BadRequest("No File sent");

    var path = Path.Combine(basePath, fileSection.FileName);
    using var stream = File.Create(path);
    await fileSection.FileStream!.CopyToAsync(stream);

    return Results.Ok();
}).DisableAntiforgery();


app.Run();