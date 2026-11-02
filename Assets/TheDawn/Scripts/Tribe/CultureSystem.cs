using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    public enum CultureTheme { Ancestors, Beasts, Hearth, Wild }

    public class Belief
    {
        public CultureTheme Theme;
        public string Name;
        public string Effect;
        public int FormedYear;
    }

    /// <summary>
    /// Culture forms from what the tribe lives through. Tendencies build up from events;
    /// when one is strong enough a belief forms, with a name invented for this run.
    /// </summary>
    public class CultureSystem : MonoBehaviour
    {
        public static CultureSystem Instance { get; private set; }

        public readonly Dictionary<CultureTheme, float> Tendency = new Dictionary<CultureTheme, float>();
        public readonly List<Belief> Beliefs = new List<Belief>();
        const float Threshold = 12f;

        void Awake()
        {
            Instance = this;
            foreach (CultureTheme t in System.Enum.GetValues(typeof(CultureTheme))) Tendency[t] = 0f;
        }

        public bool Has(CultureTheme t)
        {
            foreach (var b in Beliefs) if (b.Theme == t) return true;
            return false;
        }

        public void Add(CultureTheme t, float amount)
        {
            Tendency[t] += amount;
            if (!Has(t) && Tendency[t] >= Threshold)
                Form(t);
        }

        void Form(CultureTheme t)
        {
            string key = t == CultureTheme.Ancestors ? "ancestors" : t == CultureTheme.Beasts ? "beasts" : t == CultureTheme.Hearth ? "hearth" : "wild";
            var b = new Belief
            {
                Theme = t,
                Name = NameGen.Belief(key),
                FormedYear = GameClock.Instance.Year,
                Effect = EffectText(t)
            };
            Beliefs.Add(b);
            EventLog.Add("A belief has taken hold among the tribe: " + b.Name + ".", LogKind.Good);
            Tribe.Instance.Faith += 10f;
        }

        static string EffectText(CultureTheme t)
        {
            switch (t)
            {
                case CultureTheme.Ancestors: return "Grief weighs half as much; faith when someone dies.";
                case CultureTheme.Beasts: return "Faith from each successful hunt.";
                case CultureTheme.Hearth: return "Gatherings at the fire give 50% more faith.";
                default: return "Faith whenever a predator is driven off.";
            }
        }

        public float HearthFaithMultiplier { get { return Has(CultureTheme.Hearth) ? 1.5f : 1f; } }
        public float MoodBonus { get { return Beliefs.Count * 2f; } }
    }
}
