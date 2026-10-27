namespace PocketSync.Mobile.Services;

/// <summary>Un fișier venit prin share sheet: nume + fabrică de stream (content:// nu e path pe disc).</summary>
public record SharedItem(string Name, long? Length, Func<Stream?> OpenStream);

/// <summary>
/// Puntea dintre intent-ul Android (MainActivity) și UI (MainPage):
/// activitatea depune fișierele primite, pagina le consumă și le urcă.
/// </summary>
public static class ShareQueue
{
    private static readonly List<SharedItem> Items = [];

    public static event Action? ItemsArrived;

    public static int Count { get { lock (Items) return Items.Count; } }

    public static void Add(IEnumerable<SharedItem> items)
    {
        lock (Items) Items.AddRange(items);
        ItemsArrived?.Invoke();
    }

    public static List<SharedItem> TakeAll()
    {
        lock (Items)
        {
            var taken = new List<SharedItem>(Items);
            Items.Clear();
            return taken;
        }
    }
}
