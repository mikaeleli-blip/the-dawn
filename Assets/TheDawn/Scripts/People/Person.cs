using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    /// <summary>
    /// One member of the tribe: identity, skills, traits, needs and an autonomous brain.
    /// The player never commands a person directly.
    /// </summary>
    public class Person : MonoBehaviour
    {
        static int nextId = 1;

        // ---------- Identity ----------
        public int Id;
        public string Name;
        public Sex Sex;
        public float Age;
        public List<Trait> Traits = new List<Trait>();
        public float[] Skills = new float[PeopleInfo.SkillCount];
        public Person Partner;
        public Person Mother, Father;
        public List<Person> Children = new List<Person>();
        public HashSet<KnowledgeId> Known = new HashSet<KnowledgeId>();

        // ---------- Needs (0..100) ----------
        public float Hunger = 80f;
        public float Warmth = 70f;
        public float Rest = 80f;
        public float Mood = 55f;
        public float Health = 100f;
        public float Grief;     // decays over days

        // ---------- Work ----------
        public Role Role = Role.None;
        public bool HasSpear;
        public bool HasCloak;
        public Res CarryType;
        public float CarryAmount;
        public bool Alive = true;
        public float DaysAsRole;

        // ---------- Brain ----------
        public enum JobKind { Idle, Wander, Eat, Sleep, Warm, Flee, Fight, Harvest, Hunt, Build, Craft, Fuel, Gather, Play, SelfForage }
        public JobKind Job = JobKind.Idle;
        public string JobText = "Idle";
        int phase;
        float timer;
        float thinkTimer;
        float threatTimer;
        Vector3 dest;
        ResourceNode node;
        Animal prey;
        Animal threat;
        Building building;
        Res wantRes;
        bool sleeping;

        // ---------- Visuals ----------
        GameObject body;
        GameObject carryCube;
        GameObject spearVis;
        Renderer bodyRenderer;
        Role shownRole = (Role)(-1);

        public bool IsAdult { get { return Age >= PeopleInfo.AdultAge; } }
        public bool IsElder { get { return Age >= PeopleInfo.ElderAge; } }
        public bool IsChild { get { return !IsAdult; } }
        public bool Has(Trait t) { return Traits.Contains(t); }
        public float SkillOf(Skill s) { return Skills[(int)s]; }
        public Vector3 Position { get { return transform.position; } }
        public bool Sleeping { get { return sleeping; } }

        static Tribe T { get { return Tribe.Instance; } }
        static World W { get { return World.Instance; } }
        static GameClock C { get { return GameClock.Instance; } }

        public static Person Create(string name, Sex sex, float age, Vector3 pos, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var p = go.AddComponent<Person>();
            p.Id = nextId++;
            p.Name = name;
            p.Sex = sex;
            p.Age = age;
            p.BuildVisual();
            return p;
        }

        void BuildVisual()
        {
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0f, 0.85f, 0f);
            col.height = 1.7f;
            col.radius = 0.35f;

            body = new GameObject("Body");
            body.transform.SetParent(transform, false);
            var torso = Visuals.Part(PrimitiveType.Capsule, body.transform, new Vector3(0, 0.8f, 0), new Vector3(0.5f, 0.6f, 0.4f), Color.gray);
            bodyRenderer = torso.GetComponent<Renderer>();
            Visuals.Part(PrimitiveType.Sphere, body.transform, new Vector3(0, 1.58f, 0), Vector3.one * 0.32f, new Color(0.62f, 0.47f, 0.36f));
            Visuals.Part(PrimitiveType.Sphere, body.transform, new Vector3(0, 1.66f, -0.04f), new Vector3(0.34f, 0.22f, 0.34f), new Color(0.15f, 0.1f, 0.07f));
            carryCube = Visuals.Part(PrimitiveType.Cube, body.transform, new Vector3(0, 1.1f, -0.32f), new Vector3(0.4f, 0.4f, 0.25f), new Color(0.5f, 0.35f, 0.2f));
            carryCube.SetActive(false);
            spearVis = Visuals.Part(PrimitiveType.Cylinder, body.transform, new Vector3(0.3f, 1.0f, 0f), new Vector3(0.04f, 0.9f, 0.04f), new Color(0.45f, 0.33f, 0.2f));
            spearVis.SetActive(false);
            RefreshScale();
        }

        void RefreshScale()
        {
            float s = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01(Age / 16f));
            if (Sex == Sex.Female && IsAdult) s *= 0.94f;
            transform.localScale = Vector3.one * s;
        }

        // =====================================================================
        // Update
        // =====================================================================

        void Update()
        {
            if (!Alive || C == null) return;
            float dh = C.DeltaHours;
            UpdateNeeds(dh);
            if (!Alive) return;

            threatTimer -= Time.deltaTime;
            if (threatTimer <= 0f)
            {
                threatTimer = 0.4f;
                CheckThreats();
            }

            thinkTimer -= Time.deltaTime;
            if (Job == JobKind.Idle || thinkTimer <= 0f && CanInterrupt())
            {
                thinkTimer = 3f;
                Think();
            }
            RunJob();
            RefreshVisuals();
        }

        bool CanInterrupt()
        {
            // Only interrupt low-stakes jobs for urgent needs.
            if (Job == JobKind.Flee || Job == JobKind.Fight || Job == JobKind.Eat || Job == JobKind.Sleep) return false;
            return Hunger < 25f || (Warmth < 25f && Job != JobKind.Warm) || (C.IsNight && Job != JobKind.Sleep && Rest < 60f);
        }

        void RefreshVisuals()
        {
            if (shownRole != Role)
            {
                shownRole = Role;
                bodyRenderer.sharedMaterial = Visuals.Mat(PeopleInfo.RoleColor(Role));
            }
            carryCube.SetActive(CarryAmount > 0.01f);
            spearVis.SetActive(HasSpear && !sleeping);
            body.transform.localRotation = sleeping ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
            body.transform.localPosition = sleeping ? new Vector3(0f, 0.25f, -0.8f) : Vector3.zero;
        }

        // =====================================================================
        // Needs
        // =====================================================================

        public float EffectiveTemperature()
        {
            float t = C.Temperature;
            var fire = Building.NearestLitFire(Position, Building.FireWarmRadius);
            if (fire != null) t += 18f;
            if (sleeping && building != null && building.Def.ShelterWarmth > 0f && building.Built)
                t += building.Def.ShelterWarmth;
            if (HasCloak) t += 8f;
            if (Has(Trait.Hardy)) t += 4f;
            if (IsChild) t -= 2f;
            return t;
        }

        void UpdateNeeds(float dh)
        {
            Hunger = Mathf.Max(0f, Hunger - 2.8f * dh);
            if (sleeping) Rest = Mathf.Min(100f, Rest + 12f * dh);
            else Rest = Mathf.Max(0f, Rest - 4.2f * dh);

            float temp = EffectiveTemperature();
            Warmth = Mathf.Clamp(Warmth + (temp - 10f) * 0.6f * dh, 0f, 100f);

            // Health
            float dmg = 0f;
            if (Hunger <= 0f) dmg += 4f;
            if (Warmth <= 0f) dmg += 6f;
            if (dmg > 0f) Damage(dmg * dh, Hunger <= 0f ? "starvation" : "the cold", null);
            else if (Hunger > 30f && Warmth > 30f && Health < 100f)
            {
                float heal = 1f;
                if (sleeping) heal *= 2f;
                heal *= 1f + KnowledgeSystem.Instance.Effect(KnowledgeId.HerbalLore) * 0.6f;
                Health = Mathf.Min(100f, Health + heal * dh);
            }

            // Mood drifts toward a target made of current conditions.
            float target = 55f;
            if (Hunger < 30f) target -= 15f;
            if (Warmth < 30f) target -= 15f;
            if (Rest < 20f) target -= 10f;
            if (Health < 50f) target -= 10f;
            target -= Grief;
            if (Building.NearestLitFire(Position, Building.FireWarmRadius) != null && C.IsEvening) target += 10f;
            target += CultureSystem.Instance.MoodBonus;
            if (T != null && T.HasCamp) target += 5f;
            Mood = Mathf.MoveTowards(Mood, Mathf.Clamp(target, 0f, 100f), 6f * dh);
        }

        public void OnNewDay()
        {
            Age += 1f / C.DaysPerYear;
            Grief = Mathf.Max(0f, Grief - 3f);
            DaysAsRole += 1f;
            RefreshScale();
        }

        public void Damage(float amount, string cause, Animal by)
        {
            if (!Alive) return;
            Health -= amount;
            if (Health <= 0f)
            {
                Health = 0f;
                Die(cause);
            }
        }

        public void Die(string cause)
        {
            if (!Alive) return;
            Alive = false;
            if (node != null && node.ReservedBy == this) node.ReservedBy = null;
            if (prey != null && prey.ReservedBy == this) prey.ReservedBy = null;
            T.OnPersonDied(this, cause);
            Destroy(gameObject);
        }

        public void AddSkill(Skill s, float amount)
        {
            float cur = Skills[(int)s];
            // Learning slows as skill rises.
            Skills[(int)s] = Mathf.Min(20f, cur + amount * (1.2f - cur / 20f));
        }

        float WorkSpeed(Skill s)
        {
            float m = 0.6f + SkillOf(s) * 0.04f;
            if (Has(Trait.Hardworking)) m *= 1.2f;
            if (Has(Trait.Lazy)) m *= 0.8f;
            if (Mood < 25f) m *= 0.75f;
            if (Health < 50f) m *= 0.7f;
            if (IsChild) m *= 0.4f;
            if (IsElder) m *= 0.75f;
            return m;
        }

        float CarryCapacity
        {
            get
            {
                float c = 4f;
                if (Has(Trait.Strong)) c += 2f;
                if (IsChild) c = 1.5f;
                return c;
            }
        }

        /// <summary>Doing work builds skill and insight toward discoveries.</summary>
        void Practice(Skill s, float seconds)
        {
            AddSkill(s, 0.012f * seconds);
            float insight = seconds * (0.5f + SkillOf(s) * 0.05f);
            if (Has(Trait.Curious)) insight *= 1.6f;
            KnowledgeSystem.Instance.AddInsight(this, s, insight);
        }

        // =====================================================================
        // Threats
        // =====================================================================

        void CheckThreats()
        {
            if (Job == JobKind.Fight && threat != null && threat.Alive && !threat.Fleeing) return;

            Animal nearest = null;
            float best = float.MaxValue;
            foreach (var a in Animal.All)
            {
                if (!a.Alive) continue;
                bool dangerous = a.IsPredator && !a.Fleeing && (a.TargetPerson == this || a.Attacking);
                float d = (a.Position - Position).sqrMagnitude;
                float r = dangerous ? 14f : (a.IsPredator ? 7f : (a.Species == Species.Boar && a.Enraged ? 6f : 0f));
                if (r <= 0f || d > r * r) continue;
                if (d < best) { best = d; nearest = a; }
            }
            if (nearest == null) return;

            if (sleeping) { sleeping = false; }

            bool courage = OmenSystem.Instance.IsActive(OmenType.SpiritOfCourage);
            bool willFight = IsAdult && !IsElder && Health > 35f &&
                             (courage || Has(Trait.Brave) || (Role == Role.Hunter && HasSpear) || (HasSpear && !Has(Trait.Cautious) && FriendsNearby(10f) >= 2));
            if (willFight)
            {
                StartJob(JobKind.Fight, "Fighting a " + nearest.SpeciesName.ToLower());
                threat = nearest;
                if (nearest.IsPredator) nearest.OnConfronted(this);
            }
            else if (Job != JobKind.Flee)
            {
                StartJob(JobKind.Flee, "Fleeing a " + nearest.SpeciesName.ToLower());
                threat = nearest;
                timer = 6f;
            }
        }

        int FriendsNearby(float r)
        {
            int n = 0;
            foreach (var p in T.People)
                if (p != this && p.IsAdult && (p.Position - Position).sqrMagnitude < r * r) n++;
            return n;
        }

        // =====================================================================
        // Thinking: pick what to do next
        // =====================================================================

        void LeaveShelter()
        {
            if (Job == JobKind.Sleep && building != null) building.Sleepers.Remove(this);
        }

        void StartJob(JobKind kind, string text)
        {
            LeaveShelter();
            ReleaseReservations();
            Job = kind;
            JobText = text;
            phase = 0;
            timer = 0f;
            if (kind != JobKind.Sleep) sleeping = false;
        }

        void ReleaseReservations()
        {
            if (node != null && node.ReservedBy == this) node.ReservedBy = null;
            if (prey != null && prey.ReservedBy == this) prey.ReservedBy = null;
            node = null;
            prey = null;
        }

        void Think()
        {
            // Drop anything carried at the stockpile first.
            if (CarryAmount > 0.01f && Job == JobKind.Idle)
            {
                StartJob(JobKind.Harvest, "Bringing back " + CarryType.ToString().ToLower());
                phase = 2;
                return;
            }

            if (Hunger < 35f)
            {
                if (T.Stock(Res.Food) >= 1f) { StartJob(JobKind.Eat, "Going to eat"); return; }
                if (Hunger < 25f && FindBush(40f) != null) { StartJob(JobKind.SelfForage, "Looking for something to eat"); return; }
            }

            if (C.IsNight && (Rest < 85f || C.Hour >= 21f || C.Hour < 5f))
            {
                StartJob(JobKind.Sleep, "Going to sleep");
                return;
            }

            if (Warmth < 30f && Building.NearestLitFire(Position, 80f) != null && !C.IsNight)
            {
                StartJob(JobKind.Warm, "Warming up by the fire");
                timer = 25f;
                return;
            }

            // An interrupt only handles urgent needs; otherwise keep doing the current job.
            if (Job != JobKind.Idle) return;

            // Evening gathering around the fire: teaching, stories, faith.
            if (C.IsEvening && Building.NearestLitFire(Position, 80f) != null && Random.value < 0.75f)
            {
                StartJob(JobKind.Gather, "Gathering by the fire");
                return;
            }

            // Pick up gear at camp.
            if (Role == Role.Hunter && !HasSpear && T.Stock(Res.Spear) >= 1f && Near(T.StockpilePoint(Position), 6f))
            { T.Take(Res.Spear, 1f); HasSpear = true; }
            if (!HasCloak && C.Temperature < 8f && T.Stock(Res.Cloak) >= 1f && Near(T.StockpilePoint(Position), 6f))
            { T.Take(Res.Cloak, 1f); HasCloak = true; }

            switch (Role)
            {
                case Role.Child: ThinkChild(); break;
                case Role.Elder: ThinkElder(); break;
                case Role.Forager: if (!TryForage()) Loiter(); break;
                case Role.Hunter: if (!TryHunt()) if (!TryForage()) Loiter(); break;
                case Role.Gatherer: if (!TryGatherMaterials()) Loiter(); break;
                case Role.Builder: if (!TryBuild()) if (!TryGatherMaterials()) Loiter(); break;
                case Role.Crafter: if (!TryCraft()) if (!TryGatherMaterials()) Loiter(); break;
                case Role.Firekeeper: if (!TryFuelFire()) if (!TryGatherWood()) Loiter(); break;
                default: Loiter(); break;
            }
        }

        void ThinkChild()
        {
            StartJob(JobKind.Play, "Playing");
            dest = W.RandomLandPointNear(T.CampPosition, 12f);
            timer = Random.Range(4f, 10f);
        }

        void ThinkElder()
        {
            if (TryFuelFire()) return;
            if (Random.value < 0.4f && TryForage(25f)) return;
            var fire = Building.NearestLitFire(Position, 80f);
            if (fire != null)
            {
                StartJob(JobKind.Gather, "Sitting by the fire");
                return;
            }
            Loiter();
        }

        void Loiter()
        {
            StartJob(JobKind.Wander, "Idle");
            dest = W.RandomLandPointNear(T.CampPosition, 10f);
            timer = Random.Range(3f, 8f);
        }

        bool Near(Vector3 p, float r) { return (p - Position).sqrMagnitude < r * r; }

        ResourceNode FindBush(float maxDist)
        {
            return FindNode(ResourceType.BerryBush, maxDist, Position);
        }

        ResourceNode FindNode(ResourceType type, float maxDist, Vector3 from)
        {
            ResourceNode best = null;
            float bestD = maxDist * maxDist;
            foreach (var n in ResourceNode.All)
            {
                if (n.Type != type || !n.Available) continue;
                if (n.ReservedBy != null && n.ReservedBy != this && n.ReservedBy.Alive) continue;
                float d = (n.transform.position - from).sqrMagnitude;
                if (Has(Trait.KeenEyed)) d *= 0.8f;
                if (d < bestD) { bestD = d; best = n; }
            }
            return best;
        }

        bool TryForage(float range = 70f)
        {
            if (C.Season == Season.Winter && Random.value < 0.6f) return false;
            var n = FindNode(ResourceType.BerryBush, range, T.CampPosition);
            if (n == null) return false;
            StartJob(JobKind.Harvest, "Foraging berries");
            node = n;
            node.ReservedBy = this;
            CarryType = Res.Food;
            return true;
        }

        bool TryGatherWood()
        {
            var n = FindNode(ResourceType.Tree, 90f, T.CampPosition);
            if (n == null) return false;
            StartJob(JobKind.Harvest, "Gathering wood");
            node = n; node.ReservedBy = this; CarryType = Res.Wood;
            return true;
        }

        bool TryGatherMaterials()
        {
            float woodNeed = T.Need(Res.Wood);
            float flintNeed = T.Need(Res.Flint);
            if (woodNeed <= 0f && flintNeed <= 0f)
            {
                // Keep a little surplus anyway.
                if (T.Stock(Res.Wood) < 25f) woodNeed = 1f;
                else if (T.Stock(Res.Flint) < 8f) flintNeed = 1f;
                else return false;
            }
            if (flintNeed > woodNeed)
            {
                var n = FindNode(ResourceType.FlintRock, 120f, T.CampPosition);
                if (n != null)
                {
                    StartJob(JobKind.Harvest, "Collecting flint");
                    node = n; node.ReservedBy = this; CarryType = Res.Flint;
                    return true;
                }
            }
            return TryGatherWood();
        }

        bool TryHunt()
        {
            Animal best = null;
            float bestD = 140f * 140f;
            foreach (var a in Animal.All)
            {
                if (!a.Alive || a.IsPredator) continue;
                if (a.ReservedBy != null && a.ReservedBy != this && a.ReservedBy.Alive) continue;
                if (a.Species == Species.Boar && !HasSpear) continue;
                float d = (a.Position - T.CampPosition).sqrMagnitude;
                if (d < bestD) { bestD = d; best = a; }
            }
            if (best == null) return false;
            StartJob(JobKind.Hunt, "Hunting a " + best.SpeciesName.ToLower());
            prey = best;
            prey.ReservedBy = this;
            timer = 70f; // give up after a while
            return true;
        }

        bool TryBuild()
        {
            var b = Building.NextToBuild(Position);
            if (b == null) return false;
            StartJob(JobKind.Build, "Building a " + b.Def.Name.ToLower());
            building = b;
            return true;
        }

        bool TryCraft()
        {
            float hunters = T.CountRole(Role.Hunter);
            float spearsOut = 0f;
            foreach (var p in T.People) if (p.HasSpear) spearsOut++;
            bool needSpear = T.Stock(Res.Spear) + spearsOut < hunters + 2f;
            bool canSpear = T.Stock(Res.Wood) >= 1f && T.Stock(Res.Flint) >= 1f;
            bool hideWork = KnowledgeSystem.Instance.Effect(KnowledgeId.HideWorking) > 0f;
            float cloaked = 0f;
            foreach (var p in T.People) if (p.HasCloak) cloaked++;
            bool needCloak = hideWork && T.Stock(Res.Cloak) + cloaked < T.People.Count && T.Stock(Res.Hide) >= 2f;

            if (needSpear && canSpear) wantRes = Res.Spear;
            else if (needCloak) wantRes = Res.Cloak;
            else return false;

            StartJob(JobKind.Craft, wantRes == Res.Spear ? "Knapping a spear" : "Sewing a hide cloak");
            var spot = Building.NearestBuilt(BuildingType.KnappingSpot, Position, 200f);
            dest = spot != null ? spot.transform.position + Random.insideUnitSphere * 1.5f : T.StockpilePoint(Position);
            dest = W.OnGround(dest);
            return true;
        }

        bool TryFuelFire()
        {
            if (T.Stock(Res.Wood) < 1f) return false;
            Building low = null;
            float lowest = 0.6f;
            foreach (var b in Building.All)
            {
                if (!b.Built || b.Def.Type != BuildingType.Campfire) continue;
                float f = b.Fuel / Building.MaxFuel;
                if (f < lowest) { lowest = f; low = b; }
            }
            if (low == null) return false;
            StartJob(JobKind.Fuel, "Tending the fire");
            building = low;
            return true;
        }

        // =====================================================================
        // Running jobs
        // =====================================================================

        float Speed
        {
            get
            {
                float s = IsChild ? 2.6f : 3.4f;
                if (IsElder) s *= 0.8f;
                if (Health < 50f) s *= 0.7f;
                if (Job == JobKind.Flee || Job == JobKind.Fight) s *= 1.7f;
                return s;
            }
        }

        bool MoveTo(Vector3 target, float stopDist, float speedMul = 1f)
        {
            Vector3 pos = Position;
            Vector3 flat = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
            float dist = flat.magnitude;
            if (dist <= stopDist) return true;
            float spd = Speed * speedMul;
            if (!W.IsLand(pos, 0f)) spd *= 0.45f; // wading
            Vector3 step = flat / dist * Mathf.Min(dist, spd * Time.deltaTime);
            Vector3 next = W.ClampToBounds(pos + step);
            next.y = W.HeightAt(next);
            transform.position = next;
            if (flat.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 10f * Time.deltaTime);
            return false;
        }

        void Finish()
        {
            LeaveShelter();
            ReleaseReservations();
            Job = JobKind.Idle;
            JobText = "Idle";
            sleeping = false;
        }

        void RunJob()
        {
            switch (Job)
            {
                case JobKind.Idle: break;
                case JobKind.Wander:
                case JobKind.Play:
                    if (MoveTo(dest, 0.5f, Job == JobKind.Play ? 1.2f : 0.6f))
                    {
                        timer -= Time.deltaTime;
                        if (Job == JobKind.Play) LearnFromAdults(Time.deltaTime);
                        if (timer <= 0f) Finish();
                    }
                    break;
                case JobKind.Eat: RunEat(); break;
                case JobKind.SelfForage: RunSelfForage(); break;
                case JobKind.Sleep: RunSleep(); break;
                case JobKind.Warm: RunWarm(); break;
                case JobKind.Gather: RunGather(); break;
                case JobKind.Flee: RunFlee(); break;
                case JobKind.Fight: RunFight(); break;
                case JobKind.Harvest: RunHarvest(); break;
                case JobKind.Hunt: RunHunt(); break;
                case JobKind.Build: RunBuild(); break;
                case JobKind.Craft: RunCraft(); break;
                case JobKind.Fuel: RunFuel(); break;
            }
        }

        void RunEat()
        {
            Vector3 pile = T.StockpilePoint(Position);
            if (!MoveTo(pile, 2f)) return;
            timer += Time.deltaTime;
            if (timer < 1.5f) return;
            timer = 0f;
            if (T.Take(Res.Food, 1f) >= 1f) Hunger = Mathf.Min(100f, Hunger + 40f);
            if (Hunger > 80f || T.Stock(Res.Food) < 1f) Finish();
        }

        void RunSelfForage()
        {
            if (phase == 0)
            {
                node = FindBush(60f);
                if (node == null) { Finish(); return; }
                node.ReservedBy = this;
                phase = 1;
            }
            if (node == null || !node.Available) { Finish(); return; }
            if (!MoveTo(node.transform.position, 1.4f)) return;
            timer += Time.deltaTime * WorkSpeed(Skill.Foraging);
            if (timer >= 2.5f)
            {
                timer = 0f;
                if (node.Take(1f) >= 1f) Hunger = Mathf.Min(100f, Hunger + 30f);
                Practice(Skill.Foraging, 2.5f);
                if (Hunger > 70f || !node.Available) Finish();
            }
        }

        void RunSleep()
        {
            if (phase == 0)
            {
                building = T.FindShelterFor(this);
                if (building != null) dest = building.SleepSpot(this);
                else
                {
                    var fire = Building.NearestLitFire(Position, 100f);
                    dest = fire != null ? W.RandomLandPointNear(fire.transform.position, 4f) : W.RandomLandPointNear(T.CampPosition, 5f);
                }
                phase = 1;
            }
            if (phase == 1)
            {
                if (!MoveTo(dest, 0.6f)) return;
                sleeping = true;
                JobText = building != null ? "Sleeping in the " + building.Def.Name.ToLower() : "Sleeping in the open";
                phase = 2;
            }
            if (phase == 2)
            {
                bool morning = !C.IsNight && C.Hour >= 6f && C.Hour < 20f;
                if ((morning && Rest > 60f) || (Rest >= 99f && !C.IsNight)) { Finish(); building = null; }
                else if (Hunger < 15f && T.Stock(Res.Food) >= 1f) { StartJob(JobKind.Eat, "Waking up hungry"); building = null; }
            }
        }

        void RunWarm()
        {
            var fire = Building.NearestLitFire(Position, 100f);
            if (fire == null) { Finish(); return; }
            if (phase == 0) { dest = W.RandomLandPointNear(fire.transform.position, 3f); phase = 1; }
            timer -= Time.deltaTime;
            if (!MoveTo(dest, 0.5f)) return;
            if (Warmth > 75f || timer <= 0f) Finish();
        }

        void RunGather()
        {
            var fire = Building.NearestLitFire(Position, 100f);
            if (fire == null || (!C.IsEvening && Role != Role.Elder)) { Finish(); return; }
            if (phase == 0) { dest = W.RandomLandPointNear(fire.transform.position, 4.5f); phase = 1; timer = Random.Range(10f, 25f); }
            if (!MoveTo(dest, 0.5f)) return;
            Vector3 look = fire.transform.position - Position; look.y = 0f;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);

            // Stories and songs: lore, faith, and the young listen and learn.
            float dt = Time.deltaTime;
            Practice(Skill.Lore, dt * 0.5f);
            float faith = 0.015f * dt;
            if (Has(Trait.Spiritual)) faith *= 2f;
            faith *= CultureSystem.Instance.HearthFaithMultiplier;
            T.Faith += faith;
            CultureSystem.Instance.Add(CultureTheme.Hearth, 0.002f * dt);
            if (IsChild) LearnFromAdults(dt);

            if (Role == Role.Elder)
            {
                timer -= dt;
                if (timer <= 0f && !C.IsEvening) Finish();
            }
        }

        void LearnFromAdults(float dt)
        {
            foreach (var p in T.People)
            {
                if (!p.IsAdult || (p.Position - Position).sqrMagnitude > 64f) continue;
                for (int i = 0; i < PeopleInfo.SkillCount; i++)
                    if (p.Skills[i] > Skills[i]) AddSkill((Skill)i, 0.004f * dt);
            }
        }

        void RunFlee()
        {
            timer -= Time.deltaTime;
            Vector3 safe;
            var fire = Building.NearestLitFire(Position, 60f);
            if (fire != null) safe = fire.transform.position;
            else if (threat != null && threat.Alive)
            {
                Vector3 away = Position - threat.Position; away.y = 0f;
                safe = Position + away.normalized * 15f;
            }
            else safe = T.CampPosition;
            MoveTo(W.OnGround(safe), 2f, 1f);
            if (timer <= 0f) { threat = null; Finish(); }
        }

        void RunFight()
        {
            if (threat == null || !threat.Alive || threat.Fleeing || (threat.Position - Position).sqrMagnitude > 30f * 30f)
            {
                if (threat != null && threat.Fleeing && threat.IsPredator)
                {
                    CultureSystem.Instance.Add(CultureTheme.Wild, 0.5f);
                    T.Faith += CultureSystem.Instance.Has(CultureTheme.Wild) ? 1f : 0f;
                }
                threat = null;
                Finish();
                return;
            }
            if (!MoveTo(threat.Position, 1.6f)) return;
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = 1.2f;
            float dmg = HasSpear ? 22f : 7f;
            if (Has(Trait.Strong)) dmg *= 1.3f;
            dmg *= 1f + SkillOf(Skill.Hunting) * 0.03f;
            if (OmenSystem.Instance.IsActive(OmenType.SpiritOfCourage)) dmg *= 1.25f;
            bool killed = threat.TakeDamage(dmg, this);
            AddSkill(Skill.Hunting, 0.05f);
            if (killed)
            {
                EventLog.Add(Name + " killed a " + threat.SpeciesName.ToLower() + ".", LogKind.Good);
                T.Add(Res.Food, threat.FoodYield * 0.5f);
                T.Add(Res.Hide, threat.HideYield);
                threat = null;
                Finish();
            }
        }

        void RunHarvest()
        {
            switch (phase)
            {
                case 0: // walk to node
                    if (node == null || !node.Available) { if (CarryAmount > 0f) phase = 2; else Finish(); return; }
                    if (MoveTo(node.transform.position, 1.6f)) phase = 1;
                    break;
                case 1: // work
                    if (node == null || !node.Available) { phase = CarryAmount > 0f ? 2 : 0; if (phase == 0) Finish(); return; }
                    Skill sk = node.Type == ResourceType.BerryBush ? Skill.Foraging : (node.Type == ResourceType.Tree ? Skill.Woodcutting : Skill.Crafting);
                    float seconds = node.Type == ResourceType.BerryBush ? 2.2f : (node.Type == ResourceType.Tree ? 3.5f : 4f);
                    timer += Time.deltaTime * WorkSpeed(sk);
                    if (timer >= seconds)
                    {
                        timer = 0f;
                        CarryAmount += node.Take(1f);
                        Practice(sk, seconds);
                        if (CarryAmount >= CarryCapacity || !node.Available)
                        {
                            if (node.ReservedBy == this) node.ReservedBy = null;
                            phase = 2;
                        }
                    }
                    break;
                case 2: // carry home
                    if (MoveTo(T.StockpilePoint(Position), 2f)) phase = 3;
                    break;
                case 3:
                    T.Add(CarryType, CarryAmount);
                    CarryAmount = 0f;
                    Finish();
                    break;
            }
        }

        void RunHunt()
        {
            timer -= Time.deltaTime;
            switch (phase)
            {
                case 0: // fetch a spear first if one is waiting
                    if (!HasSpear && T.Stock(Res.Spear) >= 1f)
                    {
                        if (!MoveTo(T.StockpilePoint(Position), 2f)) return;
                        if (T.Take(Res.Spear, 1f) >= 1f) HasSpear = true;
                    }
                    phase = 1;
                    break;
                case 1: // track and stalk
                    if (prey == null || !prey.Alive || timer <= 0f)
                    {
                        if (prey != null && prey.Alive) EventLog.Add(Name + " lost the trail of a " + prey.SpeciesName.ToLower() + ".");
                        Finish();
                        return;
                    }
                    float d = Vector3.Distance(prey.Position, Position);
                    float range = HasSpear ? 9f : 3f;
                    bool stalking = d < 28f;
                    JobText = stalking ? "Stalking a " + prey.SpeciesName.ToLower() : "Tracking a " + prey.SpeciesName.ToLower();
                    if (stalking)
                    {
                        // Each moment close to prey risks being noticed.
                        float notice = 0.06f * Time.deltaTime * (1f - d / 28f);
                        notice *= 1f - SkillOf(Skill.Hunting) * 0.03f;
                        notice *= 1f - KnowledgeSystem.Instance.Effect(KnowledgeId.Tracking) * 0.35f;
                        if (Has(Trait.Patient)) notice *= 0.75f;
                        if (OmenSystem.Instance.IsActive(OmenType.VisionOfTheHunt)) notice *= 0.5f;
                        if (Random.value < notice) prey.Startle(Position);
                    }
                    if (d > range)
                    {
                        MoveTo(prey.Position, range * 0.8f, stalking ? 0.45f : 1f);
                        Practice(Skill.Hunting, Time.deltaTime * 0.3f);
                        return;
                    }
                    // Strike.
                    phase = 2;
                    break;
                case 2:
                    {
                        float hit = HasSpear ? 0.45f : 0.25f;
                        hit += SkillOf(Skill.Hunting) * 0.02f;
                        hit += KnowledgeSystem.Instance.Effect(KnowledgeId.FireHardenedSpears) * 0.12f;
                        if (Has(Trait.KeenEyed)) hit += 0.08f;
                        if (OmenSystem.Instance.IsActive(OmenType.VisionOfTheHunt)) hit += 0.15f;
                        Practice(Skill.Hunting, 4f);
                        if (Random.value < hit)
                        {
                            float dmg = HasSpear ? 55f : 25f;
                            dmg *= 1f + KnowledgeSystem.Instance.Effect(KnowledgeId.FireHardenedSpears) * 0.3f;
                            if (Has(Trait.Strong)) dmg *= 1.2f;
                            bool killed = prey.TakeDamage(dmg, this);
                            if (killed)
                            {
                                EventLog.Add(Name + " brought down a " + prey.SpeciesName.ToLower() + ".", LogKind.Good);
                                CultureSystem.Instance.Add(CultureTheme.Beasts, 1f);
                                if (CultureSystem.Instance.Has(CultureTheme.Beasts)) T.Faith += 2f;
                                CarryType = Res.Food;
                                CarryAmount = prey.FoodYield;
                                T.Add(Res.Hide, prey.HideYield); // hide is dragged home with the meat
                                prey.ReservedBy = null;
                                prey = null;
                                phase = 3;
                                return;
                            }
                        }
                        else
                        {
                            prey.Startle(Position);
                            if (HasSpear && Random.value < 0.15f)
                            {
                                HasSpear = false;
                                EventLog.Add(Name + "'s spear broke on a missed throw.");
                            }
                        }
                        phase = 1;
                    }
                    break;
                case 3: // carry meat home
                    if (MoveTo(T.StockpilePoint(Position), 2f, 0.8f))
                    {
                        T.Add(Res.Food, CarryAmount);
                        CarryAmount = 0f;
                        Finish();
                    }
                    break;
            }
        }

        void RunBuild()
        {
            if (building == null || building.Built) { Finish(); return; }
            switch (phase)
            {
                case 0:
                    Res need;
                    if (building.MissingMaterial(out need))
                    {
                        if (T.Stock(need) < 1f) { Finish(); return; }
                        wantRes = need;
                        phase = 1;
                    }
                    else phase = 3;
                    break;
                case 1: // fetch from stockpile
                    if (!MoveTo(T.StockpilePoint(Position), 2f)) return;
                    float want = Mathf.Min(CarryCapacity, building.Remaining(wantRes));
                    CarryType = wantRes;
                    CarryAmount = T.Take(wantRes, want);
                    if (CarryAmount <= 0f) { Finish(); return; }
                    phase = 2;
                    break;
                case 2: // deliver
                    if (!MoveTo(building.transform.position, building.Def.Radius + 0.8f)) return;
                    building.Deliver(CarryType, CarryAmount);
                    CarryAmount = 0f;
                    phase = 0;
                    break;
                case 3: // work
                    if (!MoveTo(building.transform.position, building.Def.Radius + 0.8f)) return;
                    float work = Time.deltaTime * WorkSpeed(Skill.Building);
                    Practice(Skill.Building, Time.deltaTime);
                    if (building.AddWork(work))
                    {
                        EventLog.Add("The tribe finished a " + building.Def.Name.ToLower() + ".", LogKind.Good);
                        Finish();
                    }
                    break;
            }
        }

        void RunCraft()
        {
            if (!MoveTo(dest, 0.8f)) return;
            float spotBonus = Building.NearestBuilt(BuildingType.KnappingSpot, Position, 4f) != null ? 1.4f : 1f;
            timer += Time.deltaTime * WorkSpeed(Skill.Crafting) * spotBonus;
            Practice(Skill.Crafting, Time.deltaTime);
            float needed = wantRes == Res.Spear ? 12f : 18f;
            if (timer < needed) return;
            if (wantRes == Res.Spear)
            {
                if (T.Take(Res.Wood, 1f) >= 1f && T.Take(Res.Flint, 1f) >= 1f) T.Add(Res.Spear, 1f);
            }
            else
            {
                if (T.Take(Res.Hide, 2f) >= 2f) T.Add(Res.Cloak, 1f);
            }
            Finish();
        }

        void RunFuel()
        {
            if (building == null) { Finish(); return; }
            if (phase == 0)
            {
                if (!MoveTo(T.StockpilePoint(Position), 2f)) return;
                CarryType = Res.Wood;
                CarryAmount = T.Take(Res.Wood, Mathf.Min(3f, Building.MaxFuel - building.Fuel));
                if (CarryAmount <= 0f) { Finish(); return; }
                phase = 1;
            }
            if (phase == 1)
            {
                if (!MoveTo(building.transform.position, 1.8f)) return;
                building.AddFuel(CarryAmount);
                CarryAmount = 0f;
                Practice(Skill.Lore, 1f);
                Finish();
            }
        }

        // =====================================================================
        // Info for UI
        // =====================================================================

        public string TraitsText()
        {
            var parts = new List<string>();
            foreach (var t in Traits) parts.Add(t.ToString());
            return string.Join(", ", parts.ToArray());
        }
    }
}
