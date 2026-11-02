using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    public class Biome
    {
        public string Name;
        public float TemperatureOffset;
        public int BerryBushes;
        public int Trees;
        public int FlintRocks;
        public int DeerHerds;
        public int BoarGroups;
        public int WolfPacks;

        public static Biome TemperateForest()
        {
            return new Biome
            {
                Name = "Temperate Forest",
                TemperatureOffset = 0f,
                BerryBushes = 70,
                Trees = 280,
                FlintRocks = 26,
                DeerHerds = 4,
                BoarGroups = 2,
                WolfPacks = 2
            };
        }
    }

    /// <summary>Procedural home region: heightmap terrain, water, resources.</summary>
    public class World : MonoBehaviour
    {
        public static World Instance { get; private set; }

        public float Size = 240f;
        public float CellSize = 2f;
        public float WaterLevel = 2f;
        public Biome Biome;

        int res;
        float[,] heights;
        Texture2D colorMap;
        Material terrainMat;
        public Vector3 StartPoint { get; private set; }

        public float HalfSize { get { return Size * 0.5f; } }

        void Awake() { Instance = this; }

        public void Generate(int seed, Biome biome)
        {
            Biome = biome;
            Random.InitState(seed);
            res = Mathf.RoundToInt(Size / CellSize) + 1;
            heights = new float[res, res];

            float ox = Random.Range(0f, 1000f), oz = Random.Range(0f, 1000f);
            Vector2 lake = new Vector2(Random.Range(-50f, 50f), Random.Range(-50f, 50f));
            float lakeR = Random.Range(18f, 28f);

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float wx = x * CellSize - HalfSize, wz = z * CellSize - HalfSize;
                    float h = 0f, amp = 1f, freq = 0.012f, norm = 0f;
                    for (int o = 0; o < 4; o++)
                    {
                        h += Mathf.PerlinNoise(ox + wx * freq, oz + wz * freq) * amp;
                        norm += amp;
                        amp *= 0.5f;
                        freq *= 2.1f;
                    }
                    h /= norm;
                    float height = 3f + h * 11f;

                    // Hills toward the edges frame the valley.
                    float edge = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz)) / HalfSize;
                    height += Mathf.SmoothStep(0f, 1f, (edge - 0.7f) / 0.3f) * 14f;

                    // A lake.
                    float d = Vector2.Distance(new Vector2(wx, wz), lake);
                    if (d < lakeR * 1.6f)
                        height -= Mathf.SmoothStep(0f, 1f, 1f - d / (lakeR * 1.6f)) * 7f;

                    heights[x, z] = height;
                }
            }

            BuildMesh();
            BuildWater();
            RefreshColors(Season.Spring);
            SpawnResources();

            // Start near the centre on dry, flat-ish ground.
            StartPoint = FindStartPoint();
        }

        Vector3 FindStartPoint()
        {
            Vector3 best = Vector3.zero;
            float bestScore = float.MaxValue;
            for (int i = 0; i < 300; i++)
            {
                Vector3 p = new Vector3(Random.Range(-40f, 40f), 0f, Random.Range(-40f, 40f));
                float h = HeightAt(p);
                if (h < WaterLevel + 1f) continue;
                float slope = Slope(p);
                float score = slope * 10f + p.magnitude * 0.05f;
                if (score < bestScore) { bestScore = score; best = p; }
            }
            best.y = HeightAt(best);
            return best;
        }

        void BuildMesh()
        {
            var verts = new Vector3[res * res];
            var uvs = new Vector2[res * res];
            for (int z = 0; z < res; z++)
                for (int x = 0; x < res; x++)
                {
                    int i = z * res + x;
                    verts[i] = new Vector3(x * CellSize - HalfSize, heights[x, z], z * CellSize - HalfSize);
                    uvs[i] = new Vector2((float)x / (res - 1), (float)z / (res - 1));
                }
            var tris = new int[(res - 1) * (res - 1) * 6];
            int t = 0;
            for (int z = 0; z < res - 1; z++)
                for (int x = 0; x < res - 1; x++)
                {
                    int i = z * res + x;
                    tris[t++] = i; tris[t++] = i + res; tris[t++] = i + 1;
                    tris[t++] = i + 1; tris[t++] = i + res; tris[t++] = i + res + 1;
                }
            var mesh = new Mesh();
            mesh.name = "Terrain";
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var go = new GameObject("Terrain");
            go.transform.SetParent(transform, false);
            go.layer = 0;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            terrainMat = new Material(Visuals.LitShader);
            if (terrainMat.HasProperty("_Glossiness")) terrainMat.SetFloat("_Glossiness", 0f);
            if (terrainMat.HasProperty("_Smoothness")) terrainMat.SetFloat("_Smoothness", 0f);
            go.AddComponent<MeshRenderer>().sharedMaterial = terrainMat;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            TerrainCollider = go.GetComponent<MeshCollider>();
        }

        public Collider TerrainCollider { get; private set; }

        void BuildWater()
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Plane);
            w.name = "Water";
            Destroy(w.GetComponent<Collider>());
            w.transform.SetParent(transform, false);
            w.transform.position = new Vector3(0f, WaterLevel, 0f);
            w.transform.localScale = new Vector3(Size / 10f, 1f, Size / 10f);
            var m = new Material(Visuals.LitShader);
            m.color = new Color(0.16f, 0.3f, 0.38f);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", m.color);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.85f);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.85f);
            w.GetComponent<Renderer>().sharedMaterial = m;
        }

        public void RefreshColors(Season season)
        {
            const int texRes = 128;
            if (colorMap == null)
            {
                colorMap = new Texture2D(texRes, texRes, TextureFormat.RGB24, false);
                colorMap.wrapMode = TextureWrapMode.Clamp;
                colorMap.filterMode = FilterMode.Bilinear;
            }
            Color grass;
            switch (season)
            {
                case Season.Spring: grass = new Color(0.33f, 0.47f, 0.22f); break;
                case Season.Summer: grass = new Color(0.36f, 0.45f, 0.2f); break;
                case Season.Autumn: grass = new Color(0.5f, 0.43f, 0.22f); break;
                default: grass = new Color(0.86f, 0.88f, 0.92f); break;
            }
            Color sand = new Color(0.62f, 0.56f, 0.42f);
            Color mud = new Color(0.3f, 0.26f, 0.2f);
            Color rock = new Color(0.42f, 0.41f, 0.4f);
            var px = new Color[texRes * texRes];
            for (int z = 0; z < texRes; z++)
                for (int x = 0; x < texRes; x++)
                {
                    Vector3 p = new Vector3((x + 0.5f) / texRes * Size - HalfSize, 0f, (z + 0.5f) / texRes * Size - HalfSize);
                    float h = HeightAt(p);
                    float slope = Slope(p);
                    float n = Mathf.PerlinNoise(p.x * 0.08f, p.z * 0.08f);
                    Color c = Color.Lerp(grass, grass * 0.8f, n);
                    if (h < WaterLevel + 0.2f) c = mud;
                    else if (h < WaterLevel + 0.9f) c = season == Season.Winter ? Color.Lerp(sand, grass, 0.6f) : sand;
                    if (slope > 0.55f) c = Color.Lerp(c, rock, Mathf.Clamp01((slope - 0.55f) * 3f));
                    px[z * texRes + x] = c;
                }
            colorMap.SetPixels(px);
            colorMap.Apply();
            if (terrainMat != null)
            {
                terrainMat.mainTexture = colorMap;
                if (terrainMat.HasProperty("_BaseMap")) terrainMat.SetTexture("_BaseMap", colorMap);
            }
        }

        void SpawnResources()
        {
            var parent = new GameObject("Resources").transform;
            parent.SetParent(transform, false);

            float fx = Random.Range(0f, 500f), fz = Random.Range(0f, 500f);
            int placed = 0, tries = 0;
            while (placed < Biome.Trees && tries < Biome.Trees * 20)
            {
                tries++;
                Vector3 p = RandomPoint(0.95f);
                if (!IsLand(p, 0.6f) || Slope(p) > 0.7f) continue;
                float forest = Mathf.PerlinNoise(fx + p.x * 0.02f, fz + p.z * 0.02f);
                if (Random.value > forest * forest * 1.8f) continue;
                ResourceNode.Create(ResourceType.Tree, p, parent);
                placed++;
            }

            placed = 0; tries = 0;
            while (placed < Biome.BerryBushes && tries < 5000)
            {
                tries++;
                Vector3 p = RandomPoint(0.85f);
                if (!IsLand(p, 0.5f) || Slope(p) > 0.5f) continue;
                ResourceNode.Create(ResourceType.BerryBush, p, parent);
                placed++;
            }

            placed = 0; tries = 0;
            while (placed < Biome.FlintRocks && tries < 5000)
            {
                tries++;
                Vector3 p = RandomPoint(0.9f);
                if (!IsLand(p, 0.3f)) continue;
                // Flint shows up on slopes and near water.
                if (Slope(p) < 0.25f && HeightAt(p) > WaterLevel + 2f && Random.value > 0.25f) continue;
                ResourceNode.Create(ResourceType.FlintRock, p, parent);
                placed++;
            }
        }

        public Vector3 RandomPoint(float extent)
        {
            float e = HalfSize * extent;
            var p = new Vector3(Random.Range(-e, e), 0f, Random.Range(-e, e));
            p.y = HeightAt(p);
            return p;
        }

        public Vector3 RandomLandPointNear(Vector3 center, float radius)
        {
            for (int i = 0; i < 30; i++)
            {
                Vector2 r = Random.insideUnitCircle * radius;
                var p = new Vector3(center.x + r.x, 0f, center.z + r.y);
                if (IsLand(p, 0.3f) && InBounds(p)) { p.y = HeightAt(p); return p; }
            }
            center.y = HeightAt(center);
            return center;
        }

        public bool InBounds(Vector3 p)
        {
            return Mathf.Abs(p.x) < HalfSize - 2f && Mathf.Abs(p.z) < HalfSize - 2f;
        }

        public Vector3 ClampToBounds(Vector3 p)
        {
            float e = HalfSize - 3f;
            p.x = Mathf.Clamp(p.x, -e, e);
            p.z = Mathf.Clamp(p.z, -e, e);
            return p;
        }

        public bool IsLand(Vector3 p, float margin = 0f)
        {
            return HeightAt(p) > WaterLevel + margin;
        }

        public float HeightAt(Vector3 p)
        {
            if (heights == null) return 0f;
            float fx = (p.x + HalfSize) / CellSize;
            float fz = (p.z + HalfSize) / CellSize;
            fx = Mathf.Clamp(fx, 0f, res - 1.001f);
            fz = Mathf.Clamp(fz, 0f, res - 1.001f);
            int x0 = Mathf.FloorToInt(fx), z0 = Mathf.FloorToInt(fz);
            float tx = fx - x0, tz = fz - z0;
            // Match the mesh triangulation so units sit exactly on the surface.
            float h00 = heights[x0, z0], h10 = heights[x0 + 1, z0], h01 = heights[x0, z0 + 1], h11 = heights[x0 + 1, z0 + 1];
            if (tx + tz <= 1f) return h00 + (h10 - h00) * tx + (h01 - h00) * tz;
            return h11 + (h01 - h11) * (1f - tx) + (h10 - h11) * (1f - tz);
        }

        /// <summary>Rise over run, roughly. 0 = flat, 1 = 45°.</summary>
        public float Slope(Vector3 p)
        {
            float d = CellSize;
            float hx = HeightAt(p + new Vector3(d, 0, 0)) - HeightAt(p - new Vector3(d, 0, 0));
            float hz = HeightAt(p + new Vector3(0, 0, d)) - HeightAt(p - new Vector3(0, 0, d));
            return new Vector2(hx, hz).magnitude / (2f * d);
        }

        public Vector3 OnGround(Vector3 p)
        {
            p.y = HeightAt(p);
            return p;
        }
    }
}
