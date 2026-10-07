# Classic client integration

Companion theme: [LibreKO-knightonline-ui-classic](https://github.com/canbolayir/LibreKO-knightonline-ui-classic).

The normal, VIP and clan storage controls and quantity/synchronization behavior are documented in [Storage integration](storage-integration.md).

The reviewed Anvil extension is documented separately in [Anvil integration](anvil-integration.md), with recipe snapshot maintenance and validation results.

The NPC services, market history and Under the Castle integration are documented in [NPC and event integration](upstream-npc-events-integration.md). The additional event and compatibility review are documented in [Forgotten Temple integration](forgotten-temple-integration.md).

## Scope and history

This series organizes existing local work into separate feature commits on upstream `9cc436e977347b7aa4067ff7c52c5b9da9b500e5`. It preserves the working source content; it does not recreate original edit dates. Shared plugin interfaces and final bridge wiring span the series, so use the complete tip with the companion plugin.

Custom Moradon terrain, server map files and zone registrations are intentionally excluded. Local import line-ending noise, installed binaries, account settings and database backups are not published.

## Why, how and diffs

### 01. feat(plugins): expose Classic character, HUD and interaction contracts

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/ebf3a65b40ece0875699823a4205976c8aa7acf6)

**Why:** Classic presentation must use live game services rather than copy their state or replace native gameplay rules.

**How:** Add backward-compatible default interface members for character pages, portraits, notifications, bag quantities, chat links, commands, minimap visibility and hotbar selection. Expose native header/attention styling and stable window IDs.

**Files:** `Client/src/Plugins/PluginGame.cs`, `Client/src/Plugins/PluginUi.cs`, `Client/src/Plugins/CharacterPanel.cs`, `Client/src/Ui/HudWindow.cs`

### 02. fix(chat): preserve Classic channels and route service notices to Info

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/a09a8f5f804747b3a5cc6ba7e73fafb797ff0b64)

**Why:** The upstream chat rework must retain Classic channel colors and service messages while keeping private messages in their own windows.

**How:** Format escaped Classic lines, publish after storing entries, filter history, expose nearby players without a duplicate native card, and distinguish gold income/expense in Info. Route noncombat service notices through ChatStatusNotice.

**Files:** `Client/src/Domain/ClassicChatFormat.cs`, `Client/tests/LibreKO.Tests/ClassicChatFormatTests.cs`, `Client/src/World/Social/ChatSystem.cs`, `Client/src/World/Social/ChatSystem.Log.cs`, `Client/src/World/Social/ChatSystem.Bubbles.cs`, `Client/src/World/World.ChatHost.cs`, `Client/src/World/World.CombatLog.cs`, `Client/src/World/World.Stats.cs`, `Client/src/World/World.AdminPanel.cs`, `Client/src/World/World.Awaken.cs`, `Client/src/World/World.BattleEvent.cs`, `Client/src/World/World.Bifrost.cs`, `Client/src/World/World.Cape.cs`, `Client/src/World/World.Challenge.cs`, `Client/src/World/World.ChangeHair.cs`, `Client/src/World/World.ClanPoints.cs`, `Client/src/World/World.Disguise.cs`, `Client/src/World/World.Genie.cs`, `Client/src/World/World.GenieAdvanced.cs`, `Client/src/World/World.GuardPet.cs`, `Client/src/World/World.King.cs`, `Client/src/World/World.NameChange.cs`, `Client/src/World/World.ObjectEvent.cs`, `Client/src/World/World.Seasonal.cs`, `Client/src/World/World.Shout.cs`, `Client/src/World/World.StateVisual.cs`, `Client/src/World/World.VipWarehouse.cs`, `Client/src/World/World.Warp.cs`

### 03. fix(skills): describe the actual equipped weapon requirement

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/42c467340ae16c6abae2feb69bd88ba79d67ef3f)

**Why:** Displayed weapon text must agree with casting checks, including unrestricted weapon skills.

**How:** Map ItemGroup to readable equipment requirements and test weapon groups and mastery pages.

