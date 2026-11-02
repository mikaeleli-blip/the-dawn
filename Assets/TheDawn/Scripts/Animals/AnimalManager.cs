using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    /// <summary>Spawns herds and packs, grows them back over time, and decides when packs go hunting.</summary>
    public class AnimalManager : MonoBehaviour
    {
        public static AnimalManager Instance { get; private set; }

        public readonly List<AnimalGroup> Groups = new List<AnimalGroup>();
        float huntCheckTimer;

        void Awake() { Instance = this; }

        public void Init(Biome biome, Vector3 tribeStart)
        {
            for (int i = 0; i < biome.DeerHerds; i++) SpawnGroup(Species.Deer, Random.Range(3, 6), tribeStart, 70f);
            for (int i = 0; i < biome.BoarGroups; i++) SpawnGroup(Species.Boar, Random.Range(2, 4), tribeStart, 80f);
            for (int i = 0; i < biome.WolfPacks; i++) SpawnGroup(Species.Wolf, Random.Range(3, 5), tribeStart, 170f);
            GameClock.Instance.OnNewDay += DailyTick;
            GameClock.Instance.OnNewSeason += SeasonTick;
        }

        AnimalGroup SpawnGroup(Species s, int count, Vector3 avoid, float minDist)
        {
            var w = World.Instance;
            Vector3 home = Vector3.zero;
            for (int i = 0; i < 100; i++)
            {
                home = w.RandomPoint(s == Species.Wolf ? 0.85f : 0.75f);
                if (!w.IsLand(home, 0.5f)) continue;
                if (Vector3.Distance(home, avoid) < minDist) continue;
                break;
            }
            var g = new AnimalGroup { Species = s, Home = home, Center = home, Hunger = Random.Range(0f, 40f) };
            for (int i = 0; i < count; i++) Animal.Create(s, g, w.RandomLandPointNear(home, 5f), transform);
            Groups.Add(g);
            return g;
        }

        void DailyTick()
        {
            var clock = GameClock.Instance;
            foreach (var g in Groups)
            {
                if (g.Species == Species.Wolf)
                    g.Hunger = Mathf.Min(100f, g.Hunger + (clock.Season == Season.Winter ? 40f : 22f));
            }
        }

        void SeasonTick()
        {
            var clock = GameClock.Instance;
            var w = World.Instance;
            Groups.RemoveAll(g => g.AliveCount == 0 && g.Species != Species.Wolf);
            foreach (var g in Groups)
            {
                int alive = g.AliveCount;
                int max = g.Species == Species.Deer ? 7 : g.Species == Species.Boar ? 4 : 5;
                if (alive == 0 || alive >= max) continue;
                // Young are born in spring; wolves recruit through the year.
                int born = g.Species == Species.Wolf ? 1 : (clock.Season == Season.Spring ? Random.Range(1, 3) : 0);
                for (int i = 0; i < born && alive + i < max; i++)
                    Animal.Create(g.Species, g, w.RandomLandPointNear(g.Center, 4f), transform);
            }

            int deer = 0, boar = 0, wolfPacks = 0;
            foreach (var g in Groups)
            {
                if (g.Species == Species.Deer && g.AliveCount > 0) deer++;
                if (g.Species == Species.Boar && g.AliveCount > 0) boar++;
                if (g.Species == Species.Wolf && g.AliveCount > 0) wolfPacks++;
            }
            // New herds wander in from beyond the valley each spring; a destroyed pack's territory is retaken after a while.
            if (clock.Season == Season.Spring)
            {
                for (int i = 0; i < 2 && deer + i < World.Instance.Biome.DeerHerds; i++)
                    SpawnGroup(Species.Deer, Random.Range(3, 5), Tribe.Instance.CampPosition, 120f);
                if (boar < World.Instance.Biome.BoarGroups)
                    SpawnGroup(Species.Boar, Random.Range(2, 4), Tribe.Instance.CampPosition, 120f);
            }
            Groups.RemoveAll(g => g.AliveCount == 0 && g.Species == Species.Wolf && Random.value < 0.5f);
            if (wolfPacks < World.Instance.Biome.WolfPacks && Random.value < 0.4f)
            {
                SpawnGroup(Species.Wolf, 3, Tribe.Instance.CampPosition, 200f);
                EventLog.Add("Howling in the hills: a new wolf pack has moved in.", LogKind.Warning);
            }
        }

        void Update()
        {
            huntCheckTimer -= Time.deltaTime;
            if (huntCheckTimer > 0f) return;
            huntCheckTimer = 2f;
            foreach (var g in Groups)
                if (g.Species == Species.Wolf && g.AliveCount > 0 && !g.HasTarget) ConsiderHunt(g);
        }

        /// <summary>
        /// Escalating danger: hungry packs hunt, at night and in winter they're bolder,
        /// and a person alone away from fire is the easiest meal.
        /// </summary>
        void ConsiderHunt(AnimalGroup g)
        {
            var clock = GameClock.Instance;
            if (g.Hunger < 40f) return;
            bool bold = clock.IsNight || clock.Season == Season.Winter || g.Hunger > 85f;
            if (!bold && Random.value > 0.1f) return;

            Vector3 c = PackCenter(g);
            float range = bold ? 80f : 50f;

            // Prefer deer and boar.
            Animal bestPrey = null;
            float bestD = range * range;
            foreach (var a in Animal.All)
            {
                if (!a.Alive || a.IsPredator) continue;
                float d = (a.Position - c).sqrMagnitude;
                if (a.Species == Species.Boar) d *= 1.6f;
                if (d < bestD) { bestD = d; bestPrey = a; }
            }

            // Lone people away from fire.
            Person bestPerson = null;
            float bestPD = range * range;
            if (bold)
            {
                foreach (var p in Tribe.Instance.People)
                {
                    if (Building.NearestLitFire(p.Position, Building.FireScareRadius) != null) continue;
                    int company = 0;
                    foreach (var o in Tribe.Instance.People)
                        if (o != p && o.IsAdult && (o.Position - p.Position).sqrMagnitude < 100f) company++;
                    if (company >= 2) continue;
                    float d = (p.Position - c).sqrMagnitude;
                    if (p.IsChild) d *= 0.6f;
                    if (p.HasSpear) d *= 1.8f;
                    if (d < bestPD) { bestPD = d; bestPerson = p; }
                }
            }

            if (bestPerson != null && (bestPrey == null || bestPD < bestD * 0.7f || g.Hunger > 90f))
            {
                g.TargetPerson = bestPerson;
                EventLog.Add("Wolves are stalking " + bestPerson.Name + "!", LogKind.Danger);
            }
            else if (bestPrey != null)
            {
                g.TargetAnimal = bestPrey;
            }
        }

        static Vector3 PackCenter(AnimalGroup g)
        {
            Vector3 sum = Vector3.zero; int n = 0;
            foreach (var m in g.Members) if (m != null && m.Alive) { sum += m.Position; n++; }
            return n > 0 ? sum / n : g.Center;
        }
    }
}
