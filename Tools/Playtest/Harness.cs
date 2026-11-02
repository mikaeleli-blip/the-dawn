// Headless playtest: runs the real game scripts with a scripted "player" and records what happens.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using TheDawn;
using UnityEngine;

public static class Harness
{
    static StreamWriter log;
    static string outDir;

    public static int Main(string[] args)
    {
        int seed = int.Parse(args[0]);
        float days = float.Parse(args[1]);
        string mode = args.Length > 2 ? args[2] : "player";
        float speed = args.Length > 3 ? float.Parse(args[3]) : 4f;
        outDir = args.Length > 4 ? args[4] : "/home/claude/playtest/out/seed" + seed + "_" + mode;
        Directory.CreateDirectory(outDir);
        log = new StreamWriter(Path.Combine(outDir, "debuglog.txt"));
        Engine.LogSink = s => log.WriteLine(s);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var root = new GameObject("The Dawn");
        var boot = root.AddComponent<GameBootstrap>();
        boot.Seed = seed;
        Engine.Frame(1f / 30f); // Start → world built
        Console.WriteLine("World built in " + sw.ElapsedMilliseconds + " ms; objects " + Engine.AllObjects.Count + ", components " + Engine.AllComponents.Count);
        Time.timeScale = speed;

        var bot = new Bot(mode == "player");
        var metrics = new Metrics(outDir);
        var clock = GameClock.Instance;
        float realDt = 1f / 30f;
        long frames = 0;
        int lastHour = -1;
        int totalHours = 0;
        float targetDays = days;

        while (clock.TotalDays + clock.Hour / 24f < targetDays + 7f / 24f)
        {
            Engine.Frame(realDt);
            frames++;
            int hour = Mathf.FloorToInt(clock.Hour);
            if (hour != lastHour)
            {
                lastHour = hour;
                totalHours++;
                bot.HourTick();
                metrics.Hourly();
                if (Environment.GetEnvironmentVariable("TRACE") != null) Trace();
                if (hour == 23) metrics.Daily();
            }
            if (Tribe.Instance.People.Count == 0 && metrics.ExtinctDay < 0) metrics.ExtinctDay = clock.TotalDays;
            if (Tribe.Instance.People.Count == 0 && clock.Hour > 12f) break;
        }
        metrics.Finish(seed, mode, frames, sw.Elapsed.TotalSeconds);
        log.Close();
        Console.WriteLine("Done: " + frames + " frames in " + sw.Elapsed.TotalSeconds.ToString("0.0") + " s");
        return 0;
    }

    static void Trace()
    {
        const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic;
        var c = GameClock.Instance;
        string who = Environment.GetEnvironmentVariable("TRACE");
        foreach (var p in Tribe.Instance.People)
        {
            if (who != "all" && p.Name != who) continue;
            var dest = (Vector3)typeof(Person).GetField("dest", F).GetValue(p);
            log.WriteLine("TRACE " + c.ShortStamp + " " + c.TimeString + " " + p.Name + " role=" + p.Role + " job=" + p.Job + " phase=" + typeof(Person).GetField("phase", F).GetValue(p) +
                " timer=" + ((float)typeof(Person).GetField("timer", F).GetValue(p)).ToString("0.0") + " pos=" + p.Position + " dest=" + dest +
                " land=" + World.Instance.IsLand(p.Position) + " text='" + p.JobText + "' hunger=" + p.Hunger.ToString("0") + " warmth=" + p.Warmth.ToString("0") + " mood=" + p.Mood.ToString("0"));
        }
    }

    // =====================================================================
    // Scripted player: does what a sensible player would do with the HUD
    // =====================================================================
    class Bot
    {
        readonly bool active;
        Transform buildRoot;
        public readonly List<string> Actions = new List<string>();
        public static Bot Instance;

        public Bot(bool active)
        {
            this.active = active;
            Instance = this;
            buildRoot = new GameObject("BotBuildings").transform;
        }

