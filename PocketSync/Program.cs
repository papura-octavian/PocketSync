using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = null);
var app = builder.Build();

app.UseDefaultFiles(); // index.html
app.UseStaticFiles(); // wwwroot

app.MapGet("/api/status", () => new { ok = true, version = "0.1" });

// Create the path for the transfered files
var configFolder = builder.Configuration["PocketSync:StorageFolder"];

string basePath;
if (string.IsNullOrEmpty(configFolder))
{
    basePath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
    basePath = Path.Combine(basePath, "PocketSync");
}
else
{
    basePath = configFolder;
}

var receivedDir = Path.Combine(basePath, "Received");
var storageDir = Path.Combine(basePath, "Storage");

Directory.CreateDirectory(receivedDir);
Directory.CreateDirectory(storageDir);

var fileMap = new Dictionary<Guid, string>();

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

    // tower 1
    var fileName = Path.GetFileName(fileSection.FileName);

    // cleaning the name, android uses ? and : which is deadly in windows >:( 
    foreach (var ch in Path.GetInvalidFileNameChars())
    {
        fileName = fileName.Replace(ch, '_');
    }

    // add the filename to the path
    var path = Path.Combine(receivedDir, fileName);

    // verify for existing filename
    if(File.Exists(path))
    {
        var extension = Path.GetExtension(fileName);
        fileName = Path.GetFileNameWithoutExtension(fileName);

        fileName = fileName + "_" + DateTime.Now.Ticks + extension;

        path = Path.Combine(receivedDir, fileName);
    }

    // tower 2
    path = Path.GetFullPath(path);

    if(path.StartsWith(basePath) == false)
    {
        return Results.BadRequest();
    }

    using var stream = File.Create(path);
    await fileSection.FileStream!.CopyToAsync(stream);

    return Results.Ok();

}).DisableAntiforgery();

app.MapGet("/api/files", () =>
{
    fileMap.Clear();
    var files = new DirectoryInfo(storageDir).GetFiles();

    return files.Select(f =>
    {
        var id = Guid.NewGuid();
        fileMap[id] = f.FullName;
        return new FileItemDto(id, f.Name, f.Length, f.LastWriteTimeUtc);
    }).ToList();
});

app.MapGet("/api/files/{id}/download", (Guid id) => 
{
    if (!fileMap.TryGetValue(id, out var path))
    {
        return Results.NotFound();
    }

    return Results.File(path, "application/octet-stream", Path.GetFileName(path));
});

app.Run();

record FileItemDto(Guid Id, string Name, long SizeBytes, DateTime ModifiedUtc);