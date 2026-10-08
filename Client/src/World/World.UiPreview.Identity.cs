using System.Linq;
using Godot;
using LibreKO.Domain;
using LibreKO.Network;

namespace LibreKO;

public partial class World
{
    internal Control BuildCouncilRateUiPreview(string view)
    {
        ItemData.EnsureLoaded();
        if (view == "nation")
        {
            _kingLayer = new CanvasLayer(); AddChild(_kingLayer);
            BuildNationTaxRate(); OnKingTariffRead(new KingTariff(KingElection.Success, 3));
            _nationTaxRatePanel.GetParent().RemoveChild(_nationTaxRatePanel);
            return _nationTaxRatePanel;
        }
        _siegeLayer = new CanvasLayer(); AddChild(_siegeLayer);
        BuildSiegeTaxRate(); _siegeRates = new SiegeTaxRates(SiegeWarfare.Success, 3, 4, SiegeWarfare.FeeLimit);
        OpenSiegeTaxRate(view == "fee" ? SiegeRateKind.DungeonFee : SiegeRateKind.Moradon);
        _siegeTaxRatePanel.GetParent().RemoveChild(_siegeTaxRatePanel);
        return _siegeTaxRatePanel;
    }
    internal Control BuildClanCreateUiPreview()
    {
        ClanCreateInit();
        _clanCreatePanel.Ready += () => Callable.From(() => OpenClanCreate($"Name your clan. Founding it costs {ClanTypes.CreationCoins:n0} gold and makes you its chief.")).CallDeferred();
        _clanCreatePanel.GetParent().RemoveChild(_clanCreatePanel);
        return _clanCreatePanel;
    }
    internal Control BuildNameChangeUiPreview()
    {
        BuildNameChangePanel(); _nameChangePanel.Ready += () => Callable.From(OpenNameChange).CallDeferred();
        _nameChangePanel.GetParent().RemoveChild(_nameChangePanel);
        return _nameChangePanel;
    }
    internal Control BuildDisguiseUiPreview(bool empty)
    {
        BuildDisguisePanel();
        _disguiseGroups = empty ? System.Array.Empty<DisguiseGroup>() : new[]
        {
            new DisguiseGroup(30, Enumerable.Range(0, 18).Select(i => new DisguiseForm(i + 1,
                i == 0 ? "Kecoon" : i == 1 ? "Orc Watcher" : "Transformation creature " + (i + 1), 30, 600001 + i, 379091000, 1,
                i == 1 ? "Increase maximum health while transformed." : "", 0)).ToArray()),
            new DisguiseGroup(50, new[] { new DisguiseForm(19, "Giant Golem", 50, 600020, 379091000, 1, "", 0) }),
            new DisguiseGroup(70, new[] { new DisguiseForm(20, "Dark Mare", 70, 600021, 379091000, 1, "", 0) })
        };
        Sheet.ApplyLevel(55, 0, 100, 1000);
        PickDisguiseGroup(0); _disguiseShown = true; _disguisePanel.Visible = true;
        _disguisePanel.GetParent().RemoveChild(_disguisePanel);
        return _disguisePanel;
    }
}
