namespace LibreKO;

internal static class CharacterFiles
{
    internal static string PathFor(string kind, int charId)
    {
        if (charId <= 0 || Game.LoginServer is null) return "";
        string key = $"{Config.ServerHost}_{Config.GamePort}_{Game.LoginServer.Account}_{charId}";
        foreach (char bad in System.IO.Path.GetInvalidFileNameChars())
            key = key.Replace(bad, '_');
        return $"user://{kind}_{key}.json";
    }

    internal static string? Read(string path)
    {
        if (path.Length == 0) return null;
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Read);
        return file?.GetAsText();
    }

    internal static void Write(string path, string text)
    {
        if (path.Length == 0) return;
        using var file = Godot.FileAccess.Open(path, Godot.FileAccess.ModeFlags.Write);
        file?.StoreString(text);
    }
}
