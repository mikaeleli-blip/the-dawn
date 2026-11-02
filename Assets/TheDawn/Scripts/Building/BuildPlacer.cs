using UnityEngine;

namespace TheDawn
{
    /// <summary>
    /// Free (gridless) placement of building plans, and marking the next camp site.
    /// R rotates, left click places, right click/Esc cancels.
    /// </summary>
    public class BuildPlacer : MonoBehaviour
    {
        public static BuildPlacer Instance { get; private set; }

        public BuildingDef Placing { get; private set; }
        public bool MarkingSite { get; private set; }
        public SiteReport HoverReport { get; private set; }
        public bool Active { get { return Placing != null || MarkingSite; } }
        public string Reason { get; private set; }

        GameObject ghost;
        float yaw;
        bool valid;
        float evalTimer;
        Vector3 lastEval = new Vector3(9999f, 0f, 0f);
        Transform buildingsRoot;

        void Awake()
        {
            Instance = this;
            buildingsRoot = new GameObject("Buildings").transform;
        }

        public static bool CanBuild(BuildingDef def, out string why)
        {
            why = null;
            if (def.Requires != KnowledgeId.None && KnowledgeSystem.Instance.Effect(def.Requires) <= 0f)
            {
                why = "Needs knowledge: " + KnowledgeSystem.Def(def.Requires).Name;
                return false;
            }
            if (def.Type != BuildingType.Campfire && !Tribe.Instance.HasCamp)
            {
                why = "Found a camp first (place a Campfire)";
                return false;
            }
            return true;
        }

        public void Begin(BuildingDef def)
        {
            Cancel();
            Placing = def;
            ghost = new GameObject("Ghost");
            Building.BuildVisual(def, ghost.transform);
            Selection.Instance.Clear();
        }

        public void BeginMarkSite()
        {
            Cancel();
            MarkingSite = true;
            ghost = new GameObject("Ghost Marker");
            Visuals.Part(PrimitiveType.Cylinder, ghost.transform, new Vector3(0, 1.5f, 0), new Vector3(0.12f, 1.5f, 0.12f), Color.white);
            Visuals.Part(PrimitiveType.Cylinder, ghost.transform, new Vector3(0, 0.05f, 0), new Vector3(60f, 0.02f, 60f), Color.white); // ~30 m shelter radius
            Selection.Instance.Clear();
        }

        public void Cancel()
        {
            Placing = null;
            MarkingSite = false;
            HoverReport = null;
            if (ghost != null) Destroy(ghost);
            ghost = null;
        }

        public Building CreatePlan(BuildingDef def, Vector3 p, float rotation)
        {
            var b = Building.Create(def, p, rotation, buildingsRoot);
            RoleAssigner.Instance.Reassign();
            return b;
        }

        void Update()
        {
            if (!Active) return;
            if (InputShim.Down(GameKey.Escape) || InputShim.MouseDown(1)) { Cancel(); return; }
            if (InputShim.Down(GameKey.R)) yaw += 45f;

            var cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(InputShim.MousePosition);
            RaycastHit hit;
            if (!World.Instance.TerrainCollider.Raycast(ray, out hit, 2000f))
            {
                ghost.SetActive(false);
                return;
            }
            ghost.SetActive(true);
            Vector3 p = hit.point;
            ghost.transform.position = p;
            ghost.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (MarkingSite) { UpdateMarking(p); return; }

            string why;
            valid = IsValidSpot(Placing, p, out why);
            Reason = why;
            Visuals.SetColor(ghost, valid ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.85f, 0.3f, 0.25f));

            if (valid && InputShim.MouseDown(0) && !GameHUD.PointerOverUI)
            {
                CreatePlan(Placing, p, yaw);
                EventLog.Add("Plan placed: " + Placing.Name + " (" + Placing.CostText() + ").");
                if (Placing.Type == BuildingType.Campfire && !Tribe.Instance.HasCamp) Cancel();
            }
        }

        void UpdateMarking(Vector3 p)
        {
            var tribe = Tribe.Instance;
            var kind = tribe.HasCamp ? (tribe.CurrentCamp.Kind == CampKind.Warm ? CampKind.Winter : CampKind.Warm) : CampKind.Warm;

            // Evaluating a site is a bit costly, so only re-check when the mouse has moved a few metres.
            evalTimer -= Time.unscaledDeltaTime;
            if (evalTimer <= 0f && (p - lastEval).sqrMagnitude > 9f)
            {
                evalTimer = 0.2f;
                lastEval = p;
                var existing = tribe.CampAt(p);
                HoverReport = existing != null ? existing.Report : CampSites.Evaluate(p, kind);
                Reason = existing != null ? (existing == tribe.CurrentCamp ? "This is the current camp." : "Return to " + existing.Name + ".") : null;
            }
            bool ok = HoverReport != null && HoverReport.Acceptable;
            Visuals.SetColor(ghost, ok ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.85f, 0.45f, 0.25f));

            if (InputShim.MouseDown(0) && !GameHUD.PointerOverUI)
            {
                var existing = tribe.CampAt(p);
                tribe.MarkSite(existing != null ? existing.Center : p);
                Cancel();
            }
        }

        /// <summary>The placement rules for a building plan.</summary>
        public static bool IsValidSpot(BuildingDef def, Vector3 p, out string why)
        {
            why = null;
            var w = World.Instance;
            if (!w.IsLand(p, 0.4f)) { why = "Too close to water"; return false; }
            if (w.Slope(p) > 0.45f) { why = "Too steep"; return false; }
            if (!w.InBounds(p)) { why = "Out of bounds"; return false; }
            foreach (var b in Building.All)
            {
                float min = b.Def.Radius + def.Radius + 0.4f;
                if ((b.transform.position - p).sqrMagnitude < min * min) { why = "Overlaps another building"; return false; }
            }
            foreach (var c in Tribe.Instance.Camps)
            {
                float min = def.Radius + 1.2f;
                if ((c.Center - p).sqrMagnitude < min * min) { why = "On top of the stockpile"; return false; }
            }
            foreach (var n in ResourceNode.All)
            {
                if (n.Type != ResourceType.Tree && n.Type != ResourceType.FlintRock) continue;
                if (!n.Available) continue;
                float min = def.Radius + 0.8f;
                if ((n.transform.position - p).sqrMagnitude < min * min) { why = "Blocked by a " + (n.Type == ResourceType.Tree ? "tree" : "rock"); return false; }
            }
            if (Tribe.Instance.HasCamp && Vector3.Distance(p, Tribe.Instance.CampPosition) > 90f) { why = "Too far from camp"; return false; }
            return true;
        }

        /// <summary>Finds the nearest valid spot in rings around a point.</summary>
        public static bool TryFindSpot(BuildingDef def, Vector3 around, float ring, out Vector3 spot)
        {
            string why;
            for (float r = ring; r < ring + 40f; r += 2f)
            {
                for (int i = 0; i < 16; i++)
                {
                    float a = (i / 16f + r * 0.13f) * Mathf.PI * 2f;
                    Vector3 p = World.Instance.OnGround(around + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
                    if (IsValidSpot(def, p, out why)) { spot = p; return true; }
                }
            }
            spot = around;
            return false;
        }
    }
}
