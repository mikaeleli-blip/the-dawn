using UnityEngine;

namespace TheDawn
{
    /// <summary>Free (gridless) placement of building plans. R rotates, left click places, right click/Esc cancels.</summary>
    public class BuildPlacer : MonoBehaviour
    {
        public static BuildPlacer Instance { get; private set; }

        public BuildingDef Placing { get; private set; }
        GameObject ghost;
        float yaw;
        bool valid;
        Transform buildingsRoot;
        public string Reason { get; private set; }

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

        public void Cancel()
        {
            Placing = null;
            if (ghost != null) Destroy(ghost);
            ghost = null;
        }

        void Update()
        {
            if (Placing == null) return;
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

            valid = Check(p, out string why);
            Reason = why;
            Visuals.SetColor(ghost, valid ? new Color(0.4f, 0.8f, 0.4f) : new Color(0.85f, 0.3f, 0.25f));

            if (valid && InputShim.MouseDown(0) && !GameHUD.PointerOverUI)
            {
                Building.Create(Placing, p, yaw, buildingsRoot);
                EventLog.Add("Plan placed: " + Placing.Name + " (" + Placing.CostText() + ").");
                RoleAssigner.Instance.Reassign();
                if (Placing.Type == BuildingType.Campfire && !Tribe.Instance.HasCamp) Cancel();
            }
        }

        bool Check(Vector3 p, out string why)
        {
            why = null;
            var w = World.Instance;
            if (!w.IsLand(p, 0.4f)) { why = "Too close to water"; return false; }
            if (w.Slope(p) > 0.45f) { why = "Too steep"; return false; }
            if (!w.InBounds(p)) { why = "Out of bounds"; return false; }
            foreach (var b in Building.All)
            {
                float min = b.Def.Radius + Placing.Radius + 0.4f;
                if ((b.transform.position - p).sqrMagnitude < min * min) { why = "Overlaps another building"; return false; }
            }
            foreach (var n in ResourceNode.All)
            {
                if (n.Type != ResourceType.Tree && n.Type != ResourceType.FlintRock) continue;
                if (!n.Available) continue;
                float min = Placing.Radius + 0.8f;
                if ((n.transform.position - p).sqrMagnitude < min * min) { why = "Blocked by a " + (n.Type == ResourceType.Tree ? "tree" : "rock"); return false; }
            }
            if (Tribe.Instance.HasCamp && Vector3.Distance(p, Tribe.Instance.CampPosition) > 90f) { why = "Too far from camp"; return false; }
            return true;
        }
    }
}
