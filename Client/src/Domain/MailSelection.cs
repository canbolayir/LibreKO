using System.Collections.Generic;
using System.Linq;
using LibreKO.Network;

namespace LibreKO.Domain;

public sealed class MailSelection
{
    private readonly HashSet<int> ticked = new();

    public int Count => ticked.Count;

    public bool Has(int mailId) => ticked.Contains(mailId);

    public static bool Deletable(MailEntry mail) => !MailView.IsParcel(mail);

    public void Set(MailEntry mail, bool on)
    {
        if (on && Deletable(mail)) ticked.Add(mail.Id);
        else ticked.Remove(mail.Id);
    }

    public bool AllTicked(IEnumerable<MailEntry> shown)
    {
        var deletable = shown.Where(Deletable).ToList();
        return deletable.Count > 0 && deletable.All(m => ticked.Contains(m.Id));
    }

    public void SetAll(IEnumerable<MailEntry> shown, bool on)
    {
        foreach (var mail in shown) Set(mail, on);
    }

    public void KeepOnly(IEnumerable<MailEntry> mails)
    {
        var deletable = mails.Where(Deletable).Select(m => m.Id).ToHashSet();
        ticked.IntersectWith(deletable);
    }

    public Queue<int> TakeAll()
    {
        var queue = new Queue<int>(ticked.OrderBy(id => id));
        ticked.Clear();
        return queue;
    }
}
