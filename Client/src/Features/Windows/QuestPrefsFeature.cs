namespace LibreKO.Features.Windows;

internal sealed class QuestPrefsFeature : IFeature
{
    private readonly QuestBook book;
    private readonly SelfSession self;
    private QuestPrefs prefs = new();
    private string path = "";

    public QuestPrefsFeature(QuestBook book, SelfSession self)
    {
        this.book = book;
        this.self = self;
    }

    public void Build()
    {
        Load();
        book.Accepted += OnAccepted;
    }

    public void Dispose() => book.Accepted -= OnAccepted;

    private void Load()
    {
        path = CharacterFiles.PathFor("quests", self.MyCharId);
        prefs = QuestPrefs.Parse(CharacterFiles.Read(path));
        foreach (int questId in prefs.Tracked) book.Track(questId, true);
    }

    private void Save() => CharacterFiles.Write(path, prefs.Write());

    private void OnAccepted(int questId) => SetTracked(questId, true);

    internal void SetTracked(int questId, bool tracked)
    {
        book.Track(questId, tracked);
        if (tracked ? prefs.Tracked.Add(questId) : prefs.Tracked.Remove(questId)) Save();
    }

    internal bool FirstOffer(int questId)
    {
        if (!prefs.Offered.Add(questId)) return false;
        Save();
        return true;
    }
}
