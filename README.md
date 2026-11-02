# The Dawn

A slow, multi-generational survival and settlement game. You guide a Neolithic
band from its first campfire toward the Bronze Age, as an unseen spirit who
never gives direct orders.

See **[docs/DESIGN.md](docs/DESIGN.md)** for the full design.

## Current build: grey-box first slice

Primitive shapes, procedural terrain and a debug HUD. The goal is to get the
systems right before art goes in.

- A nomadic band of 8 with names, ages, families, skills, traits and needs
- People choose their own roles (forager, hunter, gatherer, builder, crafter,
  firekeeper) based on tribe needs, skills and traits
- Day/night, seasons, temperature; food spoils; winter is harsh
- Simulated hunting of deer and boar (stalking, startling, missing, broken spears)
- Wolf packs that hunt lone people at night and in winter; fire keeps them away
- Free (gridless) building plans: Campfire, Lean-to, Hide Hut, Drying Rack,
  Storage Pit, Knapping Spot, Spirit Stone
- Knowledge discovered by doing, taught at the fire, and faded when every knower dies
- Faith and omens; beliefs that form on their own with invented names
- Births, pairing, aging and death

## Running it

1. Open the folder in **Unity Hub → Add → Add project from disk**. It targets
   Unity 6. If Hub asks, pick your installed Unity 6 version.
2. Press **Play** in any scene. The game builds itself at runtime, so no scene
   setup is needed.

## Controls

| Action | Input |
|---|---|
| Pan | WASD / arrow keys |
| Rotate | Q / E, or hold middle mouse and drag (drag up/down to tilt) |
| Zoom | Mouse wheel |
| Select / inspect | Left click |
| Focus selected | F |
| Build | Build panel → pick a plan → left click to place, R to rotate, right click/Esc to stop |
| Speed | Space = pause, 1/2/3/4 = 1x/2x/4x/8x |

## First goals

1. Place a **Campfire** to found your camp.
2. Build **Lean-tos** so everyone has shelter.
3. Stock food and wood and **survive the first winter**.

## Code layout

```
Assets/TheDawn/Scripts/
  Core/       GameBootstrap (builds everything), GameClock, CameraRig, Selection
  World/      World (terrain, water, biome), ResourceNode
  People/     Person (needs + autonomous brain), PeopleTypes (skills, traits, roles)
  Tribe/      Tribe (stores, camp, life events), RoleAssigner, OmenSystem, CultureSystem
  Knowledge/  KnowledgeSystem (tree, discover, teach, fade)
  Animals/    Animal, AnimalManager (herds, packs, hunting decisions)
  Building/   Building (defs, construction, fire), BuildPlacer
  UI/         GameHUD (IMGUI debug HUD)
  Util/       InputShim (old or new input system), Visuals, EventLog, NameGen
```
