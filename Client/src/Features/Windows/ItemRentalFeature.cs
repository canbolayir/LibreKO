using Godot;

namespace LibreKO.Features.Windows;

internal sealed class ItemRentalFeature : IFeature, IClosableWindow
{
    private const string Title = "Item Rental";
    private const int BodyWidth = 660;
    private const float ListHeight = 300f;
    private const float CellSize = 40f;
    private const int BagColumns = 7;
    private const int FontSize = 13;
    private const int SmallFont = 12;
    private const int MaxQueryLength = 80;
    private const double MaxDailyPrice = 2_100_000_000;
    private const double StartDailyPrice = 10_000;
    internal const string RentLabel = "Rent";
    internal const string ListLabel = "List an item";
    internal const string MineLabel = "My items";

    private static readonly (RentalCategory Value, string Label)[] Categories =
    {
        (RentalCategory.Any, "Any kind"), (RentalCategory.Weapon, "Weapons"), (RentalCategory.Armor, "Armor"), (RentalCategory.Accessory, "Accessories"),
    };

    private static readonly (RentalClass Value, string Label)[] Classes =
    {
        (RentalClass.Any, "Any class"), (RentalClass.Warrior, "Warrior"), (RentalClass.Rogue, "Rogue"), (RentalClass.Mage, "Mage"), (RentalClass.Priest, "Priest"),
    };

    private static readonly (RentalSort Value, string Label)[] Sorts =
    {
        (RentalSort.Newest, "Newest"), (RentalSort.PriceLow, "Price: low first"), (RentalSort.PriceHigh, "Price: high first"),
    };

    private readonly IWorldContext context;
    private readonly ItemRentalSession rental;
    private readonly SelfSession self;
    private readonly IItemRentalRequests requests;

    public ItemRentalFeature(IWorldContext context, ItemRentalSession rental, SelfSession self, IItemRentalRequests requests)
    {
        this.context = context;
        this.rental = rental;
        this.self = self;
        this.requests = requests;
    }

    private CanvasLayer layer = null!;
    internal HudWindow Window { get; private set; } = null!;
    private readonly Dictionary<string, Control> pages = new();
    private readonly Dictionary<string, Button> tabs = new();
    private string tab = RentLabel;
    private int npcId;
    private bool busy;
    private RentalBoard? board;
    private RentalOffer? picked;
    private int pickedBagSlot = -1;

    private LineEdit query = null!;
    private OptionButton category = null!, classPick = null!, sort = null!;
    private VBoxContainer offers = null!;
    private Label pager = null!;
    private Button previous = null!, next = null!;
    private Label detailName = null!, detailLine = null!, detailTotal = null!;
    private SpinBox rentDays = null!;
    private Button rentButton = null!;

    private GridContainer bag = null!;
    private Label listItem = null!, listFee = null!, listNote = null!;
    private SpinBox listDays = null!, listPrice = null!;
    private Button listButton = null!;

    private VBoxContainer mine = null!;
    private Label mineSummary = null!;

    private Label status = null!;

    public bool IsOpen => Window is { Visible: true };

    public void Build()
    {
        BuildWindow();
        rental.OpenEvent += OnOpen;
        rental.BoardEvent += OnBoard;
        rental.ResultEvent += OnResult;
    }

    public void Dispose()
    {
        rental.OpenEvent -= OnOpen;
        rental.BoardEvent -= OnBoard;
        rental.ResultEvent -= OnResult;
    }

    public void Close()
    {
        Window.Visible = false;
        context.Tooltips.Hide();
    }

    internal void OnOpen(int npc)
    {
        npcId = npc;
        busy = false;
        status.Text = "";
        Window.Visible = true;
        Window.MoveToFront();
        Select(RentLabel);
    }

    private void BuildWindow()
    {
        layer = new CanvasLayer { Layer = UiLayers.ServiceWindows };
        context.Root.AddChild(layer);
        Window = new HudWindow("item_rental", Title, BodyWidth) { Visible = false };
        Window.Closed += Close;
        layer.AddChild(Window);
        var body = Window.Body;
        body.AddThemeConstantOverride("separation", 8);

        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 5);
        body.AddChild(bar);
        body.AddChild(UiTheme.Rule());
        var host = new MarginContainer();
        body.AddChild(host);
        AddPage(bar, host, RentLabel, BuildRentPage());
        AddPage(bar, host, ListLabel, BuildListPage());
        AddPage(bar, host, MineLabel, BuildMinePage());

