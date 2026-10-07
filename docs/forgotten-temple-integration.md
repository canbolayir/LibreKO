# Forgotten Temple integration

The integration includes upstream `ad517236095a0abfe26a5fd6899b93feee452991` and its follow-up `20dcd08efb2039759b634173c303cb6a83fd9a96`.

## Added behavior

- Forgotten Temple uses zone 55 and the existing `clanfight_b` map. Two tiers cover levels 35–59 and 60–83. The supplied daily schedules register the low tier for 16:00 and the high tier for 20:00 in the configured server time zone, with a ten-minute countdown.
- Seed data contains 36 spawn rows across five low-tier and six high-tier waves. The event lasts up to 40 minutes. Winning grants the configured green or blue treasure chest and EXP; the service returns participants to Moradon after the victory delay.
- GM commands support `+ft low`, `+ft high`, an optional registration interval such as `30s`, immediate start with `now`, `enter`, `wave <number>`, `boss`, and `close`.
- Registration popups and automatic GM registration respect event level limits. Global registration announcements remain visible to all players.
- `+utc close` reports an inactive event only to the requesting GM. Scheduled UTC cancellation and manually opened UTC closure retain their intended announcements.

## Conflict resolution

The only textual conflict was in `Client/src/World/World.Bifrost.cs`. Keep the fork's plugin-aware `Notice.Confirm`, duplicate-click guard, event-zone guard, zone-change invalidation, and `ChatStatusNotice` routing. Add FT recognition, the post-load banner refresh, valid-label checks, and event-plate layout refreshes from upstream. Hiding a leave banner closes the previous confirmation rather than merely hiding its native control.

Existing Classic window layouts and Anvil, inventory/bag, vendor, trade, merchant, chat/Info, mail and character-page behavior are unchanged by this integration. BDW's EXP formula is moved to `TempleEventHelpers` without changing its results. Custom Moradon content and registrations are local-only and excluded from the integration's staged files.

## Deployment and verification

Deploy matching client and server assemblies together. No additional retail extraction or content-pack bake is required: the referenced map, NPCs and item content already exist. Back up the database before applying `20261007083147_AddForgottenTempleWaves`; deploy the matching wave, schedule and reward seed files.

The client, game, common and login suites pass 3,421 tests. The source-driven Godot review checks registration titles/state, all five event-zone leave dialogs, duplicate-click handling, Escape cancellation and stale-confirmation invalidation for both nations. All eight Character Report, Quest, Clan and Friend screenshots are pixel-identical to the previous approved review, including their recorded rendered bounds.

The preview retains the previously observed Classic-dialog shutdown resource warnings, now counted across six exercised confirmations per nation. Functional checks pass; this warning is not evidence of a live event test. No live player rewards, item transfers or event combat are exercised by the fixtures.
