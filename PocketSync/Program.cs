using PocketSync.Server;

// Gazda de consolă — pentru development. Gazda "reală" e tray app-ul (PocketSync.TrayApp).
var server = new PocketSyncServer();
await server.StartAsync();

Console.WriteLine();
Console.WriteLine("  PocketSync pornit");
Console.WriteLine($"  Folder:  {server.BasePath}");
Console.WriteLine($"  Local:   http://localhost:{server.Port}");
Console.WriteLine($"  LAN:     {server.LanUrl}");
Console.WriteLine();
Console.WriteLine($"  Pairing (deschide pe telefon sau scanează QR-ul din tray app):");
Console.WriteLine($"  {server.PairingUrl}");
Console.WriteLine();

await server.WaitForShutdownAsync();