        void Act(string s)
        {
            string line = GameClock.Instance.ShortStamp + " " + GameClock.Instance.TimeString + "  PLAYER: " + s;
            Actions.Add(line);
            EventLog.Add("[player] " + s);
        }

        int Count(BuildingType t, bool includePlans = true)
        {
            int n = 0;
            foreach (var b in Building.All) if (b.Def.Type == t && (includePlans || b.Built)) n++;
            return n;
        }

        public void HourTick()
        {
            if (!active) return;
            var t = Tribe.Instance;
            var c = GameClock.Instance;
            if (t.People.Count == 0) return;

            // 1. Found a camp right away, near where the band is.
            if (Count(BuildingType.Campfire) == 0)
            {
                Place(BuildingType.Campfire, t.CampPosition, 4f);
                return;
            }
            if (!t.HasCamp) return;
            Vector3 camp = t.CampPosition;

            // 2. Shelter for everyone (+1 spare), at most two plans waiting at a time.
            int plans = Building.All.Count(b => !b.Built);
            int spaces = 0;
            foreach (var b in Building.All) spaces += b.Def.ShelterCapacity;
            if (spaces < t.People.Count + 1 && plans < 2)
            {
                var hutDef = BuildingDef.Get(BuildingType.HideHut);
                string why;
                if (BuildPlacer.CanBuild(hutDef, out why) && t.Stock(Res.Hide) >= 4f) Place(BuildingType.HideHut, camp, 9f);
                else Place(BuildingType.LeanTo, camp, 8f);
                return;
            }

            // 3. Useful buildings once they're unlocked.
            if (plans < 2)
            {
                if (Count(BuildingType.KnappingSpot) == 0 && c.TotalDays >= 2) { Place(BuildingType.KnappingSpot, camp, 6f); return; }
                if (Count(BuildingType.StoragePit) == 0 && Can(BuildingType.StoragePit)) { Place(BuildingType.StoragePit, camp, 5f); return; }
                if (Count(BuildingType.DryingRack) == 0 && Can(BuildingType.DryingRack)) { Place(BuildingType.DryingRack, camp, 6f); return; }
                if (Count(BuildingType.Campfire) < 2 && t.People.Count >= 12) { Place(BuildingType.Campfire, camp, 12f); return; }
                if (Count(BuildingType.SpiritStone) == 0 && t.Stock(Res.Flint) >= 12f && c.TotalDays >= 10) { Place(BuildingType.SpiritStone, camp, 14f); return; }
            }

            // 4. Priorities: stock food in autumn, materials ahead of winter fuel.
            var wantFood = c.Season == Season.Autumn || t.FoodDays < 2f ? PriorityLevel.High : PriorityLevel.Normal;
            if (t.Priorities[(int)PriorityCat.Food] != wantFood)
            {
                t.Priorities[(int)PriorityCat.Food] = wantFood;
                Act("Food priority -> " + wantFood);
            }
            var wantMat = (c.Season == Season.Autumn && t.Stock(Res.Wood) < 30f) ? PriorityLevel.High : PriorityLevel.Normal;
            if (t.Priorities[(int)PriorityCat.Materials] != wantMat)
            {
                t.Priorities[(int)PriorityCat.Materials] = wantMat;
                Act("Materials priority -> " + wantMat);
            }

            // 5. Omens.
            var om = OmenSystem.Instance;
            bool wolvesHunting = AnimalManager.Instance.Groups.Any(g => g.Species == Species.Wolf && g.TargetPerson != null && g.TargetPerson.Alive);
            if (wolvesHunting) TrySend(OmenType.SpiritOfCourage);
            else if ((c.Season == Season.Autumn || c.Season == Season.Winter) && t.FoodDays < 4f) TrySend(OmenType.VisionOfTheHunt);
            else if (t.Faith > 70f) TrySend(OmenType.DreamOfInsight);
        }

        bool Can(BuildingType t) { string why; return BuildPlacer.CanBuild(BuildingDef.Get(t), out why); }

        void TrySend(OmenType type)
        {
            var d = OmenSystem.Defs.First(o => o.Type == type);
            if (OmenSystem.Instance.TrySend(d)) Act("Omen: " + d.Name);
        }