**Files:** `Client/src/Domain/SkillData.cs`, `Client/tests/LibreKO.Tests/SkillPageTests.cs`

### 04. fix(preview): isolate viewport particles and show elemental weapon glow

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/ce6474d75bada87fce31795113e27a52bcd5a0c8)

**Why:** Inventory portraits must retain weapon effects without sharing emitters with the live world or leaking pooled nodes.

**How:** Key particle pools by viewport and effect part, forget emitters on exit, share lazy weapon-glow lookup and apply glow alongside item shine in character previews.

**Files:** `Client/src/Fx/FxEmitterPool.cs`, `Client/src/Rig/CharacterPreview.cs`, `Client/src/World/World.Models.cs`, `Client/src/World/World.Models.Weapons.cs`

### 05. fix(presets): display and validate base stats plus allocated points

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/97d4f4f96c3e766fd3e45e22a53a1609e6077118)

**Why:** Zero-based totals are impossible in the game and the earlier conservative cap rejected valid class allocations.

**How:** Centralize creation/redistribution class-family bases; keep saved allocations, display actual totals, disable invalid steps and validate total/cap/remaining points before sending.

**Files:** `Client/src/Domain/StarterStats.cs`, `Client/src/Domain/PresetPlan.cs`, `Client/src/World/World.Preset.cs`, `Client/tests/LibreKO.Tests/PresetPlanTests.cs`, `Client/tests/LibreKO.Tests/LibreKO.Tests.csproj`

### 06. fix(quests): retain chosen rewards and expose cached NPC portraits

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/3be3f6d2be6c24518a995af5b26194aa2e993270)

**Why:** Reward choices should not reset on refresh or imply that an unavailable choice can be selected; dialogue portraits should follow the actual speaking NPC.

**How:** Cache pending choices and received receipts per quest, distinguish available options from received rewards, tag status and selection for the theme, and expose an isolated appearance-keyed NPC model factory using the retained talk target.

**Files:** `Client/src/World/World.Quest.cs`, `Client/src/World/World.QuestView.cs`, `Client/src/World/World.QuestTarget.cs`, `Client/src/World/World.QuestNotifications.cs`, `Client/src/World/World.Npc.cs`, `Client/src/World/World.NpcPortrait.cs`

### 07. feat(character): bridge live report, quest, clan and friend pages

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/58e00253228f1cd75ec40bc357f72c3ce090a42e)

**Why:** The Classic four-tab window needs real native data/actions and must not refetch a quest log when opening the selected detail.

**How:** Add CharacterPanelBridge rows/actions with permission checks; share live clan management, track selected pages and donations, and allow opening a main window without resetting its selection.

**Files:** `Client/src/World/World.CharacterPanel.cs`, `Client/src/World/World.MainPanel.cs`, `Client/src/World/World.Clan.cs`, `Client/src/World/World.Friends.cs`

### 08. fix(items): unify stack badges and expose reusable quantity controls

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/3d37660da7690ccade71e589ebd1047c7cafc91d)

**Why:** A stackable item with count one must be labeled consistently across inventory, store and warehouse, and commerce needs shared numeric controls.

**How:** Add a common count-badge rule, fixed full-rect bottom-right label bounds, tradability flags, optional ungrouped numeric entry, public quantity confirmation and trade-row item metadata.

**Files:** `Client/src/Domain/ItemData.cs`, `Client/src/Domain/ItemSlot.cs`, `Client/src/Ui/ItemSlotView.cs`, `Client/src/Ui/MoneyEdit.cs`, `Client/src/Ui/QuantityPrompt.cs`, `Client/src/World/World.WarehouseCell.cs`, `Client/src/World/World.PieceChange.cs`, `Client/src/World/World.TradeRow.cs`, `Client/tests/LibreKO.Tests/ItemSlotTradeTests.cs`

### 09. fix(vendor): confirm purchases and sales with authoritative checks

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/901f776507f7cfa49a6989de67249a0531c3b5a4)

