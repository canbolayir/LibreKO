namespace LibreKO.Features.Windows;

internal sealed partial class ManesSurvivalFeature
{
    private void Notify() => PluginHost.Game.RaiseManesSurvival();

    private GameManesBoard PluginBoard() =>
        new(Board.Open && Board.EndsAt > context.Now, SecondsUntil(Board.EndsAt), Board.Participants, Board.Applied);

    private GameManesBar PluginBar() => new(
        manes.Running && InZone && SecondsUntil(eventEndsAt) > 0, bossTitle, bossHpValue, bossMaxValue,
        SecondsUntil(eventEndsAt), SecondsUntil(nonBattleEndsAt), manes.Score);

    private GameManesOffer? PluginOffer()
    {
        if (CurrentOffer is not { } offer) return null;
        var skills = new List<GameManesChoice>();
        foreach (int code in offer.Skills)
        {
            if (Book.Pick(code) is not { } pick) continue;
            var skill = SkillData.Get(pick.Magic);
            skills.Add(new GameManesChoice(code, skill?.Name ?? code.ToString(), skill?.Desc ?? "", SkillIcons.Get(pick.Magic),
                hotbar.ManesFitOf(code, Book) == ManesFit.AlreadyHeld));
        }
        var potions = new List<GameManesPotion>();
        foreach (int code in offer.Potions)
        {
            if (Book.Pick(code) is not { } potion) continue;
            potions.Add(new GameManesPotion(code, $"{ItemData.DisplayName(potion.Item)} X {potion.Count}",
                TextTemplate.Fill(Text(ManesSurvival.NoahPriceText, "%d noah"), potion.Price), ItemIcons.Get(potion.Item), potionsWaiting.Contains(code)));
        }
        string title = Text(offer.Bonus ? ManesSurvival.BonusTitleText : ManesSurvival.PickTitleText,
            offer.Bonus ? "Pick 1 (bonus) of Skills" : "Select 1 of the skills (excluding potions)");
        return new GameManesOffer(offer.Bonus, title, skills, potions, selectedPick, manes.Offers.Count, !fullWindow, context.Now < smallBarBlinkUntil);
    }

    private IReadOnlyList<GameManesPassive> PluginPassives()
    {
        var list = new List<GameManesPassive>();
        foreach (int code in manes.Passives)
        {
            int at = list.FindIndex(p => p.Code == code);
            if (at >= 0)
            {
                list[at] = list[at] with { Count = list[at].Count + 1 };
                continue;
            }
            var pick = Book.Pick(code);
            var skill = pick == null ? null : SkillData.Get(pick.Magic);
            list.Add(new GameManesPassive(code, skill?.Name ?? "", pick == null ? null : SkillIcons.Get(pick.Magic), 1));
        }
        return list;
    }

    private GameManesTotals PluginTotals()
    {
        var totals = ManesSurvival.Totals(manes.Passives.Select(c => Book.Pick(c)).OfType<ManesPick>());
        return new GameManesTotals(
            TextTemplate.Fill(Text(ManesSurvival.IncreaseText, "%d%% increase"), totals.Hp),
            TextTemplate.Fill(Text(ManesSurvival.IncreaseText, "%d%% increase"), totals.AttackUp),
            TextTemplate.Fill(Text(ManesSurvival.DecreaseText, "%d%% decrease"), totals.AttackDown));
    }

    private GameManesChoice? PluginPassiveWon()
    {
        if (passiveWon is not { } pick) return null;
        var skill = SkillData.Get(pick.Magic);
        return new GameManesChoice(pick.Code, skill?.Name ?? "", skill?.Desc ?? "", SkillIcons.Get(pick.Magic), false);
    }

    private GameManesRanking PluginRanking()
    {
        string me = self.LastEnter.Name ?? "";
        var rows = new List<GameManesRankRow>(ManesSurvival.RankRows);
        var known = ranking?.Rows ?? [];
        for (int i = 0; i < ManesSurvival.RankRows; i++)
        {
            if (i >= known.Count)
            {
                rows.Add(new GameManesRankRow((i + 1).ToString(), Nations.NotSelected, Empty, Empty, Empty, Empty, false));
                continue;
            }
            var row = known[i];
            rows.Add(new GameManesRankRow((i + 1).ToString(), row.Nation, Number(row.Kills), row.Clan.Length == 0 ? Empty : row.Clan,
                row.Name.Length == 0 ? Empty : row.Name, Number(row.Score), row.Name.Length > 0 && row.Name == me));
        }
        var mine = new GameManesRankRow(
            ranking is { MyRank: > 0 } ? ranking.MyRank.ToString() : Text(ManesSurvival.DefeatBossText, "Defeat the Boss"),
            self.LastEnter.Nation, Number(ranking?.MyKills ?? 0),
            string.IsNullOrEmpty(self.LastEnter.ClanName) ? Empty : self.LastEnter.ClanName, me, Number(ranking?.MyScore ?? 0), true);
        return new GameManesRanking(rankShown, rows, mine);
    }

    private sealed class PluginManesBridge(ManesSurvivalFeature feature) : IGameManesSurvival
    {
        event Action? IGameManesSurvival.Changed { add { } remove { } }

        public GameManesBoard Board => feature.PluginBoard();
        public GameManesBar Bar => feature.PluginBar();
        public GameManesOffer? Offer => feature.PluginOffer();
        public IReadOnlyList<GameManesPassive> Passives => feature.PluginPassives();
        public GameManesTotals Totals => feature.PluginTotals();
        public GameManesChoice? PassiveWon => feature.PluginPassiveWon();
        public GameManesRanking Ranking => feature.PluginRanking();

        public void SignUp(bool apply)
        {
            feature.SetApplied(apply);
            feature.requests.SignUp(apply);
            feature.Notify();
        }

        public void Select(int index) => feature.SelectPick(index);
        public void Complete() => feature.Complete();
        public void Buy(int code) => feature.BuyPotion(code);
        public void Minimise() => feature.MinimisePicks();
        public void Reopen() => feature.ReopenPicks();
        public void ClosePassiveWon() => feature.ClosePassiveWon();
    }
}
