namespace LibreKO.Domain;

public readonly record struct PartyLootEntry(DateTime At, string Looter, int ItemId);

public readonly record struct PartyLootShare(string Looter, int Items, int Percent);

public sealed class PartyLootLog
{
    public const int MaxEntries = 200;
    private const int WholePercent = 100;

    private readonly List<PartyLootEntry> entries = new();

    public IReadOnlyList<PartyLootEntry> Entries => entries;

    public long MyNoah { get; private set; }

    public event Action? Changed;

    public void AddItem(PartyLootEntry entry)
    {
        entries.Add(entry);
        if (entries.Count > MaxEntries) entries.RemoveAt(0);
        Changed?.Invoke();
    }

    public void AddNoah(long amount)
    {
        if (amount <= 0) return;
        MyNoah += amount;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (entries.Count == 0 && MyNoah == 0) return;
        entries.Clear();
        MyNoah = 0;
        Changed?.Invoke();
    }

    public IReadOnlyList<PartyLootShare> Shares()
    {
        int total = entries.Count;
        if (total == 0) return Array.Empty<PartyLootShare>();
        return entries
            .GroupBy(entry => entry.Looter)
            .Select(group => new PartyLootShare(group.Key, group.Count(), group.Count() * WholePercent / total))
            .OrderByDescending(share => share.Items)
            .ThenBy(share => share.Looter, StringComparer.Ordinal)
            .ToList();
    }
}
