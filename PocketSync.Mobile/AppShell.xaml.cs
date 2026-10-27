namespace PocketSync.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		// paginile secundare: lista de PC-uri și adăugarea unui PC
		Routing.RegisterRoute(nameof(DevicesPage), typeof(DevicesPage));
		Routing.RegisterRoute(nameof(AddPcPage), typeof(AddPcPage));
	}
}
