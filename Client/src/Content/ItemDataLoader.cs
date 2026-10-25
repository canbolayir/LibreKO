using System.Text.Json;
using Godot;

namespace LibreKO.Content;

internal static class ItemDataLoader
{
    private const string ItemsPath = "res://assets/items/items.json";
    private const string PriceOverridesPath = "res://assets/items/price_overrides.json";

    internal static void Load()
    {
        if (ContentFiles.ReadText(ItemsPath) is not { } json)
        {
            GD.PushWarning("[items] missing items.json (run tools/bake_items.py)");
            return;
        }
        LoadFrom(json, ContentFiles.ReadText(PriceOverridesPath));
    }

    internal static void LoadFrom(string json, string? priceOverrides)
    {
        using var doc = JsonDocument.Parse(json);
        var d = doc.RootElement;
        if (d.ValueKind != JsonValueKind.Object) return;
        LoadTexts(d);
        LoadExtensions(d);
        LoadSellGroups(d);
        LoadPieceRewards(d);
        LoadAttendance(d);
        LoadUiHelp(d);
        LoadSetBonuses(d);
        LoadSkillOptions(d);
        LoadPriceOverrides(priceOverrides);
        foreach (var entry in d.EnumerateObject())
        {
            if (!int.TryParse(entry.Name, out int id) || entry.Value.ValueKind != JsonValueKind.Object) continue;
            var o = entry.Value;
            int reqLevel = Int(o, "reqLevel");
            if (reqLevel == 0) reqLevel = Int(o, "reqLvl");
            ItemData.AddItem(new ItemData.Item
            {
                Id = id,
                Name = Str(o, "name"), Desc = Str(o, "desc"),
                Cat = Int(o, "cat"), Kind = Int(o, "kind"), Race = Int(o, "race"),
                Slot = Int(o, "slot"), Class = Int(o, "class"),
                Value = Int(o, "value"),
                MinDamage = Int(o, "minDamage"), Damage = Int(o, "damage"),
                Delay = Int(o, "delay"), Range = Int(o, "range"), Weight = Int(o, "weight"),
                BuyPrice = Int(o, "buyPrice"), SaleType = Int(o, "saleType"),
                Ac = Int(o, "ac"), Countable = Int(o, "countable"),
                Effect1 = Int(o, "effect1"), Effect2 = Int(o, "effect2"),
                ReqLevel = reqLevel, ReqLevelMax = Int(o, "reqLevelMax"), ReqLvl = reqLevel,
                ReqRank = Int(o, "reqRank"), ReqTitle = Int(o, "reqTitle"),
                ReqStr = Int(o, "reqStr"), ReqSta = Int(o, "reqSta"), ReqDex = Int(o, "reqDex"),
                ReqInt = Int(o, "reqInt"), ReqCha = Int(o, "reqCha"),
                SellingGroup = Int(o, "sellingGroup"), ReqCls = Int(o, "reqCls"), ItemType = Int(o, "itemType"),
                UnbindStones = Int(o, "grade"), Bound = Int(o, "bound"), Notice = Int(o, "notice"),
                Icon = Int(o, "icon"), Duration = Int(o, "duration"),
            });
        }
    }

    private static bool Section(JsonElement root, string name, JsonValueKind kind, out JsonElement section) =>
        root.TryGetProperty(name, out section) && section.ValueKind == kind;

    private static void LoadTexts(JsonElement root)
    {
        if (!Section(root, "_texts", JsonValueKind.Object, out var texts)) return;
        foreach (var text in texts.EnumerateObject())
            if (int.TryParse(text.Name, out int id))
                ItemData.AddText(id, AsString(text.Value));
    }

