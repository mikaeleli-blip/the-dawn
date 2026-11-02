using System.Collections.Generic;
using UnityEngine;

namespace TheDawn
{
    public enum Species { Deer, Boar, Wolf }

    /// <summary>A herd or pack: shared home, shared target, shared hunger.</summary>
    public class AnimalGroup
    {
        public Species Species;
        public Vector3 Home;
        public Vector3 Center;
        public readonly List<Animal> Members = new List<Animal>();
        public float Hunger;
        public Person TargetPerson;
        public Animal TargetAnimal;
        public float WanderTimer;

        public int AliveCount
        {
            get { int n = 0; foreach (var m in Members) if (m != null && m.Alive) n++; return n; }
        }

        public bool HasTarget
        {
            get
            {
                return (TargetPerson != null && TargetPerson.Alive) || (TargetAnimal != null && TargetAnimal.Alive);
            }
        }

        public Vector3 TargetPos
        {
            get
            {
                if (TargetPerson != null && TargetPerson.Alive) return TargetPerson.Position;
                if (TargetAnimal != null && TargetAnimal.Alive) return TargetAnimal.Position;
                return Center;
            }
        }

        public void ClearTarget() { TargetPerson = null; TargetAnimal = null; }
    }

    public class Animal : MonoBehaviour
    {
        public static readonly List<Animal> All = new List<Animal>();

        public Species Species;
        public AnimalGroup Group;
        public float Health, MaxHealth;
        public bool Alive = true;
        public Person ReservedBy;
        public bool Enraged;
        Person enragedAt;

        enum State { Wander, Flee, Chase, Retreat }
        State state = State.Wander;
        float stateTimer;
        float attackTimer;
        float senseTimer;
        Vector3 dest;
        Vector3 fleeFrom;

        public bool IsPredator { get { return Species == Species.Wolf; } }
        public bool Fleeing { get { return state == State.Flee || state == State.Retreat; } }
        public bool Attacking { get { return state == State.Chase; } }
        public Person TargetPerson { get { return state == State.Chase && Group != null ? Group.TargetPerson : (Enraged ? enragedAt : null); } }
        public Vector3 Position { get { return transform.position; } }
        public string SpeciesName { get { return Species.ToString(); } }

        public float FoodYield { get { return Species == Species.Deer ? 12f : Species == Species.Boar ? 10f : 4f; } }
        public float HideYield { get { return Species == Species.Boar ? 1f : 1f; } }
        float WalkSpeed { get { return Species == Species.Wolf ? 2.6f : Species == Species.Boar ? 1.6f : 1.8f; } }
        float RunSpeed { get { return Species == Species.Wolf ? 7f : Species == Species.Boar ? 5f : 7.5f; } }

        static World W { get { return World.Instance; } }

        public static Animal Create(Species s, AnimalGroup g, Vector3 pos, Transform parent)
        {
            var go = new GameObject(s.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var a = go.AddComponent<Animal>();
            a.Species = s;
            a.Group = g;
            a.MaxHealth = a.Health = s == Species.Deer ? 60f : s == Species.Boar ? 90f : 55f;
            a.BuildVisual();
            g.Members.Add(a);
            return a;
        }

        void BuildVisual()
        {
            Color c; Vector3 body; float headY, legH;
            switch (Species)
            {
                case Species.Deer: c = new Color(0.55f, 0.38f, 0.22f); body = new Vector3(0.5f, 0.7f, 0.55f); headY = 1.35f; legH = 0.75f; break;
                case Species.Boar: c = new Color(0.22f, 0.17f, 0.14f); body = new Vector3(0.6f, 0.6f, 0.65f); headY = 0.7f; legH = 0.35f; break;
                default: c = new Color(0.45f, 0.45f, 0.47f); body = new Vector3(0.38f, 0.55f, 0.42f); headY = 0.85f; legH = 0.45f; break;
            }
            var col = gameObject.AddComponent<BoxCollider>();
            col.size = new Vector3(0.8f, legH + body.x, 1.6f);
            col.center = new Vector3(0f, (legH + body.x) * 0.5f, 0f);

            var torso = Visuals.Part(PrimitiveType.Capsule, transform, new Vector3(0, legH + body.x * 0.4f, 0), body, c);
            torso.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Visuals.Part(PrimitiveType.Sphere, transform, new Vector3(0, headY, body.y + 0.15f), Vector3.one * 0.32f, c * 0.9f);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * 0.15f;
                float z = (i < 2 ? -1 : 1) * body.y * 0.55f;
                Visuals.Part(PrimitiveType.Cylinder, transform, new Vector3(x, legH * 0.5f, z), new Vector3(0.08f, legH * 0.5f, 0.08f), c * 0.8f);
            }
            if (Species == Species.Deer && Random.value < 0.5f)
            {
                Visuals.Part(PrimitiveType.Cylinder, transform, new Vector3(-0.12f, headY + 0.3f, body.y + 0.1f), new Vector3(0.04f, 0.25f, 0.04f), new Color(0.8f, 0.75f, 0.6f));
                Visuals.Part(PrimitiveType.Cylinder, transform, new Vector3(0.12f, headY + 0.3f, body.y + 0.1f), new Vector3(0.04f, 0.25f, 0.04f), new Color(0.8f, 0.75f, 0.6f));
            }
            if (Species == Species.Boar)
                Visuals.Part(PrimitiveType.Cube, transform, new Vector3(0, headY - 0.1f, body.y + 0.35f), new Vector3(0.25f, 0.06f, 0.06f), new Color(0.9f, 0.88f, 0.8f));
            if (Species == Species.Wolf)
            {
                var tail = Visuals.Part(PrimitiveType.Capsule, transform, new Vector3(0, legH + 0.2f, -body.y - 0.1f), new Vector3(0.12f, 0.3f, 0.12f), c * 0.85f);
                tail.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);
            }
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        // ---------- Events from outside ----------