**Why:** Classic vendor interactions need explicit approval, compact original catalogue pages, full-stack sale defaults and stale-item protection.

**How:** Use compact catalogue paging, guard busy/dead/move states, ask quantities only when useful, reuse buy/sell confirmation data, revalidate item and quantity before sending, and expose sellability/drag metadata to the theme.

**Files:** `Client/src/Domain/ShopCatalogue.cs`, `Client/src/World/World.Vendor.cs`, `Client/tests/LibreKO.Tests/ShopCatalogueTests.cs`

### 10. fix(inventory): synchronize bag amounts, stacks and zone snapshots

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/1b0cef9d4b075bc78daa5b628e2403208b909c39)

**Why:** Whole-stack right-click transfers and stale swap-only snapshots could split existing stacks or resurrect removed bags after teleporting.

**How:** Plan bag-to-grid merges before free slots, queue requested portions, apply confirmed merge/partial moves to LastEnter, append optional amount to the wire and validate it on the server. Reuse guarded item destruction and cover snapshots, bag transfers and server amount rules.

**Files:** `Client/src/Domain/Inventory.cs`, `Client/src/Domain/ItemMove.cs`, `Client/src/Network/Net.Items.cs`, `Client/src/Network/Net.Senders.cs`, `Client/src/Network/Net.World.cs`, `Client/src/World/World.Inventory.cs`, `Client/src/World/World.Inventory.Move.cs`, `Server/LibreKO.Game/Protocol/ItemMoveService.cs`, `Client/tests/LibreKO.Tests/BagStackTransferTests.cs`, `Client/tests/LibreKO.Tests/InventorySnapshotTests.cs`, `Server/tests/LibreKO.Game.Tests/ItemTests.cs`

### 11. feat(party): expose member state and original party docking

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/09aeb190cbb5b5c9c60470e3d9d0186f62356f91)

**Why:** Classic party rendering needs authoritative HP/MP, selection, leader/status data and seeking state without changing native invitation rules.

**How:** Attach structured member/list metadata, preserve tooltips and right-click leader actions, support member selection/private messages, detach obsolete rows immediately and dock the Classic party at the screen edge.

**Files:** `Client/src/World/World.Party.cs`, `Client/src/World/World.SeekParty.cs`, `Client/src/World/World.Dock.cs`

### 12. fix(trade): require explicit final approval and validate offer state

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/6e6ae334453dbedab8f2eb6e6be8f300b74be320)

**Why:** Trade must keep all twelve slots accessible and never finalize merely because Enter was used for quantity input.

**How:** Add themed request/final layers and explicit decide action; returning No preserves the offer. Validate quantity and tradability, prevent edits while pending or partner-locked, consolidate stack slot counting and publish authoritative metadata/tooltips.

**Files:** `Client/src/World/World.Exchange.cs`

### 13. fix(merchant): synchronize purchases and validate listing interactions

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/026d28c023783f7f4f1e5604e7c1c6ba9f47f528)

**Why:** Merchant quantity changes must follow server stack notifications, not a guessed single-item mutation; stale prompts must not send to a different stall.

**How:** Track pending purchase/sale identity, validate balance/coin cap/capacity/price, use chosen listing slots, support right-click and drag without double activation, seed full sale quantities, keep advert approval and expose Buying/Selling sign state. Notify both inventories and seller weight on the server.

**Files:** `Client/src/World/World.Merchant.cs`, `Client/src/World/World.MerchantBuy.cs`, `Client/src/World/World.MerchantCell.cs`, `Client/src/World/World.MerchantPrompt.cs`, `Client/src/World/World.MerchantStall.cs`, `Server/LibreKO.Game/Protocol/MerchantListingService.cs`, `Server/tests/LibreKO.Game.Tests/MerchantTransactionTests.cs`

### 14. fix(mail): refresh unread badges and fill partial stacks safely

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/a85315012312b26e8ab52cb90e46bff7fc1dfc53)

