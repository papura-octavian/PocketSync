using Microsoft.Extensions.Logging;
using PocketSync.Mobile.Services;
#if ANDROID
using ZXing.Net.Maui.Controls;
#endif

namespace PocketSync.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});
#if ANDROID
		// camera pentru scanarea QR-ului de pairing
		builder.UseBarcodeReader();

		// câmpurile de text stau în carduri rotunjite — fără linia de sub text pusă de Android
		Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("NoUnderline", (handler, _) =>
			handler.PlatformView.BackgroundTintList =
				Android.Content.Res.ColorStateList.ValueOf(Android.Graphics.Color.Transparent));
#endif

		// HttpClient-ul din ApiService e singleton — socket exhaustion altfel
		builder.Services.AddSingleton<ApiService>();
		builder.Services.AddSingleton<PcStore>();
		builder.Services.AddSingleton<MainPage>();
		builder.Services.AddTransient<DevicesPage>();
		builder.Services.AddTransient<AddPcPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