        void Place(BuildingType type, Vector3 around, float ring)
        {
            var def = BuildingDef.Get(type);
            string why;
            if (!BuildPlacer.CanBuild(def, out why)) return;
            for (float r = ring; r < ring + 40f; r += 2f)
            {
                for (int i = 0; i < 16; i++)
                {
                    float a = (i / 16f + r * 0.13f) * Mathf.PI * 2f;
                    Vector3 p = World.Instance.OnGround(around + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                    if (Valid(def, p, out why))
                    {
                        Building.Create(def, p, 0f, buildRoot);
                        RoleAssigner.Instance.Reassign();
                        Act("Placed " + def.Name + " " + Vector3.Distance(p, Tribe.Instance.CampPosition).ToString("0") + " m from camp");
                        return;
                    }
                }
            }
            Act("Could not find a spot for " + def.Name);
        }

        // Same rules as BuildPlacer.Check
        static bool Valid(BuildingDef placing, Vector3 p, out string why)
        {
            why = null;
            var w = World.Instance;
            if (!w.IsLand(p, 0.4f)) { why = "water"; return false; }
            if (w.Slope(p) > 0.45f) { why = "steep"; return false; }
            if (!w.InBounds(p)) { why = "bounds"; return false; }
            foreach (var b in Building.All)
            {
                float min = b.Def.Radius + placing.Radius + 0.4f;
                if ((b.transform.position - p).sqrMagnitude < min * min) { why = "overlap"; return false; }
            }
            foreach (var n in ResourceNode.All)
            {
                if (n.Type != ResourceType.Tree && n.Type != ResourceType.FlintRock) continue;
                if (!n.Available) continue;
                float min = placing.Radius + 0.8f;
                if ((n.transform.position - p).sqrMagnitude < min * min) { why = "blocked"; return false; }
            }
            if (Tribe.Instance.HasCamp && Vector3.Distance(p, Tribe.Instance.CampPosition) > 90f) { why = "far"; return false; }
            return true;
        }
    }

    // =====================================================================
    // Metrics
    // =====================================================================
    class Metrics
    {
        readonly string dir;
        readonly StringBuilder daily = new StringBuilder();
        readonly Dictionary<string, int> jobHours = new Dictionary<string, int>();
        readonly Dictionary<Season, Dictionary<string, int>> jobBySeason = new Dictionary<Season, Dictionary<string, int>>();
        readonly Dictionary<int, Vector3> lastPos = new Dictionary<int, Vector3>();
        readonly Dictionary<int, int> stillHours = new Dictionary<int, int>();
        readonly List<string> stuck = new List<string>();
        float minWarmthDay = 100f, minHealthDay = 100f, minTempDay = 99f, maxTempDay = -99f;
        public int ExtinctDay = -1;
        static readonly FieldInfo phaseField = typeof(Person).GetField("phase", BindingFlags.Instance | BindingFlags.NonPublic);
        int peakPop;

        public Metrics(string dir)
        {
            this.dir = dir;
            daily.AppendLine("day,stamp,pop,adults,children,food,foodDays,wood,flint,hides,spears,cloaks,faith,shelter,builtBuildings,plans,fireFuel,deaths,births,known,beliefs,avgHunger,avgWarmth,avgHealth,avgMood,minWarmth,minHealth,tempMin,tempMax,roles,wolves,deer,boar");
        }

