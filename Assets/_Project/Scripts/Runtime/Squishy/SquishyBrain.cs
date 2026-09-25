using System.Collections.Generic;
using Squishy.Data;
using Squishy.Runtime.Room;
using Squishy.Simulation.Care;
using Squishy.Simulation.Content;
using UnityEngine;

namespace Squishy.Runtime.SquishyPet
{
    /// <summary>
    /// What the squishy does: idles, wanders, looks after itself (never above the autonomy cap),
    /// and goes to stations when the player asks. Needs are changed only through <see cref="CareSim"/>.
    /// </summary>
    [RequireComponent(typeof(SquishyBody))]
    public sealed class SquishyBrain : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float speed = 0.7f;
        [SerializeField] private float hopHeight = 0.16f;
        [Tooltip("Floor distance per hop at Mini size; bigger squishies take longer hops.")]
        [SerializeField] private float hopLength = 0.22f;
        [SerializeField] private float bigHopHeight = 0.4f;
        [SerializeField] private float turnSpeed = 10f;
        [SerializeField] private float landingSquash = 2.2f;

        [Header("Idle")]
        [SerializeField] private Vector2 idleSeconds = new Vector2(2f, 4.5f);
        [SerializeField, Range(0f, 1f)] private float wanderChance = 0.65f;
        [SerializeField] private float warnEverySeconds = 6f;

        private enum Mode { Idle, Walk, Act, Dead }

        private struct Waypoint
        {
            public Vector3 pos;
            public float height;
            public bool big;
        }

        private SquishyBody _body;
        private SteamerRoom _room;
        private List<RoomItem> _items;
        private CareAsset _careData;
        private CareState _care;
        private System.Func<int> _snackCount;
        private System.Action _useSnack;
        private SquishySize _size;
        private float _scale = 1f;

        private Mode _mode = Mode.Idle;
        private Vector3 _pos;
        private float _height;
        private float _idleTimer, _warnTimer;
        private readonly Queue<Waypoint> _path = new Queue<Waypoint>();
        private bool _hasSeg;
        private Vector3 _segFrom, _segTo;
        private float _segFromH, _segToH, _segLen, _segDist, _hopPhase;
        private bool _segBig;
        private Vector3? _perchExit;

        private ActivityDef _act;
        private RoomItem _actItem;
        private bool _byPlayer;
        private float _actTime;
        private Vector3 _facePoint;
        private bool _hasFace;

        private string _status;
        private bool _statusUrgent;
        private float _statusUntil;

        /// <summary>Speech bubble text, or null.</summary>
        public string Status { get { return Time.time < _statusUntil ? _status : null; } }
        public bool StatusUrgent { get { return _statusUrgent; } }
        public ActivityDef CurrentActivity { get { return _mode == Mode.Act ? _act : null; } }
        /// <summary>Raised when an activity finishes, with whether the player asked for it.</summary>
        public event System.Action<ActivityDef, RoomItem, bool> ActivityFinished;

        public void Init(SteamerRoom room, List<RoomItem> items, CareAsset careData, CareState care,
            SquishySize size, float relativeScale, System.Func<int> snackCount, System.Action useSnack)
        {
            _body = GetComponent<SquishyBody>();
            _room = room;
            _items = items;
            _careData = careData;
            _care = care;
            _size = size;
            _scale = relativeScale;
            _snackCount = snackCount;
            _useSnack = useSnack;
            _pos = FreeSpot(0.1f);
            _height = 0f;
            _mode = care.dead ? Mode.Dead : Mode.Idle;
            _path.Clear();
            _hasSeg = false;
            ClearActivity();
            _idleTimer = 1f;
            transform.position = new Vector3(_pos.x, _room.FloorY, _pos.z);
        }

        // ---- Player requests -------------------------------------------------------

        /// <summary>The player tapped an item. Returns false (and says why) if it can't be used.</summary>
        public bool RequestUse(RoomItem item)
        {
            if (_mode == Mode.Dead) return false;
            if (item.Activity == null)
            {
                Say(item.Type.kind == ItemKind.Decor ? "Decor · +" + item.Type.comfort + " comfort" : item.Type.displayName, false, 1.6f);
                return false;
            }
            string reason = WhyNot(item);
            if (reason != null) { Say(reason, true, 2f); return false; }
            StartUse(item, true);
            return true;
        }

        // ---- Update -----------------------------------------------------------------

