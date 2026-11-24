namespace WhalePet.Game;

/// <summary>Process-wide gameplay services.</summary>
public static class GameRuntime
{
    public static GameStore? Store { get; private set; }
    public static GifCatalog? Catalog { get; private set; }
    public static GameEngine? Engine { get; private set; }
    public static GifProvider? Gifs { get; private set; }
    public static string? Error { get; private set; }
    public static string? GifError { get; private set; }
    public static bool Initialized { get; private set; }

    public static bool Initialize()
    {
        if (Initialized) return Error is null;
        try
        {
            Catalog = new GifCatalog();
            Store = new GameStore();
            Engine = new GameEngine(Store, Catalog);
            Engine.Initialize();
            try { Engine.MigrateLegacyDefaultAvatars(SettingsStore.Load()); } catch { }
            try
            {
                Gifs = new GifProvider(Catalog, Engine.IsGifUnlocked);
                Store.SavePackageMeta(Catalog.PackageVersion, Gifs.PackageSignature, Catalog.Manifest.EntryCount);
            }
            catch (Exception error)
            {
                GifError = error.Message;
            }
            Initialized = true;
            return true;
        }
        catch (Exception error)
        {
            Error = error.Message;
            return false;
        }
    }

    public static void Shutdown(bool save = true)
    {
        try { if (save) Engine?.Save(); } catch { }
        try { Store?.Dispose(); } catch { }
        Store = null;
        Engine = null;
        Gifs = null;
        Initialized = false;
    }
}