        public void Hourly()
        {
            var t = Tribe.Instance;
            var c = GameClock.Instance;
            minTempDay = Math.Min(minTempDay, c.Temperature);
            maxTempDay = Math.Max(maxTempDay, c.Temperature);
            Dictionary<string, int> bySeason;
            if (!jobBySeason.TryGetValue(c.Season, out bySeason)) jobBySeason[c.Season] = bySeason = new Dictionary<string, int>();
            foreach (var p in t.People)
            {
                minWarmthDay = Math.Min(minWarmthDay, p.Warmth);
                minHealthDay = Math.Min(minHealthDay, p.Health);
                string k = p.Job + (p.Sleeping ? "(asleep)" : "");
                Inc(jobHours, k); Inc(bySeason, k);

                // Stuck detection: hasn't moved in many hours while supposedly working.
                Vector3 lp;
                bool working = p.Job != Person.JobKind.Sleep && p.Job != Person.JobKind.Gather && p.Job != Person.JobKind.Idle && p.Job != Person.JobKind.Warm;
                if (lastPos.TryGetValue(p.Id, out lp) && (lp - p.Position).sqrMagnitude < 0.25f && working)
                {
                    int s; stillHours.TryGetValue(p.Id, out s); s++;
                    stillHours[p.Id] = s;
                    if (s == 6) stuck.Add(c.ShortStamp + " " + c.TimeString + " " + p.Name + " (" + p.Role + ") stuck in " + p.Job + " phase " + phaseField.GetValue(p) + " '" + p.JobText + "' at " + p.Position);
                }
                else stillHours[p.Id] = 0;
                lastPos[p.Id] = p.Position;
            }
            peakPop = Math.Max(peakPop, t.People.Count);
        }

        static void Inc(Dictionary<string, int> d, string k) { int n; d.TryGetValue(k, out n); d[k] = n + 1; }

        public void Daily()
        {
            var t = Tribe.Instance;
            var c = GameClock.Instance;
            var ks = KnowledgeSystem.Instance;
            int built = Building.All.Count(b => b.Built), plans = Building.All.Count(b => !b.Built);
            float fuel = Building.All.Where(b => b.Built && b.Def.Type == BuildingType.Campfire).Sum(b => b.Fuel);
            var known = KnowledgeSystem.Defs.Where(d => ks.KnowersOf(d.Id) > 0).Select(d => d.Name + "(" + ks.KnowersOf(d.Id) + ")");
            string roles = string.Join(" ", PeopleInfo.WorkRoles.Select(r => r.ToString().Substring(0, 3) + t.CountRole(r)));
            Func<Func<Person, float>, float> avg = f => t.People.Count == 0 ? 0 : t.People.Average(f);
            int wolves = Animal.All.Count(a => a.Alive && a.Species == Species.Wolf);
            int deer = Animal.All.Count(a => a.Alive && a.Species == Species.Deer);
            int boar = Animal.All.Count(a => a.Alive && a.Species == Species.Boar);
            daily.AppendLine(string.Join(",", new object[] {
                c.TotalDays, c.ShortStamp, t.People.Count, t.Adults, t.People.Count - t.Adults,
                t.Stock(Res.Food).ToString("0"), t.FoodDays.ToString("0.0"), t.Stock(Res.Wood).ToString("0"), t.Stock(Res.Flint).ToString("0"),
                t.Stock(Res.Hide).ToString("0"), t.Stock(Res.Spear).ToString("0"), t.Stock(Res.Cloak).ToString("0"), t.Faith.ToString("0"),
                t.ShelterSpaces, built, plans, fuel.ToString("0.0"), t.Deaths, t.Births,
                "\"" + string.Join(" ", known) + "\"", CultureSystem.Instance.Beliefs.Count,
                avg(p => p.Hunger).ToString("0"), avg(p => p.Warmth).ToString("0"), avg(p => p.Health).ToString("0"), avg(p => p.Mood).ToString("0"),
                minWarmthDay.ToString("0"), minHealthDay.ToString("0"), minTempDay.ToString("0"), maxTempDay.ToString("0"),
                "\"" + roles + "\"", wolves, deer, boar }));
            minWarmthDay = 100f; minHealthDay = 100f; minTempDay = 99f; maxTempDay = -99f;
        }

