using UnityEngine;

namespace TheDawn
{
    /// <summary>Click to inspect a person, animal or building. Selection is for looking, not commanding.</summary>
    public class Selection : MonoBehaviour
    {
        public static Selection Instance { get; private set; }

        public Component Selected { get; private set; }
        GameObject ring;

        void Awake()
        {
            Instance = this;
            ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "SelectionRing";
            Destroy(ring.GetComponent<Collider>());
            ring.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f);
            ring.GetComponent<Renderer>().sharedMaterial = Visuals.Emissive(new Color(1f, 0.9f, 0.4f), 1.5f);
            ring.SetActive(false);
        }

        public void Select(Component c) { Selected = c; }
        public void Clear() { Selected = null; }

        void Update()
        {
            if (Selected == null) { ring.SetActive(false); }
            else
            {
                ring.SetActive(true);
                float r = 1.4f;
                var b = Selected as Building;
                if (b != null) r = b.Def.Radius * 2.2f;
                ring.transform.localScale = new Vector3(r, 0.02f, r);
                ring.transform.position = Selected.transform.position + Vector3.up * 0.05f;
            }

            if (BuildPlacer.Instance.Active) return;
            if (InputShim.Down(GameKey.Escape)) Clear();
            if (!InputShim.MouseDown(0) || GameHUD.PointerOverUI) return;

            var cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ScreenPointToRay(InputShim.MousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 2000f))
            {
                var p = hit.collider.GetComponentInParent<Person>();
                if (p != null) { Select(p); return; }
                var a = hit.collider.GetComponentInParent<Animal>();
                if (a != null) { Select(a); return; }
                var bld = hit.collider.GetComponentInParent<Building>();
                if (bld != null) { Select(bld); return; }
            }
            Clear();
        }
    }
}
