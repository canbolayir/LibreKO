"""Generate client placement hints from this fork's server seed tables."""
import gzip
import json
from pathlib import Path

client = Path(__file__).resolve().parents[1]
seed = client.parent / "Server/LibreKO.Game/Seed/Data"
recipes = {}
for row in json.loads((seed / "ItemUpgradeRecipes.json").read_text(encoding="utf-8-sig")):
    if row["NewNumber"]:
        recipes.setdefault(row["OriginNumber"], set()).add(row["RequiredItem"])
origins = {}
for source in sorted(seed.glob("Items.slot*.json")):
    for row in json.loads(source.read_text(encoding="utf-8-sig")):
        item = row["Num"]
        if item in recipes:
            origins[item] = [item, row.get("ItemClass", 0), row.get("ItemType", 0),
                             row.get("Grade", 0) or item % 10, row["Kind"], *sorted(recipes[item])]
payload = json.dumps([origins[k] for k in sorted(origins)], separators=(",", ":")).encode()
(client / "data/anvil_origins.json.gz").write_bytes(gzip.compress(payload, mtime=0))
(client / "data/anvil_upgrade_settings.json").write_bytes((seed / "ItemUpgradeSettings.json").read_bytes())
print(f"Generated {len(origins)} origins; source tables stay authoritative on the server.")