        public void Finish(int seed, string mode, long frames, double secs)
        {
            File.WriteAllText(Path.Combine(dir, "daily.csv"), daily.ToString());
            var sb = new StringBuilder();
            var t = Tribe.Instance;
            var c = GameClock.Instance;
            sb.AppendLine("=== Playtest seed " + seed + " mode " + mode + " ===");
            sb.AppendLine("Ended: " + c.ShortStamp + " " + c.TimeString + " (day " + c.TotalDays + "), " + frames + " frames, " + secs.ToString("0") + " s real");
            sb.AppendLine("Population: " + t.People.Count + " (peak " + peakPop + "), births " + t.Births + ", deaths " + t.Deaths + (ExtinctDay >= 0 ? ", EXTINCT on day " + ExtinctDay : ""));
            sb.AppendLine("Stores: food " + t.Stock(Res.Food).ToString("0") + ", wood " + t.Stock(Res.Wood).ToString("0") + ", flint " + t.Stock(Res.Flint).ToString("0") +
                          ", hides " + t.Stock(Res.Hide).ToString("0") + ", spears " + t.Stock(Res.Spear).ToString("0") + ", cloaks " + t.Stock(Res.Cloak).ToString("0") + ", faith " + t.Faith.ToString("0"));
            sb.AppendLine("Buildings: " + string.Join(", ", Building.All.GroupBy(b => b.Def.Name + (b.Built ? "" : " (plan)")).Select(g => g.Key + " x" + g.Count())));
            sb.AppendLine("Era: " + KnowledgeSystem.Instance.CurrentEra);
            sb.AppendLine("Knowledge:");
            foreach (var d in KnowledgeSystem.Defs.Where(d => d.Implemented))
                sb.AppendLine("  " + d.Name + ": " + KnowledgeSystem.Instance.StatusOf(d.Id) + ", knowers " + KnowledgeSystem.Instance.KnowersOf(d.Id) +
                              ", insight " + (KnowledgeSystem.Instance.InsightOf(d.Id) / d.Insight * 100f).ToString("0") + "%");
            sb.AppendLine("Beliefs: " + string.Join("; ", CultureSystem.Instance.Beliefs.Select(b => b.Name + " [" + b.Theme + ", Y" + b.FormedYear + "]")));
            sb.AppendLine("Tendencies: " + string.Join(", ", CultureSystem.Instance.Tendency.Select(kv => kv.Key + " " + kv.Value.ToString("0.0"))));

            var entries = EventLog.Entries;
            sb.AppendLine();
            sb.AppendLine("Event counts (from game log, last " + entries.Count + " entries kept by the game; full log in events.txt):");
            sb.AppendLine("  (see counts below computed over full debug log)");
            sb.AppendLine();
            sb.AppendLine("Time use (person-hours by job):");
            int total = jobHours.Values.Sum();
            foreach (var kv in jobHours.OrderByDescending(k => k.Value))
                sb.AppendLine("  " + kv.Key.PadRight(16) + (kv.Value * 100f / Math.Max(1, total)).ToString("0.0").PadLeft(5) + "%");
            foreach (var s in jobBySeason)
            {
                int tot = s.Value.Values.Sum();
                sb.AppendLine("  " + s.Key + ": " + string.Join(", ", s.Value.OrderByDescending(k => k.Value).Take(7).Select(k => k.Key + " " + (k.Value * 100f / tot).ToString("0") + "%")));
            }
            sb.AppendLine();
            sb.AppendLine("Stuck people (6+ hours without moving while working): " + stuck.Count);
            foreach (var s in stuck.Take(25)) sb.AppendLine("  " + s);
            sb.AppendLine();
            sb.AppendLine("Exceptions: " + Engine.Exceptions.Count + " distinct");
            foreach (var kv in Engine.Exceptions.OrderByDescending(k => k.Value)) sb.AppendLine("  x" + kv.Value + "  " + kv.Key);
            foreach (var s in Engine.ExceptionSamples.Take(5)) sb.AppendLine("--- sample ---\n" + s);
            sb.AppendLine();
            sb.AppendLine("Player actions: " + Bot.Instance.Actions.Count);
            foreach (var a in Bot.Instance.Actions.Take(60)) sb.AppendLine("  " + a);
            File.WriteAllText(Path.Combine(dir, "summary.txt"), sb.ToString());
        }
    }
}
