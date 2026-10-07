# NPC services, market history and event integration

This fork incorporates the complete upstream changes through `28112eea232f7160fe4f017165b9fc79a3e76687`, including `7f21442` and `f4c4127`. Use the matching Classic plugin and deploy the client and server together. The source merge, Classic adaptations, tests and this guide belong to one integration commit.

## Added or replaced functionality

| Area | Result |
| --- | --- |
| Market price | Completed merchant trades feed daily price history. Premium clients can compare a listing price and open the history chart without losing the pending listing. |
| Akara's Altar | Scheduled special auctions, bids, balance, results and claims replace the old auction implementation. |
| Shojin/Jewel | Table-driven item combination and a browsable recipe book replace the old combination flow. |
| Mekin | Tarot readings use the generated original card art and spin/reveal sequences. |
| Transformation | The item/premium-specific form list, selection and refusals come from the generated transformation table. |
| King, nation and Delos | Election, ruler actions, treasury and siege/castle services receive new native screens and protocol handlers. |
| Under the Castle | Zone 86, event registration, boss/gate progression, rewards, scheduler and GM event commands are included. Event scheduling follows the server's configured time zone. |
| Event departure | Chaos, Border Defense War, Under the Castle and Juraid have a movable Leave banner and confirmation; `/leave` and `/exit` remain available. |
| Retired windows | Obsolete Daily Quest and Event Quest fixed-list windows and handlers are removed. Ordinary daily quests in the quest log remain available. Other legacy NPC services/opcodes removed upstream are removed here too. |

New service pages retain upstream native compositions. Their eventual Classic redesign is separate from this integration.

## Classic behavior retained

Existing Anvil, inventory/bag, vendor, trade, merchant, chat/Info, mail and character-page bridges remain intact. Recipe-backed Anvil socket validation and success/failure animations are unchanged. Trade quantity Enter handling remains separate from explicit final trade approval. Stack merging, bag quantity prompts, pending transaction guards, Silver Selling/Copper Buying signs and existing merchant approval remain in place.

The listing price stage exposes stable names for its market comparison row, hint and history button. The Classic plugin uses those live controls and callbacks instead of duplicating market state. It extends only the straight rails of the original price frame when premium comparison is available. Non-premium, quantity and purchase-approval geometry remain unchanged. Escape closes history before the underlying price prompt; listing input remains preserved.

New noncombat service/event notices use the existing Info routing. Event departure uses the plugin-aware Notice confirmation instead of a separate Godot popup. Repeated clicks keep one confirmation, cancellation leaves the player in the event, and a zone change invalidates the old confirmation. Existing Classic window IDs, Anvil choice and clan-detail hooks remain registered.

Custom Moradon maps/registrations, local import noise, generated retail content, settings, database backups and installed binaries are excluded from the commit.

## Content and deployment

Use [LibreKO-Assets](https://github.com/ZeusAFK/LibreKO-Assets) revision `e6553f57f67c1d7d99334610c6861f2ef76ba251` or a compatible later version. Source commits do not include generated tables or card textures. Generate them from an installed retail client you are authorized to use:

```powershell
python bake.py --ko "<retail installation>" --out "<assets output>" --only disguise fortune special-auction item-combine
```

Required outputs are `skills/disguise.json`, `ui/item_combine.json`, `ui/special_auction.json` and the complete `ui/fortune` folder. Import and include them in the client content pack. Extension item icons, alias weapon glows and elemental trail textures are also supported by the updated Assets pipeline; weapon glow generation requires the already baked weapon index, aliases and effect descriptors.

Back up the database before running the matching server. Four migrations add market price days, special auctions, item combination recipes, and nation introductions/dungeon entrance fees. Startup applies migrations and updated seed data. Preserve installation-specific settings and deploy the matching client/plugin/content pack together with the server.

## Verification limits

The integrated source passed 1,071 client, 2,263 game, 57 common and 16 login tests (3,407 total). Source-driven Godot fixtures check both nations, live merchant price/history callbacks, unchanged parent-page pixels and rendered bounds, Anvil placement/animations, populated recipe/auction/transformation content, fortune animation completion, and event confirmation cancellation/zone invalidation.

See the companion plugin's review and sanitized verification summary. Fixture replies are not live player transactions; no live auction bids, purchases, item upgrades or event rewards were performed by the audit.
