using System;
using UnityEngine;

namespace TheDawn
{
    public enum Season { Spring, Summer, Autumn, Winter }

    /// <summary>Time of day, days, seasons and years. Game speed is Time.timeScale.</summary>
    public class GameClock : MonoBehaviour
    {
        public static GameClock Instance { get; private set; }

        [Tooltip("Real seconds per in-game day at 1x speed.")]
        public float SecondsPerDay = 240f;
        public int DaysPerSeason = 6;

        public float Hour = 7f;          // 0..24
        public int DayOfSeason = 1;      // 1..DaysPerSeason
        public Season Season = Season.Spring;
        public int Year = 1;
        public int TotalDays;

        public event Action OnNewDay;
        public event Action OnNewSeason;
        public event Action OnNewYear;

        Light sun;

        public int DaysPerYear { get { return DaysPerSeason * 4; } }
        /// <summary>In-game hours that passed this frame.</summary>
        public float DeltaHours { get; private set; }
        public bool IsNight { get { return Hour < 6f || Hour >= 21f; } }
        public bool IsEvening { get { return Hour >= 18f && Hour < 21f; } }
        public float SeasonProgress { get { return (DayOfSeason - 1 + Hour / 24f) / DaysPerSeason; } }

        public string ShortStamp
        {
            get { return "Y" + Year + " " + Season + " " + DayOfSeason; }
        }

        public string TimeString
        {
            get
            {
                int h = Mathf.FloorToInt(Hour);
                int m = Mathf.FloorToInt((Hour - h) * 60f);
                return h.ToString("00") + ":" + m.ToString("00");
            }
        }

        void Awake()
        {
            Instance = this;
        }

        public void Init(Light sunLight)
        {
            sun = sunLight;
            UpdateLighting();
        }

        void Update()
        {
            DeltaHours = Time.deltaTime / SecondsPerDay * 24f;
            Hour += DeltaHours;
            if (Hour >= 24f)
            {
                Hour -= 24f;
                AdvanceDay();
            }
            UpdateLighting();
        }

        void AdvanceDay()
        {
            TotalDays++;
            DayOfSeason++;
            bool newSeason = false, newYear = false;
            if (DayOfSeason > DaysPerSeason)
            {
                DayOfSeason = 1;
                newSeason = true;
                if (Season == Season.Winter) { Season = Season.Spring; Year++; newYear = true; }
                else Season = (Season)((int)Season + 1);
            }
            if (OnNewDay != null) OnNewDay();
            if (newSeason)
            {
                EventLog.Add(Season == Season.Winter ? "Winter has come. Keep the fires burning." : Season + " begins.",
                    Season == Season.Winter ? LogKind.Warning : LogKind.Info);
                if (OnNewSeason != null) OnNewSeason();
            }
            if (newYear && OnNewYear != null) OnNewYear();
        }

        /// <summary>Air temperature in °C for the current biome, season and hour.</summary>
        public float Temperature
        {
            get
            {
                float baseT;
                switch (Season)
                {
                    case Season.Spring: baseT = 9f; break;
                    case Season.Summer: baseT = 19f; break;
                    case Season.Autumn: baseT = 7f; break;
                    default: baseT = -9f; break;
                }
                if (World.Instance != null) baseT += World.Instance.Biome.TemperatureOffset;
                // Coldest around 4:00, warmest around 15:00.
                float daily = Mathf.Cos((Hour - 15f) / 24f * Mathf.PI * 2f);
                return baseT + daily * 5f;
            }
        }

        void UpdateLighting()
        {
            if (sun == null) return;
            float t = Hour / 24f;
            float angle = (t - 0.25f) * 360f; // 6:00 = sunrise
            sun.transform.rotation = Quaternion.Euler(angle, 35f, 0f);
            float dayAmount = Mathf.Clamp01(Mathf.Sin((t - 0.25f) * Mathf.PI * 2f) * 2f + 0.2f);
            float seasonLight = Season == Season.Winter ? 0.75f : 1f;
            sun.intensity = Mathf.Lerp(0f, 1.15f, dayAmount) * seasonLight;

            Color dawn = new Color(1f, 0.6f, 0.4f);
            Color noon = Season == Season.Winter ? new Color(0.85f, 0.9f, 1f) : new Color(1f, 0.96f, 0.88f);
            sun.color = Color.Lerp(dawn, noon, dayAmount);

            Color nightAmb = new Color(0.07f, 0.08f, 0.14f);
            Color dayAmb = Season == Season.Winter ? new Color(0.45f, 0.48f, 0.55f) : new Color(0.45f, 0.45f, 0.42f);
            RenderSettings.ambientLight = Color.Lerp(nightAmb, dayAmb, dayAmount);

            Color fogNight = new Color(0.04f, 0.05f, 0.09f);
            Color fogDay = Season == Season.Winter ? new Color(0.72f, 0.76f, 0.82f) : new Color(0.62f, 0.68f, 0.66f);
            RenderSettings.fogColor = Color.Lerp(fogNight, fogDay, dayAmount);
            var cam = Camera.main;
            if (cam != null) cam.backgroundColor = RenderSettings.fogColor;
        }
    }
}