**Why:** Successful reads must clear notifications even after selection changes, and partial claims must use existing stack capacity without duplication.

**How:** Refresh unread count on successful ACK, update cached read state only on success, fill available space up to 9999 and retain the undelivered attachment remainder. Add a full-inventory repeated-claim regression test.

**Files:** `Client/src/Network/Net.Mail.cs`, `Client/src/World/World.Mail.cs`, `Server/LibreKO.Game/Protocol/MailService.cs`, `Server/tests/LibreKO.Game.Tests/MailServiceTests.cs`

### 15. fix(store): fit the upstream Power-Up Store on compact screens

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/47997b01811f3c2076eda43e189e055413451532)

**Why:** New cart and gift features must fit an 800x600 Classic client and preserve the existing Info notice route.

**How:** Keep category/cart controls for responsive width changes, switch search/sort tools to vertical layout on compact screens and route purchase results through ChatStatusNotice.

**Files:** `Client/src/World/World.ShoppingMall.cs`, `Client/src/World/World.ShoppingMall.Grid.cs`

### 16. feat(hud): connect Classic bridges, chat docking and private focus

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/16a1e1bbdcd1cc22c97c7f414a8bf851056199d8)

**Why:** Theme replacements must control native services consistently while retaining map heading, fixed chat tabs, independent dragging and predictable private-message focus.

**How:** Wire all Classic contracts to live services, preserve shoppingmall identity after upstream changes, expose HUD notification/hotbar state, snap adjacent chat/Info resize within 12 pixels with drag detachment, integrate whisper styling/focus and keep minimap heading tied to resolved movement. Extend Escape ordering for modal states.

**Files:** `Client/src/Ui/HudLayout.cs`, `Client/src/World/World.PluginBridge.cs`, `Client/src/World/World.Player.cs`, `Client/src/World/World.UiPanels.cs`, `Client/src/World/World.FullMap.cs`, `Client/src/World/World.Whisper.cs`, `Client/src/World/World.EscapeStack.cs`, `Client/src/World/World.Hotbar.cs`, `Client/src/World/World.Achievement.cs`, `Client/src/World/World.Attendance.cs`, `Client/src/World/World.MailIcon.cs`, `Client/src/World/World.TopIcons.cs`, `Client/src/World/World.cs`

### 17. perf(client): support Mobile rendering and reproducible diagnostics

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/2fa0ecff1f4c72cebab4fda311048e622b952206)

**Why:** Local performance work uses Mobile/D3D12 and needs explicit unsupported-feature handling plus reproducible source/content diagnostics.

**How:** Set Mobile defaults and threading policy, disable unavailable SSAO/fog controls, allow mounting existing content without extraction or overriding source files, and add renderer/character-selection smoke commands.

**Files:** `Client/project.godot`, `Client/settings.default.cfg`, `Client/src/Boot.cs`, `Client/src/RendererBenchmark.cs`, `Client/src/Packs.cs`, `Client/src/Sky/Sky.cs`, `Client/src/Ui/Ui.cs`

### 18. chore(godot): retain stable C# resource identifiers