        public void Startle(Vector3 from)
        {
            if (Species == Species.Boar && Random.value < 0.25f) return; // boars hold their ground sometimes
            if (Group != null)
                foreach (var m in Group.Members)
                    if (m != null && m.Alive && (m.Position - Position).sqrMagnitude < 400f) m.BeginFlee(from, Random.Range(6f, 10f));
            BeginFlee(from, Random.Range(6f, 10f));
        }

        void BeginFlee(Vector3 from, float seconds)
        {
            if (state == State.Retreat) return;
            state = State.Flee;
            fleeFrom = from;
            stateTimer = seconds;
        }

        public void OnConfronted(Person p)
        {
            if (!IsPredator || Group == null) return;
            int fighters = 0;
            foreach (var o in Tribe.Instance.People)
                if (o.Job == Person.JobKind.Fight && (o.Position - Position).sqrMagnitude < 15f * 15f) fighters++;
            if (fighters > Group.AliveCount || Random.value < 0.15f)
            {
                foreach (var m in Group.Members) if (m != null && m.Alive) m.BeginFlee(p.Position, 8f);
                Group.ClearTarget();
            }
        }

        /// <summary>Returns true if this killed the animal.</summary>
        public bool TakeDamage(float dmg, Person by)
        {
            if (!Alive) return false;
            Health -= dmg;
            if (Health <= 0f) { Die(); return true; }
            if (Species == Species.Boar && by != null) { Enraged = true; enragedAt = by; state = State.Chase; stateTimer = 12f; }
            else if (IsPredator && Health < MaxHealth * 0.4f) { state = State.Retreat; stateTimer = 30f; }
            else if (!IsPredator) Startle(by != null ? by.Position : Position);
            return false;
        }

        void Die()
        {
            Alive = false;
            if (Group != null) Group.Members.Remove(this);
            Destroy(gameObject);
        }

        // ---------- Update ----------

        void Update()
        {
            if (!Alive || GameClock.Instance == null) return;
            stateTimer -= Time.deltaTime;
            senseTimer -= Time.deltaTime;
            attackTimer -= Time.deltaTime;

            if (IsPredator) UpdatePredator();
            else UpdatePrey();
        }

