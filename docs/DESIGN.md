# The Dawn — Design Document

A slow, multi-generational survival and settlement game set at the dawn of
civilisation. A small nomadic band becomes a Neolithic village and, over many
generations, reaches the Bronze Age.

## Pillars

1. **The tribe lives on its own.** You never command a person. People choose
   their own roles from their skills, traits and personality, and what the tribe
   needs.
2. **Knowledge lives in people.** Discoveries belong to individuals and are
   taught to the young. If the people who know something die, the tribe partly
   loses it.
3. **Nature is the first enemy.** Cold, hunger and predators decide the early
   game. Walls, fire and weapons slowly push the wild back.
4. **It takes a long time.** Progress is measured in generations, not minutes.
   The Bronze Age is a milestone and the game goes on after it.
5. **Every tribe's culture is different.** Beliefs, rituals and how they look
   come from what the tribe lives through.

## Platform and presentation

- Unity 6 with URP; 3D top-down; free camera (pan, 360° rotation, tilt, zoom).
- **Art:** semi-realistic, atmospheric. Realistic terrain and foliage, strong
  lighting, fog and day/night colour grading. Characters are slightly
  stylized so they read well from above. No low-poly look.
- The current build is a **grey-box**: primitive shapes and a procedural
  terrain, so systems can be built and tuned before any art goes in.

## The player

- An **unseen guiding spirit**. No avatar.
- What you can do:
  - **Place building plans** freely (no grid). People build them when they
    choose to.
  - **Set tribe priorities** (Food, Materials, Building, Crafting:
    Low/Normal/High). These change how strongly the tribe weighs each need.
  - **Send omens and visions**, paid for with **Faith**, which the tribe makes
    through gatherings, rituals and belief.
- You cannot pick roles, draft people or give direct orders.

## People

Simulated in depth, like RimWorld.

- Name, sex, age, family (parents, partner, children).
- **Skills:** Foraging, Hunting, Woodcutting, Building, Crafting, Lore. Skills
  improve with use.
- **Traits (2 each):** Brave, Cautious, Strong, Patient, KeenEyed, Curious,
  Lazy, Hardworking, Hardy, Spiritual, Kind, Restless. Traits change role
  preferences, work speed, courage, cold tolerance, faith and teaching.
- **Needs:** Hunger, Warmth, Rest, Mood. **Health** drops from starvation, cold
  and injuries.
- **Roles:** Forager, Hunter, Gatherer (wood/flint), Builder, Crafter,
  Firekeeper. Children play and learn; elders tend the fire and teach.
- **Role assignment:** the tribe works out what it needs (days of food left,
  winter coming, fuel, plans waiting, spears missing), scaled by the player's
  priorities. Each person is then scored on skill, trait affinity and
  stickiness (people don't switch jobs constantly), and the best fit takes each
  open role.

### Generations

- People age, pair up, have children and die of age, cold, hunger or animals.
- Children inherit traits from their parents and pick up skills from adults
  around them.
- Elders are the tribe's memory: they teach knowledge at the fire in the
  evenings.

## Seasonal camps (Wanderers era)

Until the band discovers **Staying Put**, it moves **twice a year**: to a
warm-season camp at the start of spring, and to a winter camp at the start of
autumn.

- **The spirit marks a site; the elders judge it.** The elders look at water,
  berry bushes, firewood, flint, nearby herds, wolf dens and flat ground. Warm
  camps want water and berries; winter camps want wood, game and shelter from
  the wind (a winter camp among trees is 3°C warmer). If the elders reject a
  site, they return to a camp they know or choose a place themselves.
- **Each camp keeps its own stores.** On moving day everyone packs what they can
  carry and walks. Whatever is left behind is fetched over the next few days, or
  waits at the old camp until the band comes back. Unguarded food spoils faster
  unless the camp has a storage pit.
- **Old camps are left standing.** Lean-tos, hide huts and drying racks slowly
  fall apart while a camp stands empty. When the band returns, builders repair
  what's left. Fires, pits, knapping spots and spirit stones last.
- **Staying Put** needs Food Drying, Storage Pits and Hide Working first. After
  that it grows mostly from seasons lived at the band's camps (stories at the
  fire add a little), and faster each time the band returns to a camp it knows
  (up to 2.5x). In playtests it arrives around year 7–11, typically ~8.
- **Settling:** once Staying Put is known, the player chooses a camp and the
  band settles there for good. That ends the Wanderers era (~8 game years).

## Knowledge and progression

Learning works through a mix of doing and a tech tree:

- Every node in the tree is tied to an **activity** (hunting, foraging,
  building, crafting, lore). Doing that activity builds **insight**. Curious
  people and higher skill build it faster.
- When insight is full, the person who made the breakthrough **discovers** the
  idea. Only they know it at first.
