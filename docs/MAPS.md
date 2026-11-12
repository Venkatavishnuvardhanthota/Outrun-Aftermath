# Maps

Source of truth: `shared/OutrunAftermath.Simulation/Maps/MapCatalog.cs`.

| Id | Name | Theme | Ground | Convoy loop | Max team size |
|----|------|-------|--------|-------------|---------------|
| deadcity | Dead City | urban ruins | yes | 1400 m | 4 |
| dustvalley | Dust Valley | desert | yes | 1800 m | 4 |
| blackwood | Blackwood Forest | forest | yes | 1100 m | 3 |
| frostbite | Frostbite Ridge | snow | yes | 1000 m | 4 |
| ironworks | Ironworks | industrial | yes | 1200 m | 4 |
| hollowcreek | Hollow Creek | countryside | yes | 1600 m | 4 |
| ashfall | Ashfall | abandoned town | yes | 900 m | 3 |

Every map supports both modes. Random map selection draws only from maps whose team-size cap
fits the lobby, so a 4v4 lobby never lands on Blackwood or Ashfall. Maps need an 800 m loose
loop to be convoy-capable and at least 40 cover positions to be ground-capable; both checks are
applied by `MapPool.Compatible` so future maps that fail either test are excluded automatically.

These numbers are design data, not measurements of an authored level. No Unity scene exists for
any map yet; the geometry, cover placement, extraction zones, and vehicle routes are unbuilt.
