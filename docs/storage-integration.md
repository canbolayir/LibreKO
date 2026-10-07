# Warehouse, VIP vault and clan storage integration

The companion [Classic storage implementation](https://github.com/canbolayir/LibreKO-knightonline-ui-classic/blob/main/docs/storage.md) provides the Human/Karus artwork and editable control geometry. This client change supplies the live controls, quantity interactions and synchronization that implementation requires.

## Problem and result

VIP and clan storage previously chose the first empty destination and moved complete stacks. The client now uses the existing partial-transfer packets, prefers compatible stacks, preserves the source remainder and applies changes only after the matching server acknowledgement. The normal warehouse retains its existing move protocol and capacity.

Classic uses 24 normal/clan storage cells above 28 embedded inventory cells, retaining all 192 slots across eight pages. VIP retains 48 slots across four 12-cell pages. The modern normal warehouse retains its original 48-cell presentation. The theme selects Classic presentation through window metadata rather than changing server addresses.

## Interactions and validation

- Right-click stores or withdraws. Left drag/drop respects an explicit destination; a plain left click does not transfer an item.
- Countable stacks above one unit open a quantity prompt with the complete source count selected. A single unit transfers directly. Enter confirms the quantity; Escape and Cancel dismiss it without moving the item.
- Automatic destinations prefer a compatible stack with enough capacity before an empty cell. Explicit destinations reject occupied incompatible cells and overflowing stacks.
- A changed source invalidates an open quantity prompt. Pending storage or inventory moves block concurrent transfers. Local contents update only after the expected operation acknowledgement; unrelated response opcodes cannot complete the request.
- Embedded inventory rearrangement uses the existing inventory move path. Normal storage retains cross-page rearrangement. VIP/clan rearrangement uses the existing same-page store packet; clan rearrangement and withdrawals require chief/vice-chief permission.
- Linked/non-storable items, expired VIP rental, unavailable clan contents and unauthorized withdrawals are rejected. Existing server weight, balance, permission and persistence checks remain authoritative.
- Coin prompts respect source funds and destination capacity. VIP PIN entry uses a reusable in-game window with a secret field, exactly four ASCII digits, Enter submission and Escape cancellation. Interaction teardown closes the PIN window.
- Valid VIP/clan item moves and clan coin transfers clear obsolete refusal messages. Inventory refreshes also refresh open embedded storage grids.

## Compatibility and presentation

The Classic plugin requires this fork's `VipVaultPinPrompt`, storage controls, metadata and transfer changes. Build the plugin against this client's `LibreKO.dll` and install the matching pair. The plugin's storage skin hides search and redundant instructions/capacity summaries; modern search remains available. No server packet format or server source changes are required by this storage update.

## Verification

The full client suite passes 1,074 tests. Existing server warehouse/vault tests pass 14 tests. The source-driven Godot harness checks 20 rendered states per nation, live right-click/drag/drop/page callbacks, quantity Enter/Escape/Cancel, partial stack merging, stale snapshots, expiry, PIN validation and clan permissions. All eight existing Human/Karus Character Report, Quest, Clan and Friend pages, including 20 selection/disabled render states and actual control bounds, remain identical to the previous verified build.

These are unit tests and controlled source fixtures, not live player storage transactions. The companion repository retains [rendered examples and verification](https://github.com/canbolayir/LibreKO-knightonline-ui-classic/blob/main/docs/storage.md). Local review artifacts remain under `research/warehouse-window-audit` in the workspace. Reviewed assemblies were installed into the client and benchmark directories without restarting an active game or server.

Custom Moradon terrain, maps and zone registrations, databases, settings and installed binaries are excluded from this change. No official-client packages were extracted for this storage work.
