using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    /// <summary>
    /// Grey-box debug HUD (IMGUI). Will be replaced by a real UI later; kept simple so it
    /// needs no prefabs or scene setup.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }
        public static bool PointerOverUI { get; private set; }

        enum Panel { None, Build, Camps, Tribe, Knowledge, Omens, Priorities, Culture }
        Panel panel = Panel.Build;

        readonly List<Rect> uiRects = new List<Rect>();
        float scale = 1f;
        Vector2 scrollTribe, scrollKnow, scrollLog, scrollInspect, scrollCamps;
        GUIStyle box, label, small, header, wrap, good, warn, danger;
        bool stylesReady;
        int speedBeforePause = 1;

        void Awake() { Instance = this; }

        void Update()
        {
            // Pointer-over-UI from last frame's rects (screen pixels, top-left origin).
            Vector2 m = InputShim.MousePosition;
            m.y = Screen.height - m.y;
            bool over = false;
            foreach (var r in uiRects) if (r.Contains(m)) { over = true; break; }
            PointerOverUI = over;

            if (InputShim.Down(GameKey.Space)) TogglePause();
            if (InputShim.Down(GameKey.Alpha1)) SetSpeed(1);
            if (InputShim.Down(GameKey.Alpha2)) SetSpeed(2);
            if (InputShim.Down(GameKey.Alpha3)) SetSpeed(4);
            if (InputShim.Down(GameKey.Alpha4)) SetSpeed(8);
        }

        void SetSpeed(int s)
        {
            Time.timeScale = s;
            if (s > 0) speedBeforePause = s;
        }

        void TogglePause()
        {
            if (Time.timeScale > 0f) SetSpeed(0);
            else SetSpeed(speedBeforePause);
        }

        void InitStyles()
        {
            if (stylesReady) return;
            stylesReady = true;
            var bg = new Texture2D(1, 1);
            bg.SetPixel(0, 0, new Color(0.08f, 0.07f, 0.06f, 0.86f));
            bg.Apply();
            box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 6, 6) };
            box.normal.background = bg;
            label = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            label.normal.textColor = new Color(0.92f, 0.88f, 0.8f);
            small = new GUIStyle(label) { fontSize = 11 };
            wrap = new GUIStyle(small) { wordWrap = true };
            header = new GUIStyle(label) { fontSize = 15, fontStyle = FontStyle.Bold };
            header.normal.textColor = new Color(1f, 0.8f, 0.5f);
            good = new GUIStyle(small); good.normal.textColor = new Color(0.6f, 0.9f, 0.5f);
            warn = new GUIStyle(small); warn.normal.textColor = new Color(1f, 0.8f, 0.4f);
            danger = new GUIStyle(small); danger.normal.textColor = new Color(1f, 0.45f, 0.4f);
            GUI.skin.button.richText = true;
        }

        Rect Area(float x, float y, float w, float h)
        {
            var r = new Rect(x, y, w, h);
            uiRects.Add(new Rect(x * scale, y * scale, w * scale, h * scale));
            return r;
        }

        void OnGUI()
        {
            if (Tribe.Instance == null || GameClock.Instance == null) return;
            InitStyles();
            if (Event.current.type == EventType.Layout) uiRects.Clear();
            scale = Mathf.Max(1f, Screen.height / 1080f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float sw = Screen.width / scale, sh = Screen.height / scale;

            DrawTopBar(sw);
            DrawObjective(sw);
            DrawLeft(sh);
            DrawInspector(sw);
            DrawLog(sh);
            DrawMiniMap(sw, sh);
            if (BuildPlacer.Instance.Active) DrawPlacingHint(sw, sh);
        }

        // ------------------------------------------------------------------
        void DrawMiniMap(float sw, float sh)
        {
            var w = World.Instance;
            if (w == null || w.MiniMap == null) return;
            const float size = 220f;
            var outer = Area(sw - size - 16, sh - size - 16, size + 8, size + 8);
            GUI.Box(outer, "", box);
            var r = new Rect(outer.x + 4, outer.y + 4, size, size);
            GUI.DrawTexture(r, w.MiniMap);

            foreach (var camp in Tribe.Instance.Camps)
            {
                bool cur = camp == Tribe.Instance.CurrentCamp;
                Dot(r, camp.Center, cur ? 10f : 8f, cur ? new Color(1f, 0.85f, 0.3f, 0.9f) : new Color(0.7f, 0.7f, 0.7f, 0.8f));
            }
            if (Tribe.Instance.MarkedSite.HasValue) Dot(r, Tribe.Instance.MarkedSite.Value, 8f, new Color(0.4f, 0.9f, 1f));
            foreach (var b in Building.All)
                Dot(r, b.transform.position, b.Built ? 4f : 3f, b.IsLitFire ? new Color(1f, 0.6f, 0.2f) : new Color(0.75f, 0.6f, 0.4f));
            foreach (var a in Animal.All)
                if (a.Alive) Dot(r, a.Position, a.IsPredator ? 4f : 3f, a.IsPredator ? new Color(0.9f, 0.2f, 0.15f) : new Color(0.55f, 0.4f, 0.25f));
            foreach (var p in Tribe.Instance.People)
                Dot(r, p.Position, 3f, new Color(1f, 0.95f, 0.8f));
            var sel = Selection.Instance.Selected;
            if (sel != null) Dot(r, sel.transform.position, 6f, new Color(1f, 0.9f, 0.4f));

            // Camera view marker
            if (CameraRig.Instance != null)
            {
                Vector2 c = MapToGui(r, CameraRig.Instance.Focus);
                float half = Mathf.Clamp(CameraRig.Instance.Distance / w.Size * size, 4f, 40f);
                var col = new Color(1f, 1f, 1f, 0.8f);
                Line(new Rect(c.x - half, c.y - half, half * 2f, 1f), col);
                Line(new Rect(c.x - half, c.y + half, half * 2f, 1f), col);
                Line(new Rect(c.x - half, c.y - half, 1f, half * 2f), col);
                Line(new Rect(c.x + half, c.y - half, 1f, half * 2f + 1f), col);
            }

            // Click or drag on the map to move the camera there.
            Event e = Event.current;
            if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && r.Contains(e.mousePosition))
            {
                Vector2 uv = new Vector2((e.mousePosition.x - r.x) / r.width, 1f - (e.mousePosition.y - r.y) / r.height);
                if (CameraRig.Instance != null) CameraRig.Instance.Focus = w.FromMap(uv);
                e.Use();
            }
        }

        static Vector2 MapToGui(Rect r, Vector3 world)
        {
            Vector2 uv = World.Instance.ToMap(world);
            return new Vector2(r.x + uv.x * r.width, r.y + (1f - uv.y) * r.height);
        }

        static void Dot(Rect r, Vector3 world, float s, Color c)
        {
            Vector2 g = MapToGui(r, world);
            Line(new Rect(g.x - s * 0.5f, g.y - s * 0.5f, s, s), c);
        }

        static void Line(Rect rect, Color c)
        {
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        // ------------------------------------------------------------------
        void DrawTopBar(float sw)
        {
            var t = Tribe.Instance;
            var c = GameClock.Instance;
            GUILayout.BeginArea(Area(0, 0, sw, 34), box);
            GUILayout.BeginHorizontal();
            GUILayout.Label("<b>" + c.Season + "</b> day " + c.DayOfSeason + ", year " + c.Year + "  " + c.TimeString +
                            "  " + Mathf.RoundToInt(c.Temperature) + "°C", label, GUILayout.Width(300));
            string campText = !t.HasCamp ? "No camp" : t.Moving ? "Moving to " + t.CurrentCamp.Name
                : t.CurrentCamp.Name + (t.Settled ? " (home)" : "  (moves in " + t.DaysUntilMove.ToString("0.0") + "d)");
            GUILayout.Label(campText, label, GUILayout.Width(250));
            string sp = Time.timeScale <= 0f ? "Paused" : Time.timeScale + "x";
            if (GUILayout.Button(Time.timeScale <= 0f ? ">" : "||", GUILayout.Width(34))) TogglePause();
            if (GUILayout.Button("1x", GUILayout.Width(34))) SetSpeed(1);
            if (GUILayout.Button("2x", GUILayout.Width(34))) SetSpeed(2);
            if (GUILayout.Button("4x", GUILayout.Width(34))) SetSpeed(4);
            if (GUILayout.Button("8x", GUILayout.Width(34))) SetSpeed(8);
            GUILayout.Label(sp, label, GUILayout.Width(60));
            GUILayout.Space(10);
            GUILayout.Label("People <b>" + t.People.Count + "</b>   Food <b>" + Mathf.FloorToInt(t.Stock(Res.Food)) + "</b> (" + t.FoodDays.ToString("0.0") + "d)" +
                            "   Wood <b>" + Mathf.FloorToInt(t.Stock(Res.Wood)) + "</b>   Flint <b>" + Mathf.FloorToInt(t.Stock(Res.Flint)) + "</b>" +
                            "   Hides <b>" + Mathf.FloorToInt(t.Stock(Res.Hide)) + "</b>   Spears <b>" + Mathf.FloorToInt(t.Stock(Res.Spear)) + "</b>" +
                            "   Cloaks <b>" + Mathf.FloorToInt(t.Stock(Res.Cloak)) + "</b>   Shelter <b>" + t.ShelterSpaces + "/" + t.People.Count + "</b>" +
                            "   Faith <b>" + Mathf.FloorToInt(t.Faith) + "</b>", label);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        void DrawObjective(float sw)
        {
            var t = Tribe.Instance;
            string goal;
            var c = GameClock.Instance;
            var ks = KnowledgeSystem.Instance;
            var stay = KnowledgeSystem.Def(KnowledgeId.Sedentism);
            bool needWinterSite = !t.Settled && c.Season == Season.Summer && !t.MarkedSite.HasValue && !t.Camps.Exists(x => x.Kind == CampKind.Winter);
            if (t.People.Count == 0) goal = "The band is gone.";
            else if (!t.HasCamp) goal = "Goal: make camp. Open Build and place a Campfire.";
            else if (t.Moving) goal = "The band is moving to " + t.CurrentCamp.Name + ".";
            else if (t.ShelterSpaces < t.People.Count) goal = "Goal: shelter everyone at this camp (Lean-tos).";
            else if (needWinterSite) goal = "Goal: mark a winter camp site (Camps panel): sheltered, with wood and game.";
            else if (!t.SurvivedFirstWinter) goal = "Goal: survive the first winter. Store food and wood.";
            else if (t.Settled) goal = "The Wanderers era is complete. Settling & Farming comes next (not in this build yet).";
            else if (t.CanSettle) goal = "Staying Put is known: choose a camp and settle for good (Camps panel).";
            else if (!ks.PrereqsMet(stay)) goal = "Goal: learn Food Drying, Storage Pits and Hide Working, the roots of Staying Put.";
            else goal = "Goal: discover Staying Put (" + Pct(ks.InsightOf(KnowledgeId.Sedentism) / stay.Insight) + "). Returning to known camps helps.";
            var r = Area(sw * 0.5f - 300, 40, 600, 26);
            GUI.Box(r, "", box);
            GUI.Label(new Rect(r.x + 10, r.y + 4, r.width - 20, 20), goal, label);
        }

        void DrawPlacingHint(float sw, float sh)
        {
            var bp = BuildPlacer.Instance;
            if (bp.MarkingSite)
            {
                var mr = Area(sw * 0.5f - 260, sh - 130, 520, 104);
                GUI.Box(mr, "", box);
                var rep = bp.HoverReport;
                string txt = "Marking the next camp site. Left click to mark, right click/Esc to stop.";
                if (!string.IsNullOrEmpty(bp.Reason)) txt += "\n<b>" + bp.Reason + "</b>";
                if (rep != null) txt += "\n" + (rep.Acceptable ? "<color=#99dd77>" : "<color=#ff8866>") + rep.Verdict + "</color>  (score " + Mathf.RoundToInt(rep.Total * 100f) + ")\n" + rep.Details();
                GUI.Label(new Rect(mr.x + 10, mr.y + 4, mr.width - 20, mr.height - 8), txt, wrap);
                return;
            }
            var r = Area(sw * 0.5f - 230, sh - 70, 460, 44);
            GUI.Box(r, "", box);
            string reason = string.IsNullOrEmpty(bp.Reason) ? "" : "  <color=#ff8866>" + bp.Reason + "</color>";
            GUI.Label(new Rect(r.x + 10, r.y + 4, r.width - 20, 36),
                "Placing <b>" + bp.Placing.Name + "</b>. Left click to place, R to rotate, right click/Esc to stop." + reason, wrap);
        }

        // ------------------------------------------------------------------
        void DrawLeft(float sh)
        {
            GUILayout.BeginArea(Area(8, 72, 90, 240), box);
            if (Tab("Build", Panel.Build)) { }
            if (Tab("Camps", Panel.Camps)) { }
            if (Tab("Tribe", Panel.Tribe)) { }
            if (Tab("Knowledge", Panel.Knowledge)) { }
            if (Tab("Omens", Panel.Omens)) { }
            if (Tab("Priorities", Panel.Priorities)) { }
            if (Tab("Culture", Panel.Culture)) { }
            GUILayout.EndArea();

            if (panel == Panel.None) return;
            float h = Mathf.Min(sh - 72 - 150, 560);
            GUILayout.BeginArea(Area(104, 72, 330, h), box);
            switch (panel)
            {
                case Panel.Build: DrawBuild(); break;
                case Panel.Camps: DrawCamps(); break;
                case Panel.Tribe: DrawTribe(); break;
                case Panel.Knowledge: DrawKnowledge(); break;
                case Panel.Omens: DrawOmens(); break;
                case Panel.Priorities: DrawPriorities(); break;
                case Panel.Culture: DrawCulture(); break;
            }
            GUILayout.EndArea();
        }

        bool Tab(string name, Panel p)
        {
            bool on = panel == p;
            if (GUILayout.Button(on ? "<b>" + name + "</b>" : name))
            {
                panel = on ? Panel.None : p;
                return true;
            }
            return false;
        }

        void DrawBuild()
        {
            GUILayout.Label("Build plans", header);
            GUILayout.Label("Place a plan; the tribe builds it when they choose to.", wrap);
            foreach (var d in BuildingDef.All)
            {
                string why;
                bool ok = BuildPlacer.CanBuild(d, out why);
                GUI.enabled = ok;
                if (GUILayout.Button(d.Name + "  (" + d.CostText() + ")")) BuildPlacer.Instance.Begin(d);
                GUI.enabled = true;
                GUILayout.Label(ok ? d.Description : "<color=#cc8866>" + why + "</color>", wrap);
                GUILayout.Space(4);
            }
        }

        void DrawCamps()
        {
            var t = Tribe.Instance;
            GUILayout.Label("Camps", header);
            if (!t.HasCamp)
            {
                GUILayout.Label("The band has no camp yet. Place a Campfire to make the first one.", wrap);
                return;
            }
            scrollCamps = GUILayout.BeginScrollView(scrollCamps);
            var cur = t.CurrentCamp;
            GUILayout.Label("<b>" + cur.Name + "</b>, " + cur.KindName + (t.Settled ? " (home for good)" : ""), label);
            if (cur.Report != null) GUILayout.Label(cur.Report.Details(), small);
            if (cur.Kind == CampKind.Winter && cur.Report != null && cur.Report.Sheltered) GUILayout.Label("Out of the wind: +3°C around camp.", good);

            GUILayout.Space(6);
            if (t.Settled)
                GUILayout.Label("The band lives here all year round now.", wrap);
            else if (t.Moving)
                GUILayout.Label("The band is on the move.", wrap);
            else
            {
                var c = GameClock.Instance;
                bool toWinter = cur.Kind == CampKind.Warm;
                GUILayout.Label("Next move: to a " + (toWinter ? "winter camp" : "warm-season camp") + " in " + t.DaysUntilMove.ToString("0.0") + " days (start of " + (toWinter ? "autumn" : "spring") + ").", wrap);
                if (t.MarkedSite.HasValue && t.MarkedReport != null)
                {
                    var existing = t.CampAt(t.MarkedSite.Value);
                    if (existing != null) GUILayout.Label("Marked: return to " + existing.Name + ".", good);
                    else
                    {
                        float dist = Vector3.Distance(t.MarkedSite.Value, cur.Center);
                        GUILayout.Label("Marked site, " + Mathf.RoundToInt(dist) + " m away. " + t.MarkedReport.Verdict, t.MarkedReport.Acceptable ? good : warn);
                        GUILayout.Label(t.MarkedReport.Details(), small);
                        if (dist > CampSites.MaxMoveDistance) GUILayout.Label("Too far: the elders won't walk more than " + CampSites.MaxMoveDistance + " m.", danger);
                        if (dist < CampSites.MinMoveDistance) GUILayout.Label("Too close: that's the land we already live on.", danger);
                    }
                    if (GUILayout.Button("Clear the mark")) t.ClearMark();
                }
                else
                {
                    var known = t.Camps.Find(x => x.Kind != cur.Kind);
                    GUILayout.Label(known != null ? "No site marked: the band will return to " + known.Name + "." : "No site marked: the elders will choose a place themselves.", small);
                }
                if (GUILayout.Button("Mark the next camp site")) BuildPlacer.Instance.BeginMarkSite();
            }

            if (t.CanSettle)
            {
                GUILayout.Space(6);
                GUILayout.Label("Staying Put is known. The band can make " + cur.Name + " its home all year round.", good);
                if (GUILayout.Button("<b>Settle here for good</b>")) t.Settle();
            }

            GUILayout.Space(8);
            GUILayout.Label("Known camps", label);
            foreach (var camp in t.Camps)
            {
                int buildings = 0;
                foreach (var b in Building.All) if (b.Camp == camp && b.Built) buildings++;
                GUILayout.Label((camp == cur ? "<b>" + camp.Name + "</b>" : camp.Name) + " (" + (camp.Kind == CampKind.Warm ? "warm" : "winter") + ")  stays " + camp.TimesUsed +
                                ", buildings " + buildings + (camp != cur ? ", food left " + Mathf.FloorToInt(camp.Stock[(int)Res.Food]) : ""), small);
            }
            GUILayout.Label("Returning to known camps: Staying Put insight x" + t.FamiliarityMultiplier.ToString("0.00"), small);
            GUILayout.EndScrollView();
        }

        void DrawTribe()
        {
            var t = Tribe.Instance;
            GUILayout.Label(t.TribeName + ": " + t.People.Count + " people", header);
            GUILayout.Label("Births " + t.Births + "   Deaths " + t.Deaths, small);
            string roles = "";
            foreach (var r in PeopleInfo.WorkRoles) roles += r + " " + t.CountRole(r) + "  ";
            GUILayout.Label(roles, wrap);
            scrollTribe = GUILayout.BeginScrollView(scrollTribe);
            foreach (var p in t.People)
            {
                string line = p.Name + " (" + Mathf.FloorToInt(p.Age) + (p.Sex == Sex.Female ? "F" : "M") + ")  " + p.Role +
                              "  HP " + Mathf.RoundToInt(p.Health) + "\n<color=#a89f90>" + p.JobText + "</color>";
                if (GUILayout.Button(line, GUILayout.Height(36))) Selection.Instance.Select(p);
            }
            GUILayout.EndScrollView();
        }

        void DrawKnowledge()
        {
            var k = KnowledgeSystem.Instance;
            GUILayout.Label("Knowledge  (era: " + k.CurrentEra + ")", header);
            GUILayout.Label("Insight comes from doing. Discoveries live in people and are taught at the fire. If all who know something die, it fades.", wrap);
            scrollKnow = GUILayout.BeginScrollView(scrollKnow);
            Era lastEra = (Era)(-1);
            foreach (var d in KnowledgeSystem.Defs)
            {
                if (d.Era != lastEra)
                {
                    lastEra = d.Era;
                    GUILayout.Label("— " + d.Era + (d.Era > k.CurrentEra ? " (locked)" : "") + " —", label);
                }
                var st = k.StatusOf(d.Id);
                int knowers = k.KnowersOf(d.Id);
                string state;
                GUIStyle style = small;
                if (knowers > 0) { state = "Known by " + knowers; style = good; }
                else if (st == KnowledgeStatus.Faded) { state = "Faded: " + Pct(k.InsightOf(d.Id) / d.Insight); style = danger; }
                else if (!d.Implemented) state = "not in this build";
                else if (!k.PrereqsMet(d)) state = "needs earlier knowledge";
                else state = d.Activity + (d.Activity2.HasValue ? "/" + d.Activity2.Value : "") + " insight " + Pct(k.InsightOf(d.Id) / d.Insight);
                GUILayout.Label("<b>" + d.Name + "</b>  " + state, style);
                GUILayout.Label(d.Description, wrap);
            }
            GUILayout.EndScrollView();
        }

        static string Pct(float f) { return Mathf.FloorToInt(f * 100f) + "%"; }

        void DrawOmens()
        {
            var t = Tribe.Instance;
            GUILayout.Label("Omens  (faith " + Mathf.FloorToInt(t.Faith) + ")", header);
            GUILayout.Label("Faith grows as the tribe gathers at the fire, through births, discoveries and beliefs.", wrap);
            foreach (var d in OmenSystem.Defs)
            {
                bool active = OmenSystem.Instance.IsActive(d.Type);
                GUI.enabled = !active && t.Faith >= d.Cost;
                string label = active ? d.Name + " (active, " + OmenSystem.Instance.DaysLeft(d.Type).ToString("0.0") + "d)" : d.Name + "  (" + d.Cost + " faith)";
                if (GUILayout.Button(label)) OmenSystem.Instance.TrySend(d);
                GUI.enabled = true;
                GUILayout.Label(d.Description, wrap);
                GUILayout.Space(4);
            }
        }

        void DrawPriorities()
        {
            var t = Tribe.Instance;
            GUILayout.Label("Tribe priorities", header);
            GUILayout.Label("You can't assign people. These change how much weight the tribe gives each need when they decide who does what.", wrap);
            foreach (PriorityCat c in System.Enum.GetValues(typeof(PriorityCat)))
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(c.ToString(), label, GUILayout.Width(90));
                foreach (PriorityLevel l in System.Enum.GetValues(typeof(PriorityLevel)))
                {
                    bool on = t.Priorities[(int)c] == l;
                    if (GUILayout.Button(on ? "<b>[" + l + "]</b>" : l.ToString()))
                    {
                        t.Priorities[(int)c] = l;
                        RoleAssigner.Instance.Reassign();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
            GUILayout.Label("Current demand", label);
            foreach (var kv in RoleAssigner.Instance.Demand)
                GUILayout.Label(kv.Key + ": " + kv.Value.ToString("0.00") + "  (" + t.CountRole(kv.Key) + " people)", small);
        }

        void DrawCulture()
        {
            var cs = CultureSystem.Instance;
            GUILayout.Label("Culture & belief", header);
            GUILayout.Label("Beliefs form from what the tribe lives through. They're different every time.", wrap);
            if (cs.Beliefs.Count == 0) GUILayout.Label("<i>No beliefs have formed yet.</i>", small);
            foreach (var b in cs.Beliefs)
            {
                GUILayout.Label("<b>" + b.Name + "</b>  (year " + b.FormedYear + ")", good);
                GUILayout.Label(b.Effect, wrap);
            }
            GUILayout.Space(8);
            GUILayout.Label("Tendencies", label);
            foreach (var kv in cs.Tendency)
                GUILayout.Label(kv.Key + ": " + kv.Value.ToString("0.0"), small);
        }

        // ------------------------------------------------------------------
        void DrawInspector(float sw)
        {
            var sel = Selection.Instance.Selected;
            if (sel == null) return;
            GUILayout.BeginArea(Area(sw - 318, 72, 310, 470), box);
            scrollInspect = GUILayout.BeginScrollView(scrollInspect);
            var p = sel as Person;
            var a = sel as Animal;
            var b = sel as Building;
            if (p != null) InspectPerson(p);
            else if (a != null) InspectAnimal(a);
            else if (b != null) InspectBuilding(b);
            GUILayout.EndScrollView();
            if (GUILayout.Button("Close")) Selection.Instance.Clear();
            GUILayout.EndArea();
        }

        void InspectPerson(Person p)
        {
            GUILayout.Label(p.Name, header);
            GUILayout.Label((p.Sex == Sex.Female ? "Woman" : "Man") + ", " + Mathf.FloorToInt(p.Age) + " years.  Role: <b>" + p.Role + "</b>", label);
            GUILayout.Label(p.JobText, small);
            GUILayout.Space(4);
            GUILayout.Label("Health " + Bar(p.Health) + "\nHunger " + Bar(p.Hunger) + "\nWarmth " + Bar(p.Warmth) +
                            "\nRest   " + Bar(p.Rest) + "\nMood   " + Bar(p.Mood), small);
            GUILayout.Label("Feels like " + Mathf.RoundToInt(p.EffectiveTemperature()) + "°C" + (p.HasCloak ? ", wearing a cloak" : "") + (p.HasSpear ? ", carries a spear" : ""), small);
            GUILayout.Space(4);
            GUILayout.Label("<b>Traits</b>", label);
            foreach (var t in p.Traits) GUILayout.Label(t + ": " + PeopleInfo.Describe(t), wrap);
            GUILayout.Label("<b>Skills</b>", label);
            for (int i = 0; i < PeopleInfo.SkillCount; i++)
                GUILayout.Label((Skill)i + ": " + p.Skills[i].ToString("0.0"), small);
            GUILayout.Label("<b>Knows</b>", label);
            if (p.Known.Count == 0) GUILayout.Label("<i>nothing special yet</i>", small);
            foreach (var k in p.Known) GUILayout.Label(KnowledgeSystem.Def(k).Name, small);
            GUILayout.Label("<b>Family</b>", label);
            if (p.Partner != null) GUILayout.Label("Partner: " + p.Partner.Name, small);
            if (p.Mother != null) GUILayout.Label("Mother: " + p.Mother.Name + (p.Mother.Alive ? "" : " (dead)"), small);
            if (p.Father != null) GUILayout.Label("Father: " + p.Father.Name + (p.Father.Alive ? "" : " (dead)"), small);
            foreach (var c in p.Children) GUILayout.Label("Child: " + c.Name, small);
        }

        static string Bar(float v)
        {
            int n = Mathf.Clamp(Mathf.RoundToInt(v / 10f), 0, 10);
            string col = v < 25f ? "#ff6655" : v < 50f ? "#ffcc55" : "#99dd77";
            return "<color=" + col + ">" + new string('|', n * 2) + "</color><color=#444>" + new string('|', (10 - n) * 2) + "</color> " + Mathf.RoundToInt(v);
        }

        void InspectAnimal(Animal a)
        {
            GUILayout.Label(a.SpeciesName, header);
            GUILayout.Label("Health " + Bar(a.Health / a.MaxHealth * 100f), small);
            if (a.IsPredator && a.Group != null)
            {
                GUILayout.Label("Pack of " + a.Group.AliveCount + ". Hunger " + Mathf.RoundToInt(a.Group.Hunger), small);
                GUILayout.Label(a.Fleeing ? "Fleeing" : a.Attacking ? "Hunting" : "Roaming", small);
            }
            else if (a.Group != null) GUILayout.Label("Herd of " + a.Group.AliveCount + (a.ReservedBy != null ? ". Hunted by " + a.ReservedBy.Name : ""), small);
        }

        void InspectBuilding(Building b)
        {
            GUILayout.Label(b.Def.Name, header);
            GUILayout.Label(b.Def.Description, wrap);
            if (b.Camp != null) GUILayout.Label("At " + b.Camp.Name + (b.Camp == Tribe.Instance.CurrentCamp ? " (current camp)" : ""), small);
            if (b.Built && b.Decays) GUILayout.Label("Condition " + Bar(b.Condition * 100f) + (b.Camp != Tribe.Instance.CurrentCamp ? "  (falling apart while empty)" : ""), small);
            if (!b.Built)
            {
                GUILayout.Label("Plan: " + Pct(b.Progress / b.Def.Work) + " built", label);
                foreach (var r in new[] { Res.Wood, Res.Flint, Res.Hide })
                    if (b.Def.Cost(r) > 0) GUILayout.Label(r + ": " + (b.Def.Cost(r) - b.Remaining(r)) + "/" + b.Def.Cost(r), small);
                if (GUILayout.Button("Cancel plan")) { b.Cancel(); Selection.Instance.Clear(); }
            }
            else
            {
                if (b.Def.Type == BuildingType.Campfire)
                    GUILayout.Label(b.Fuel > 0f ? "Burning. Fuel " + b.Fuel.ToString("0.0") + "/" + Building.MaxFuel : "<color=#ff8866>Out</color>", label);
                if (b.Def.ShelterCapacity > 0)
                {
                    GUILayout.Label("Sleeping here: " + b.Sleepers.Count + "/" + b.Def.ShelterCapacity, label);
                    foreach (var s in b.Sleepers) if (s != null) GUILayout.Label(s.Name, small);
                }
            }
        }

        // ------------------------------------------------------------------
        void DrawLog(float sh)
        {
            GUILayout.BeginArea(Area(8, sh - 140, 540, 132), box);
            scrollLog = GUILayout.BeginScrollView(scrollLog);
            var e = EventLog.Entries;
            for (int i = e.Count - 1; i >= 0 && i >= e.Count - 40; i--)
            {
                var style = e[i].Kind == LogKind.Good ? good : e[i].Kind == LogKind.Warning ? warn : e[i].Kind == LogKind.Danger ? danger : small;
                GUILayout.Label("<color=#776f60>" + e[i].Stamp + "</color>  " + e[i].Text, style);
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
    }
}
