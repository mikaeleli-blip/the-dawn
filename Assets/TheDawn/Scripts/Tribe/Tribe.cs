using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    public enum PriorityCat { Food, Materials, Building, Crafting }
    public enum PriorityLevel { Low, Normal, High }

    /// <summary>The band as a whole: people, shared stores, camp, faith, priorities, life events.</summary>
    public class Tribe : MonoBehaviour
    {
        public static Tribe Instance { get; private set; }

        public string TribeName = "The Band";
        public readonly List<Person> People = new List<Person>();
        readonly float[] stock = new float[6];
        public PriorityLevel[] Priorities = { PriorityLevel.Normal, PriorityLevel.Normal, PriorityLevel.Normal, PriorityLevel.Normal };
        public float Faith = 5f;
        public bool HasCamp;
        public Vector3 CampPosition;
        public int Deaths;
        public int Births;
        public bool SurvivedFirstWinter;

        GameObject bundle;
        Transform peopleRoot;

        void Awake() { Instance = this; }

        public void Init(Vector3 start)
        {
            CampPosition = start;
            peopleRoot = new GameObject("People").transform;
            peopleRoot.SetParent(transform, false);

            // The band's bundles: their shared belongings before they have a camp.
            bundle = new GameObject("Bundles");
            bundle.transform.SetParent(transform, false);
            bundle.transform.position = start;
            Visuals.Part(PrimitiveType.Cube, bundle.transform, new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.5f, 0.6f), new Color(0.45f, 0.33f, 0.22f));
            Visuals.Part(PrimitiveType.Cube, bundle.transform, new Vector3(0.6f, 0.2f, 0.3f), new Vector3(0.6f, 0.4f, 0.5f), new Color(0.5f, 0.4f, 0.28f));

            stock[(int)Res.Food] = 24f;
            stock[(int)Res.Wood] = 4f;
            stock[(int)Res.Flint] = 2f;
            stock[(int)Res.Spear] = 1f;

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

        // ---------- Stores ----------

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
            foreach (var b in Building.All)
                if (!b.Built) want += b.Remaining(r);
            if (r == Res.Wood)
            {
                int fires = 0;
                foreach (var b in Building.All) if (b.Built && b.Def.Type == BuildingType.Campfire) fires++;
                float reserve = GameClock.Instance.Season == Season.Autumn || GameClock.Instance.Season == Season.Winter ? 30f : 12f;
                want += fires * reserve;
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

        public Vector3 StockpilePoint(Vector3 from)
        {
            var pit = Building.NearestBuilt(BuildingType.StoragePit, from, 1000f);
            return pit != null ? pit.transform.position : CampPosition;
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

        // ---------- Camp ----------

        public void FoundCamp(Vector3 at)
        {
            if (HasCamp) return;
            HasCamp = true;
            CampPosition = at;
            if (bundle != null) bundle.transform.position = World.Instance.OnGround(at + new Vector3(2.5f, 0f, 1.5f));
            CampPosition = bundle.transform.position;
            EventLog.Add("The band has chosen a place to make camp.", LogKind.Good);
        }

        public Building FindShelterFor(Person p)
        {
            Building best = null;
            float bestScore = float.MinValue;
            foreach (var b in Building.All)
            {
                if (!b.Built || b.Def.ShelterCapacity <= 0) continue;
                if (b.Sleepers.Count >= b.Def.ShelterCapacity && !b.Sleepers.Contains(p)) continue;
                float score = b.Def.ShelterWarmth - Vector3.Distance(b.transform.position, p.Position) * 0.05f;
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
                foreach (var b in Building.All) if (b.Built) n += b.Def.ShelterCapacity;
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

            // Spoilage
            float rate;
            switch (clock.Season)
            {
                case Season.Summer: rate = 0.07f; break;
                case Season.Winter: rate = 0.015f; break;
                default: rate = 0.04f; break;
            }
            if (Building.AnyBuilt(BuildingType.DryingRack)) rate *= 1f - 0.5f * KnowledgeSystem.Instance.Effect(KnowledgeId.FoodDrying);
            if (Building.AnyBuilt(BuildingType.StoragePit)) rate *= 0.6f;
            float spoiled = stock[(int)Res.Food] * rate;
            stock[(int)Res.Food] -= spoiled;

            foreach (var n in ResourceNode.All) n.DailyTick(clock.Season);

            // Old age
            foreach (var p in People.ToArray())
            {
                if (p.Age > 55f && Random.value < (p.Age - 55f) * 0.004f) p.Die("old age");
            }

            PairUp();
            Births_();
        }

        void SeasonTick()
        {
            var clock = GameClock.Instance;
            World.Instance.RefreshColors(clock.Season);
            if (clock.Season == Season.Spring && clock.Year == 2 && People.Count > 0 && !SurvivedFirstWinter)
            {
                SurvivedFirstWinter = true;
                EventLog.Add("The band survived its first winter.", LogKind.Good);
                Faith += 15f;
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
            if (!HasCamp || FoodDays < 2f) return;
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