        bool Move(Vector3 target, float speed, float stop)
        {
            Vector3 pos = Position;
            Vector3 flat = new Vector3(target.x - pos.x, 0f, target.z - pos.z);
            float d = flat.magnitude;
            if (d <= stop) return true;
            Vector3 next = pos + flat / d * Mathf.Min(d, speed * Time.deltaTime);
            next = W.ClampToBounds(next);
            if (!W.IsLand(next, 0f) && W.IsLand(pos, 0f)) return true; // animals won't step into water
            next.y = W.HeightAt(next);
            transform.position = next;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat), 8f * Time.deltaTime);
            return false;
        }

        void Wander(float radius)
        {
            if (Group != null)
            {
                Group.WanderTimer -= Time.deltaTime / Mathf.Max(1, Group.Members.Count);
                if (Group.WanderTimer <= 0f)
                {
                    Group.WanderTimer = Random.Range(20f, 50f);
                    Group.Center = W.RandomLandPointNear(Group.Home, radius);
                }
            }
            if (stateTimer <= 0f)
            {
                Vector3 c = Group != null ? Group.Center : Position;
                dest = W.RandomLandPointNear(c, 6f);
                stateTimer = Random.Range(4f, 10f);
            }
            Move(dest, WalkSpeed * 0.6f, 0.4f);
        }

        void UpdatePrey()
        {
            if (state == State.Chase && Enraged)
            {
                if (enragedAt == null || !enragedAt.Alive || stateTimer <= 0f) { Enraged = false; state = State.Wander; return; }
                if (Move(enragedAt.Position, RunSpeed, 1.4f) && attackTimer <= 0f)
                {
                    attackTimer = 1.4f;
                    enragedAt.Damage(14f, "a boar", this);
                }
                return;
            }

            if (state == State.Flee)
            {
                Vector3 away = Position - fleeFrom; away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
                Move(Position + away.normalized * 10f, RunSpeed, 0.1f);
                if (stateTimer <= 0f)
                {
                    state = State.Wander;
                    if (Group != null) Group.Center = Position;
                }
                return;
            }

            // Sense danger: people walking openly and wolves.
            if (senseTimer <= 0f)
            {
                senseTimer = 0.5f;
                foreach (var p in Tribe.Instance.People)
                {
                    float r = p.Job == Person.JobKind.Hunt ? 0f : (Species == Species.Boar ? 4f : 8f);
                    if (r > 0f && (p.Position - Position).sqrMagnitude < r * r) { Startle(p.Position); return; }
                }
                foreach (var a in All)
                {
                    if (!a.IsPredator || !a.Alive) continue;
                    if ((a.Position - Position).sqrMagnitude < 14f * 14f) { Startle(a.Position); return; }
                }
            }
            Wander(45f);
        }

        void UpdatePredator()
        {
            var clock = GameClock.Instance;

            if (state == State.Retreat)
            {
                if (Move(Group.Home, RunSpeed * 0.8f, 2f) || stateTimer <= 0f)
                {
                    state = State.Wander;
                    Health = Mathf.Min(MaxHealth, Health + MaxHealth * 0.3f);
                }
                return;
            }

            // Fire scares wolves away.
            var fire = Building.NearestLitFire(Position, Building.FireScareRadius);
            if (fire != null)
            {
                BeginFlee(fire.transform.position, 5f);
                if (Group.TargetPerson != null && Building.NearestLitFire(Group.TargetPerson.Position, Building.FireScareRadius) != null)
                    Group.ClearTarget();
            }

            if (state == State.Flee)
            {
                Vector3 away = Position - fleeFrom; away.y = 0f;
                if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
                Move(Position + away.normalized * 10f, RunSpeed, 0.1f);
                if (stateTimer <= 0f) state = State.Wander;
                return;
            }

            if (Group.HasTarget)
            {
                state = State.Chase;
                Vector3 t = Group.TargetPos;
                if (Move(t, RunSpeed, 1.3f) && attackTimer <= 0f)
                {
                    attackTimer = 1.8f;
                    if (Group.TargetPerson != null && Group.TargetPerson.Alive)
                    {
                        var victim = Group.TargetPerson;
                        victim.Damage(6f, "a wolf attack", this);
                        if (!victim.Alive) { Group.Hunger = 0f; Group.ClearTarget(); }
                    }
                    else if (Group.TargetAnimal != null && Group.TargetAnimal.Alive)
                    {
                        if (Group.TargetAnimal.TakeDamage(20f, null)) { Group.Hunger = 0f; Group.ClearTarget(); }
                    }
                }
                // Give up a chase that runs too far from home.
                if ((t - Group.Home).sqrMagnitude > 320f * 320f) Group.ClearTarget();
                return;
            }

            state = State.Wander;
            // Packs range further at night, and much further when winter makes them hungry.
            float range = clock.Season == Season.Winter ? 170f : clock.IsNight ? 110f : 50f;
            if (Group.Hunger > 70f) range += 50f;
            Wander(range);
        }
    }
}
