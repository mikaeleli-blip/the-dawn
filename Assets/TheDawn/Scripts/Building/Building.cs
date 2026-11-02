using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    public enum BuildingType { Campfire, LeanTo, HideHut, DryingRack, StoragePit, KnappingSpot, SpiritStone }

    public class BuildingDef
    {
        public BuildingType Type;
        public string Name;
        public string Description;
        public float Wood, Flint, Hide;
        public float Work;          // seconds of skilled work at 1x
        public float Radius;        // footprint
        public KnowledgeId Requires = KnowledgeId.None;
        public int ShelterCapacity;
        public float ShelterWarmth;

        public float Cost(Res r)
        {
            switch (r)
            {
                case Res.Wood: return Wood;
                case Res.Flint: return Flint;
                case Res.Hide: return Hide;
                default: return 0f;
            }
        }

        public string CostText()
        {
            var parts = new List<string>();
            if (Wood > 0) parts.Add(Wood + " wood");
            if (Flint > 0) parts.Add(Flint + " flint");
            if (Hide > 0) parts.Add(Hide + " hide");
            return parts.Count == 0 ? "free" : string.Join(", ", parts.ToArray());
        }

        public static readonly BuildingDef[] All =
        {
            new BuildingDef { Type = BuildingType.Campfire, Name = "Campfire", Wood = 5, Work = 8, Radius = 1.2f,
                Description = "Warmth, light and safety. Wolves keep away from a lit fire. Burns wood. The first campfire founds your camp.",
                Requires = KnowledgeId.Firemaking },
            new BuildingDef { Type = BuildingType.LeanTo, Name = "Lean-to", Wood = 8, Work = 18, Radius = 1.6f, ShelterCapacity = 3, ShelterWarmth = 14f,
                Description = "A simple shelter of branches. Sleeps 3." },
            new BuildingDef { Type = BuildingType.HideHut, Name = "Hide Hut", Wood = 10, Hide = 4, Work = 40, Radius = 2.2f, ShelterCapacity = 5, ShelterWarmth = 24f,
                Description = "A warm hut of poles and hides. Sleeps 5.", Requires = KnowledgeId.HideWorking },
            new BuildingDef { Type = BuildingType.DryingRack, Name = "Drying Rack", Wood = 6, Work = 14, Radius = 1.3f,
                Description = "Dries meat and berries so less food spoils.", Requires = KnowledgeId.FoodDrying },
            new BuildingDef { Type = BuildingType.StoragePit, Name = "Storage Pit", Wood = 4, Flint = 4, Work = 25, Radius = 1.4f,
                Description = "A lined pit. Becomes the stockpile and slows spoilage.", Requires = KnowledgeId.StoragePits },
            new BuildingDef { Type = BuildingType.KnappingSpot, Name = "Knapping Spot", Flint = 3, Work = 8, Radius = 1.2f,
                Description = "A place to shape flint and sew hides. Crafters work faster here." },
            new BuildingDef { Type = BuildingType.SpiritStone, Name = "Spirit Stone", Flint = 12, Wood = 4, Work = 35, Radius = 1.5f,
                Description = "A standing stone. The tribe gathers here and faith grows." },
        };

        public static BuildingDef Get(BuildingType t)
        {
            foreach (var d in All) if (d.Type == t) return d;
            return null;
        }
    }

    public class Building : MonoBehaviour
    {
        public static readonly List<Building> All = new List<Building>();
        public const float MaxFuel = 12f;
        public const float FireWarmRadius = 9f;
        public const float FireScareRadius = 16f;

        public BuildingDef Def;
        public bool Built;
        public float Progress;
        public float Fuel;
        public readonly List<Person> Sleepers = new List<Person>();
        readonly float[] delivered = new float[6];

        GameObject visual;
        GameObject flame;
        Light fireLight;

        public bool IsLitFire { get { return Built && Def.Type == BuildingType.Campfire && Fuel > 0f; } }

        public static Building Create(BuildingDef def, Vector3 pos, float yaw, Transform parent)
        {
            var go = new GameObject(def.Name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            var b = go.AddComponent<Building>();
            b.Def = def;
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(def.Radius * 1.8f, 1.5f, def.Radius * 1.8f);
            col.center = new Vector3(0f, 0.75f, 0f);
            b.visual = BuildVisual(def, go.transform);
            b.RefreshLook();
            return b;
        }

        /// <summary>Grey-box model for a building type. Also used for the placement ghost.</summary>
        public static GameObject BuildVisual(BuildingDef def, Transform parent)
        {
            var root = new GameObject("Visual");
            root.transform.SetParent(parent, false);
            Color wood = new Color(0.42f, 0.31f, 0.2f);
            Color hide = new Color(0.62f, 0.5f, 0.36f);
            Color stone = new Color(0.45f, 0.44f, 0.46f);
            switch (def.Type)
            {
                case BuildingType.Campfire:
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i / 7f * Mathf.PI * 2f;
                        Visuals.Part(PrimitiveType.Sphere, root.transform, new Vector3(Mathf.Cos(a), 0.12f, Mathf.Sin(a)), new Vector3(0.4f, 0.28f, 0.4f), stone);
                    }
                    var l1 = Visuals.Part(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.2f, 0), new Vector3(0.12f, 0.5f, 0.12f), wood);
                    l1.transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
                    var l2 = Visuals.Part(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.2f, 0), new Vector3(0.12f, 0.5f, 0.12f), wood);
                    l2.transform.localRotation = Quaternion.Euler(70f, 120f, 0f);
                    var l3 = Visuals.Part(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.2f, 0), new Vector3(0.12f, 0.5f, 0.12f), wood);
                    l3.transform.localRotation = Quaternion.Euler(70f, 240f, 0f);
                    break;
                case BuildingType.LeanTo:
                    var roof = Visuals.Part(PrimitiveType.Cube, root.transform, new Vector3(0, 0.9f, 0.2f), new Vector3(2.8f, 0.12f, 2.2f), wood);
                    roof.transform.localRotation = Quaternion.Euler(-40f, 0f, 0f);
                    Visuals.Part(PrimitiveType.Cylinder, root.transform, new Vector3(-1.3f, 0.8f, -0.6f), new Vector3(0.12f, 0.8f, 0.12f), wood);
                    Visuals.Part(PrimitiveType.Cylinder, root.transform, new Vector3(1.3f, 0.8f, -0.6f), new Vector3(0.12f, 0.8f, 0.12f), wood);
                    break;
                case BuildingType.HideHut:
                    Visuals.Part(PrimitiveType.Sphere, root.transform, new Vector3(0, 0.6f, 0), new Vector3(4f, 3.2f, 4f), hide);
                    Visuals.Part(PrimitiveType.Cylinder, root.transform, new Vector3(0, 2.1f, 0), new Vector3(0.1f, 0.6f, 0.1f), wood);
                    Visuals.Part(PrimitiveType.Cube, root.transform, new Vector3(0, 0.5f, 1.9f), new Vector3(0.8f, 1f, 0.3f), new Color(0.15f, 0.1f, 0.08f));
                    break;
                case BuildingType.DryingRack:
                    Visuals.Part(PrimitiveType.Cylinder, root.transform, new Vector3(-1f, 0.8f, 0), new Vector3(0.1f, 0.8f, 0.1f), wood);
                    Visuals.Part(PrimitiveType.Cylinder, root.transform, new Vector3(1f, 0.8f, 0), new Vector3(0.1f, 0.8f, 0.1f), wood);
                    var bar = Visuals.Part(PrimitiveType.Cylinder, root.transform, new Vector3(0, 1.5f, 0), new Vector3(0.08f, 1.1f, 0.08f), wood);
                    bar.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    for (int i = 0; i < 4; i++)
                        Visuals.Part(PrimitiveType.Cube, root.transform, new Vector3(-0.6f + i * 0.4f, 1.2f, 0), new Vector3(0.18f, 0.5f, 0.06f), new Color(0.5f, 0.2f, 0.18f));
                    break;
                case BuildingType.StoragePit:
                    Visuals.Part(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.05f, 0), new Vector3(2.4f, 0.08f, 2.4f), new Color(0.22f, 0.17f, 0.12f));
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i / 8f * Mathf.PI * 2f;
                        Visuals.Part(PrimitiveType.Sphere, root.transform, new Vector3(Mathf.Cos(a) * 1.3f, 0.12f, Mathf.Sin(a) * 1.3f), new Vector3(0.45f, 0.3f, 0.45f), stone);
                    }
                    break;
                case BuildingType.KnappingSpot:
                    Visuals.Part(PrimitiveType.Cylinder, root.transform, new Vector3(0, 0.25f, 0), new Vector3(0.9f, 0.25f, 0.9f), stone);
                    Visuals.Part(PrimitiveType.Sphere, root.transform, new Vector3(0.9f, 0.1f, 0.4f), new Vector3(0.5f, 0.2f, 0.4f), new Color(0.3f, 0.3f, 0.34f));
                    break;
                case BuildingType.SpiritStone:
                    var st = Visuals.Part(PrimitiveType.Cube, root.transform, new Vector3(0, 1.4f, 0), new Vector3(0.9f, 2.8f, 0.5f), new Color(0.5f, 0.5f, 0.52f));
                    st.transform.localRotation = Quaternion.Euler(3f, 0f, 2f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i / 6f * Mathf.PI * 2f;
                        Visuals.Part(PrimitiveType.Sphere, root.transform, new Vector3(Mathf.Cos(a) * 2f, 0.1f, Mathf.Sin(a) * 2f), new Vector3(0.35f, 0.2f, 0.35f), stone);
                    }
                    break;
            }
            return root;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        // ---------- Construction ----------

        public float Remaining(Res r)
        {
            if (Built) return 0f;
            return Mathf.Max(0f, Def.Cost(r) - delivered[(int)r]);
        }

        public bool MissingMaterial(out Res r)
        {
            foreach (var res in new[] { Res.Wood, Res.Flint, Res.Hide })
                if (Remaining(res) > 0.01f) { r = res; return true; }
            r = Res.Wood;
            return false;
        }

        public bool HasAllMaterials { get { Res r; return !MissingMaterial(out r); } }

        public void Deliver(Res r, float amount)
        {
            delivered[(int)r] += amount;
            float extra = delivered[(int)r] - Def.Cost(r);
            if (extra > 0f) { delivered[(int)r] -= extra; Tribe.Instance.Add(r, extra); }
            RefreshLook();
        }

        /// <summary>Adds work; returns true the moment it completes.</summary>
        public bool AddWork(float seconds)
        {
            if (Built || !HasAllMaterials) return false;
            Progress += seconds;
            if (Progress >= Def.Work)
            {
                Complete();
                return true;
            }
            RefreshLook();
            return false;
        }

        void Complete()
        {
            Built = true;
            Progress = Def.Work;
            if (Def.Type == BuildingType.Campfire)
            {
                Fuel = 4f;
                if (!Tribe.Instance.HasCamp) Tribe.Instance.FoundCamp(transform.position);
            }
            RefreshLook();
            RoleAssigner.Instance.Reassign();
        }

        public void Cancel()
        {
            foreach (var r in new[] { Res.Wood, Res.Flint, Res.Hide })
                if (delivered[(int)r] > 0f) Tribe.Instance.Add(r, delivered[(int)r]);
            Destroy(gameObject);
        }

        void RefreshLook()
        {
            if (visual == null) return;
            if (Built)
            {
                visual.transform.localScale = Vector3.one;
                foreach (var r in visual.GetComponentsInChildren<Renderer>()) r.enabled = true;
            }
            else
            {
                // Plans grow from the ground as work progresses.
                float t = Def.Work > 0f ? Progress / Def.Work : 0f;
                visual.transform.localScale = new Vector3(1f, Mathf.Lerp(0.15f, 1f, t), 1f);
            }
        }

        public Vector3 SleepSpot(Person p)
        {
            int i = Mathf.Max(0, Sleepers.IndexOf(p));
            float a = i * 1.3f;
            Vector3 local = new Vector3(Mathf.Cos(a) * 0.7f, 0f, Mathf.Sin(a) * 0.7f);
            return World.Instance.OnGround(transform.position + transform.rotation * local);
        }

        // ---------- Fire ----------

        public void AddFuel(float wood)
        {
            float before = Fuel;
            Fuel = Mathf.Min(MaxFuel, Fuel + wood);
            float extra = wood - (Fuel - before);
            if (extra > 0f) Tribe.Instance.Add(Res.Wood, extra);
        }

        void Update()
        {
            if (Def.Type != BuildingType.Campfire || !Built) return;
            var clock = GameClock.Instance;
            // One wood lasts ~4 hours; faster in the cold.
            float burn = clock.DeltaHours / 4f * (clock.Temperature < 0f ? 1.4f : 1f);
            bool wasLit = Fuel > 0f;
            Fuel = Mathf.Max(0f, Fuel - burn);
            if (wasLit && Fuel <= 0f) EventLog.Add("A campfire has gone out.", LogKind.Warning);

            if (flame == null)
            {
                flame = new GameObject("Flame");
                flame.transform.SetParent(transform, false);
                var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(core.GetComponent<Collider>());
                core.transform.SetParent(flame.transform, false);
                core.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                core.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
                core.GetComponent<Renderer>().sharedMaterial = Visuals.Emissive(new Color(1f, 0.55f, 0.15f), 3f);
                var lgo = new GameObject("FireLight");
                lgo.transform.SetParent(flame.transform, false);
                lgo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                fireLight = lgo.AddComponent<Light>();
                fireLight.type = LightType.Point;
                fireLight.color = new Color(1f, 0.6f, 0.3f);
                fireLight.range = 14f;
                fireLight.shadows = LightShadows.None;
            }
            bool lit = Fuel > 0f;
            flame.SetActive(lit);
            if (lit)
            {
                float f = Fuel / MaxFuel;
                fireLight.intensity = (1.6f + Mathf.PerlinNoise(Time.time * 4f, 0f) * 0.8f) * Mathf.Lerp(0.5f, 1f, f);
                flame.transform.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.1f, f);
            }
        }

        // ---------- Queries ----------

        public static Building NearestLitFire(Vector3 p, float maxDist)
        {
            Building best = null;
            float bestD = maxDist * maxDist;
            foreach (var b in All)
            {
                if (!b.IsLitFire) continue;
                float d = (b.transform.position - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        public static Building NearestBuilt(BuildingType t, Vector3 p, float maxDist)
        {
            Building best = null;
            float bestD = maxDist * maxDist;
            foreach (var b in All)
            {
                if (!b.Built || b.Def.Type != t) continue;
                float d = (b.transform.position - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        public static bool AnyBuilt(BuildingType t)
        {
            foreach (var b in All) if (b.Built && b.Def.Type == t) return true;
            return false;
        }

        /// <summary>The plan a builder should work on next: one that can progress, oldest first.</summary>
        public static Building NextToBuild(Vector3 from)
        {
            Building best = null;
            float bestScore = float.MaxValue;
            foreach (var b in All)
            {
                if (b.Built) continue;
                Res need;
                bool canProgress = !b.MissingMaterial(out need) || Tribe.Instance.Stock(need) >= 1f;
                if (!canProgress) continue;
                float score = Vector3.Distance(b.transform.position, from) * 0.1f + All.IndexOf(b);
                if (b.Def.Type == BuildingType.Campfire) score -= 50f; // fire first
                if (score < bestScore) { bestScore = score; best = b; }
            }
            return best;
        }
    }
}
