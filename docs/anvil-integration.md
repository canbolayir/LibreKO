# Reviewed Classic Anvil integration

The companion [Classic plugin](https://github.com/canbolayir/LibreKO-knightonline-ui-classic) supplies editable geometry and original artwork. This fork supplies inventory actions, serialized previews and recipe-backed placement. Install a matching client/plugin pair; an unmodified upstream client does not provide these hooks.

## Changes and reasons

- preserve nonzero item results after failed Logos upgrades, including identity, flags and existing durability. A failed upgrade is not always item destruction.
- preserve the displayed icon geometry and initial grab point when dragging native items, matching Classic inventory behavior.
- embed upgrade recipes and settings from this fork's seed tables. Validate origin class, scroll role, protection conflicts and positive-rate combinations rather than inferring server rules from the client artwork table.
- expose Classic bench sockets and embedded inventory through native actions. Serialize preview requests, discard stale snapshots, preserve reservations during pending results and validate placement consistently across right-click, drag, movement and swaps.

Ordinary inventory rearrangement remains available. Materials may be placed in either order when a valid completion exists. Upgrade suitability is checked at the receiving bench; unrelated inventory items retain their normal colors. Returning a staged stack material moves one unit through the existing server-confirmed inventory path.

## Recipe snapshot maintenance

Run `python Client/scripts/generate_anvil_rules.py` after changing `ItemUpgradeRecipes.json`, `Items.slot*.json` or `ItemUpgradeSettings.json` in the server seed data. Commit both generated files under `Client/data` and rebuild the client. The reviewed snapshot contains 95,056 origins. Customized database recipes may differ from the snapshot; server preview and execution remain authoritative for recipes, coins and final upgrade outcomes.

## Verification and limits

The reviewed build passed 934 client tests, including 15 added placement-rule regression cases. The plugin's source-driven Godot audit produced 102 captures and 2,290 checks per nation, including drag geometry, request locks, Enter/Escape warning behavior and timed success/failure sequences. Eight Character Report, Quest, Clan and Friend screenshots remained pixel-identical to the previous baseline.

See the plugin's [review and rendered examples](https://github.com/canbolayir/LibreKO-knightonline-ui-classic/blob/main/docs/anvil.md) and [verification record](https://github.com/canbolayir/LibreKO-knightonline-ui-classic/blob/main/docs/anvil-verification.json). Animation responses are controlled fixtures, not live upgrade rolls. Existing world success/failure effects remain unchanged.

No server code, custom Moradon content, generated import noise or installed binaries are included. The previously rejected Anvil commits were removed from `main` before publishing this replacement; local recovery refs retain the old history.
