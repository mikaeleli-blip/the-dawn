// Functional headless stand-in for the parts of UnityEngine the game uses.
// Runs the real game scripts under Mono: object lifecycle, hierarchy, math, noise, randomness.
using System;
using System.Collections.Generic;
using System.Reflection;

namespace UnityEngine
{
    // =====================================================================
    // Engine core: object registry, lifecycle, frame loop
    // =====================================================================
    public static class Engine
    {
        public static readonly List<Component> AllComponents = new List<Component>();
        public static readonly List<GameObject> AllObjects = new List<GameObject>();
        static readonly List<MonoBehaviour> pendingStart = new List<MonoBehaviour>();
        static readonly List<MonoBehaviour> behaviours = new List<MonoBehaviour>();
        static readonly List<Object> pendingDestroy = new List<Object>();
        public static readonly Dictionary<string, int> Exceptions = new Dictionary<string, int>();
        public static readonly List<string> ExceptionSamples = new List<string>();
        public static Action<string> LogSink;

        class TypeInfo { public MethodInfo Awake, OnEnable, OnDisable, Start, Update, LateUpdate; }
        static readonly Dictionary<Type, TypeInfo> infos = new Dictionary<Type, TypeInfo>();

        static TypeInfo Info(Type t)
        {
            TypeInfo i;
            if (infos.TryGetValue(t, out i)) return i;
            const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            i = new TypeInfo
            {
                Awake = t.GetMethod("Awake", F, null, Type.EmptyTypes, null),
                OnEnable = t.GetMethod("OnEnable", F, null, Type.EmptyTypes, null),
                OnDisable = t.GetMethod("OnDisable", F, null, Type.EmptyTypes, null),
                Start = t.GetMethod("Start", F, null, Type.EmptyTypes, null),
                Update = t.GetMethod("Update", F, null, Type.EmptyTypes, null),
                LateUpdate = t.GetMethod("LateUpdate", F, null, Type.EmptyTypes, null),
            };
            infos[t] = i;
            return i;
        }

        public static void Call(MethodInfo m, object target, string what)
        {
            if (m == null) return;
            try { m.Invoke(target, null); }
            catch (TargetInvocationException e)
            {
                var inner = e.InnerException ?? e;
                string key = inner.GetType().Name + ": " + inner.Message + " @ " + FirstFrame(inner.StackTrace);
                int n;
                Exceptions.TryGetValue(key, out n);
                Exceptions[key] = n + 1;
                if (n == 0) ExceptionSamples.Add(what + " -> " + inner);
            }
        }

        static string FirstFrame(string st)
        {
            if (st == null) return "?";
            foreach (var line in st.Split('\n'))
                if (line.Contains("TheDawn")) return line.Trim();
            return st.Split('\n')[0].Trim();
        }

        internal static void Register(Component c)
        {
            AllComponents.Add(c);
            var mb = c as MonoBehaviour;
            if (mb != null)
            {
                var info = Info(c.GetType());
                Call(info.Awake, c, c.GetType().Name + ".Awake");
                if (c.gameObject.activeInHierarchy && mb.enabled) { mb.enabledCalled = true; Call(info.OnEnable, c, c.GetType().Name + ".OnEnable"); }
                pendingStart.Add(mb);
                behaviours.Add(mb);
            }
        }

        internal static void SetActiveChanged(GameObject go, bool nowActive)
        {
            foreach (var c in go.components)
            {
                var mb = c as MonoBehaviour;
                if (mb == null || !mb.enabled) continue;
                var info = Info(c.GetType());
                if (nowActive && !mb.enabledCalled) { mb.enabledCalled = true; Call(info.OnEnable, c, "OnEnable"); }
                else if (!nowActive && mb.enabledCalled) { mb.enabledCalled = false; Call(info.OnDisable, c, "OnDisable"); }
            }
            foreach (var ch in go.transform.children)
                if (ch.gameObject.activeSelf) SetActiveChanged(ch.gameObject, nowActive);
        }

        internal static void QueueDestroy(Object o)
        {
            if (o == null || o.destroyQueued) return;
            o.destroyQueued = true;
            pendingDestroy.Add(o);
        }

