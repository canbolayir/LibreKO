using System;
using System.Linq;
using Godot;
using LibreKO.Network;
using LibreKO.Domain;

namespace LibreKO;

public partial class World
{
    internal CanvasLayer BuildMailClassicUiPreview(string view)
    {
        ItemData.EnsureLoaded(); BuildMailWindow(); BuildMailComposeWindow();
        _mailLayer.Ready += () => Callable.From(() =>
        {
            var mails = MailUiPreviewEntries();
            mails.Add(PusPreviewMail());
            for (int i = 0; i < 24; i++) mails.Add(new MailEntry { Id = i + 10, Sender = "LongCharacterName1299", Subject = "A long message subject that should stay in its own column", Read = i % 2 == 0, SentAt = DateTime.UtcNow.AddDays(-1), Attachments = MailAttachmentState.None });
            if (view == "empty") mails.Clear();
            OnMailList(mails); OnMailUnread(mails.Count(m => !m.Read));
            if (view is "inbox" or "empty") { _mailShown = true; _mailWindow.Visible = true; }
            else if (view == "read")
            {
                SelectMail(3); OnMailRead(3, true, string.Concat(Enumerable.Repeat("You completed the Collection Race in Moradon. Your rewards are attached to this letter.\n\n", 12)));
            }
            else if (view == "store")
            {
                var mail = mails.Last(m => m.Kind == MailKind.Store); SelectMail(mail.Id); OnMailRead(mail.Id, true, "Your Power-Up Store purchase has arrived. Claim individual attachments below or claim all remaining attachments.");
            }
            else
            {
                Inv.EnsureLength(InventoryConstants.InventoryTotal);
                Inv[Inventory.GridStart] = new ItemSlot { ItemId = 810418000, Count = 20, Durability = 1 };
                Inv[Inventory.GridStart + 1] = new ItemSlot { ItemId = 379154000, Count = 1, Durability = 1 };
                Inv[Inventory.GridStart + 2] = new ItemSlot { ItemId = 389018000, Count = 5, Durability = 1 };
                _mailContacts.UnionWith(new[] { "Rikka", "Zeus", "Ariel", "Marduk", "Ranger", "Guardian", "Magician", "LongCharacterName1299", "Warrior" });
                Sheet.SetGold(100_000); OpenMailCompose();
                _mailTo.Text = "Rikka"; _mailSubject.Text = "Apples for the raid"; _mailBody.Text = "Here are the apples you asked for. Good hunting!"; _mailGold.Value = 25_000;
                OnMailBodyChanged();
                _mailAttachments.Add((Inventory.GridStart, 20)); _mailAttachments.Add((Inventory.GridStart + 1, 1)); RenderMailAttachments();
            }
        }).CallDeferred();
        RemoveChild(_mailLayer); return _mailLayer;
    }
}
