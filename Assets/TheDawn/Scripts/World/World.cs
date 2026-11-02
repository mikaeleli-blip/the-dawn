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
                BerryBushes = 900,
                Trees = 3500,
                FlintRocks = 220,
                DeerHerds = 14,
                BoarGroups = 7,
                WolfPacks = 4
            };
        }
    }

    /// <summary>Procedural home region (1 km x 1 km): heightmap terrain, a river, lakes, resources.</summary>
    public class World : MonoBehaviour
    {
        public static World Instance { get; private set; }

        public float Size = 1000f;
        public float CellSize = 4f;   // 251x251 vertices stays under the 65k-vertex mesh limit
        public float WaterLevel = 2f;
        public Biome Biome;

        int res;
        float[,] heights;
        Texture2D colorMap;
        Material terrainMat;
        public Texture2D MiniMap { get; private set; }
        public Vector3 StartPoint { get; private set; }
        public Collider TerrainCollider { get; private set; }

        public float HalfSize { get { return Size * 0.5f; } }

        // River centreline: z = riverZ + A1 sin(x f1 + p1) + A2 sin(x f2 + p2)
        float riverZ, rA1, rF1, rP1, rA2, rF2, rP2;

        void Awake() { Instance = this; }

        public void Generate(int seed, Biome biome)
        {
            Biome = biome;
            Random.InitState(seed);
            res = Mathf.RoundToInt(Size / CellSize) + 1;
            heights = new float[res, res];

            float ox = Random.Range(0f, 1000f), oz = Random.Range(0f, 1000f);
            float bx = Random.Range(0f, 1000f), bz = Random.Range(0f, 1000f);

            riverZ = Random.Range(-0.3f, 0.3f) * HalfSize;
            rA1 = Random.Range(50f, 90f); rF1 = Mathf.PI * 2f / Random.Range(600f, 900f); rP1 = Random.Range(0f, 6.28f);
            rA2 = Random.Range(12f, 28f); rF2 = Mathf.PI * 2f / Random.Range(140f, 220f); rP2 = Random.Range(0f, 6.28f);

            var lakes = new List<Vector3>(); // x, z, radius
            for (int attempt = 0; attempt < 200 && lakes.Count < 2; attempt++)
            {
                Vector2 c = new Vector2(Random.Range(-0.65f, 0.65f), Random.Range(-0.65f, 0.65f)) * HalfSize;
                // Keep lakes off the river and apart from each other.
                if (Mathf.Abs(c.y - RiverZAt(c.x)) < 110f) continue;
                bool tooClose = false;
                foreach (var l in lakes) if (Vector2.Distance(c, new Vector2(l.x, l.y)) < 300f) tooClose = true;
                if (tooClose) continue;
                lakes.Add(new Vector3(c.x, c.y, Random.Range(35f, 65f)));
            }

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float wx = x * CellSize - HalfSize, wz = z * CellSize - HalfSize;

                    // Broad hills and valleys...
                    float broad = Mathf.PerlinNoise(bx + wx * 0.0025f, bz + wz * 0.0025f);
                    // ...with smaller rolling detail on top.
                    float h = 0f, amp = 1f, freq = 0.008f, norm = 0f;
                    for (int o = 0; o < 4; o++)
                    {
                        h += Mathf.PerlinNoise(ox + wx * freq, oz + wz * freq) * amp;
                        norm += amp;
                        amp *= 0.5f;
                        freq *= 2.1f;
                    }
                    h /= norm;
                    float height = 3.5f + broad * 16f + h * 9f;

                    // Hills toward the edges frame the region.
                    float edge = Mathf.Max(Mathf.Abs(wx), Mathf.Abs(wz)) / HalfSize;
                    height += Mathf.SmoothStep(0f, 1f, (edge - 0.82f) / 0.18f) * 28f;

                    // Lakes
                    foreach (var l in lakes)
                    {
                        // Noise on the distance gives an irregular shoreline.
                        float d = Vector2.Distance(new Vector2(wx, wz), new Vector2(l.x, l.y));
                        d *= 0.7f + Mathf.PerlinNoise(ox + wx * 0.015f, oz + wz * 0.015f) * 0.6f;
                        // Full depth inside the radius, sloping banks out to 1.6x.
                        if (d < l.z * 1.6f)
                            height = Mathf.Lerp(height, WaterLevel - 4f, Mathf.SmoothStep(0f, 1f, (l.z * 1.6f - d) / (l.z * 0.6f)));
                    }

                    // River: a channel below water level with sloping banks.
                    float rd = RiverDistance(wx, wz);
                    float halfWidth = 6f + Mathf.PerlinNoise(ox + wx * 0.01f, 7.3f) * 6f;
                    if (rd < halfWidth * 3f)
                    {
                        float t = Mathf.Clamp01((rd - halfWidth) / (halfWidth * 2f));
                        height = Mathf.Lerp(WaterLevel - 1.4f, height, Mathf.SmoothStep(0f, 1f, t));
                    }

                    heights[x, z] = height;
                }
            }

            BuildMesh();
            BuildWater();
            RefreshColors(Season.Spring);
            SpawnResources();
            StartPoint = FindStartPoint();
        }

        float RiverZAt(float x)
        {
            return riverZ + rA1 * Mathf.Sin(x * rF1 + rP1) + rA2 * Mathf.Sin(x * rF2 + rP2);
        }

        float RiverDistance(float x, float z)
        {
            float slope = rA1 * rF1 * Mathf.Cos(x * rF1 + rP1) + rA2 * rF2 * Mathf.Cos(x * rF2 + rP2);
            return Mathf.Abs(z - RiverZAt(x)) / Mathf.Sqrt(1f + slope * slope);
        }

        Vector3 FindStartPoint()
        {
            Vector3 best = Vector3.zero;
            float bestScore = float.MaxValue;
            for (int i = 0; i < 500; i++)
            {
                Vector3 p = new Vector3(Random.Range(-140f, 140f), 0f, Random.Range(-140f, 140f));
                float h = HeightAt(p);
                if (h < WaterLevel + 1.2f) continue;
                // Flat ground, not far from the river (fresh water), not right on its bank.
                float rd = RiverDistance(p.x, p.z);
                float waterScore = rd < 25f ? 30f : Mathf.Abs(rd - 60f) * 0.1f;
                float score = Slope(p) * 40f + waterScore + p.magnitude * 0.02f;
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
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            terrainMat = new Material(Visuals.LitShader);
            if (terrainMat.HasProperty("_Glossiness")) terrainMat.SetFloat("_Glossiness", 0f);
            if (terrainMat.HasProperty("_Smoothness")) terrainMat.SetFloat("_Smoothness", 0f);
            go.AddComponent<MeshRenderer>().sharedMaterial = terrainMat;
            TerrainCollider = go.AddComponent<MeshCollider>();
            ((MeshCollider)TerrainCollider).sharedMesh = mesh;
        }

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
            const int texRes = 512;
            if (colorMap == null)
            {
                colorMap = new Texture2D(texRes, texRes, TextureFormat.RGB24, false);
                colorMap.wrapMode = TextureWrapMode.Clamp;
                colorMap.filterMode = FilterMode.Bilinear;
                MiniMap = new Texture2D(texRes, texRes, TextureFormat.RGB24, false);
                MiniMap.wrapMode = TextureWrapMode.Clamp;
                MiniMap.filterMode = FilterMode.Bilinear;
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
            Color water = season == Season.Winter ? new Color(0.55f, 0.65f, 0.72f) : new Color(0.2f, 0.36f, 0.48f);
            var px = new Color[texRes * texRes];
            var mini = new Color[texRes * texRes];
            for (int z = 0; z < texRes; z++)
                for (int x = 0; x < texRes; x++)
                {
                    Vector3 p = new Vector3((x + 0.5f) / texRes * Size - HalfSize, 0f, (z + 0.5f) / texRes * Size - HalfSize);
                    float h = HeightAt(p);
                    float slope = Slope(p);
                    float n = Mathf.PerlinNoise(p.x * 0.05f, p.z * 0.05f) * 0.6f + Mathf.PerlinNoise(p.x * 0.2f, p.z * 0.2f) * 0.4f;
                    Color c = Color.Lerp(grass, grass * 0.78f, n);
                    if (h < WaterLevel + 0.2f) c = mud;
                    else if (h < WaterLevel + 0.9f) c = season == Season.Winter ? Color.Lerp(sand, grass, 0.6f) : sand;
                    if (slope > 0.55f) c = Color.Lerp(c, rock, Mathf.Clamp01((slope - 0.55f) * 3f));
                    int i = z * texRes + x;
                    px[i] = c;
                    mini[i] = h < WaterLevel ? water : c * (0.75f + Mathf.Clamp01((h - WaterLevel) / 40f) * 0.4f);
                }
            colorMap.SetPixels(px);
            colorMap.Apply();
            MiniMap.SetPixels(mini);
            MiniMap.Apply();
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

            // Forests: large patches with smaller clearings inside them.
            float fx = Random.Range(0f, 500f), fz = Random.Range(0f, 500f);
            int placed = 0, tries = 0;
            while (placed < Biome.Trees && tries < Biome.Trees * 25)
            {
                tries++;
                Vector3 p = RandomPoint(0.96f);
                if (!IsLand(p, 0.6f) || Slope(p) > 0.7f) continue;
                float forest = Mathf.PerlinNoise(fx + p.x * 0.006f, fz + p.z * 0.006f) * 0.7f
                             + Mathf.PerlinNoise(fx + p.x * 0.03f, fz + p.z * 0.03f) * 0.3f;
                if (Random.value > forest * forest * 2f) continue;
                ResourceNode.Create(ResourceType.Tree, p, parent);
                placed++;
            }

            // Berry bushes favour forest edges and clearings, spread through the valley.
            placed = 0; tries = 0;
            while (placed < Biome.BerryBushes && tries < Biome.BerryBushes * 20)
            {
                tries++;
                Vector3 p = RandomPoint(0.9f);
                if (!IsLand(p, 0.5f) || Slope(p) > 0.5f) continue;
                float forest = Mathf.PerlinNoise(fx + p.x * 0.006f, fz + p.z * 0.006f);
                if (Random.value > 1f - Mathf.Abs(forest - 0.5f) * 1.4f) continue;
                ResourceNode.Create(ResourceType.BerryBush, p, parent);
                placed++;
            }

            // Flint shows up on slopes and along water.
            placed = 0; tries = 0;
            while (placed < Biome.FlintRocks && tries < Biome.FlintRocks * 40)
            {
                tries++;
                Vector3 p = RandomPoint(0.92f);
                if (!IsLand(p, 0.3f)) continue;
                bool rocky = Slope(p) > 0.25f;
                bool shore = HeightAt(p) < WaterLevel + 1.5f;
                if (!rocky && !shore && Random.value > 0.15f) continue;
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

        /// <summary>World position to 0..1 map coordinates (x right, y up/north).</summary>
        public Vector2 ToMap(Vector3 p)
        {
            return new Vector2((p.x + HalfSize) / Size, (p.z + HalfSize) / Size);
        }

        public Vector3 FromMap(Vector2 uv)
        {
            return OnGround(new Vector3(uv.x * Size - HalfSize, 0f, uv.y * Size - HalfSize));
        }
    }
}
