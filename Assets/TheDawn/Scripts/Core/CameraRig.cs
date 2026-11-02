using UnityEngine;

namespace TheDawn
{
    /// <summary>
    /// Free top-down camera. WASD/arrows pan, Q/E rotate, middle mouse drag rotates and tilts,
    /// scroll zooms, F focuses the selection.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public static CameraRig Instance { get; private set; }

        public float Yaw = 30f;
        public float Pitch = 52f;
        public float Distance = 38f;
        public float MinDistance = 8f, MaxDistance = 320f;
        public Vector3 Focus;

        Camera cam;
        Vector2 lastMouse;

        void Awake() { Instance = this; }

        public void Init(Camera c, Vector3 focus)
        {
            cam = c;
            Focus = focus;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            Apply();
        }

        void LateUpdate()
        {
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;

            // Pan
            Vector3 fwd = Quaternion.Euler(0f, Yaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, Yaw, 0f) * Vector3.right;
            Vector3 move = Vector3.zero;
            if (InputShim.Held(GameKey.W) || InputShim.Held(GameKey.Up)) move += fwd;
            if (InputShim.Held(GameKey.S) || InputShim.Held(GameKey.Down)) move -= fwd;
            if (InputShim.Held(GameKey.D) || InputShim.Held(GameKey.Right)) move += right;
            if (InputShim.Held(GameKey.A) || InputShim.Held(GameKey.Left)) move -= right;
            Focus += move.normalized * (12f + Distance * 0.9f) * dt;

            // Rotate
            if (InputShim.Held(GameKey.Q)) Yaw += 90f * dt;
            if (InputShim.Held(GameKey.E)) Yaw -= 90f * dt;
            Vector2 mouse = InputShim.MousePosition;
            if (InputShim.MouseHeld(2))
            {
                Vector2 d = mouse - lastMouse;
                Yaw += d.x * 0.3f;
                Pitch = Mathf.Clamp(Pitch - d.y * 0.2f, 20f, 85f);
            }
            lastMouse = mouse;

            // Zoom
            if (!GameHUD.PointerOverUI)
            {
                float s = InputShim.Scroll;
                if (Mathf.Abs(s) > 0.001f) Distance = Mathf.Clamp(Distance * (1f - s * 0.12f), MinDistance, MaxDistance);
            }

            // Focus selection
            if (InputShim.Down(GameKey.F) && Selection.Instance.Selected != null)
                Focus = Selection.Instance.Selected.transform.position;

            var w = World.Instance;
            if (w != null)
            {
                Focus = w.ClampToBounds(Focus);
                Focus.y = Mathf.Lerp(Focus.y, Mathf.Max(w.HeightAt(Focus), w.WaterLevel), 5f * dt);
            }
            Apply();
        }

        void Apply()
        {
            Quaternion rot = Quaternion.Euler(Pitch, Yaw, 0f);
            cam.transform.position = Focus - rot * Vector3.forward * Distance;
            cam.transform.rotation = rot;
            RenderSettings.fogStartDistance = Distance * 1.2f;
            RenderSettings.fogEndDistance = Distance * 1.2f + 220f;
            // Nothing past the fog is visible, so don't draw it.
            cam.farClipPlane = RenderSettings.fogEndDistance + 40f;
            QualitySettings.shadowDistance = Mathf.Clamp(Distance * 2.5f, 60f, 400f);
        }
    }
}
