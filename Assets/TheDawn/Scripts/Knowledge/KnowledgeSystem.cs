using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    public enum Era { Wanderers, Settling, Pottery, Copper, Bronze }

    public enum KnowledgeId
    {
        None,
        // Wanderers
        Firemaking, FireHardenedSpears, Tracking, HideWorking, FoodDrying, HerbalLore, StoragePits, Sedentism,
        // Settling & Farming
        WildGrain, Cultivation, AnimalTaming, Palisades,
        // Pottery & Storage
        Pottery, Kilns, Weaving,
        // Copper
        NativeCopper, Smelting,
        // Bronze
        TinTrade, Bronze
    }

    public enum KnowledgeStatus { Unknown, Known, Faded }

    public class KnowledgeDef
    {
        public KnowledgeId Id;
        public string Name;
        public string Description;
        public Era Era;
        public Skill Activity;
        public Skill? Activity2;   // a second kind of work that also builds insight
        public float Insight;
        public KnowledgeId[] Requires = new KnowledgeId[0];
        public bool Implemented = true;
    }

    /// <summary>
    /// Discoveries come from doing (insight builds through activity), belong to the
    /// people who know them, spread by teaching at the fire, and fade if every
    /// knower dies.
    /// </summary>
    public class KnowledgeSystem : MonoBehaviour
    {
        public static KnowledgeSystem Instance { get; private set; }

        public static readonly KnowledgeDef[] Defs =
        {
            K(KnowledgeId.Firemaking, "Firemaking", "Making and keeping fire. The band's oldest knowledge.", Era.Wanderers, Skill.Lore, 300),
            K(KnowledgeId.FireHardenedSpears, "Fire-hardened Spears", "Hunters hit more often and harder.", Era.Wanderers, Skill.Hunting, 450),
            K(KnowledgeId.Tracking, "Tracking", "Hunters are less likely to startle prey.", Era.Wanderers, Skill.Hunting, 750),
            K(KnowledgeId.HideWorking, "Hide Working", "Unlocks Hide Huts and hide cloaks.", Era.Wanderers, Skill.Crafting, 380),
            K(KnowledgeId.FoodDrying, "Food Drying", "Unlocks the Drying Rack. Less food spoils.", Era.Wanderers, Skill.Foraging, 600),
            K(KnowledgeId.HerbalLore, "Herbal Lore", "Wounds and sickness heal faster.", Era.Wanderers, Skill.Foraging, 900),
            K2(KnowledgeId.StoragePits, "Storage Pits", "Unlocks the Storage Pit: food keeps longer, even at a camp left empty.", Era.Wanderers, Skill.Foraging, Skill.Building, 1800,
                KnowledgeId.FoodDrying),
            K(KnowledgeId.Sedentism, "Staying Put", "The idea of living in one place all year. Grows from stories at the fire, faster each time the band returns to a camp it knows. Lets the band settle for good.", Era.Wanderers, Skill.Lore, 20000,
                KnowledgeId.FoodDrying, KnowledgeId.StoragePits, KnowledgeId.HideWorking),

            K(KnowledgeId.WildGrain, "Wild Grain Gathering", "Harvesting wild grasses for seed.", Era.Settling, Skill.Foraging, 1500, KnowledgeId.Sedentism),
            K(KnowledgeId.Cultivation, "Cultivation", "Sowing seed in cleared earth: the first fields.", Era.Settling, Skill.Foraging, 4000, KnowledgeId.WildGrain),
            K(KnowledgeId.AnimalTaming, "Animal Taming", "Pens, captured young animals, and slow breeding toward tame herds and dogs.", Era.Settling, Skill.Hunting, 3500, KnowledgeId.Sedentism),
            K(KnowledgeId.Palisades, "Palisades", "Wooden walls that keep predators out.", Era.Settling, Skill.Building, 2500, KnowledgeId.Sedentism),

            K(KnowledgeId.Pottery, "Pottery", "Fired clay vessels for storage and cooking.", Era.Pottery, Skill.Crafting, 4000, KnowledgeId.Cultivation),
            K(KnowledgeId.Kilns, "Kilns", "Hotter, controlled fires.", Era.Pottery, Skill.Crafting, 5000, KnowledgeId.Pottery),
            K(KnowledgeId.Weaving, "Weaving", "Plant-fibre cloth.", Era.Pottery, Skill.Crafting, 4500, KnowledgeId.Cultivation),

            K(KnowledgeId.NativeCopper, "Native Copper", "Hammering soft copper found in the rock.", Era.Copper, Skill.Crafting, 6000, KnowledgeId.Kilns),
            K(KnowledgeId.Smelting, "Smelting", "Melting copper out of ore.", Era.Copper, Skill.Crafting, 9000, KnowledgeId.NativeCopper),

            K(KnowledgeId.TinTrade, "Tin Trade", "Tin is rare; it must be found or traded for.", Era.Bronze, Skill.Lore, 8000, KnowledgeId.Smelting),
            K(KnowledgeId.Bronze, "Bronze", "Copper and tin together: the Bronze Age.", Era.Bronze, Skill.Crafting, 12000, KnowledgeId.TinTrade),
        };

        static KnowledgeDef K(KnowledgeId id, string name, string desc, Era era, Skill act, float insight, params KnowledgeId[] req)
        {
            return new KnowledgeDef
            {
                Id = id, Name = name, Description = desc, Era = era, Activity = act, Insight = insight, Requires = req,
                Implemented = era == Era.Wanderers
            };
        }

        static KnowledgeDef K2(KnowledgeId id, string name, string desc, Era era, Skill act, Skill act2, float insight, params KnowledgeId[] req)
        {
            var d = K(id, name, desc, era, act, insight, req);
            d.Activity2 = act2;
            return d;
        }

        public static KnowledgeDef Def(KnowledgeId id)
        {
            foreach (var d in Defs) if (d.Id == id) return d;
            return null;
        }

        public Era CurrentEra = Era.Wanderers;
        readonly Dictionary<KnowledgeId, float> insight = new Dictionary<KnowledgeId, float>();
        readonly Dictionary<KnowledgeId, KnowledgeStatus> status = new Dictionary<KnowledgeId, KnowledgeStatus>();
        readonly Dictionary<KnowledgeId, int> knowers = new Dictionary<KnowledgeId, int>();
        float recountTimer;
        bool taughtToday;

        void Awake()
        {
            Instance = this;
            foreach (var d in Defs)
            {
                insight[d.Id] = 0f;
                status[d.Id] = KnowledgeStatus.Unknown;
                knowers[d.Id] = 0;
            }
        }

        public void Init()
        {
            status[KnowledgeId.Firemaking] = KnowledgeStatus.Known;
            insight[KnowledgeId.Firemaking] = Def(KnowledgeId.Firemaking).Insight;
            GameClock.Instance.OnNewDay += () => { taughtToday = false; DailyInsight(); };
            Recount();
        }

        public float InsightOf(KnowledgeId id) { return insight[id]; }
        public KnowledgeStatus StatusOf(KnowledgeId id) { return status[id]; }
        public int KnowersOf(KnowledgeId id) { return knowers[id]; }

        /// <summary>1 = fully known by a living person, 0.5 = faded, 0 = unknown.</summary>
        public float Effect(KnowledgeId id)
        {
            if (id == KnowledgeId.None) return 1f;
            if (knowers[id] > 0) return 1f;
            return status[id] == KnowledgeStatus.Faded ? 0.5f : 0f;
        }

        public bool PrereqsMet(KnowledgeDef d)
        {
            foreach (var r in d.Requires) if (Effect(r) <= 0f) return false;
            return true;
        }

        public bool Researchable(KnowledgeDef d)
        {
            return d.Implemented && d.Era <= CurrentEra && knowers[d.Id] == 0 && PrereqsMet(d);
        }

        public void AddInsight(Person p, Skill activity, float amount)
        {
            if (OmenSystem.Instance.IsActive(OmenType.DreamOfInsight)) amount *= 2f;
            if (Tribe.Instance.CountRole(Role.Elder) > 0) amount *= 1.15f;
            foreach (var d in Defs)
            {
                if ((d.Activity != activity && d.Activity2 != activity) || !Researchable(d)) continue;
                // Staying Put grows mostly from seasons lived at known camps (see DailyInsight); stories add a little.
                insight[d.Id] += d.Id == KnowledgeId.Sedentism ? amount * 0.3f * Tribe.Instance.FamiliarityMultiplier : amount;
                if (insight[d.Id] >= d.Insight)
                {
                    insight[d.Id] = d.Insight;
                    Discover(d, p);
                }
            }
        }

        void Discover(KnowledgeDef d, Person p)
        {
            bool re = status[d.Id] == KnowledgeStatus.Faded;
            status[d.Id] = KnowledgeStatus.Known;
            p.Known.Add(d.Id);
            Recount();
            EventLog.Add(re ? p.Name + " has rediscovered " + d.Name + "." : p.Name + " has discovered " + d.Name + "!", LogKind.Good);
            Tribe.Instance.Faith += 4f;
            if (d.Id == KnowledgeId.Sedentism && !re)
                EventLog.Add("The elders talk of staying in one place all year round. Choose a camp and settle for good (Camps panel).", LogKind.Good);
        }

        /// <summary>Every day the band lives off the land at its camps, the idea of staying grows.</summary>
        void DailyInsight()
        {
            var tribe = Tribe.Instance;
            var d = Def(KnowledgeId.Sedentism);
            if (!tribe.HasCamp || tribe.People.Count == 0 || !Researchable(d)) return;
            insight[d.Id] += 55f * tribe.FamiliarityMultiplier;
            if (insight[d.Id] >= d.Insight)
            {
                insight[d.Id] = d.Insight;
                Person eldest = null;
                foreach (var p in tribe.People) if (p.IsAdult && (eldest == null || p.Age > eldest.Age)) eldest = p;
                if (eldest != null) Discover(d, eldest);
            }
        }

        public void OnSettled()
        {
            if (CurrentEra != Era.Wanderers) return;
            CurrentEra = Era.Settling;
            EventLog.Add("The Wanderers era is over. A new era begins: Settling & Farming. (Not yet in this build.)", LogKind.Good);
        }

        public void OnPersonDied(Person p)
        {
            Recount();
            foreach (var id in p.Known)
            {
                if (knowers[id] == 0 && status[id] == KnowledgeStatus.Known)
                {
                    status[id] = KnowledgeStatus.Faded;
                    // Fragments remain: rediscovery needs only ~30% of the original insight.
                    insight[id] = Def(id).Insight * 0.7f;
                    EventLog.Add("With " + p.Name + " gone, no one fully remembers " + Def(id).Name + ".", LogKind.Danger);
                }
            }
        }

        void Recount()
        {
            foreach (var d in Defs) knowers[d.Id] = 0;
            if (Tribe.Instance == null) return;
            foreach (var p in Tribe.Instance.People)
                foreach (var id in p.Known)
                    knowers[id]++;
        }

        void Update()
        {
            recountTimer -= Time.deltaTime;
            if (recountTimer <= 0f) { recountTimer = 1f; Recount(); }

            var clock = GameClock.Instance;
            if (!taughtToday && clock.Hour >= 20f && clock.Hour < 21f)
            {
                taughtToday = true;
                TeachAtFires();
            }
        }

        /// <summary>Each evening, people gathered at a fire learn from those who know.</summary>
        void TeachAtFires()
        {
            var people = Tribe.Instance.People;
            int lessons = 0;
            foreach (var learner in people)
            {
                if (learner.Age < 4f) continue;
                if (Building.NearestLitFire(learner.Position, 9f) == null) continue;
                foreach (var teacher in people)
                {
                    if (teacher == learner || teacher.Known.Count == 0) continue;
                    if ((teacher.Position - learner.Position).sqrMagnitude > 12f * 12f) continue;
                    foreach (var id in teacher.Known)
                    {
                        if (learner.Known.Contains(id)) continue;
                        float chance = 0.25f;
                        if (teacher.IsElder) chance *= 1.6f;
                        if (teacher.Has(Trait.Kind)) chance *= 1.4f;
                        if (learner.IsChild) chance *= 1.3f;
                        if (learner.Has(Trait.Curious)) chance *= 1.3f;
                        if (Random.value < chance)
                        {
                            learner.Known.Add(id);
                            lessons++;
                            break; // one lesson per teacher per evening
                        }
                    }
                }
            }
            if (lessons > 0)
            {
                Recount();
                EventLog.Add("Knowledge was passed on at the fire tonight (" + lessons + (lessons == 1 ? " lesson)." : " lessons)."));
            }
        }
    }
}