- Knowers **teach** others at the fire each evening. Elders and Kind people
  are better teachers.
- **Partial loss:** if every living knower dies, the knowledge is **faded**.
  Its effects work at half strength and it needs fresh discovery, though that
  only takes ~30% of the original insight.
- Omens (Dream of Insight) and elders speed things up.

### Eras (milestones)

| Era | Theme | Example knowledge |
|---|---|---|
| Wanderers | Nomadic band, fire, first shelters | Fire-hardened Spears, Tracking, Hide Working, Food Drying, Herbal Lore, Storage Pits |
| Settling & Farming | Permanent village | Wild Grain Gathering, Cultivation, Animal Taming, Sedentism |
| Pottery & Storage | Surviving winters, trade goods | Pottery, Kilns, Weaving |
| Copper (Chalcolithic) | First metal | Native Copper, Smelting |
| Tin & Bronze | Needs trade or a rare deposit | Tin Trade, Alloying, Bronze Tools |

Each era should take **many in-game years** and several generations.

## Hunting and animals

- **Normal hunts are simulated.** Hunters pick prey, track it, stalk it
  (slower and quieter up close), then throw or strike. Prey can notice them and
  run. Hunters can miss, lose spears or get hurt (boars fight back).
- **Group hunts are events.** Every so often a herd of big game (aurochs,
  mammoth) shows up and the tribe forms a coordinated party. *(Not in the
  first slice.)*
- **Predators:** wolves (packs), bears, big cats, and rare Ice Age megafauna
  (cave bear, sabre-tooth).
  - **Danger grows, then shrinks.** Predators are the main threat early on,
    worse at night, in winter and when they're hungry. Fire scares them off
    and isolated people are the most at risk. Later, walls, dogs and weapons
    tame the wild.
- **Domestication** comes in two steps. Research unlocks pens and taming, then
  real taming takes generations: capture, breed, and wolves slowly become dogs.

## World

- A detailed **home region** plus an abstract **world map** for neighbouring
  tribes, trade, migration and raids.
- **Biomes** (temperate forest, steppe, tundra edge, river valley...) each
  have their own climate. Seasons are harsh where the biome is harsh: winter
  tests food stores, fuel and shelter.
- **Other tribes:** trade, marriage, conflict.

## Culture and belief

Culture matters a lot, and it **forms on its own**:

- The tribe builds up **tendencies** from what happens: deaths make ancestor
  reverence grow, hunts feed animal spirits, nights by the fire feed the hearth,
  predator attacks bring fear of the wild, and so on.
- When a tendency gets strong enough, a **belief** forms, with a name made up
  for that run (e.g. "The Ash Mothers", "Spirit of the Grey Wolf"). Beliefs
  change faith, mood and which rituals and buildings are available.
- Later: burials, shamans, cave art, megaliths, and how rituals and buildings
  **look** all come from the tribe's beliefs, so two playthroughs end up with
  different cultures.

## Failure

Chosen as a difficulty setting: **game over** (permadeath), or **the line
continues** (a wandering band or survivors from a neighbour take over).

## First playable slice (current build)

- One temperate biome: a procedural 1 km × 1 km valley with a river, two lakes,
  forests and hills. About 3,500 trees, 900 berry bushes, 14 deer herds, 7 boar
  groups and 4 wolf packs.
- A nomadic band of 8 (adults, an elder, children). Goals: make the first
  camp, shelter everyone, survive the first winter, move between seasonal camps,
  and discover **Staying Put** (~8 years) to settle for good.
- Foraging (seasonal berries), wood and flint gathering, food spoiling.
- Simulated hunting of deer and boar.
- Wolf packs that get bolder at night and in winter; fire keeps them away.
- Buildings: Campfire, Lean-to, Hide Hut, Drying Rack, Storage Pit, Knapping
  Spot, Spirit Stone.
- Knowledge for the Wanderers era (discover, teach, fade), with later eras
  shown on the tree but locked.
- Faith and three omens. Emergent beliefs (first version).
- Births, aging, pairing, deaths.
- IMGUI debug HUD with minimap. Camera: WASD/arrows pan, Q/E or middle mouse to
  rotate, scroll to zoom.

## Roadmap

1. **Slice polish:** tune survival numbers, pathfinding around water, better
   feedback (icons over heads, sounds).
2. **Bears and big cats;** group hunt events (aurochs).
3. **Settling & Farming era:** wild grain, fields, sedentism, pens/taming.
4. **Real UI** (UI Toolkit) replacing the IMGUI debug HUD.
5. **Art pass 1:** URP lighting, terrain and foliage packs, character models.
6. **World map and neighbouring tribes:** trade, marriage, raids.
7. **Pottery, copper, bronze** eras.
8. **Culture visuals:** rituals, megaliths and cave art driven by beliefs.
9. Save/load, difficulty and failure settings, more biomes.