    private static void LoadExtensions(JsonElement root)
    {
        if (!Section(root, "_ext", JsonValueKind.Object, out var cats)) return;
        foreach (var catEntry in cats.EnumerateObject())
        {
            if (!int.TryParse(catEntry.Name, out int cat) || catEntry.Value.ValueKind != JsonValueKind.Object) continue;
            foreach (var extEntry in catEntry.Value.EnumerateObject())
            {
                if (!int.TryParse(extEntry.Name, out int extId) || extEntry.Value.ValueKind != JsonValueKind.Object) continue;
                var o = extEntry.Value;
                var e = new ItemData.Ext
                {
                    Cat = cat, Id = extId,
                    Name = Str(o, "name"), Desc = Str(o, "desc"),
                    Linked = Int(o, "linked"), GlowFx = Int(o, "glowFx"),
                    Resrc = Int(o, "resrc"), Icon = Int(o, "icon"), Special = Int(o, "special"),
                    PriceMultiply = Int(o, "priceMultiply"),
                    AttackIntervalPercent = RawInt(o, ExtAttackIntervalColumn),
                    BonusDamage = Int(o, "bonusDamage"), BonusAc = Int(o, "bonusAc"),
                    BonusMaxHp = Int(o, "bonusMaxHp"), BonusMaxMp = Int(o, "bonusMaxMp"),
                    BonusStr = Int(o, "bonusStr"), BonusSta = Int(o, "bonusSta"),
                    BonusDex = Int(o, "bonusDex"), BonusInt = Int(o, "bonusInt"), BonusCha = Int(o, "bonusCha"),
                    BonusFireR = Int(o, "bonusFireR"), BonusColdR = Int(o, "bonusColdR"),
                    BonusLightningR = Int(o, "bonusLightningR"), BonusMagicR = Int(o, "bonusMagicR"),
                    BonusPoisonR = Int(o, "bonusPoisonR"), BonusCurseR = Int(o, "bonusCurseR"),
                    BonusHitrate = Int(o, "bonusHitrate"), BonusEvasionrate = Int(o, "bonusEvasionrate"),
                    BonusDaggerAc = Int(o, "bonusDaggerAc"), BonusJamadarAc = Int(o, "bonusJamadarAc"),
                    BonusSwordAc = Int(o, "bonusSwordAc"), BonusClubAc = Int(o, "bonusClubAc"),
                    BonusAxeAc = Int(o, "bonusAxeAc"), BonusSpearAc = Int(o, "bonusSpearAc"),
                    BonusArrowAc = Int(o, "bonusArrowAc"),
                    MagicOrRare = Int(o, "magicOrRare"),
                    DurationBonus = Int(o, "durationBonus"),
                    ReqStrBonus = Int(o, "reqStrBonus"),
                    ReqStaBonus = Int(o, "reqStaBonus"),
                    ReqDexBonus = Int(o, "reqDexBonus"),
                    ReqIntBonus = Int(o, "reqIntBonus"),
                    ReqChaBonus = Int(o, "reqChaBonus"),
                    ReqLevelBonus = Int(o, "reqLevelBonus"),
                    Plus = Int(o, "plus"),
                    FireDamage = Int(o, "fireDamage"), IceDamage = Int(o, "iceDamage"),
                    LightningDamage = Int(o, "lightningDamage"), PoisonDamage = Int(o, "poisonDamage"),
                    HpDrain = Int(o, "hpDrain"), MpDamage = Int(o, "mpDamage"),
                    MpDrain = Int(o, "mpDrain"), Mirror = Int(o, "mirror"),
                    GradeClass = RawInt(o, ExtGradeClassColumn),
                    UnbindStones = RawInt(o, ExtUnbindStonesColumn),
                };
                ItemData.AddExt(e);
            }
        }

        ItemData.IndexReverseCategories();
    }

    private static void LoadSetBonuses(JsonElement root)
    {
        if (!Section(root, "_sets", JsonValueKind.Object, out var rows)) return;
        foreach (var row in rows.EnumerateObject())
        {
            if (!int.TryParse(row.Name, out int id) || row.Value.ValueKind != JsonValueKind.Object) continue;
            var o = row.Value;
            ItemData.AddSetBonus(id, new ItemData.SetBonus
            {
                Name = Str(o, "name"),
                Ac = Int(o, "ac"), Hp = Int(o, "hp"), Mp = Int(o, "mp"),
                Str = Int(o, "str"), Sta = Int(o, "sta"), Dex = Int(o, "dex"), Int = Int(o, "int"), Cha = Int(o, "cha"),
                FireR = Int(o, "fireR"), ColdR = Int(o, "coldR"), LightningR = Int(o, "lightningR"),
                PoisonR = Int(o, "poisonR"), MagicR = Int(o, "magicR"), CurseR = Int(o, "curseR"),
                XpPct = Int(o, "xpPct"), NoahPct = Int(o, "noahPct"), ApPct = Int(o, "apPct"), Ap = Int(o, "ap"),
                ApClass = Int(o, "apClass"), ApClassPct = Int(o, "apClassPct"),
                AcClass = Int(o, "acClass"), AcClassPct = Int(o, "acClassPct"),
                MaxWeight = Int(o, "maxWeight"), Np = Int(o, "np"),
            });
        }
    }

