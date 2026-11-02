using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    public enum ResourceType { BerryBush, Tree, FlintRock }

    public class ResourceNode : MonoBehaviour
    {
        public static readonly List<ResourceNode> All = new List<ResourceNode>();

        public ResourceType Type;
        public float Amount;
        public float Max;
        public Person ReservedBy;
        int depletedDays;

        GameObject berries;
        GameObject visual;

        public bool Available { get { return Amount >= 1f && visual.activeSelf; } }

        public static ResourceNode Create(ResourceType type, Vector3 pos, Transform parent)
        {
            var go = new GameObject(type.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var n = go.AddComponent<ResourceNode>();
            n.Type = type;
            n.BuildVisual();
            return n;
        }

        void BuildVisual()
        {
            visual = new GameObject("Visual");
            visual.transform.SetParent(transform, false);
            float s = Random.Range(0.8f, 1.25f);
            switch (Type)
            {
                case ResourceType.Tree:
                    Max = Amount = 10f;
                    Visuals.Part(PrimitiveType.Cylinder, visual.transform, new Vector3(0, 1.6f * s, 0), new Vector3(0.35f, 1.6f, 0.35f) * s, new Color(0.3f, 0.22f, 0.15f));
                    Color leaf = Random.value < 0.4f ? new Color(0.13f, 0.27f, 0.15f) : new Color(0.2f, 0.33f, 0.14f);
                    Visuals.Part(PrimitiveType.Sphere, visual.transform, new Vector3(0, 3.8f * s, 0), new Vector3(2.4f, 2.8f, 2.4f) * s, leaf);
                    if (Random.value < 0.5f)
                        Visuals.Part(PrimitiveType.Sphere, visual.transform, new Vector3(0.5f, 3.2f * s, 0.3f), new Vector3(1.6f, 1.8f, 1.6f) * s, leaf * 0.9f);
                    break;
                case ResourceType.BerryBush:
                    Max = 8f;
                    Amount = Random.Range(3f, 8f);
                    Visuals.Part(PrimitiveType.Sphere, visual.transform, new Vector3(0, 0.45f, 0), new Vector3(1.3f, 0.9f, 1.3f) * s, new Color(0.18f, 0.32f, 0.14f));
                    berries = new GameObject("Berries");
                    berries.transform.SetParent(visual.transform, false);
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 bp = Random.onUnitSphere * 0.55f * s;
                        bp.y = Mathf.Abs(bp.y) * 0.6f + 0.45f;
                        Visuals.Part(PrimitiveType.Sphere, berries.transform, bp, Vector3.one * 0.18f, new Color(0.6f, 0.08f, 0.15f));
                    }
                    break;
                case ResourceType.FlintRock:
                    Max = Amount = 15f;
                    Visuals.Part(PrimitiveType.Sphere, visual.transform, new Vector3(0, 0.3f, 0), new Vector3(1.6f, 0.8f, 1.2f) * s, new Color(0.38f, 0.37f, 0.4f));
                    Visuals.Part(PrimitiveType.Sphere, visual.transform, new Vector3(0.6f, 0.2f, 0.4f), new Vector3(0.8f, 0.5f, 0.7f) * s, new Color(0.25f, 0.25f, 0.3f));
                    break;
            }
            RefreshVisual();
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Take up to 'want' units. Returns what was taken.</summary>
        public float Take(float want)
        {
            float got = Mathf.Min(want, Amount);
            Amount -= got;
            if (Amount < 1f && Type == ResourceType.Tree)
            {
                visual.SetActive(false);
                depletedDays = 0;
            }
            RefreshVisual();
            return got;
        }

        public void DailyTick(Season season)
        {
            switch (Type)
            {
                case ResourceType.BerryBush:
                    float regrow = season == Season.Summer ? 2f : season == Season.Autumn ? 1.6f : season == Season.Spring ? 0.6f : 0f; // autumn: nuts and late fruit
                    if (OmenSystem.Instance != null && OmenSystem.Instance.IsActive(OmenType.BountifulEarth)) regrow *= 2f;
                    Amount = Mathf.Min(Max, Amount + regrow);
                    // Winter frost ruins what is left on the bush.
                    if (season == Season.Winter) Amount = Mathf.Max(0f, Amount - 2f);
                    break;
                case ResourceType.Tree:
                    if (!visual.activeSelf)
                    {
                        depletedDays++;
                        if (depletedDays > 30) { visual.SetActive(true); Amount = Max; }
                    }
                    break;
                case ResourceType.FlintRock:
                    break;
            }
            RefreshVisual();
        }

        void RefreshVisual()
        {
            if (berries != null) berries.SetActive(Amount >= 1f);
        }
    }
}
