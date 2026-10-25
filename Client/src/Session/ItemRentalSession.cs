namespace LibreKO.Session;

public sealed class ItemRentalSession
{
    public event Action<int>? OpenEvent;
    public event Action<RentalBoard>? BoardEvent;
    public event Action<bool, string>? ResultEvent;

    internal void RaiseOpen(int npcId) => OpenEvent?.Invoke(npcId);

    internal void RaiseBoard(RentalBoard board) => BoardEvent?.Invoke(board);

    internal void RaiseResult(bool ok, string message) => ResultEvent?.Invoke(ok, message);
}