    private static void LoadSkillOptions(JsonElement root)
    {
        if (!Section(root, "_ops", JsonValueKind.Object, out var rows)) return;
        foreach (var row in rows.EnumerateObject())
        {
            if (!int.TryParse(row.Name, out int id) || row.Value.ValueKind != JsonValueKind.Array) continue;
            var list = new List<ItemData.SkillOption>();
            foreach (var entry in row.Value.EnumerateArray())
                if (entry.ValueKind == JsonValueKind.Array && entry.GetArrayLength() >= 3)
                    list.Add(new ItemData.SkillOption(AsInt(entry[0]), AsInt(entry[1]), AsInt(entry[2])));
            if (list.Count > 0) ItemData.AddSkillOptions(id, list);
        }
    }

    private static void LoadSellGroups(JsonElement root)
    {
        if (!Section(root, "_sell", JsonValueKind.Object, out var groups)) return;
        foreach (var groupEntry in groups.EnumerateObject())
        {
            if (!int.TryParse(groupEntry.Name, out int group) || groupEntry.Value.ValueKind != JsonValueKind.Array) continue;
            var list = new List<ItemData.SellEntry>(groupEntry.Value.GetArrayLength());
            foreach (var e in groupEntry.Value.EnumerateArray())
            {
                if (e.ValueKind != JsonValueKind.Array || e.GetArrayLength() < 3) continue;
                list.Add(new ItemData.SellEntry { Id = AsInt(e[0]), Line = AsInt(e[1]), List = AsInt(e[2]) });
            }
            ItemData.AddSellGroup(group, list);
        }
    }

    private static void LoadPieceRewards(JsonElement root)
    {
        if (!Section(root, "_pieces", JsonValueKind.Object, out var pieces)) return;
        foreach (var piece in pieces.EnumerateObject())
        {
            if (!int.TryParse(piece.Name, out int pieceId) || piece.Value.ValueKind != JsonValueKind.Array) continue;
            var rewards = new int[piece.Value.GetArrayLength()];
            int i = 0;
            foreach (var reward in piece.Value.EnumerateArray()) rewards[i++] = AsInt(reward);
            if (rewards.Length > 0) ItemData.AddPieceRewards(pieceId, rewards);
        }
    }

    private static void LoadAttendance(JsonElement root)
    {
        if (!Section(root, "_attendance", JsonValueKind.Object, out var slots)) return;
        foreach (var slotEntry in slots.EnumerateObject())
        {
            if (!int.TryParse(slotEntry.Name, out int slot) || slotEntry.Value.ValueKind != JsonValueKind.Array
                || slotEntry.Value.GetArrayLength() < 2) continue;
            ItemData.AddAttendance(slot, AsInt(slotEntry.Value[0]), AsInt(slotEntry.Value[1]));
        }
    }

    private static void LoadUiHelp(JsonElement root)
    {
        if (!Section(root, "_help", JsonValueKind.Object, out var entries)) return;
        foreach (var entry in entries.EnumerateObject())
        {
            if (!int.TryParse(entry.Name, out int id) || entry.Value.ValueKind != JsonValueKind.Array
                || entry.Value.GetArrayLength() < 2) continue;
            ItemData.AddHelp(id, AsString(entry.Value[0]), AsString(entry.Value[1]));
        }
    }

    private static void LoadPriceOverrides(string? json)
    {
        if (json == null) return;
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) return;
        foreach (var entry in doc.RootElement.EnumerateObject())
            if (int.TryParse(entry.Name, out int id)) ItemData.AddPriceOverride(id, AsInt(entry.Value));
    }

    private static string Str(JsonElement o, string k) => o.TryGetProperty(k, out var v) ? AsString(v) : "";

    private static int Int(JsonElement o, string k) => o.TryGetProperty(k, out var v) ? AsInt(v) : 0;

    private static string AsString(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.String => v.GetString() ?? "",
        JsonValueKind.Number => v.GetRawText(),
        _ => "",
    };

    private static int AsInt(JsonElement v) => v.ValueKind switch
    {
        JsonValueKind.Number when v.TryGetInt64(out long whole) => unchecked((int)whole),
        JsonValueKind.Number => (int)v.GetDouble(),
        JsonValueKind.True => 1,
        _ => 0,
    };

    private const string ExtAttackIntervalColumn = "9";
    private const string ExtGradeClassColumn = "30";
    private const string ExtUnbindStonesColumn = "55";

    private static int RawInt(JsonElement o, string column) =>
        o.TryGetProperty("raw", out var raw) && raw.ValueKind == JsonValueKind.Object ? Int(raw, column) : 0;
}