        public static void Frame(float realDt)
        {
            Time.unscaledDeltaTime = realDt;
            Time.deltaTime = Math.Min(realDt * Time.timeScale, 0.3333f);
            Time.time += Time.deltaTime;

            // Start
            if (pendingStart.Count > 0)
            {
                var starting = pendingStart.ToArray();
                pendingStart.Clear();
                foreach (var mb in starting)
                    if (!mb.destroyed) Call(Info(mb.GetType()).Start, mb, mb.GetType().Name + ".Start");
            }

            var list = behaviours.ToArray();
            foreach (var mb in list)
            {
                if (mb.destroyed || !mb.enabled || !mb.gameObject.activeInHierarchy) continue;
                var info = Info(mb.GetType());
                if (info.Update != null) Call(info.Update, mb, mb.GetType().Name + ".Update");
            }
            foreach (var mb in list)
            {
                if (mb.destroyed || !mb.enabled || !mb.gameObject.activeInHierarchy) continue;
                var info = Info(mb.GetType());
                if (info.LateUpdate != null) Call(info.LateUpdate, mb, mb.GetType().Name + ".LateUpdate");
            }
            ProcessDestroys();
        }

        static void ProcessDestroys()
        {
            while (pendingDestroy.Count > 0)
            {
                var batch = pendingDestroy.ToArray();
                pendingDestroy.Clear();
                foreach (var o in batch)
                {
                    var go = o as GameObject;
                    if (go != null) DestroyGO(go);
                    else
                    {
                        var c = o as Component;
                        if (c != null) DestroyComponent(c);
                    }
                }
            }
            behaviours.RemoveAll(b => b.destroyed);
        }

        static void DestroyGO(GameObject go)
        {
            if (go.destroyed) return;
            foreach (var ch in go.transform.children.ToArray()) DestroyGO(ch.gameObject);
            foreach (var c in go.components.ToArray()) DestroyComponent(c);
            go.destroyed = true;
            if (go.transform.parent != null) go.transform.parent.children.Remove(go.transform);
            AllObjects.Remove(go);
        }

        static void DestroyComponent(Component c)
        {
            if (c.destroyed) return;
            var mb = c as MonoBehaviour;
            if (mb != null && mb.enabledCalled)
            {
                mb.enabledCalled = false;
                Call(Info(c.GetType()).OnDisable, c, c.GetType().Name + ".OnDisable");
            }
            c.destroyed = true;
            c.gameObject.components.Remove(c);
            AllComponents.Remove(c);
            if (c is Camera) Camera.cameras.Remove((Camera)c);
        }
    }

    // =====================================================================
    // Objects
    // =====================================================================
    public class Object
    {
        public string name;
        internal bool destroyed;
        internal bool destroyQueued;

        public static bool operator ==(Object a, Object b)
        {
            bool an = ReferenceEquals(a, null) || a.destroyed;
            bool bn = ReferenceEquals(b, null) || b.destroyed;
            if (an || bn) return an && bn;
            return ReferenceEquals(a, b);
        }
        public static bool operator !=(Object a, Object b) { return !(a == b); }
        public static implicit operator bool(Object o) { return o != null; }
        public override bool Equals(object o) { return ReferenceEquals(this, o); }
        public override int GetHashCode() { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this); }

        public static void Destroy(Object o) { Engine.QueueDestroy(o); }

