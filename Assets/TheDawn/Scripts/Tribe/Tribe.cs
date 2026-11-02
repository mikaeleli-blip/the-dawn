using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    public enum PriorityCat { Food, Materials, Building, Crafting }
    public enum PriorityLevel { Low, Normal, High }

    /// <summary>
    /// The band as a whole: people, camps and their stores, seasonal moves, faith,
    /// priorities and life events.
    /// </summary>
    public class Tribe : MonoBehaviour
    {
        public static Tribe Instance { get; private set; }

        public string TribeName = "The Band";
        public readonly List<Person> People = new List<Person>();
        public PriorityLevel[] Priorities = { PriorityLevel.Normal, PriorityLevel.Normal, PriorityLevel.Normal, PriorityLevel.Normal };
        public float Faith = 5f;
        public int Deaths;
        public int Births;
        public bool SurvivedFirstWinter;

        // ---------- Camps ----------
        public readonly List<Camp> Camps = new List<Camp>();
        public Camp CurrentCamp { get; private set; }
        public Camp MovingFrom { get; private set; }
        public bool Moving { get; private set; }
        public bool Settled { get; private set; }
        public int Returns { get; private set; }
        public Camp HaulSource { get; private set; }
        public Vector3? MarkedSite { get; private set; }
        public SiteReport MarkedReport { get; private set; }

        readonly float[] bandStock = new float[6]; // carried bundles before the first camp
        GameObject bundle;
        GameObject marker;
        Transform peopleRoot;
        Transform campsRoot;
        bool movePending;
        float moveStartedAt;
        float haulUntil;
        bool hintedThisSeason;

        public bool HasCamp { get { return CurrentCamp != null; } }
        public Vector3 CampPosition { get { return CurrentCamp != null ? CurrentCamp.Center : bundle.transform.position; } }
        float[] stock { get { return CurrentCamp != null ? CurrentCamp.Stock : bandStock; } }
        float Now { get { var c = GameClock.Instance; return c.TotalDays + c.Hour / 24f; } }

        /// <summary>Returning to known camps teaches the band that the land can keep them. Speeds up Staying Put.</summary>
        public float FamiliarityMultiplier { get { return Mathf.Min(2.5f, 1f + Returns * 0.15f); } }

        void Awake() { Instance = this; }

        public void Init(Vector3 start)
        {
            CampSites.ResetNames();
            peopleRoot = new GameObject("People").transform;
            peopleRoot.SetParent(transform, false);
            campsRoot = new GameObject("Camps").transform;
            campsRoot.SetParent(transform, false);

            // The band's bundles: their shared belongings before they have a camp.
            bundle = new GameObject("Bundles");
            bundle.transform.SetParent(transform, false);
            bundle.transform.position = start;
            Visuals.Part(PrimitiveType.Cube, bundle.transform, new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.5f, 0.6f), new Color(0.45f, 0.33f, 0.22f));
            Visuals.Part(PrimitiveType.Cube, bundle.transform, new Vector3(0.6f, 0.2f, 0.3f), new Vector3(0.6f, 0.4f, 0.5f), new Color(0.5f, 0.4f, 0.28f));

            bandStock[(int)Res.Food] = 24f;
            bandStock[(int)Res.Wood] = 4f;
            bandStock[(int)Res.Flint] = 2f;
            bandStock[(int)Res.Spear] = 1f;

            // Starting band: an elder, five adults, two children.
            SpawnStarting(Sex.Female, Random.Range(53f, 58f));
            SpawnStarting(Sex.Male, Random.Range(28f, 36f));
            SpawnStarting(Sex.Female, Random.Range(25f, 33f));
            SpawnStarting(Sex.Male, Random.Range(19f, 26f));
            SpawnStarting(Sex.Female, Random.Range(18f, 24f));
            SpawnStarting(Random.value < 0.5f ? Sex.Male : Sex.Female, Random.Range(30f, 42f));
            var c1 = SpawnStarting(Random.value < 0.5f ? Sex.Male : Sex.Female, Random.Range(5f, 9f));
            var c2 = SpawnStarting(Random.value < 0.5f ? Sex.Male : Sex.Female, Random.Range(3f, 7f));

            // Two couples; the children belong to the first.
            Pair(People[1], People[2]);
            Pair(People[3], People[4]);
            foreach (var c in new[] { c1, c2 })
            {
                c.Mother = People[2]; c.Father = People[1];
                People[1].Children.Add(c); People[2].Children.Add(c);
            }

            // The elder carries the band's oldest knowledge.
            People[0].Known.Add(KnowledgeId.Firemaking);
            People[0].Skills[(int)Skill.Lore] = 8f;
            People[1].Known.Add(KnowledgeId.Firemaking);

            GameClock.Instance.OnNewDay += DailyTick;
            GameClock.Instance.OnNewSeason += SeasonTick;
        }

        Person SpawnStarting(Sex sex, float age)
        {
            var pos = World.Instance.RandomLandPointNear(CampPosition, 5f);
            var p = Person.Create(NameGen.Person(sex == Sex.Female), sex, age, pos, peopleRoot);
            p.Traits = PeopleInfo.RandomTraits(2, null, null);
            float years = Mathf.Max(0f, age - 8f);
            for (int i = 0; i < PeopleInfo.SkillCount; i++)
                p.Skills[i] = Mathf.Clamp(Random.Range(0f, 3f) + years * Random.Range(0.05f, 0.25f), 0f, 12f);
            People.Add(p);
            return p;
        }

        void Pair(Person a, Person b)
        {
            a.Partner = b;
            b.Partner = a;
        }

        // ---------- Stores (the current camp's) ----------

        public float Stock(Res r) { return stock[(int)r]; }
        public void Add(Res r, float amount) { stock[(int)r] += amount; }
        public float Take(Res r, float amount)
        {
            float got = Mathf.Min(amount, stock[(int)r]);
            stock[(int)r] -= got;
            return got;
        }

        /// <summary>How much more of a material the tribe wants right now (plans + fire fuel + reserve).</summary>
        public float Need(Res r)
        {
            float want = 0f;
            int fires = 0;
            foreach (var b in Building.All)
            {
                if (!InCurrentCamp(b)) continue;
                if (!b.Built) want += b.Remaining(r);
                else if (b.Def.Type == BuildingType.Campfire) fires++;
            }
            if (r == Res.Wood)
            {
                float reserve = GameClock.Instance.Season == Season.Autumn || GameClock.Instance.Season == Season.Winter ? 30f : 12f;
                want += Mathf.Max(1, fires) * reserve;
            }
            if (r == Res.Flint) want += 4f;
            return Mathf.Max(0f, want - Stock(r));
        }

        public float PriorityMul(PriorityCat c)
        {
            switch (Priorities[(int)c])
            {
                case PriorityLevel.Low: return 0.5f;
                case PriorityLevel.High: return 1.8f;
                default: return 1f;
            }
        }

        public bool InCurrentCamp(Building b)
        {
            return b.Camp == CurrentCamp || (b.Camp == null && CurrentCamp == null);
        }

        public Vector3 StockpilePoint(Vector3 from)
        {
            var pit = NearestInCamp(BuildingType.StoragePit, from);
            return pit != null ? pit.transform.position : CampPosition;
        }

        public Building NearestInCamp(BuildingType t, Vector3 from)
        {
            Building best = null;
            float bestD = float.MaxValue;
            foreach (var b in Building.All)
            {
                if (!b.Built || b.Def.Type != t || !InCurrentCamp(b)) continue;
                float d = (b.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        public bool CampHas(Camp camp, BuildingType t)
        {
            foreach (var b in Building.All) if (b.Built && b.Camp == camp && b.Def.Type == t) return true;
            return false;
        }

        public int CountRole(Role r)
        {
            int n = 0;
            foreach (var p in People) if (p.Role == r) n++;
            return n;
        }

        public int Adults
        {
            get { int n = 0; foreach (var p in People) if (p.IsAdult) n++; return n; }
        }

        public float FoodPerDay { get { return People.Count * 1.7f; } }
        public float FoodDays { get { return People.Count == 0 ? 0f : Stock(Res.Food) / FoodPerDay; } }

        // ---------- Camps ----------

        static CampKind KindForSeason(Season s)
        {
            return s == Season.Autumn || s == Season.Winter ? CampKind.Winter : CampKind.Warm;
        }

        /// <summary>The camp a position belongs to (within 100 m), if any.</summary>
        public Camp CampAt(Vector3 p)
        {
            Camp best = null;
            float bestD = 100f;
            foreach (var c in Camps)
            {
                float d = Vector3.Distance(c.Center, p);
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        Camp NewCamp(Vector3 center, CampKind kind, SiteReport report)
        {
            var c = new Camp(World.Instance.OnGround(center), kind, report ?? CampSites.Evaluate(center, kind), campsRoot);
            Camps.Add(c);
            return c;
        }

        /// <summary>The first campfire founds the first camp.</summary>
        public void FoundCamp(Vector3 firePos)
        {
            if (HasCamp) return;
            var kind = KindForSeason(GameClock.Instance.Season);
            Vector3 center = World.Instance.OnGround(firePos + new Vector3(2.5f, 0f, 1.5f));
            var camp = NewCamp(center, kind, null);
            for (int i = 0; i < bandStock.Length; i++) { camp.Stock[i] = bandStock[i]; bandStock[i] = 0f; }
            camp.TimesUsed = 1;
            camp.LastUsedYear = GameClock.Instance.Year;
            CurrentCamp = camp;
            foreach (var b in Building.All) if (b.Camp == null) b.Camp = camp;
            if (bundle != null) bundle.SetActive(false);
            EventLog.Add("The band has made its " + camp.KindName + ": " + camp.Name + ".", LogKind.Good);
        }

        public void MarkSite(Vector3 p)
        {
            MarkedSite = World.Instance.OnGround(p);
            var kind = HasCamp ? (CurrentCamp.Kind == CampKind.Warm ? CampKind.Winter : CampKind.Warm) : KindForSeason(GameClock.Instance.Season);
            MarkedReport = CampSites.Evaluate(MarkedSite.Value, kind);
            if (marker == null)
            {
                marker = new GameObject("Camp Marker");
                Visuals.Part(PrimitiveType.Cylinder, marker.transform, new Vector3(0, 1.5f, 0), new Vector3(0.12f, 1.5f, 0.12f), new Color(0.85f, 0.8f, 0.7f));
                Visuals.Part(PrimitiveType.Sphere, marker.transform, new Vector3(0, 0.25f, 0), new Vector3(1.2f, 0.5f, 1.2f), new Color(0.5f, 0.5f, 0.52f));
                Visuals.Part(PrimitiveType.Cube, marker.transform, new Vector3(0.25f, 2.7f, 0), new Vector3(0.5f, 0.35f, 0.04f), new Color(0.75f, 0.3f, 0.2f));
            }
            marker.transform.position = MarkedSite.Value;
            marker.SetActive(true);
            EventLog.Add("The spirit marks a place for the next camp. " + MarkedReport.Verdict);
        }

        public void ClearMark()
        {
            MarkedSite = null;
            MarkedReport = null;
            if (marker != null) marker.SetActive(false);
        }

        /// <summary>In-game days until the next seasonal move (or -1 if the band has settled / has no camp).</summary>
        public float DaysUntilMove
        {
            get
            {
                if (!HasCamp || Settled) return -1f;
                var c = GameClock.Instance;
                int seasonsAhead = c.Season == Season.Spring || c.Season == Season.Autumn ? 2 : 1;
                return seasonsAhead * c.DaysPerSeason - (c.DayOfSeason - 1) - c.Hour / 24f + 7f / 24f;
            }
        }

        void Update()
        {
            var clock = GameClock.Instance;
            if (clock == null) return;

            if (movePending && clock.Hour >= 7f && clock.Hour < 18f)
            {
                movePending = false;
                BeginMove();
            }

            if (Moving)
            {
                bool all = true;
                foreach (var p in People) if (!p.Migrated) { all = false; break; }
                if (all || Now - moveStartedAt > 1.5f)
                {
                    Moving = false;
                    foreach (var p in People) p.Migrated = true;
                    EventLog.Add("The band has settled into " + CurrentCamp.Name + ".", LogKind.Good);
                    if (MovingFrom != null && MovingFrom.StockTotal(false) >= 3f && Vector3.Distance(MovingFrom.Center, CurrentCamp.Center) < 500f)
                    {
                        HaulSource = MovingFrom;
                        haulUntil = Now + 4f;
                    }
                    MovingFrom = null;
                    RoleAssigner.Instance.Reassign();
                }
            }

            if (HaulSource != null && (Moving || Now > haulUntil || HaulSource.StockTotal(false) < 2f || HaulSource == CurrentCamp))
                HaulSource = null;
        }

        void BeginMove()
        {
            if (!HasCamp || Settled || People.Count == 0) return;
            var kind = KindForSeason(GameClock.Instance.Season);
            if (CurrentCamp.Kind == kind) return;

            Camp dest = null;
            if (MarkedSite.HasValue)
            {
                var existing = CampAt(MarkedSite.Value);
                float dist = Vector3.Distance(MarkedSite.Value, CurrentCamp.Center);
                if (existing == CurrentCamp || dist < CampSites.MinMoveDistance)
                    EventLog.Add("The elders won't move to the marked place: it's the land we already live on.", LogKind.Warning);
                else if (existing != null)
                    dest = existing;
                else if (dist > CampSites.MaxMoveDistance)
                    EventLog.Add("The elders won't move to the marked place: it's too far to walk with the children and the stores.", LogKind.Warning);
                else
                {
                    var report = CampSites.Evaluate(MarkedSite.Value, kind);
                    if (report.Acceptable) dest = NewCamp(MarkedSite.Value, kind, report);
                    else EventLog.Add("The elders refuse the marked place: " + report.Weakness + ".", LogKind.Warning);
                }
            }

            if (dest == null)
            {
                // Go back to a camp of this kind the band already knows.
                Camp best = null;
                foreach (var c in Camps)
                    if (c != CurrentCamp && c.Kind == kind && (best == null || c.TimesUsed > best.TimesUsed)) best = c;
                dest = best;
            }

            if (dest == null)
            {
                Vector3 p = CampSites.FindBest(CurrentCamp.Center, kind, Camps);
                dest = NewCamp(p, kind, null);
                EventLog.Add("The elders choose a place for the " + dest.KindName + " themselves: " + dest.Name + ".");
            }

            ClearMark();
            MovingFrom = CurrentCamp;
            CurrentCamp = dest;
            Moving = true;
            moveStartedAt = Now;
            HaulSource = null;
            if (dest.TimesUsed > 0)
            {
                Returns++;
                EventLog.Add("The band returns to " + dest.Name + ", the " + dest.KindName + " they know.", LogKind.Good);
            }
            dest.TimesUsed++;
            dest.LastUsedYear = GameClock.Instance.Year;
            foreach (var p in People) p.Migrated = false;

            // The band makes a fire wherever it stops.
            bool hasFire = false;
            foreach (var b in Building.All) if (b.Camp == dest && b.Def.Type == BuildingType.Campfire) hasFire = true;
            if (!hasFire)
            {
                Vector3 spot;
                var fireDef = BuildingDef.Get(BuildingType.Campfire);
                if (BuildPlacer.TryFindSpot(fireDef, dest.Center, 3f, out spot))
                    BuildPlacer.Instance.CreatePlan(fireDef, spot, 0f);
            }

            float km = Vector3.Distance(MovingFrom.Center, dest.Center);
            EventLog.Add("The band packs up and walks " + Mathf.RoundToInt(km) + " m to " + dest.Name + ".", LogKind.Good);
            RoleAssigner.Instance.Reassign();
        }

        /// <summary>Fills a pack from a camp's stores, most needed things first.</summary>
        public void PackFrom(Camp from, float[] pack, float capacity, bool includeWood)
        {
            Res[] order = { Res.Food, Res.Spear, Res.Cloak, Res.Wood, Res.Hide, Res.Flint };
            float room = capacity;
            foreach (var r in order)
            {
                if (r == Res.Wood && !includeWood) continue;
                float want = r == Res.Wood ? Mathf.Min(room, 2f) : room;
                float got = Mathf.Min(want, from.Stock[(int)r]);
                from.Stock[(int)r] -= got;
                pack[(int)r] += got;
                room -= got;
                if (room <= 0.01f) break;
            }
        }

        public void Unpack(float[] pack)
        {
            for (int i = 0; i < pack.Length; i++) { stock[i] += pack[i]; pack[i] = 0f; }
        }

        /// <summary>With Staying Put known, the band can make a camp its home for good.</summary>
        public bool CanSettle
        {
            get { return HasCamp && !Settled && !Moving && KnowledgeSystem.Instance.Effect(KnowledgeId.Sedentism) > 0f; }
        }

        public void Settle()
        {
            if (!CanSettle) return;
            Settled = true;
            ClearMark();
            EventLog.Add("The band will stay at " + CurrentCamp.Name + " all year round. The wandering is over.", LogKind.Good);
            KnowledgeSystem.Instance.OnSettled();
            Faith += 20f;
        }

        public Building FindShelterFor(Person p)
        {
            Building best = null;
            float bestScore = float.MinValue;
            foreach (var b in Building.All)
            {
                if (!b.Built || b.Def.ShelterCapacity <= 0 || !InCurrentCamp(b)) continue;
                if (b.Sleepers.Count >= b.Def.ShelterCapacity && !b.Sleepers.Contains(p)) continue;
                float score = b.ShelterWarmth - Vector3.Distance(b.transform.position, p.Position) * 0.05f;
                // Families sleep together.
                foreach (var s in b.Sleepers) if (s == p.Partner || s == p.Mother || p.Children.Contains(s)) score += 5f;
                if (score > bestScore) { bestScore = score; best = b; }
            }
            if (best != null && !best.Sleepers.Contains(p)) best.Sleepers.Add(p);
            return best;
        }

        public int ShelterSpaces
        {
            get
            {
                int n = 0;
                foreach (var b in Building.All) if (b.Built && InCurrentCamp(b)) n += b.Def.ShelterCapacity;
                return n;
            }
        }

        // ---------- Life and death ----------

        public void OnPersonDied(Person p, string cause)
        {
            People.Remove(p);
            Deaths++;
            EventLog.Add(p.Name + " (" + Mathf.FloorToInt(p.Age) + ") died of " + cause + ".", LogKind.Danger);
            if (p.Partner != null) { p.Partner.Grief += 30f; p.Partner.Partner = null; }
            float griefMul = CultureSystem.Instance.Has(CultureTheme.Ancestors) ? 0.5f : 1f;
            foreach (var o in People)
            {
                o.Grief += (o.Children.Contains(p) || p.Children.Contains(o) ? 25f : 8f) * griefMul;
                o.Children.Remove(p);
            }
            CultureSystem.Instance.Add(CultureTheme.Ancestors, 4f);
            if (cause.Contains("wolf") || cause.Contains("bear") || cause.Contains("boar")) CultureSystem.Instance.Add(CultureTheme.Wild, 3f);
            if (CultureSystem.Instance.Has(CultureTheme.Ancestors)) Faith += 5f;
            foreach (var b in Building.All) b.Sleepers.Remove(p);
            KnowledgeSystem.Instance.OnPersonDied(p);
            RoleAssigner.Instance.Reassign();

            if (People.Count == 0)
                EventLog.Add("The last of the band is gone. The fire goes cold.", LogKind.Danger);
        }

        void DailyTick()
        {
            var clock = GameClock.Instance;
            foreach (var p in People.ToArray()) p.OnNewDay();

            // Spoilage, camp by camp.
            float rate;
            switch (clock.Season)
            {
                case Season.Summer: rate = 0.07f; break;
                case Season.Winter: rate = 0.015f; break;
                default: rate = 0.04f; break;
            }
            float drying = KnowledgeSystem.Instance.Effect(KnowledgeId.FoodDrying);
            foreach (var c in Camps)
            {
                float r = rate;
                if (CampHas(c, BuildingType.DryingRack)) r *= 1f - 0.5f * drying;
                if (CampHas(c, BuildingType.StoragePit)) r *= 0.6f;
                if (c != CurrentCamp && !CampHas(c, BuildingType.StoragePit)) r *= 1.5f; // unguarded food: animals get at it
                c.Stock[(int)Res.Food] -= c.Stock[(int)Res.Food] * r;
            }
            if (!HasCamp) bandStock[(int)Res.Food] -= bandStock[(int)Res.Food] * rate;

            // Buildings at camps nobody is living at slowly fall apart.
            foreach (var b in Building.All.ToArray())
                if (b.Camp != null && b.Camp != CurrentCamp) b.DailyDecay();

            foreach (var n in ResourceNode.All) n.DailyTick(clock.Season);

            // Old age
            foreach (var p in People.ToArray())
            {
                if (p.Age > 55f && Random.value < (p.Age - 55f) * 0.004f) p.Die("old age");
            }

            PairUp();
            Births_();
            SeasonHints();
            if (People.Count > 0 && HasCamp && FoodDays < 1.5f)
                EventLog.Add("Food is running low: about " + FoodDays.ToString("0.0") + " days left at " + CurrentCamp.Name + ".", LogKind.Danger);
        }

        void SeasonTick()
        {
            var clock = GameClock.Instance;
            World.Instance.RefreshColors(clock.Season);
            hintedThisSeason = false;
            if (clock.Season == Season.Spring && clock.Year == 2 && People.Count > 0 && !SurvivedFirstWinter)
            {
                SurvivedFirstWinter = true;
                EventLog.Add("The band survived its first winter.", LogKind.Good);
                Faith += 15f;
            }
            // Wanderers move twice a year: to a warm-season camp in spring, a winter camp in autumn.
            if (HasCamp && !Settled && (clock.Season == Season.Spring || clock.Season == Season.Autumn))
                movePending = true;
        }

        void SeasonHints()
        {
            var c = GameClock.Instance;
            if (!HasCamp || Settled || hintedThisSeason || MarkedSite.HasValue) return;
            if (c.DayOfSeason < c.DaysPerSeason - 2) return;
            if (c.Season == Season.Summer || c.Season == Season.Winter)
            {
                var kind = c.Season == Season.Summer ? CampKind.Winter : CampKind.Warm;
                bool known = false;
                foreach (var camp in Camps) if (camp.Kind == kind) known = true;
                hintedThisSeason = true;
                if (!known)
                    EventLog.Add(kind == CampKind.Winter
                        ? "Autumn is near and the band will move to a winter camp. Mark a sheltered place among trees, with game and flint nearby."
                        : "Spring is near and the band will move to a warm-season camp. Mark a place near water and berries.", LogKind.Warning);
            }
        }

        void PairUp()
        {
            foreach (var a in People)
            {
                if (a.Partner != null || a.Age < 16f || a.Sex != Sex.Male) continue;
                foreach (var b in People)
                {
                    if (b.Partner != null || b.Age < 16f || b.Sex != Sex.Female) continue;
                    if (a.Mother == b.Mother && a.Mother != null) continue;
                    if (Mathf.Abs(a.Age - b.Age) > 15f) continue;
                    if (Random.value < 0.08f)
                    {
                        Pair(a, b);
                        EventLog.Add(a.Name + " and " + b.Name + " have become partners.", LogKind.Good);
                        return;
                    }
                }
            }
        }

        void Births_()
        {
            if (!HasCamp || Moving || FoodDays < 2f) return;
            foreach (var mother in People.ToArray())
            {
                if (mother.Sex != Sex.Female || mother.Partner == null || mother.Age < 17f || mother.Age > 40f) continue;
                if (mother.Health < 60f || mother.Hunger < 30f) continue;
                bool youngChild = false;
                foreach (var c in mother.Children) if (c.Age < 2.5f) youngChild = true;
                if (youngChild) continue;
                if (Random.value > 1f / (GameClock.Instance.DaysPerYear * 1.5f)) continue;

                var father = mother.Partner;
                Sex sex = Random.value < 0.5f ? Sex.Male : Sex.Female;
                var baby = Person.Create(NameGen.Person(sex == Sex.Female), sex, 0f, World.Instance.RandomLandPointNear(mother.Position, 1f), peopleRoot);
                baby.Traits = PeopleInfo.RandomTraits(2, mother.Traits, father.Traits);
                baby.Mother = mother; baby.Father = father;
                mother.Children.Add(baby); father.Children.Add(baby);
                People.Add(baby);
                Births++;
                Faith += 3f;
                EventLog.Add(mother.Name + " and " + father.Name + " had a child: " + baby.Name + ".", LogKind.Good);
            }
        }
    }
}