        status = UiTheme.Text("", SmallFont, UiTheme.TextLo);
        status.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        status.CustomMinimumSize = new Vector2(BodyWidth, 0);
        body.AddChild(status);
    }

    private void AddPage(HBoxContainer bar, MarginContainer host, string label, Control page)
    {
        page.Visible = false;
        host.AddChild(page);
        pages[label] = page;
        var button = UiTheme.UnderlineTabButton(label);
        button.Pressed += () => { if (!busy) Select(label); else button.ButtonPressed = label == tab; };
        tabs[label] = button;
        bar.AddChild(button);
    }

    internal void Select(string label)
    {
        tab = label;
        foreach (var (key, page) in pages) page.Visible = key == label;
        foreach (var (key, button) in tabs) button.ButtonPressed = key == label;
        picked = null;
        pickedBagSlot = -1;
        if (label == ListLabel) FillBag();
        else Ask(0);
        RefreshDetails();
        RefreshListForm();
        Callable.From(Window.ResetSize).CallDeferred();
    }

    internal void PickOffer(int id)
    {
        picked = board?.Offers.FirstOrDefault(offer => offer.Id == id);
        if (board is { Own: false } shown) FillOffers(shown);
    }

    internal void PickBagSlot(int bagSlot)
    {
        pickedBagSlot = bagSlot;
        FillBag();
        RefreshListForm();
    }

    private RentalQuery Query(int page) => tab == MineLabel
        ? new RentalQuery(true, page, "", RentalCategory.Any, RentalClass.Any, RentalSort.Newest)
        : new RentalQuery(false, page, query.Text.Trim(), Categories[category.Selected].Value, Classes[classPick.Selected].Value, Sorts[sort.Selected].Value);

    private void Ask(int page)
    {
        if (npcId == 0) return;
        requests.Board(npcId, Query(page));
    }

    private Control BuildRentPage()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        var filters = new HBoxContainer();
        filters.AddThemeConstantOverride("separation", 5);
        box.AddChild(filters);
        query = new LineEdit { PlaceholderText = "Item name", MaxLength = MaxQueryLength, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        query.TextSubmitted += _ => Ask(0);
        filters.AddChild(query);
        category = Pick(Categories.Select(c => c.Label));
        classPick = Pick(Classes.Select(c => c.Label));
        sort = Pick(Sorts.Select(s => s.Label));
        foreach (var option in new[] { category, classPick, sort })
        {
            option.ItemSelected += _ => Ask(0);
            filters.AddChild(option);
        }
        var search = UiTheme.SmallButton("Search", "Search the listings");
        search.Pressed += () => Ask(0);
        filters.AddChild(search);

        box.AddChild(Scroll(offers = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }));

        var pageBar = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        pageBar.AddThemeConstantOverride("separation", 8);
        previous = UiTheme.SmallButton("<", "Previous page");
        previous.Pressed += () => Ask((board?.Page ?? 0) - 1);
        next = UiTheme.SmallButton(">", "Next page");
        next.Pressed += () => Ask((board?.Page ?? 0) + 1);
        pager = UiTheme.Text("", SmallFont, UiTheme.TextLo);
        pageBar.AddChild(previous);
        pageBar.AddChild(pager);
        pageBar.AddChild(next);
        box.AddChild(pageBar);

        box.AddChild(UiTheme.Rule());
        detailName = UiTheme.Text("Pick an item to see its terms.", FontSize + 1, UiTheme.GoldVivid);
        box.AddChild(detailName);
        detailLine = UiTheme.Text("", SmallFont, UiTheme.TextLo);
        box.AddChild(detailLine);
        var rentRow = new HBoxContainer();
        rentRow.AddThemeConstantOverride("separation", 8);
        box.AddChild(rentRow);
        rentRow.AddChild(UiTheme.Text("Days", FontSize, UiTheme.TextHi));
        rentDays = UiTheme.NumberBox(1, 1, 1, 84, FontSize);
        rentDays.ValueChanged += _ => RefreshDetails();
        rentRow.AddChild(rentDays);
        detailTotal = UiTheme.Text("", FontSize, UiTheme.TextHi);
        detailTotal.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        rentRow.AddChild(detailTotal);
        rentButton = UiTheme.ActionButton("Rent this item", "Pay and receive a copy for the days chosen");
        rentButton.Pressed += ConfirmRent;
        rentRow.AddChild(rentButton);
        box.AddChild(UiTheme.Text("The rented copy disappears from your bag when the rental ends.", SmallFont, UiTheme.TextDim));
        return box;
    }

    private Control BuildListPage()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        box.AddChild(UiTheme.Text("Items you can rent out: single weapons, armor and accessories that can be traded.", SmallFont, UiTheme.TextLo));
        bag = new GridContainer { Columns = BagColumns };
        bag.AddThemeConstantOverride("h_separation", 3);
        bag.AddThemeConstantOverride("v_separation", 3);
        box.AddChild(bag);
        box.AddChild(UiTheme.Rule());
        listItem = UiTheme.Text("Pick an item from your bag.", FontSize + 1, UiTheme.GoldVivid);
        box.AddChild(listItem);
        var form = new HBoxContainer();
        form.AddThemeConstantOverride("separation", 8);
        box.AddChild(form);
        form.AddChild(UiTheme.Text("Days", FontSize, UiTheme.TextHi));
        listDays = UiTheme.NumberBox(1, 1, 1, 84, FontSize);
        listDays.ValueChanged += _ => RefreshListForm();
        form.AddChild(listDays);
        form.AddChild(UiTheme.Text("Gold a day", FontSize, UiTheme.TextHi));
        listPrice = UiTheme.NumberBox(1, MaxDailyPrice, 1, 150, FontSize);
        listPrice.Value = StartDailyPrice;
        listPrice.ValueChanged += _ => RefreshListForm();
        form.AddChild(listPrice);
        listFee = UiTheme.Text("", FontSize, UiTheme.TextHi);
        listFee.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        form.AddChild(listFee);
        listButton = UiTheme.ActionButton("List item", "Hand the item to the broker and list it");
        listButton.Pressed += ConfirmList;
        form.AddChild(listButton);
        listNote = UiTheme.Text("", SmallFont, UiTheme.TextDim);
        listNote.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        listNote.CustomMinimumSize = new Vector2(BodyWidth, 0);
        box.AddChild(listNote);
        return box;
    }

    private Control BuildMinePage()
    {
        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 6);
        mineSummary = UiTheme.Text("", SmallFont, UiTheme.TextLo);
        box.AddChild(mineSummary);
        box.AddChild(Scroll(mine = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill }));
        return box;
    }

    private static OptionButton Pick(IEnumerable<string> labels)
    {
        var option = new OptionButton { FocusMode = Control.FocusModeEnum.None };
        foreach (var label in labels) option.AddItem(label);
        option.Selected = 0;
        return option;
    }

    private static ScrollContainer Scroll(VBoxContainer content)
    {
        content.AddThemeConstantOverride("separation", 3);
        var scroll = new ScrollContainer
        {
            CustomMinimumSize = new Vector2(BodyWidth, ListHeight),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        scroll.AddChild(content);
        return scroll;
    }

    internal void OnBoard(RentalBoard shown)
    {
        busy = false;
        if (shown.Own != (tab == MineLabel)) return;
        board = shown;
        if (!shown.Enabled) Say(false, "The item rental market is closed.");
        if (shown.Own) FillMine(shown);
        else FillOffers(shown);
        RefreshListForm();
    }

    private void FillOffers(RentalBoard shown)
    {
        Clear(offers);
        foreach (var offer in shown.Offers)
        {
            var row = OfferRow(offer, $"{offer.Owner}   ·   {offer.DailyPrice:N0} gold a day   ·   {Days(offer.WholeDaysLeft)} left",
                ItemRentalRules.CanRent(shown, offer) ? UiTheme.TextHi : UiTheme.TextLo);
            row.GuiInput += input =>
            {
                if (input is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) return;
                picked = offer;
                FillOffers(shown);
                RefreshDetails();
            };
            offers.AddChild(row);
        }
        if (shown.Offers.Count == 0) offers.AddChild(UiTheme.Text("Nothing is listed that matches.", FontSize, UiTheme.TextLo));
        pager.Text = $"Page {shown.Page + 1} / {shown.Pages}   ({shown.Total} listed)";
        previous.Disabled = shown.Page <= 0;
        next.Disabled = shown.Page + 1 >= shown.Pages;
        if (picked != null) picked = shown.Offers.FirstOrDefault(offer => offer.Id == picked.Id);
        RefreshDetails();
    }

    private PanelContainer OfferRow(RentalOffer offer, string line, Color colour)
    {
        var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        row.AddThemeStyleboxOverride("panel", UiTheme.Row(picked?.Id == offer.Id));
        var hb = new HBoxContainer();
        hb.AddThemeConstantOverride("separation", 8);
        row.AddChild(hb);
        hb.AddChild(Icon(offer.ItemId, offer.Durability));
        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", -2);
        hb.AddChild(text);
        text.AddChild(UiTheme.Text(ItemData.DisplayName(offer.ItemId) is { Length: > 0 } name ? name : $"item {offer.ItemId}", FontSize, colour));
        text.AddChild(UiTheme.Text(line, SmallFont, UiTheme.TextDim));
        return row;
    }

    private ItemSlotView Icon(int itemId, short durability)
    {
        var cell = new ItemSlotView(CellSize) { SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        cell.Set(new ItemSlot { ItemId = itemId, Durability = durability, Count = 1 });
        cell.Hovered += view => context.Tooltips.Show(-1, view.Item);
        cell.Unhovered += _ => context.Tooltips.Hide();
        return cell;
    }

    private void RefreshDetails()
    {
        if (detailName == null) return;
        if (picked is not { } offer || board is not { } shown)
        {
            detailName.Text = "Pick an item to see its terms.";
            detailLine.Text = "";
            detailTotal.Text = "";
            rentButton.Disabled = true;
            return;
        }
        detailName.Text = ItemData.DisplayName(offer.ItemId);
        int most = Math.Max(1, offer.WholeDaysLeft);
        rentDays.MaxValue = most;
        if (rentDays.Value > most) rentDays.Value = most;
        detailLine.Text = $"Owner {offer.Owner}   ·   {offer.DailyPrice:N0} gold a day   ·   Listing time remaining: {Days(offer.WholeDaysLeft)}   ·   {ItemRentalRules.Status(offer, shown.NowUnix)}";
        detailTotal.Text = $"Total payment {ItemRentalRules.Cost((int)rentDays.Value, offer.DailyPrice):N0} gold";
        rentButton.Disabled = busy || !ItemRentalRules.CanRent(shown, offer);
    }

    private void ConfirmRent()
    {
        if (picked is not { } offer || busy) return;
        int days = (int)rentDays.Value;
        Notice.Confirm(context.Root,
            $"Rent {ItemData.DisplayName(offer.ItemId)} for {Days(days)} for {ItemRentalRules.Cost(days, offer.DailyPrice):N0} gold? The copy disappears when the rental ends.",
            "Rent", "Cancel", () => Send(() => requests.Rent(npcId, offer, days)), title: Title);
    }

    private void FillBag()
    {
        Clear(bag);
        var inventory = self.Inventory;
        for (int abs = Inventory.GridStart; abs < Inventory.GridStart + Inventory.GridCount && abs < inventory.Length; abs++)
        {
            var slot = inventory[abs];
            if (slot.IsEmpty || ItemData.Get(slot.ItemId) is not { } item || !ItemRentalRules.Rentable(item, slot)) continue;
            int bagSlot = abs - Inventory.GridStart;
            var cell = new ItemSlotView(CellSize) { Index = bagSlot };
            cell.Set(slot);
            cell.Look = bagSlot == pickedBagSlot ? SlotLook.Selected : SlotLook.Normal;
            cell.Hovered += view => context.Tooltips.Show(abs, view.Item);
            cell.Unhovered += _ => context.Tooltips.Hide();
            cell.Clicked += view =>
            {
                pickedBagSlot = view.Index;
                FillBag();
                RefreshListForm();
            };
            bag.AddChild(cell);
        }
        if (bag.GetChildCount() == 0) bag.AddChild(UiTheme.Text("Nothing in your bag can be rented out.", FontSize, UiTheme.TextLo));
    }

    private ItemSlot? PickedBagItem()
    {
        if (pickedBagSlot < 0) return null;
        var slot = self.Inventory[Inventory.GridStart + pickedBagSlot];
        return slot.IsEmpty ? null : slot;
    }

    private void RefreshListForm()
    {
        if (listFee == null) return;
        int minDays = board?.MinDays ?? 1, maxDays = Math.Max(minDays, board?.MaxDays ?? 1), fee = board?.FeePerDay ?? 0;
        listDays.MinValue = minDays;
        listDays.MaxValue = maxDays;
        if (listDays.Value < minDays) listDays.Value = minDays;
        long total = ItemRentalRules.Fee((int)listDays.Value, fee);
        listFee.Text = $"Fee {total:N0} gold";
        listNote.Text = $"The item stays with the broker until you collect it. The fee of {fee:N0} gold a day is paid now and not refunded; "
                        + $"the market keeps {board?.TaxPercent ?? 0}% of each rent.";
        var item = PickedBagItem();
        listItem.Text = item is { } held ? ItemData.DisplayName(held.ItemId) : "Pick an item from your bag.";
        listButton.Disabled = busy || item == null || board is not { Enabled: true } || self.Sheet.Gold < total;
    }

    private void ConfirmList()
    {
        if (PickedBagItem() is not { } item || busy) return;
        int days = (int)listDays.Value, price = (int)listPrice.Value, bagSlot = pickedBagSlot;
        long fee = ItemRentalRules.Fee(days, board?.FeePerDay ?? 0);
        Notice.Confirm(context.Root,
            $"List {ItemData.DisplayName(item.ItemId)} for {Days(days)} at {price:N0} gold a day? It stays with the broker until you collect it, and the fee of {fee:N0} gold is not refunded.",
            "List", "Cancel", () => Send(() => requests.List(npcId, (byte)bagSlot, item.ItemId, days, price)), title: Title);
    }

    private void FillMine(RentalBoard shown)
    {
        Clear(mine);
        long earnings = 0;
        foreach (var offer in shown.Offers)
        {
            earnings += offer.Earnings;
            var row = OfferRow(offer, $"{ItemRentalRules.Status(offer, shown.NowUnix)}   ·   {offer.DailyPrice:N0} gold a day   ·   earned {offer.Earnings:N0} gold", UiTheme.TextHi);
            var hb = (HBoxContainer)row.GetChild(0);
            var collect = UiTheme.SmallButton("Collect earnings", "Take the gold this item has earned");
            collect.Disabled = busy || offer.Earnings <= 0;
            collect.Pressed += () => Send(() => requests.CollectEarnings(npcId, offer.Id));
            hb.AddChild(collect);
            bool running = offer.ListingEndsUnix > shown.NowUnix;
            var back = UiTheme.SmallButton(offer.Collected ? "Collected" : running ? "Cancel listing" : "Collect item",
                running ? "End the listing and take the original back" : "Take the original back");
            back.Disabled = busy || !ItemRentalRules.CanCollectItem(offer);
            back.Pressed += () => ConfirmCollect(offer);
            hb.AddChild(back);
            mine.AddChild(row);
        }
        if (shown.Offers.Count == 0) mine.AddChild(UiTheme.Text("You have nothing listed.", FontSize, UiTheme.TextLo));
        mineSummary.Text = $"Earnings on this page: {earnings:N0} gold";
    }

    private void ConfirmCollect(RentalOffer offer)
    {
        if (offer.ListingEndsUnix > (board?.NowUnix ?? 0))
            Notice.Confirm(context.Root, $"Cancel the listing of {ItemData.DisplayName(offer.ItemId)} and take it back? Fees are not refunded.",
                "Collect", "Cancel", () => Send(() => requests.CollectItem(npcId, offer.Id)), title: Title);
        else
            Send(() => requests.CollectItem(npcId, offer.Id));
    }

    private void Send(Action request)
    {
        if (busy) return;
        busy = true;
        Say(true, "Processing...");
        request();
        RefreshDetails();
        RefreshListForm();
    }

    private void OnResult(bool ok, string message)
    {
        busy = false;
        Say(ok, message);
        if (!IsOpen) return;
        if (ok)
        {
            picked = null;
            pickedBagSlot = -1;
            if (tab == ListLabel) FillBag();
            Ask(tab == ListLabel ? 0 : board?.Page ?? 0);
        }
        RefreshDetails();
        RefreshListForm();
    }

    private void Say(bool ok, string message)
    {
        status.Text = message;
        status.AddThemeColorOverride("font_color", ok ? UiTheme.Good : UiTheme.Bad);
    }

    private static string Days(int days) => days == 1 ? "1 day" : $"{days} days";

    private static void Clear(Node node)
    {
        foreach (var child in node.GetChildren())
        {
            node.RemoveChild(child);
            child.QueueFree();
        }
    }
}
