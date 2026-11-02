using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    public enum CampKind { Warm, Winter }

    /// <summary>A seasonal camp. Each camp keeps its own stores; buildings belong to the camp they were built in.</summary>
    public class Camp
    {
        static int nextId = 1;

        public int Id;
        public string Name;
        public CampKind Kind;
        public Vector3 Center;
        public readonly float[] Stock = new float[6];
        public int TimesUsed;
        public int LastUsedYear;
        public SiteReport Report;
        public GameObject Pile;

        public string KindName { get { return Kind == CampKind.Warm ? "warm-season camp" : "winter camp"; } }

        public Camp(Vector3 center, CampKind kind, SiteReport report, Transform parent)
        {
            Id = nextId++;
            Center = center;
            Kind = kind;
            Report = report;
            Name = CampSites.MakeName(report, kind);

            // The camp's stockpile: bundles, baskets and hides piled together.
            Pile = new GameObject("Stockpile " + Name);
            Pile.transform.SetParent(parent, false);
            Pile.transform.position = center;
            Visuals.Part(PrimitiveType.Cube, Pile.transform, new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.5f, 0.6f), new Color(0.45f, 0.33f, 0.22f));
            Visuals.Part(PrimitiveType.Cube, Pile.transform, new Vector3(0.6f, 0.2f, 0.3f), new Vector3(0.6f, 0.4f, 0.5f), new Color(0.5f, 0.4f, 0.28f));
            Visuals.Part(PrimitiveType.Cylinder, Pile.transform, new Vector3(-0.5f, 0.25f, 0.2f), new Vector3(0.5f, 0.25f, 0.5f), new Color(0.55f, 0.45f, 0.3f));
        }

        public float StockTotal(bool includeWood)
        {
            float t = 0f;
            for (int i = 0; i < Stock.Length; i++)
                if (includeWood || i != (int)Res.Wood) t += Stock[i];
            return t;
        }
    }

    /// <summary>What the elders see when they look over a possible camp site.</summary>
    public class SiteReport
    {
        public CampKind Kind;
        public float Water, Berries, Wood, Flint, Game, Shelter, Flat, WolfPenalty, Total;
        public float WaterDist, WolfDist;
        public int BerryCount, TreeCount, FlintCount, HerdCount, ShelterTrees;
        public string Weakness;
        public bool OnLand = true;

        /// <summary>Winter camps among trees are out of the wind.</summary>
        public bool Sheltered { get { return ShelterTrees >= 8; } }
        public bool Acceptable { get { return OnLand && Total >= CampSites.AcceptScore; } }

        public string Verdict
        {
            get
            {
                if (!OnLand) return "That's water.";
                if (Total >= 0.65f) return "The elders like this place.";
                if (Acceptable) return "The elders would accept this place.";
                return "The elders would refuse: " + Weakness + ".";
            }
        }

        public string Details()
        {
            return "Water " + (WaterDist > 150f ? "far" : Mathf.RoundToInt(WaterDist) + " m") +
                   "   Berry bushes " + BerryCount + "   Trees " + TreeCount +
                   "\nFlint " + FlintCount + "   Herds nearby " + HerdCount +
                   "   Wolves " + (WolfDist > 600f ? "far" : Mathf.RoundToInt(WolfDist) + " m") +
                   (Kind == CampKind.Winter ? "\nShelter from wind: " + (Sheltered ? "yes" : "no (" + ShelterTrees + " trees close by)") : "");
        }
    }

    public static class CampSites
    {
        public const float AcceptScore = 0.42f;
        public const float MinMoveDistance = 120f;
        public const float MaxMoveDistance = 450f;

        public static SiteReport Evaluate(Vector3 p, CampKind kind)
        {
            var w = World.Instance;
            var r = new SiteReport { Kind = kind };
            if (!w.IsLand(p, 0.5f) || !w.InBounds(p))
            {
                r.OnLand = false;
                r.Weakness = "it's water";
                return r;
            }

            // Distance to water: rings outward until one touches water.
            r.WaterDist = 999f;
            for (float rad = 0f; rad <= 160f && r.WaterDist > 900f; rad += 8f)
            {
                int steps = rad <= 0f ? 1 : 12;
                for (int i = 0; i < steps; i++)
                {
                    float a = i / (float)steps * Mathf.PI * 2f;
                    if (!w.IsLand(p + new Vector3(Mathf.Cos(a) * rad, 0f, Mathf.Sin(a) * rad), 0f)) { r.WaterDist = rad; break; }
                }
            }

            foreach (var n in ResourceNode.All)
            {
                float d = (n.transform.position - p).sqrMagnitude;
                switch (n.Type)
                {
                    case ResourceType.BerryBush: if (d < 90f * 90f) r.BerryCount++; break;
                    case ResourceType.Tree:
                        if (d < 60f * 60f) r.TreeCount++;
                        if (d < 30f * 30f) r.ShelterTrees++;
                        break;
                    case ResourceType.FlintRock: if (d < 150f * 150f) r.FlintCount++; break;
                }
            }

            r.WolfDist = 999f;
            if (AnimalManager.Instance != null)
            {
                foreach (var g in AnimalManager.Instance.Groups)
                {
                    if (g.AliveCount == 0) continue;
                    float d = Vector3.Distance(g.Species == Species.Wolf ? g.Home : g.Center, p);
                    if (g.Species == Species.Wolf) r.WolfDist = Mathf.Min(r.WolfDist, d);
                    else if (d < 250f) r.HerdCount++;
                }
            }

            r.Water = r.WaterDist <= 40f ? 1f : Mathf.Clamp01(1f - (r.WaterDist - 40f) / 110f);
            r.Berries = Mathf.Clamp01(r.BerryCount / 18f);
            r.Wood = Mathf.Clamp01(r.TreeCount / 35f);
            r.Flint = Mathf.Clamp01(r.FlintCount / 3f);
            r.Game = Mathf.Clamp01(r.HerdCount / 3f);
            r.Shelter = Mathf.Clamp01(r.ShelterTrees / 10f);
            float slope = w.Slope(p);
            r.Flat = slope < 0.25f ? 1f : Mathf.Clamp01(1f - (slope - 0.25f) * 4f);
            r.WolfPenalty = r.WolfDist < 150f ? 1f : r.WolfDist < 300f ? (300f - r.WolfDist) / 150f : 0f;

            // Weights: what matters depends on the season the camp is for.
            float[] score, weight;
            string[] lack;
            if (kind == CampKind.Warm)
            {
                score = new[] { r.Water, r.Berries, r.Game, r.Flint, r.Wood, r.Flat };
                weight = new[] { 0.3f, 0.3f, 0.15f, 0.1f, 0.1f, 0.05f };
                lack = new[] { "too far from water", "too few berry bushes", "too little game nearby", "no flint nearby", "too little wood", "the ground is too uneven" };
            }
            else
            {
                score = new[] { r.Wood, r.Shelter, r.Game, r.Flint, r.Water, r.Flat };
                weight = new[] { 0.2f, 0.25f, 0.2f, 0.1f, 0.15f, 0.1f };
                lack = new[] { "too little firewood", "too exposed to the wind", "too little game for winter", "no flint nearby", "too far from water", "the ground is too uneven" };
            }
            float total = 0f, worst = -1f;
            for (int i = 0; i < score.Length; i++)
            {
                total += score[i] * weight[i];
                float missing = (1f - score[i]) * weight[i];
                if (missing > worst) { worst = missing; r.Weakness = lack[i]; }
            }
            total -= 0.35f * r.WolfPenalty;
            if (r.WolfPenalty > 0.5f && 0.35f * r.WolfPenalty > worst) r.Weakness = "wolves den close by";
            r.Total = Mathf.Clamp01(total);
            return r;
        }

        /// <summary>The elders' own pick: the best of a number of places within walking distance.</summary>
        public static Vector3 FindBest(Vector3 from, CampKind kind, IList<Camp> avoid, int samples = 50)
        {
            var w = World.Instance;
            Vector3 best = from;
            float bestScore = -1f;
            for (int i = 0; i < samples; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                float d = Random.Range(MinMoveDistance + 30f, 380f);
                Vector3 p = w.OnGround(from + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d));
                if (!w.InBounds(p) || !w.IsLand(p, 0.6f) || w.Slope(p) > 0.4f) continue;
                bool nearOther = false;
                foreach (var c in avoid) if (Vector3.Distance(c.Center, p) < 100f) nearOther = true;
                if (nearOther) continue;
                var r = Evaluate(p, kind);
                if (r.Total > bestScore) { bestScore = r.Total; best = p; }
            }
            return best;
        }

        static readonly string[] Waterside = { "Reed Bank", "Otter Bend", "Willow Shore", "Heron Water", "Alder Ford" };
        static readonly string[] Berry = { "Bramble Field", "Berry Hollow", "Sloe Meadow" };
        static readonly string[] Open = { "Open Meadow", "Lark Rise", "Grass Hill" };
        static readonly string[] Wooded = { "Pine Hollow", "Deep Wood", "Oak Shelter", "Elder Grove", "Yew Dell" };
        static readonly string[] Exposed = { "Windy Ridge", "Stone Hill", "Bare Knoll" };
        static readonly HashSet<string> used = new HashSet<string>();

        public static string MakeName(SiteReport r, CampKind kind)
        {
            string[] pool;
            if (kind == CampKind.Winter) pool = r != null && r.Sheltered ? Wooded : Exposed;
            else if (r != null && r.WaterDist <= 50f) pool = Waterside;
            else if (r != null && r.BerryCount >= 10) pool = Berry;
            else pool = Open;
            for (int i = 0; i < 10; i++)
            {
                string n = pool[Random.Range(0, pool.Length)];
                if (used.Add(n)) return n;
            }
            string fallback = pool[0] + " " + (used.Count + 1);
            used.Add(fallback);
            return fallback;
        }

        public static void ResetNames() { used.Clear(); }
    }
}
