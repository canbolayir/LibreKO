using LibreKO.Common.Domain.Entities.GameData;
using LibreKO.Common.Enums;
using LibreKO.Common.Infrastructure.Persistence;
using LibreKO.Game.Protocol.Writers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.World;

public sealed class GameplayTipRotation
{
    private readonly HashSet<int> shown = new();
    private DateTime? due;

    public GameplayTipData? Next(DateTime now, TimeSpan interval, IReadOnlyList<GameplayTipData> tips, Random random)
    {
        if (due == null)
        {
            due = now + interval;
            return null;
        }

        if (now < due)
            return null;

        due = now + interval;
        var fresh = tips.Where(tip => tip.Enabled && !shown.Contains(tip.Id)).ToList();
        if (fresh.Count == 0)
            return null;

        var picked = fresh[random.Next(fresh.Count)];
        shown.Add(picked.Id);
        return picked;
    }

    public static IReadOnlyList<GameplayTipData> InLanguage(IReadOnlyList<GameplayTipData> tips, GameLanguage language)
    {
        var own = tips.Where(tip => tip.Enabled && tip.Language == language).ToList();
        return own.Count > 0 || language == GameLanguage.English
            ? own
            : tips.Where(tip => tip.Enabled && tip.Language == GameLanguage.English).ToList();
    }
}

public interface IGameplayTipStore
{
    Task<(IReadOnlyList<GameplayTipData> Tips, GameplayTipSettingsData Settings)> LoadAsync();
    Task<int> SaveTipAsync(int id, GameLanguage language, string text, bool enabled);
    Task<bool> DeleteTipAsync(int id);
    Task SaveSettingsAsync(bool enabled, int intervalMinutes);
}

public sealed class GameplayTipStore(IServiceScopeFactory scopeFactory) : IGameplayTipStore
{
    public async Task<(IReadOnlyList<GameplayTipData> Tips, GameplayTipSettingsData Settings)> LoadAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var tips = await db.GameplayTips.AsNoTracking().OrderBy(tip => tip.Id).ToListAsync();
        var settings = await db.GameplayTipSettings.AsNoTracking().FirstOrDefaultAsync(row => row.Id == GameplayTipSettingsData.SettingsId)
            ?? new GameplayTipSettingsData();
        return (tips, settings);
    }

    public async Task<int> SaveTipAsync(int id, GameLanguage language, string text, bool enabled)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = id > 0 ? await db.GameplayTips.FindAsync(id) : null;
        if (stored == null)
        {
            var ids = await db.GameplayTips.Select(tip => tip.Id).ToListAsync();
            stored = new GameplayTipData { Id = id > 0 ? id : ids.DefaultIfEmpty(0).Max() + 1 };
            db.GameplayTips.Add(stored);
        }

        stored.Language = language;
        stored.Text = text;
        stored.Enabled = enabled;
        await db.SaveChangesAsync();
        return stored.Id;
    }

    public async Task<bool> DeleteTipAsync(int id)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.GameplayTips.FindAsync(id);
        if (stored == null)
            return false;

        db.GameplayTips.Remove(stored);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task SaveSettingsAsync(bool enabled, int intervalMinutes)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var stored = await db.GameplayTipSettings.FindAsync(GameplayTipSettingsData.SettingsId);
        if (stored == null)
        {
            stored = new GameplayTipSettingsData();
            db.GameplayTipSettings.Add(stored);
        }

        stored.Enabled = enabled;
        stored.IntervalMinutes = intervalMinutes;
        await db.SaveChangesAsync();
    }
}

public interface IGameplayTipService
{
    IReadOnlyList<GameplayTipData> Tips { get; }
    GameplayTipSettingsData Settings { get; }
    Task ReloadAsync();
}

public sealed class GameplayTipService(
    SessionManager sessionManager,
    IGameplayTipStore store,
    ILogger<GameplayTipService> logger) : BackgroundService, IGameplayTipService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(10);

    public IReadOnlyList<GameplayTipData> Tips { get; private set; } = [];
    public GameplayTipSettingsData Settings { get; private set; } = new();

    public async Task ReloadAsync()
    {
        var (tips, settings) = await store.LoadAsync();
        Tips = tips;
        Settings = settings;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ReloadAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Gameplay tips failed to load");
        }

        using var timer = new PeriodicTimer(TickInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await TickAsync(DateTime.UtcNow);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Gameplay tip tick failed");
            }
        }
    }

    public async Task TickAsync(DateTime now)
    {
        var settings = Settings;
        if (!settings.Enabled)
            return;

        var interval = TimeSpan.FromMinutes(settings.IntervalMinutes);
        var tips = Tips;
        var byLanguage = new Dictionary<GameLanguage, IReadOnlyList<GameplayTipData>>();
        foreach (var session in sessionManager.GetAll())
        {
            if (session.IsBot)
                continue;

            if (!byLanguage.TryGetValue(session.Language, out var shown))
                byLanguage[session.Language] = shown = GameplayTipRotation.InLanguage(tips, session.Language);
            if (session.GameplayTips.Next(now, interval, shown, Random.Shared) is { } tip)
                await session.Client.SendPacket(ChatPacketWriter.Tip(tip.Text));
        }
    }
}
