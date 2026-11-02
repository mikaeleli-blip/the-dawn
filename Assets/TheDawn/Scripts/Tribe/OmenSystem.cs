using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    public enum OmenType { VisionOfTheHunt, SpiritOfCourage, DreamOfInsight, BountifulEarth }

    public class OmenDef
    {
        public OmenType Type;
        public string Name;
        public string Description;
        public float Cost;
        public float Days;
    }

    /// <summary>The spirit's only voice: omens bought with the tribe's faith.</summary>
    public class OmenSystem : MonoBehaviour
    {
        public static OmenSystem Instance { get; private set; }

        public static readonly OmenDef[] Defs =
        {
            new OmenDef { Type = OmenType.VisionOfTheHunt, Name = "Vision of the Hunt", Cost = 25f, Days = 1f,
                Description = "Hunters strike true and prey is slow to notice them." },
            new OmenDef { Type = OmenType.SpiritOfCourage, Name = "Spirit of Courage", Cost = 20f, Days = 1f,
                Description = "Adults stand and fight predators instead of fleeing." },
            new OmenDef { Type = OmenType.DreamOfInsight, Name = "Dream of Insight", Cost = 40f, Days = 2f,
                Description = "Ideas come twice as fast." },
            new OmenDef { Type = OmenType.BountifulEarth, Name = "Bountiful Earth", Cost = 30f, Days = 3f,
                Description = "Berry bushes regrow twice as fast." },
        };

        readonly Dictionary<OmenType, float> activeUntil = new Dictionary<OmenType, float>();

        void Awake() { Instance = this; }

        float Now
        {
            get { var c = GameClock.Instance; return c.TotalDays + c.Hour / 24f; }
        }

        public bool IsActive(OmenType t)
        {
            float until;
            return activeUntil.TryGetValue(t, out until) && until > Now;
        }

        public float DaysLeft(OmenType t)
        {
            float until;
            return activeUntil.TryGetValue(t, out until) ? Mathf.Max(0f, until - Now) : 0f;
        }

        public bool TrySend(OmenDef d)
        {
            var tribe = Tribe.Instance;
            if (tribe.Faith < d.Cost || IsActive(d.Type)) return false;
            tribe.Faith -= d.Cost;
            activeUntil[d.Type] = Now + d.Days;
            EventLog.Add("The tribe sees an omen: " + d.Name + ".", LogKind.Good);
            return true;
        }
    }
}
