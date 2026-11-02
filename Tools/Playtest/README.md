# Headless playtest

Runs the real game scripts from `Assets/` outside Unity, using a small working stand-in
for the parts of the Unity engine the game uses (`UnityShim.cs`). A scripted player
(`Harness.cs`) founds a camp, builds shelters and other buildings, sets priorities and sends
omens. `idle` mode does nothing, to test what happens without a player.

```
./run.sh 101 48 player 4     # seed 101, 48 in-game days (2 years), scripted player, 4x speed
./run.sh 404 26 idle 4       # no player
TRACE=all ./run.sh 101 4     # hourly state of every person written to debuglog.txt
```

Output goes to `out/<run>/`:
- `summary.txt`: population, deaths, stores, buildings, knowledge, beliefs, time use, exceptions
- `daily.csv`: one row per day (stores, needs, roles, temperatures, animals)
- `debuglog.txt`: the full game event log

Needs Mono (`apt install mono-mcs mono-devel`). Not part of the Unity build: Unity only
compiles `Assets/`. Rendering, input and UI aren't simulated.
