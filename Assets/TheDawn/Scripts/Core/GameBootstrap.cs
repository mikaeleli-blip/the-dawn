using UnityEngine;

namespace TheDawn
{
    /// <summary>
    /// Builds the whole game at runtime, so pressing Play in any scene (even an empty one) starts
    /// a new game. Add a GameObject named "NoDawnAutoStart" to a scene to stop that.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public int Seed = 0; // 0 = random

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (GameObject.Find("NoDawnAutoStart") != null) return;
            if (Object.FindFirstObjectByType<GameBootstrap>() != null) return;
            new GameObject("The Dawn").AddComponent<GameBootstrap>();
        }

        void Start()
        {
            EventLog.Clear();
            Time.timeScale = 1f;
            int seed = Seed != 0 ? Seed : Random.Range(1, int.MaxValue);

            // Camera and light
            var cam = Camera.main;
            if (cam == null)
            {
                var cgo = new GameObject("Main Camera");
                cgo.tag = "MainCamera";
                cam = cgo.AddComponent<Camera>();
                cgo.AddComponent<AudioListener>();
            }
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                var lgo = new GameObject("Sun");
                sun = lgo.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.skybox = null;

            // Systems (Awake sets each Instance as it's added)
            var clock = gameObject.AddComponent<GameClock>();
            var world = gameObject.AddComponent<World>();
            var knowledge = gameObject.AddComponent<KnowledgeSystem>();
            gameObject.AddComponent<OmenSystem>();
            gameObject.AddComponent<CultureSystem>();
            var tribe = gameObject.AddComponent<Tribe>();
            var roles = gameObject.AddComponent<RoleAssigner>();
            var animals = gameObject.AddComponent<AnimalManager>();
            gameObject.AddComponent<BuildPlacer>();
            gameObject.AddComponent<Selection>();
            gameObject.AddComponent<GameHUD>();

            clock.Init(sun);
            world.Generate(seed, Biome.TemperateForest());
            Random.InitState(seed + 1);
            tribe.Init(world.StartPoint);
            knowledge.Init();
            animals.Init(world.Biome, world.StartPoint);
            roles.Reassign();

            var rig = gameObject.AddComponent<CameraRig>();
            rig.Init(cam, world.StartPoint);

            EventLog.Add("A small band of " + tribe.People.Count + " wanders into an unknown valley. (" + world.Biome.Name + ", seed " + seed + ")");
            EventLog.Add("Nights are cold and the wolves are hungry. The band needs a fire.", LogKind.Warning);
        }
    }
}
