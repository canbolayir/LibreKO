using LibreKO.Game.Protocol.Writers;
using Microsoft.Extensions.Logging;

namespace LibreKO.Game.World;

public interface IFlashBonusService
{
    bool CanAdd(UserSession session, FlashKind kind);
    Task AddAsync(UserSession session, FlashKind kind);
    Task CheckExpiryAsync();
}

public sealed class FlashBonusService : IFlashBonusService, IDisposable
{
    private readonly SessionManager sessionManager;
    private readonly ILogger<FlashBonusService> logger;
    private readonly Timer? expiryTimer;
    private int checking;

    public FlashBonusService(SessionManager sessionManager, ILogger<FlashBonusService> logger, bool startTimer = true)
    {
        this.sessionManager = sessionManager;
        this.logger = logger;
        if (startTimer)
            expiryTimer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
    }

    public bool CanAdd(UserSession session, FlashKind kind) => session.Flash.CanAdd(kind, DateTime.UtcNow);

    public async Task AddAsync(UserSession session, FlashKind kind)
    {
        var replaced = session.Flash.Add(kind, DateTime.UtcNow);
        if (replaced != FlashKind.None)
            await session.Client.SendPacket(FlashBonusPacketWriter.Ended(replaced));
        await session.Client.SendPacket(FlashBonusPacketWriter.Started(kind, session.Flash.Amount));
    }

    public async Task CheckExpiryAsync()
    {
        var now = DateTime.UtcNow;
        foreach (var session in sessionManager.GetAll())
        {
            var ended = session.Flash.TakeExpired(now);
            if (ended != FlashKind.None)
                await session.Client.SendPacket(FlashBonusPacketWriter.Ended(ended));
        }
    }

    private void OnTimerTick(object? _)
    {
        if (Interlocked.Exchange(ref checking, 1) == 1)
            return;
        _ = Task.Run(async () =>
        {
            try
            {
                await CheckExpiryAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error ending flash bonuses");
            }
            finally
            {
                Volatile.Write(ref checking, 0);
            }
        });
    }

    public void Dispose() => expiryTimer?.Dispose();
}