[Commit and diff](https://github.com/canbolayir/LibreKO/commit/8e97bc54f323d1ea5fad57dc764c8b8b3d993a70)

**Why:** Plugin and new source scripts should retain their Godot resource identity across clean checkouts.

**How:** Track generated source UID sidecars only; exclude build/cache files and import-only line-ending noise.

**Files:** `Client/src/Domain/ClassicChatFormat.cs.uid`, `Client/src/Plugins/CharacterPanel.cs.uid`, `Client/src/Plugins/IPlugin.cs.uid`, `Client/src/Plugins/PluginAssets.cs.uid`, `Client/src/Plugins/PluginContext.cs.uid`, `Client/src/Plugins/PluginGame.cs.uid`, `Client/src/Plugins/PluginHost.cs.uid`, `Client/src/Plugins/PluginInfo.cs.uid`, `Client/src/Plugins/PluginLog.cs.uid`, `Client/src/Plugins/PluginManifest.cs.uid`, `Client/src/Plugins/PluginSettings.cs.uid`, `Client/src/Plugins/PluginUi.cs.uid`, `Client/src/RendererBenchmark.cs.uid`, `Client/src/Ui/SettingsPanel.Plugins.cs.uid`, `Client/src/World/World.CharacterPanel.cs.uid`, `Client/src/World/World.NpcPortrait.cs.uid`, `Client/src/World/World.PluginBridge.cs.uid`

## Clean-source validation

On 2026-10-06, a clean `git archive` of the committed implementation (with no custom Moradon content or untracked local files) passed 869 client, 2,018 game and 16 login tests: 2,903 total, zero failures. The Classic plugin also compiled against that clean client with zero warnings/errors.

The preceding source-driven Godot review inspected 20 Human/Karus store/mail captures, 40 inventory captures, 82 merchant captures, 28 trade captures and all eight Character Report/Quest/Clan/Friend parent pages; the parent pages match the approved baseline. NPC, chat/Info docking and mail wire/unread checks passed. Screenshots and sanitized records are in the companion plugin `docs` directory. No visual implementation changed during this Git publication.

## Behavior and compatibility

- Bag movement supports an optional amount on the existing item-move packet; old whole-stack requests remain supported. Client and server amount/snapshot handling must ship together.
- Trade Enter confirms quantity only. Final trade approval requires an explicit action; No keeps the current offer, with editing blocked once the partner has locked.
- Vendor and merchant prompts revalidate item identity, counts, funds and pending state. Server notifications remain authoritative for inventory quantities.
- Mail reads refresh unread count after successful acknowledgement. Partial attachment claims fill existing stack capacity and retain undelivered items; repeated claims cannot duplicate delivery.
- The upstream Power-Up Store/cart/gift mail protocol and database migrations remain integrated; deploy compatible client/server builds together.
- Theme interfaces use backward-compatible default members. The Classic window/bridge identity is retained, including `shoppingmall`.
- Renderer defaults now use Mobile/D3D12; unsupported SSAO/volumetric-fog options are disabled. This performance change is isolated in its own commit.

## Limits

## NPC services integration: 4772e7a

Upstream `4772e7abf4ee98630b4a8765e048ffb7f32408b1` is retained as a merge parent, preserving its feature history and the nineteen earlier local commits. New Kelly gender/race/face/hair, Kaishan account nation transfer and Menissiah merchant search windows deliberately retain upstream styling; their Classic redesign is deferred. Kaira/Hemes certificate and package exchanges, Maestro potion pricing, consumed clan rename scrolls and compatible humanoid transformation accessories are included. Character rename now requires the identity scroll and NPC entry rather than its removed hotkey; the live player name and plate update while notices retain the Info route.

The only textual merge conflict was in the rename success handler. The resolution preserves both live name refresh and local status routing. A separate interaction commit adds the three native service windows to the existing Escape stack and routes non-combat service notices to Info. Nation transfer retains its in-flight cancellation guard. No database migration was added. Custom Moradon terrain, maps and zone registrations remain local and excluded from publication.

Final verification: 915 client, 2,113 game and 16 login tests passed. Source-driven Godot audits cover six native service windows and their Escape callbacks, 40 inventory renders, 82 merchant renders, 28 trade renders, 20 PUS/mail renders, NPC speech/reward controls, chat docking and mail badges. All eight Human/Karus Character Report, Quest, Clan and Friend parent pages retain identical baseline pixels and checked control bounds. Existing bag quantity and stacking rules, vendor approvals, final trade approval with no Enter acceptance, merchant quantity/pending guards, mail partial claims and chat docking are retained. Review artifacts live under `research/upstream-4772e7a-audit` in the workspace.

Optional elemental weapon-trail textures are absent; the existing colored fallback remains active. Automated UI fixtures do not replace live player-to-player transaction testing. Original game artwork retains its original ownership; adapter code licensing does not relicense those assets.