        private void Update()
        {
            if (_room == null) return;
            float dt = Time.deltaTime;

            if (_care.dead && _mode != Mode.Dead)
            {
                _mode = Mode.Dead;
                _path.Clear();
                _hasSeg = false;
                _body.SetSleeping(true);
                Say(null, false, 0f);
            }

            float condition = CareSim.Condition(_care);
            float droop = _mode == Mode.Dead ? 1f : Mathf.InverseLerp(_careData.care.happyAbove, 0.08f, condition);
            float grey = _mode == Mode.Dead ? 1f : Mathf.InverseLerp(_careData.care.droopyAbove, 0.03f, condition) * 0.85f;
            _body.SetCondition(droop, grey);
            float slow = 1f - droop * 0.55f;
            float lift = 0f;

            switch (_mode)
            {
                case Mode.Idle:
                    _idleTimer -= dt;
                    if (_idleTimer <= 0f) Decide();
                    _warnTimer -= dt;
                    if (_warnTimer <= 0f)
                    {
                        _warnTimer = warnEverySeconds;
                        var low = CareSim.Lowest(_care);
                        if (_care.Get(low) < _careData.care.warnBelow) Say(NeedWord(low), true, 2.2f);
                    }
                    FaceTowards(Camera.main != null ? Camera.main.transform.position : _pos + Vector3.back, dt, 3f);
                    break;
                case Mode.Walk:
                    lift = StepWalk(dt * slow);
                    break;
                case Mode.Act:
                    StepAct(dt);
                    break;
            }

            transform.position = new Vector3(_pos.x, _room.FloorY + _height + lift, _pos.z);
        }

        private void Decide()
        {
            var def = _careData.care;
            if (!CareSim.CanSelfCare(_care, def))
            {
                Say("Help…", true, 4f);
                _idleTimer = 4f;
                return;
            }
            var low = CareSim.Lowest(_care);
            if (_care.Get(low) < def.selfCareBelow && Random.value < def.selfCareChance)
            {
                SelfCare(low);
                return;
            }
            if (Random.value < wanderChance) Wander();
            else _idleTimer = Random.Range(idleSeconds.x, idleSeconds.y);
        }

        private void SelfCare(NeedKind need)
        {
            RoomItem best = null;
            float bestDist = float.MaxValue;
            foreach (var item in _items)
            {
                var a = item.Activity;
                if (a == null || !a.selfCare || !a.fillsNeed || a.need != need || WhyNot(item) != null) continue;
                float d = Vector3.Distance(item.transform.position, _pos);
                if (d < bestDist) { bestDist = d; best = item; }
            }
            if (best != null) { StartUse(best, false); return; }

            // Nothing suitable: make do on the spot.
            var def = _careData.care;
            var makeDo = new ActivityDef
            {
                id = "make_do", label = MakeDoLabel(need), need = need, fillsNeed = true,
                seconds = def.makeDoSeconds, ratePerSecond = def.makeDoRate, selfCare = true,
            };
            BeginAct(makeDo, null, false);
        }

        private void Wander()
        {
            ClearActivity();
            PlanPath(FreeSpot(1.4f), _pos, 0f, null);
            _mode = Mode.Walk;
        }

        // ---- Using items ------------------------------------------------------------

        private string WhyNot(RoomItem item)
        {
            var a = item.Activity;
            if (item.Type.minSquishySize > _size) return "Needs " + item.Type.minSquishySize + " size";
            if (a.needsSeatNearby && SeatNear(item) == null) return "Needs a seat nearby";
            if (a.usesSnack && _snackCount() <= 0) return "No snacks";
            return null;
        }

        private RoomItem SeatNear(RoomItem table)
        {
            RoomItem best = null;
            float bestDist = 1.1f;
            foreach (var item in _items)
            {
                if (item == table || item.Activity == null) continue;
                if (item.Activity.id != "sit" && item.Activity.id != "lounge") continue;
                float d = Vector3.Distance(item.transform.position, table.transform.position);
                if (d < bestDist) { bestDist = d; best = item; }
            }
            return best;
        }

        private void StartUse(RoomItem item, bool byPlayer)
        {
            ClearActivity();
            _act = item.Activity;
            _actItem = item;
            _byPlayer = byPlayer;

            RoomItem standOn = item;
            if (_act.needsSeatNearby) standOn = SeatNear(item);

            Vector3 stand, approach, face;
            float h;
            standOn.UseSpot(_pos, _body.Radius, out stand, out approach, out h, out face);
            if (standOn != item) face = item.transform.position;
            _facePoint = face;
            _hasFace = true;

            PlanPath(stand, approach, h, standOn);
            _mode = Mode.Walk;
            Say(_act.label + (byPlayer ? "" : " (on its own)"), false, 60f);
        }

