var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseDefaultFiles(); // index.html
app.UseStaticFiles(); // wwwroot

app.MapGet("/api/status", () => new { ok = true, version = "0.1" });

var basePath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

basePath = Path.Combine(basePath, "PocketSync");

Console.WriteLine(basePath);

app.Run();