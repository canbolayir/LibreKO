namespace LibreKO.Features.PluginBridge;

internal sealed class PluginQuestsBridge(QuestTrackerFeature tracker) : IGameQuests
{
    event Action? IGameQuests.Changed { add { } remove { } }

    public IReadOnlyList<GameQuestTrack> Tracked => tracker.TrackedQuestsForPlugins();

    public void Open(int questId) => tracker.Pick(questId);
}