        private void BeginAct(ActivityDef act, RoomItem item, bool byPlayer)
        {
            _act = act;
            _actItem = item;
            _byPlayer = byPlayer;
            _actTime = 0f;
            _mode = Mode.Act;
            if (act.usesSnack) _useSnack();
            if (byPlayer && act.fillsNeed) CareSim.RecordPlayerCare(_care);
            _body.SetSleeping(act.isSleep);
            _body.Impulse(landingSquash);
            Say(act.label + (byPlayer ? "" : " (on its own)"), false, act.seconds + 0.5f);
        }

        private void StepAct(float dt)
        {
            _actTime += dt;
            if (_hasFace) FaceTowards(_facePoint, dt, 6f);

            if (_act.fillsNeed)
            {
                var def = _careData.care;
                float rate = _act.ratePerSecond;
                if (_act.isSleep && _byPlayer && AnyLampOn()) rate *= 0.5f;
                if (!_byPlayer) rate *= CareSim.SelfCareStrength(_care, def); // neglect wears self-care down
                float cap = CareSim.CapFor(_act, _byPlayer, def);
                CareSim.Fill(_care, _act.need, rate * dt, cap);
                if (_act.fillsAlso) CareSim.Fill(_care, _act.alsoNeed, rate * 0.5f * dt, cap);
            }

            if (_actTime >= _act.seconds) FinishAct();
        }

        private void FinishAct()
        {
            var act = _act;
            var item = _actItem;
            bool byPlayer = _byPlayer;
            _body.SetSleeping(false);

            if (item != null && act.id == "lamp") item.ToggleLamp();
            if (item != null && act.id == "play") item.Roll(FreeSpot(1.2f), _room.FloorY);
            if (act.fillsNeed) Say("+" + act.need + (byPlayer ? "" : " (half)"), false, 1.5f);

            ClearActivity();
            _mode = Mode.Idle;
            _idleTimer = Random.Range(idleSeconds.x, idleSeconds.y);
            if (_height > 0.02f) Wander(); // hop down off the bed / out of the bath

            if (ActivityFinished != null) ActivityFinished(act, item, byPlayer);
        }

        private void ClearActivity()
        {
            _act = null;
            _actItem = null;
            _hasFace = false;
            _body.SetSleeping(false);
        }

        private bool AnyLampOn()
        {
            foreach (var item in _items) if (item.Type.id == "lamp" && item.LampOn) return true;
            return false;
        }

        // ---- Walking ----------------------------------------------------------------

        private void PlanPath(Vector3 stand, Vector3 approach, float standHeight, RoomItem target)
        {
            _path.Clear();
            _hasSeg = false;
            Vector3 cursor = _pos;
            if (_height > 0.02f)
            {
                Vector3 exit = _perchExit ?? (_pos + transform.forward * 0.5f);
                _path.Enqueue(new Waypoint { pos = Clamp(exit), height = 0f, big = true });
                cursor = exit;
            }

            Vector3 goal = standHeight > 0f ? approach : stand;
            var d1 = Detour(cursor, goal, target);
            if (d1.HasValue)
            {
                _path.Enqueue(new Waypoint { pos = d1.Value });
                var d2 = Detour(d1.Value, goal, target);
                if (d2.HasValue) _path.Enqueue(new Waypoint { pos = d2.Value });
            }
            if (standHeight > 0f)
            {
                _path.Enqueue(new Waypoint { pos = Clamp(approach) });
                _path.Enqueue(new Waypoint { pos = stand, height = standHeight, big = true });
                _perchExit = Clamp(approach);
            }
            else
            {
                _path.Enqueue(new Waypoint { pos = Clamp(stand) });
                _perchExit = null;
            }
        }

