using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    public enum Sex { Male, Female }

    public enum Skill { Foraging, Hunting, Woodcutting, Building, Crafting, Lore }

    public enum Trait { Brave, Cautious, Strong, Patient, KeenEyed, Curious, Lazy, Hardworking, Hardy, Spiritual, Kind, Restless }

    public enum Role { None, Child, Elder, Forager, Hunter, Gatherer, Builder, Crafter, Firekeeper }

    public enum Res { Food, Wood, Flint, Hide, Spear, Cloak }

    public static class PeopleInfo
    {
        public const int SkillCount = 6;
        public const float AdultAge = 13f;
        public const float ElderAge = 52f;

        public static readonly Role[] WorkRoles = { Role.Forager, Role.Hunter, Role.Gatherer, Role.Builder, Role.Crafter, Role.Firekeeper };

        public static Skill MainSkill(Role r)
        {
            switch (r)
            {
                case Role.Forager: return Skill.Foraging;
                case Role.Hunter: return Skill.Hunting;
                case Role.Gatherer: return Skill.Woodcutting;
                case Role.Builder: return Skill.Building;
                case Role.Crafter: return Skill.Crafting;
                default: return Skill.Lore;
            }
        }

        public static Color RoleColor(Role r)
        {
            switch (r)
            {
                case Role.Forager: return new Color(0.55f, 0.65f, 0.3f);
                case Role.Hunter: return new Color(0.55f, 0.3f, 0.2f);
                case Role.Gatherer: return new Color(0.45f, 0.38f, 0.28f);
                case Role.Builder: return new Color(0.6f, 0.5f, 0.3f);
                case Role.Crafter: return new Color(0.4f, 0.42f, 0.5f);
                case Role.Firekeeper: return new Color(0.75f, 0.45f, 0.15f);
                case Role.Elder: return new Color(0.7f, 0.7f, 0.68f);
                case Role.Child: return new Color(0.75f, 0.65f, 0.5f);
                default: return new Color(0.5f, 0.45f, 0.4f);
            }
        }

        public static string Describe(Trait t)
        {
            switch (t)
            {
                case Trait.Brave: return "Stands and fights predators. Drawn to hunting.";
                case Trait.Cautious: return "Flees danger early. Prefers safe work near camp.";
                case Trait.Strong: return "Carries more, hits harder. Good gatherer and builder.";
                case Trait.Patient: return "Good at slow careful work: crafting, foraging, stalking.";
                case Trait.KeenEyed: return "Spots prey and berries. Good hunter and forager.";
                case Trait.Curious: return "Makes discoveries faster.";
                case Trait.Lazy: return "Works slower.";
                case Trait.Hardworking: return "Works faster.";
                case Trait.Hardy: return "Handles the cold better.";
                case Trait.Spiritual: return "Brings more faith to gatherings. Drawn to tending the fire.";
                case Trait.Kind: return "Teaches others well. Lifts the mood of those nearby.";
                default: return "Dislikes staying put. Drawn to hunting and roaming work.";
            }
        }

        /// <summary>How much a trait pulls someone toward (or away from) a role.</summary>
        public static float Affinity(Trait t, Role r)
        {
            switch (t)
            {
                case Trait.Brave: return r == Role.Hunter ? 1f : 0f;
                case Trait.Cautious: return r == Role.Hunter ? -1f : (r == Role.Forager || r == Role.Crafter ? 0.5f : 0f);
                case Trait.Strong: return r == Role.Gatherer || r == Role.Builder ? 1f : 0f;
                case Trait.Patient: return r == Role.Crafter ? 1f : (r == Role.Forager ? 0.5f : 0f);
                case Trait.KeenEyed: return r == Role.Hunter || r == Role.Forager ? 0.8f : 0f;
                case Trait.Spiritual: return r == Role.Firekeeper ? 1f : 0f;
                case Trait.Restless: return r == Role.Hunter ? 0.7f : (r == Role.Firekeeper || r == Role.Crafter ? -0.6f : 0f);
                case Trait.Kind: return r == Role.Firekeeper ? 0.3f : 0f;
                default: return 0f;
            }
        }

        static readonly Trait[][] Opposites =
        {
            new[] { Trait.Brave, Trait.Cautious },
            new[] { Trait.Lazy, Trait.Hardworking },
            new[] { Trait.Patient, Trait.Restless },
        };

        public static bool Conflicts(Trait a, Trait b)
        {
            if (a == b) return true;
            foreach (var pair in Opposites)
                if ((pair[0] == a && pair[1] == b) || (pair[0] == b && pair[1] == a)) return true;
            return false;
        }

        public static List<Trait> RandomTraits(int count, List<Trait> inheritA, List<Trait> inheritB)
        {
            var result = new List<Trait>();
            int guard = 0;
            while (result.Count < count && guard++ < 50)
            {
                Trait t;
                float roll = Random.value;
                if (roll < 0.3f && inheritA != null && inheritA.Count > 0) t = inheritA[Random.Range(0, inheritA.Count)];
                else if (roll < 0.6f && inheritB != null && inheritB.Count > 0) t = inheritB[Random.Range(0, inheritB.Count)];
                else t = (Trait)Random.Range(0, 12);
                bool ok = true;
                foreach (var e in result) if (Conflicts(e, t)) ok = false;
                if (ok) result.Add(t);
            }
            return result;
        }
    }
}