        public static T FindFirstObjectByType<T>() where T : Object
        {
            foreach (var c in Engine.AllComponents) if (!c.destroyed && c is T) return c as T;
            return null;
        }
        public static T[] FindObjectsByType<T>(FindObjectsSortMode m) where T : Object
        {
            var r = new List<T>();
            foreach (var c in Engine.AllComponents) if (!c.destroyed && c is T) r.Add(c as T);
            return r.ToArray();
        }
    }
    public enum FindObjectsSortMode { None, InstanceID }

    public class GameObject : Object
    {
        internal readonly List<Component> components = new List<Component>();
        public Transform transform;
        public int layer;
        public string tag = "Untagged";
        bool active = true;

        public GameObject() : this("GameObject") { }
        public GameObject(string n)
        {
            name = n;
            transform = new Transform();
            transform.gameObject = this;
            transform.transform = transform;
            components.Add(transform);
            Engine.AllObjects.Add(this);
        }

        public bool activeSelf { get { return active; } }
        public bool activeInHierarchy
        {
            get
            {
                if (!active) return false;
                var p = transform.parent;
                while (p != null) { if (!p.gameObject.active) return false; p = p.parent; }
                return true;
            }
        }

        public void SetActive(bool b)
        {
            if (active == b) return;
            bool before = activeInHierarchy;
            active = b;
            bool after = activeInHierarchy;
            if (before != after) Engine.SetActiveChanged(this, after);
        }

        public T AddComponent<T>() where T : Component
        {
            var c = (T)Activator.CreateInstance(typeof(T), true);
            c.gameObject = this;
            c.transform = transform;
            components.Add(c);
            if (c is Camera) Camera.cameras.Add(c as Camera);
            Engine.Register(c);
            return c;
        }

        public T GetComponent<T>()
        {
            foreach (var c in components) if (!c.destroyed && c is T) return (T)(object)c;
            return default(T);
        }

        public T[] GetComponentsInChildren<T>() { return transform.GetComponentsInChildren<T>(); }

        public static GameObject CreatePrimitive(PrimitiveType t)
        {
            var go = new GameObject(t.ToString());
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
            switch (t)
            {
                case PrimitiveType.Sphere: go.AddComponent<SphereCollider>(); break;
                case PrimitiveType.Capsule: go.AddComponent<CapsuleCollider>(); break;
                case PrimitiveType.Cylinder: go.AddComponent<CapsuleCollider>(); break;
                case PrimitiveType.Cube: go.AddComponent<BoxCollider>(); break;
                default: go.AddComponent<MeshCollider>(); break;
            }
            return go;
        }

        public static GameObject Find(string n)
        {
            foreach (var g in Engine.AllObjects) if (!g.destroyed && g.name == n && g.activeInHierarchy) return g;
            return null;
        }
    }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }

    public class Component : Object
    {
        public Transform transform;
        public GameObject gameObject;
        public T GetComponent<T>() { return gameObject.GetComponent<T>(); }
        public T GetComponentInParent<T>()
        {
            var t = transform;
            while (t != null) { var c = t.gameObject.GetComponent<T>(); if (c != null) return c; t = t.parent; }
            return default(T);
        }
        public T[] GetComponentsInChildren<T>() { return transform.GetComponentsInChildren<T>(); }
        public new string name { get { return gameObject.name; } set { gameObject.name = value; } }
    }

    public class Behaviour : Component { public bool enabled = true; }
    public class MonoBehaviour : Behaviour { internal bool enabledCalled; }

    public class Transform : Component
    {
        internal Transform parent;
        internal readonly List<Transform> children = new List<Transform>();
        Vector3 localPos;
        Quaternion localRot = Quaternion.identity;
        Vector3 localScl = Vector3.one;

        public Transform Parent { get { return parent; } }

        public void SetParent(Transform p, bool worldPositionStays)
        {
            Vector3 wp = position; Quaternion wr = rotation;
            if (parent != null) parent.children.Remove(this);
            parent = p;
            if (p != null) p.children.Add(this);
            if (worldPositionStays) { position = wp; rotation = wr; }
        }

        Vector3 LossyScale
        {
            get
            {
                Vector3 s = localScl;
                var t = parent;
                while (t != null) { s = Vector3.Scale(s, t.localScl); t = t.parent; }
                return s;
            }
        }

        public Vector3 position
        {
            get
            {
                if (parent == null) return localPos;
                return parent.position + parent.rotation * Vector3.Scale(parent.LossyScale, localPos);
            }
            set
            {
                if (parent == null) { localPos = value; return; }
                Vector3 rel = Quaternion.Inverse(parent.rotation) * (value - parent.position);
                Vector3 s = parent.LossyScale;
                localPos = new Vector3(s.x != 0 ? rel.x / s.x : 0, s.y != 0 ? rel.y / s.y : 0, s.z != 0 ? rel.z / s.z : 0);
            }
        }
        public Quaternion rotation
        {
            get { return parent == null ? localRot : parent.rotation * localRot; }
            set { localRot = parent == null ? value : Quaternion.Inverse(parent.rotation) * value; }
        }
        public Vector3 localPosition { get { return localPos; } set { localPos = value; } }
        public Quaternion localRotation { get { return localRot; } set { localRot = value; } }
        public Vector3 localScale { get { return localScl; } set { localScl = value; } }
        public Vector3 forward { get { return rotation * Vector3.forward; } }

        public new T[] GetComponentsInChildren<T>()
        {
            var r = new List<T>();
            Collect(this, r);
            return r.ToArray();
        }
        static void Collect<T>(Transform t, List<T> r)
        {
            foreach (var c in t.gameObject.components) if (!c.destroyed && c is T) r.Add((T)(object)c);
            foreach (var ch in t.children) Collect(ch, r);
        }
    }

    public class Collider : Component { public bool Raycast(Ray r, out RaycastHit h, float d) { h = new RaycastHit(); return false; } }
    public class CapsuleCollider : Collider { public Vector3 center; public float height, radius; }
    public class SphereCollider : Collider { }
    public class BoxCollider : Collider { public Vector3 center, size; }
    public class MeshCollider : Collider { public Mesh sharedMesh; }
    public class Renderer : Component { public Material sharedMaterial; public bool enabled = true; }
    public class MeshRenderer : Renderer { }
    public class MeshFilter : Component { public Mesh sharedMesh; }
    public class Mesh : Object
    {
        public Vector3[] vertices; public Vector2[] uv; public int[] triangles;
        public void RecalculateNormals() { } public void RecalculateBounds() { }
    }
    public class Shader : Object { public static Shader Find(string n) { return new Shader { name = n }; } }
    public class Texture : Object { public TextureWrapMode wrapMode; public FilterMode filterMode; }
    public class Texture2D : Texture
    {
        public static Texture2D whiteTexture = new Texture2D(1, 1);
        public Texture2D(int w, int h) { } public Texture2D(int w, int h, TextureFormat f, bool m) { }
        public void SetPixel(int x, int y, Color c) { } public void SetPixels(Color[] c) { } public void Apply() { }
    }
    public enum TextureFormat { RGB24, RGBA32 }
    public enum TextureWrapMode { Repeat, Clamp }
    public enum FilterMode { Point, Bilinear }
    public class Material : Object
    {
        public Material(Shader s) { }
        public Color color; public Texture mainTexture; public bool enableInstancing;
        public bool HasProperty(string n) { return n == "_Glossiness" || n == "_EmissionColor"; }
        public void SetColor(string n, Color c) { } public void SetFloat(string n, float f) { } public void SetTexture(string n, Texture t) { }
        public void EnableKeyword(string k) { }
    }
    public class Light : Behaviour { public LightType type; public float intensity, range; public Color color; public LightShadows shadows; }
    public enum LightType { Spot, Directional, Point }
    public enum LightShadows { None, Hard, Soft }
    public class Camera : Behaviour
    {
        internal static readonly List<Camera> cameras = new List<Camera>();
        public static Camera main
        {
            get { foreach (var c in cameras) if (!c.destroyed && c.gameObject.tag == "MainCamera") return c; return null; }
        }
        public float nearClipPlane, farClipPlane; public Color backgroundColor; public CameraClearFlags clearFlags;
        public Ray ScreenPointToRay(Vector3 p) { return new Ray(); }
    }
    public enum CameraClearFlags { Skybox, SolidColor }
    public class AudioListener : Behaviour { }
    public enum FogMode { Linear, Exponential }
    public static class RenderSettings
    {
        public static Color ambientLight, fogColor; public static bool fog; public static FogMode fogMode;
        public static float fogStartDistance, fogEndDistance; public static Material skybox;
        public static Rendering.AmbientMode ambientMode;
    }
    public static class QualitySettings { public static float shadowDistance; }
    public struct Ray { }
    public struct RaycastHit { public Vector3 point; public Collider collider; }
    public static class Physics { public static bool Raycast(Ray r, out RaycastHit h, float d) { h = new RaycastHit(); return false; } }
    public static class Time { public static float deltaTime, unscaledDeltaTime, timeScale = 1f, time; }
    public static class Screen { public static int width = 1920, height = 1080; }
    public static class Debug
    {
        public static void Log(object o) { if (Engine.LogSink != null) Engine.LogSink(o == null ? "null" : o.ToString()); }
    }

    // =====================================================================
    // Randomness and noise
    // =====================================================================
    public static class Random
    {
        static System.Random rng = new System.Random(1);
        public static void InitState(int s) { rng = new System.Random(s); }
        public static float value { get { return (float)rng.NextDouble(); } }
        public static float Range(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }
        public static int Range(int a, int b) { return b <= a ? a : rng.Next(a, b); }
        public static Vector2 insideUnitCircle
        {
            get
            {
                float ang = value * Mathf.PI * 2f, r = Mathf.Sqrt(value);
                return new Vector2(Mathf.Cos(ang) * r, Mathf.Sin(ang) * r);
            }
        }
        public static Vector3 onUnitSphere
        {
            get
            {
                Vector3 v;
                do { v = new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f)); } while (v.sqrMagnitude > 1f || v.sqrMagnitude < 0.0001f);
                return v.normalized;
            }
        }
        public static Vector3 insideUnitSphere { get { return onUnitSphere * Mathf.Pow(value, 1f / 3f); } }
    }

    public static class Mathf
    {
        public const float PI = (float)Math.PI;
        public static float Clamp(float v, float a, float b) { return v < a ? a : v > b ? b : v; }
        public static int Clamp(int v, int a, int b) { return v < a ? a : v > b ? b : v; }
        public static float Clamp01(float v) { return Clamp(v, 0f, 1f); }
        public static float Lerp(float a, float b, float t) { return a + (b - a) * Clamp01(t); }
        public static float Min(float a, float b) { return a < b ? a : b; }
        public static float Max(float a, float b) { return a > b ? a : b; }
        public static int Min(int a, int b) { return a < b ? a : b; }
        public static int Max(int a, int b) { return a > b ? a : b; }
        public static float Abs(float a) { return Math.Abs(a); }
        public static float Sin(float a) { return (float)Math.Sin(a); }
        public static float Cos(float a) { return (float)Math.Cos(a); }
        public static float Sqrt(float f) { return (float)Math.Sqrt(f); }
        public static float Floor(float f) { return (float)Math.Floor(f); }
        public static float Pow(float a, float b) { return (float)Math.Pow(a, b); }
        public static int FloorToInt(float f) { return (int)Math.Floor(f); }
        public static int RoundToInt(float f) { return (int)Math.Round(f); }
        public static float SmoothStep(float from, float to, float t)
        {
            t = Clamp01(t);
            t = -2f * t * t * t + 3f * t * t;
            return to * t + from * (1f - t);
        }
        public static float MoveTowards(float a, float b, float d)
        {
            if (Math.Abs(b - a) <= d) return b;
            return a + Math.Sign(b - a) * d;
        }

        // Classic improved Perlin noise, remapped to ~0..1 like Unity's Mathf.PerlinNoise.
        static readonly int[] p = BuildPerm();
        static int[] BuildPerm()
        {
            var perm = new int[256];
            for (int i = 0; i < 256; i++) perm[i] = i;
            var r = new System.Random(1337);
            for (int i = 255; i > 0; i--) { int j = r.Next(i + 1); int t = perm[i]; perm[i] = perm[j]; perm[j] = t; }
            var pp = new int[512];
            for (int i = 0; i < 512; i++) pp[i] = perm[i & 255];
            return pp;
        }
        static float Fade(float t) { return t * t * t * (t * (t * 6 - 15) + 10); }
        static float Grad(int h, float x, float y)
        {
            switch (h & 7)
            {
                case 0: return x + y; case 1: return -x + y; case 2: return x - y; case 3: return -x - y;
                case 4: return x; case 5: return -x; case 6: return y; default: return -y;
            }
        }
        public static float PerlinNoise(float x, float y)
        {
            int xi = (int)Math.Floor(x) & 255, yi = (int)Math.Floor(y) & 255;
            float xf = x - (float)Math.Floor(x), yf = y - (float)Math.Floor(y);
            float u = Fade(xf), v = Fade(yf);
            int aa = p[p[xi] + yi], ab = p[p[xi] + yi + 1], ba = p[p[xi + 1] + yi], bb = p[p[xi + 1] + yi + 1];
            float x1 = Lerp(Grad(aa, xf, yf), Grad(ba, xf - 1, yf), u);
            float x2 = Lerp(Grad(ab, xf, yf - 1), Grad(bb, xf - 1, yf - 1), u);
            float n = x1 + (x2 - x1) * v;
            return Clamp01(n * 0.5f + 0.5f);
        }
    }

    // =====================================================================
    // Math types
    // =====================================================================
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float magnitude { get { return Mathf.Sqrt(x * x + y * y); } }
        public static Vector2 zero { get { return new Vector2(0, 0); } }
        public static float Distance(Vector2 a, Vector2 b) { return (a - b).magnitude; }
        public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator *(Vector2 a, float b) { return new Vector2(a.x * b, a.y * b); }
        public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); }
        public static implicit operator Vector3(Vector2 v) { return new Vector3(v.x, v.y, 0); }
    }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public float sqrMagnitude { get { return x * x + y * y + z * z; } }
        public float magnitude { get { return Mathf.Sqrt(sqrMagnitude); } }
        public Vector3 normalized { get { float m = magnitude; return m > 1e-5f ? this / m : zero; } }
        public static Vector3 zero { get { return new Vector3(0, 0, 0); } }
        public static Vector3 one { get { return new Vector3(1, 1, 1); } }
        public static Vector3 up { get { return new Vector3(0, 1, 0); } }
        public static Vector3 forward { get { return new Vector3(0, 0, 1); } }
        public static Vector3 right { get { return new Vector3(1, 0, 0); } }
        public static float Distance(Vector3 a, Vector3 b) { return (a - b).magnitude; }
        public static Vector3 Scale(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
        public static float Dot(Vector3 a, Vector3 b) { return a.x * b.x + a.y * b.y + a.z * b.z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator -(Vector3 a) { return new Vector3(-a.x, -a.y, -a.z); }
        public static Vector3 operator *(Vector3 a, float b) { return new Vector3(a.x * b, a.y * b, a.z * b); }
        public static Vector3 operator *(float b, Vector3 a) { return a * b; }
        public static Vector3 operator /(Vector3 a, float b) { return new Vector3(a.x / b, a.y / b, a.z / b); }
        public override string ToString() { return "(" + x.ToString("0.0") + ", " + y.ToString("0.0") + ", " + z.ToString("0.0") + ")"; }
    }

    public struct Quaternion
    {
        public float x, y, z, w;
        public Quaternion(float x, float y, float z, float w) { this.x = x; this.y = y; this.z = z; this.w = w; }
        public static Quaternion identity { get { return new Quaternion(0, 0, 0, 1); } }

        static Quaternion Axis(Vector3 a, float deg)
        {
            float r = deg * Mathf.PI / 180f * 0.5f, s = Mathf.Sin(r);
            return new Quaternion(a.x * s, a.y * s, a.z * s, Mathf.Cos(r));
        }
        // Unity applies Z, then X, then Y.
        public static Quaternion Euler(float x, float y, float z)
        {
            return Axis(Vector3.up, y) * Axis(Vector3.right, x) * Axis(Vector3.forward, z);
        }
        public static Quaternion Inverse(Quaternion q) { return new Quaternion(-q.x, -q.y, -q.z, q.w); }
        public static Quaternion operator *(Quaternion a, Quaternion b)
        {
            return new Quaternion(
                a.w * b.x + a.x * b.w + a.y * b.z - a.z * b.y,
                a.w * b.y + a.y * b.w + a.z * b.x - a.x * b.z,
                a.w * b.z + a.z * b.w + a.x * b.y - a.y * b.x,
                a.w * b.w - a.x * b.x - a.y * b.y - a.z * b.z);
        }
        public static Vector3 operator *(Quaternion q, Vector3 v)
        {
            Vector3 u = new Vector3(q.x, q.y, q.z);
            Vector3 t = Vector3.Cross(u, v) * 2f;
            return v + t * q.w + Vector3.Cross(u, t);
        }
        public static Quaternion LookRotation(Vector3 f)
        {
            f = f.normalized;
            if (f.sqrMagnitude < 1e-6f) return identity;
            float yaw = (float)Math.Atan2(f.x, f.z) * 180f / Mathf.PI;
            float pitch = -(float)Math.Asin(Mathf.Clamp(f.y, -1f, 1f)) * 180f / Mathf.PI;
            return Euler(pitch, yaw, 0f);
        }
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t)
        {
            t = Mathf.Clamp01(t);
            float dot = a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
            if (dot < 0) { b = new Quaternion(-b.x, -b.y, -b.z, -b.w); }
            var q = new Quaternion(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t, a.w + (b.w - a.w) * t);
            float m = Mathf.Sqrt(q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);
            return new Quaternion(q.x / m, q.y / m, q.z / m, q.w / m);
        }
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) { this.r = r; this.g = g; this.b = b; a = 1; }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color gray { get { return new Color(0.5f, 0.5f, 0.5f); } }
        public static Color white { get { return new Color(1, 1, 1); } }
        public static Color Lerp(Color x, Color y, float t)
        {
            t = Mathf.Clamp01(t);
            return new Color(x.r + (y.r - x.r) * t, x.g + (y.g - x.g) * t, x.b + (y.b - x.b) * t, x.a + (y.a - x.a) * t);
        }
        public static Color operator *(Color c, float f) { return new Color(c.r * f, c.g * f, c.b * f, c.a * f); }
    }

    // =====================================================================
    // Compile-only UI / input (not exercised headless)
    // =====================================================================
    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; }
        public bool Contains(Vector2 p) { return p.x >= x && p.x < x + width && p.y >= y && p.y < y + height; }
    }
    public struct Matrix4x4 { public static Matrix4x4 Scale(Vector3 v) { return new Matrix4x4(); } }
    public class RectOffset { public RectOffset(int a, int b, int c, int d) { } }
    public enum FontStyle { Normal, Bold }
    public class GUIStyleState { public Texture2D background; public Color textColor; }
    public class GUIStyle
    {
        public GUIStyle() { normal = new GUIStyleState(); } public GUIStyle(GUIStyle o) { normal = new GUIStyleState(); }
        public RectOffset padding; public GUIStyleState normal; public int fontSize; public bool richText, wordWrap; public FontStyle fontStyle;
    }
    public class GUISkin { public GUIStyle box = new GUIStyle(), label = new GUIStyle(), button = new GUIStyle(); }
    public class GUILayoutOption { }
    public enum EventType { Layout, Repaint, MouseDown, MouseDrag }
    public class Event { public static Event current; public EventType type; public int button; public Vector2 mousePosition; public void Use() { } }
    public static class GUI
    {
        public static GUISkin skin = new GUISkin(); public static bool enabled; public static Matrix4x4 matrix; public static Color color;
        public static void DrawTexture(Rect r, Texture t) { }
        public static void Box(Rect r, string s, GUIStyle st) { } public static void Label(Rect r, string s, GUIStyle st) { }
    }
    public static class GUILayout
    {
        public static void BeginArea(Rect r, GUIStyle s) { } public static void EndArea() { }
        public static void BeginHorizontal() { } public static void EndHorizontal() { }
        public static Vector2 BeginScrollView(Vector2 v) { return v; } public static void EndScrollView() { }
        public static void Label(string s, GUIStyle st, params GUILayoutOption[] o) { }
        public static bool Button(string s, params GUILayoutOption[] o) { return false; }
        public static void Space(float f) { } public static void FlexibleSpace() { }
        public static GUILayoutOption Width(float w) { return null; } public static GUILayoutOption Height(float h) { return null; }
    }
    public enum KeyCode { W, A, S, D, UpArrow, DownArrow, LeftArrow, RightArrow, Q, E, R, F, Escape, Space, Alpha1, Alpha2, Alpha3, Alpha4 }
    public static class Input
    {
        public static bool GetKey(KeyCode k) { return false; } public static bool GetKeyDown(KeyCode k) { return false; }
        public static Vector3 mousePosition; public static Vector2 mouseScrollDelta;
        public static bool GetMouseButtonDown(int b) { return false; } public static bool GetMouseButton(int b) { return false; }
    }
    public class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }
    public class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad }
}
namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox, Trilight, Flat }
    public class RenderPipelineAsset : UnityEngine.Object { }
    public static class GraphicsSettings { public static RenderPipelineAsset currentRenderPipeline; }
}