        private float StepWalk(float dt)
        {
            if (!_hasSeg)
            {
                if (_path.Count == 0) { ArriveAtEnd(); return 0f; }
                var w = _path.Dequeue();
                _segFrom = _pos;
                _segTo = w.pos;
                _segFromH = _height;
                _segToH = w.height;
                _segBig = w.big;
                _segLen = Mathf.Max(0.001f, Flat(_segTo - _segFrom).magnitude);
                _segDist = 0f;
                _hasSeg = true;
                if (_segBig) _body.Impulse(-1.5f);
            }

            float step = speed * (0.8f + _scale * 0.2f) * dt;
            _segDist = Mathf.Min(_segLen, _segDist + step);
            float u = _segDist / _segLen;
            _pos = Vector3.Lerp(_segFrom, _segTo, u);
            _height = Mathf.Lerp(_segFromH, _segToH, u);

            Vector3 dir = Flat(_segTo - _segFrom);
            if (dir.sqrMagnitude > 1e-6f) FaceTowards(_pos + dir, dt, turnSpeed);

            float lift;
            if (_segBig) lift = bigHopHeight * Mathf.Sin(Mathf.PI * u);
            else
            {
                float prev = _hopPhase;
                _hopPhase += step / (hopLength * (0.8f + _scale * 0.4f));
                lift = hopHeight * Mathf.Sqrt(_scale) * Mathf.Abs(Mathf.Sin(Mathf.PI * _hopPhase));
                if (Mathf.Floor(_hopPhase) > Mathf.Floor(prev)) _body.Impulse(landingSquash);
            }

            if (u >= 1f)
            {
                _hasSeg = false;
                if (_segBig) _body.Impulse(landingSquash * 1.4f);
            }
            return lift;
        }

        private void ArriveAtEnd()
        {
            _hopPhase = 0f;
            _body.Impulse(landingSquash);
            if (_act != null) BeginAct(_act, _actItem, _byPlayer);
            else
            {
                _mode = Mode.Idle;
                _idleTimer = Random.Range(idleSeconds.x, idleSeconds.y);
            }
        }

        /// <summary>A waypoint beside the first item blocking the straight line, or null.</summary>
        private Vector3? Detour(Vector3 a, Vector3 b, RoomItem skip)
        {
            Vector3 ab = Flat(b - a);
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-6f) return null;
            foreach (var item in _items)
            {
                if (item == skip || item == _actItem || !item.Blocks) continue;
                Vector3 o = item.transform.position;
                float t = Vector3.Dot(Flat(o - a), ab) / len2;
                if (t < 0.03f || t > 0.97f) continue;
                Vector3 closest = a + ab * t;
                float clear = item.Radius + _body.Radius + 0.04f;
                if (Flat(closest - o).sqrMagnitude >= clear * clear) continue;

                Vector3 n = new Vector3(-ab.z, 0f, ab.x).normalized;
                float side = Vector3.Dot(Flat(o - a), n) > 0f ? -1f : 1f;
                float off = item.Radius + _body.Radius + 0.18f;
                Vector3 w = o + n * off * side;
                if (Flat(w).magnitude > _room.WalkRadius - _body.Radius) w = o - n * off * side;
                return Clamp(w);
            }
            return null;
        }

        private Vector3 FreeSpot(float maxRadius)
        {
            float limit = Mathf.Min(maxRadius, _room.WalkRadius - _body.Radius);
            Vector3 p = Vector3.zero;
            for (int k = 0; k < 12; k++)
            {
                Vector2 r = Random.insideUnitCircle * limit;
                p = new Vector3(r.x, 0f, r.y);
                bool clear = true;
                if (_items != null)
                    foreach (var item in _items)
                        if (item.Blocks && Flat(item.transform.position - p).magnitude < item.Radius + _body.Radius + 0.1f) { clear = false; break; }
                if (clear) break;
            }
            return p;
        }

        private Vector3 Clamp(Vector3 p)
        {
            p.y = 0f;
            float limit = _room.WalkRadius - _body.Radius;
            return p.magnitude > limit ? p.normalized * limit : p;
        }

        private void FaceTowards(Vector3 point, float dt, float rate)
        {
            Vector3 dir = Flat(point - _pos);
            if (dir.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), Mathf.Min(1f, dt * rate));
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        // ---- Talking ----------------------------------------------------------------

        private void Say(string text, bool urgent, float seconds)
        {
            _status = text;
            _statusUrgent = urgent;
            _statusUntil = text == null ? 0f : Time.time + seconds;
        }

        private static string NeedWord(NeedKind need)
        {
            switch (need)
            {
                case NeedKind.Hunger: return "Hungry!";
                case NeedKind.Play: return "Bored!";
                case NeedKind.Rest: return "Sleepy!";
                default: return "Grubby!";
            }
        }

        private static string MakeDoLabel(NeedKind need)
        {
            switch (need)
            {
                case NeedKind.Hunger: return "Nibbling crumbs";
                case NeedKind.Play: return "Wiggling";
                case NeedKind.Rest: return "Dozing";
                default: return "Grooming";
            }
        }
    }
}
