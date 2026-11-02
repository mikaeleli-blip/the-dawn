using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    /// <summary>
    /// The tribe decides among itself who does what. Demand for each role comes from
    /// the tribe's needs (scaled by the player's priorities); each person is scored on
    /// skill, traits and how long they've held their current role.
    /// </summary>
    public class RoleAssigner : MonoBehaviour
    {
        public static RoleAssigner Instance { get; private set; }

        public readonly Dictionary<Role, float> Demand = new Dictionary<Role, float>();
        float timer;

        void Awake() { Instance = this; }

        void Update()
        {
            timer -= Time.deltaTime;
            if (timer <= 0f)
            {
                timer = 10f; // every ~1 in-game hour at 1x
                Reassign();
            }
        }

        public void Reassign()
        {
            var tribe = Tribe.Instance;
            if (tribe == null) return;
            var clock = GameClock.Instance;

            // Children and elders have their own roles.
            var workers = new List<Person>();
            foreach (var p in tribe.People)
            {
                if (p.IsChild) p.Role = Role.Child;
                else if (p.IsElder) p.Role = Role.Elder;
                else workers.Add(p);
            }
            if (workers.Count == 0) return;

            ComputeDemand(tribe, clock);

            // Turn demand weights into head counts (largest remainder).
            float total = 0f;
            foreach (var r in PeopleInfo.WorkRoles) total += Demand[r];
            var slots = new Dictionary<Role, int>();
            var rema = new List<KeyValuePair<Role, float>>();
            int assigned = 0;
            foreach (var r in PeopleInfo.WorkRoles)
            {
                float exact = total > 0f ? Demand[r] / total * workers.Count : 0f;
                int n = Mathf.FloorToInt(exact);
                slots[r] = n;
                assigned += n;
                rema.Add(new KeyValuePair<Role, float>(r, exact - n));
            }
            rema.Sort((a, b) => b.Value.CompareTo(a.Value));
            for (int i = 0; assigned < workers.Count && i < rema.Count; i++, assigned++) slots[rema[i].Key]++;

            // Fill the most demanded roles first with the best-suited people.
            var order = new List<Role>(PeopleInfo.WorkRoles);
            order.Sort((a, b) => Demand[b].CompareTo(Demand[a]));
            var free = new List<Person>(workers);
            foreach (var role in order)
            {
                for (int k = 0; k < slots[role] && free.Count > 0; k++)
                {
                    Person best = null;
                    float bestScore = float.MinValue;
                    foreach (var p in free)
                    {
                        float s = Score(p, role);
                        if (s > bestScore) { bestScore = s; best = p; }
                    }
                    free.Remove(best);
                    if (best.Role != role)
                    {
                        best.Role = role;
                        best.DaysAsRole = 0f;
                    }
                }
            }
            foreach (var p in free) p.Role = Role.Gatherer;
        }

        public static float Score(Person p, Role role)
        {
            float s = p.SkillOf(PeopleInfo.MainSkill(role));
            foreach (var t in p.Traits) s += PeopleInfo.Affinity(t, role) * 5f;
            if (p.Role == role) s += 3f + Mathf.Min(p.DaysAsRole, 6f) * 0.5f; // people settle into their work
            if (role == Role.Hunter && p.Sex == Sex.Female && p.Children.Exists(c => c.Age < 3f)) s -= 4f;
            return s;
        }

        void ComputeDemand(Tribe tribe, GameClock clock)
        {
            float foodDays = tribe.FoodDays;
            // Outside the growing season nothing regrows: aim to have enough to last until spring.
            float targetDays = 4f;
            float toSpring = clock.Season == Season.Autumn ? clock.DaysPerSeason * (2f - clock.SeasonProgress)
                           : clock.Season == Season.Winter ? clock.DaysPerSeason * (1f - clock.SeasonProgress) : 0f;
            if (toSpring > 0f) targetDays = Mathf.Min(14f, toSpring + 2f);
            float food = Mathf.Clamp(targetDays - foodDays, 0.3f, 10f) * tribe.PriorityMul(PriorityCat.Food);
            if (foodDays < 2f) food *= 2f; // hunger comes first

            bool winter = clock.Season == Season.Winter;
            // Split food work by what's actually out there: bare bushes send people hunting.
            float berries = 0f;
            Vector3 camp = tribe.CampPosition;
            foreach (var n in ResourceNode.All)
                if (n.Type == ResourceType.BerryBush && (n.transform.position - camp).sqrMagnitude < 90f * 90f) berries += n.Amount;
            float forage = Mathf.Clamp(berries / (12f + tribe.People.Count * 4f), 0.1f, 1f);
            Demand[Role.Forager] = food * forage;
            Demand[Role.Hunter] = food * (1.3f - forage * 0.6f) + 0.4f;

            float materials = (tribe.Need(Res.Wood) / 15f + tribe.Need(Res.Flint) / 8f + 0.3f) * tribe.PriorityMul(PriorityCat.Materials);
            if (tribe.HaulSource != null) materials += Mathf.Min(3f, tribe.HaulSource.StockTotal(false) / 15f); // fetch what was left behind
            Demand[Role.Gatherer] = Mathf.Min(materials, 6f);

            int plans = 0, repairs = 0;
            foreach (var b in Building.All)
            {
                if (!tribe.InCurrentCamp(b)) continue;
                if (!b.Built) plans++;
                else if (b.NeedsRepair) repairs++;
            }
            Demand[Role.Builder] = plans + repairs > 0 ? (0.8f + plans * 0.6f + repairs * 0.4f) * tribe.PriorityMul(PriorityCat.Building) : 0f;

            float spears = tribe.Stock(Res.Spear);
            foreach (var p in tribe.People) if (p.HasSpear) spears++;
            float craftNeed = Mathf.Max(0f, tribe.CountRole(Role.Hunter) + 2f - spears) * 0.4f;
            bool hideWork = KnowledgeSystem.Instance.Effect(KnowledgeId.HideWorking) > 0f;
            if (hideWork && tribe.Stock(Res.Hide) >= 2f) craftNeed += 0.6f;
            if (!hideWork && tribe.Stock(Res.Hide) >= 2f) craftNeed += 0.5f; // someone tinkers with the hides
            Demand[Role.Crafter] = craftNeed * tribe.PriorityMul(PriorityCat.Crafting);

            bool hasFire = tribe.NearestInCamp(BuildingType.Campfire, tribe.CampPosition) != null;
            bool elderTends = tribe.CountRole(Role.Elder) > 0;
            Demand[Role.Firekeeper] = hasFire ? (elderTends ? 0.25f : 0.6f) * (winter ? 2f : 1f) : 0f;
        }
    }
}
