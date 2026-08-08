# Tile Turfer

A top-down turf-war arena shooter in the Splatoon mould. You and the AI paint the floor your colour, and the share of the map you hold is the single number that governs everything: the difficulty, your own fire rate, and the attacks you unlock.

[Play on itch.io](https://olivernealdev.itch.io/tile-turfer) · [Full technical breakdown](https://oliverneal.dev/tile-turfer.html) · [Portfolio](https://oliverneal.dev)

| | |
|---|---|
| **Role** | Solo developer |
| **Engine** | Unity 6 · URP 2D · C# |
| **Released** | January 2026 |
| **Genre** | Turf-war arena shooter |
| **Navigation** | 2D NavMesh baked at runtime |

> The repository is named `ThreatHealth` for historical reasons. It is Tile Turfer.

---

## Overview

Tile Turfer takes the central idea of Splatoon, that winning is about ground covered rather than kills, and builds a tight single-player arena around it. Every floor tile can be painted your colour or an enemy's, and the game constantly measures what fraction of the map each side controls.

What makes it hang together is where that measurement is wired. Instead of a difficulty slider or a wave timer, **one turf-percentage value feeds the entire game at once**: it sets how fast you fire, which attack patterns you have unlocked, how many enemies spawn, and how hard those enemies push. Winning territory literally raises the stakes.

---

## Key systems

### One value, one source of truth

The measurement itself is one line, and everything else reads it rather than keeping its own copy:

```csharp
// Assets/Scripts/TurfManager.cs
public float GetTurfPercentage()
{
    if (totalTiles == 0) return 0f;
    return (float)ownedTiles / (float)totalTiles;
}
```

Because the player's fire rate and the enemy's fire pattern are both derived from the same number, they can never disagree about how far into the match you are. The enemy picks its pattern from it:

```csharp
// Assets/Scripts/EnemyController.cs
float difficulty = (turfManager != null) ? turfManager.GetTurfPercentage() : 0f;
if      (difficulty >= milestone3) StartCoroutine(FirePatternWave());
else if (difficulty >= milestone2) StartCoroutine(FirePatternTriangle());
else if (difficulty >= milestone1) { ShootBullet(0f, 0.2f); ShootBullet(0f, -0.2f); }
```

and the player's weapon interpolates its cadence from the same call:

```csharp
currentFireDelay = Mathf.Lerp(slowFireRate, fastFireRate, turfManager.GetTurfPercentage());
```

### Procedural caves with a runtime NavMesh bake

Every arena is generated from scratch. A cellular-automata pass fills a 60x60 grid at random then smooths it into cave-like walls; a **flood fill finds the largest connected floor region** and fills in every disconnected pocket, so the playable space is guaranteed to be a single reachable area. A safe zone is cleared at the centre for the spawn.

The demanding part is that enemies still need to path through this freshly invented geometry, so once the map is drawn the game **bakes a 2D NavMesh at runtime** over the new layout. The AI navigates a level that did not exist a frame earlier.

### Enemies that see, remember and hunt your turf

The enemy AI runs a finite state machine, moving between spawning, roaming, chasing a remembered position and chasing a visible target, with real line-of-sight checks against the walls. It has **memory**: after losing sight of you it keeps hunting your last known position for a few seconds before giving up, so breaking line of sight buys time rather than instant safety.

When roaming, enemies do not wander at random. They **score nearby tiles** and prefer to move toward ground you own, actively overwriting your territory. That turns the paint mechanic into a two-way fight instead of a passive objective.

### A self-balancing difficulty curve

As you gain ground your fire rate speeds up and you unlock wider attack patterns at 25%, 50% and 75%. So do the enemies, and the spawn cap climbs while the spawn interval tightens. The result is a curve expressed through a single number, with no hand-authored difficulty stages: the better you do, the harder the game pushes back.

---

## By the numbers

- **Unity 6, URP 2D**, with a 2D NavMesh integration for pathfinding on generated maps
- **60x60 tile arenas**, cellular automata plus flood-fill region cleanup
- **Enemy cap scales 3 to 40**, spawn interval tightens from 4s to 0.5s as territory shifts
- **Attack patterns unlock at 25, 50 and 75%** territory, for the player and the AI alike
- **17 gameplay scripts, ~2,500 lines of C#**
- Documented in a full technical report covering the generation and AI systems

---

## Project structure

```
Assets/Scripts/
  TurfManager.cs        Ownership grid and the single turf-percentage source of truth
  MapGenerator.cs       Cellular automata, smoothing, flood-fill cleanup, safe zone
  NavMeshController.cs  Runtime 2D NavMesh bake over the generated layout
  PlayerController.cs   Movement, aiming and turf-scaled fire rate
  EnemyController.cs    Line-of-sight FSM, memory, tile scoring, fire patterns
  EnemyManager.cs       Spawn cap and interval, both driven by turf percentage
  PaintBullet.cs        Projectiles that write ownership into the grid
  GameManager.cs        Match flow, win and loss, scoring
  Minimap*.cs           Minimap rendering and sync
```

---

## Running it

```bash
git clone https://github.com/OliverNealDev/TileTurfer.git
```

Open the project in **Unity 6 (6000.0.55f1 or newer)** and load the main scene from `Assets/Scenes`.

Or skip the editor and [play it in the browser on itch.io](https://olivernealdev.itch.io/tile-turfer).

---

## Author

**Oliver Neal**, gameplay programmer specialising in Unity and C#.

[oliverneal.dev](https://oliverneal.dev) · [itch.io](https://olivernealdev.itch.io) · [LinkedIn](https://www.linkedin.com/in/oliverjackneal/) · [GitHub](https://github.com/OliverNealDev)
