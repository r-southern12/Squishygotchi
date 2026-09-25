using Squishy.Runtime.Room;
using UnityEngine;

namespace Squishy.Runtime.SquishyPet
{
    /// <summary>
    /// Idle behaviour for Milestone 2: pause, pick a spot on the floor, hop there, repeat.
    /// Milestone 3 replaces the choice of destination with needs and stations; the hop stays.
    /// </summary>
    [RequireComponent(typeof(SquishyBody))]
    public sealed class SquishyWander : MonoBehaviour
    {
        [SerializeField] private float speed = 0.7f;
        [SerializeField] private float hopHeight = 0.16f;
        [Tooltip("Floor distance per hop at Mini size; bigger squishies take longer hops.")]
        [SerializeField] private float hopLength = 0.22f;
        [SerializeField] private Vector2 pauseSeconds = new Vector2(0.8f, 3f);
        [SerializeField] private float turnSpeed = 10f;
        [SerializeField] private float landingSquash = 2.2f;

        private SquishyBody _body;
        private SteamerRoom _room;
        private Vector3 _floorPos, _target;
        private float _pause, _hopPhase, _scale = 1f;
        private bool _moving;

        public void Init(SteamerRoom room, float relativeScale)
        {
            _body = GetComponent<SquishyBody>();
            _room = room;
            _scale = relativeScale;
            _floorPos = new Vector3(0f, room.FloorY, 0f);
            transform.position = _floorPos;
            _pause = Random.Range(pauseSeconds.x, pauseSeconds.y);
        }

        private void Update()
        {
            if (_room == null) return;
            float dt = Time.deltaTime;
            float lift = 0f;

            if (!_moving)
            {
                _pause -= dt;
                if (_pause <= 0f) PickTarget();
            }
            else
            {
                Vector3 to = _target - _floorPos;
                to.y = 0f;
                float dist = to.magnitude;
                float step = Mathf.Min(dist, speed * (0.8f + _scale * 0.2f) * dt);
                if (dist > 1e-4f) _floorPos += to / dist * step;

                float prev = _hopPhase;
                _hopPhase += step / (hopLength * (0.8f + _scale * 0.4f));
                lift = hopHeight * Mathf.Sqrt(_scale) * Mathf.Abs(Mathf.Sin(Mathf.PI * _hopPhase));
                if (Mathf.Floor(_hopPhase) > Mathf.Floor(prev)) _body.Impulse(landingSquash);

                if (dist > 1e-4f)
                {
                    var look = Quaternion.LookRotation(to);
                    transform.rotation = Quaternion.Slerp(transform.rotation, look, Mathf.Min(1f, dt * turnSpeed));
                }

                if (dist - step <= 1e-3f)
                {
                    // Settle: finish on a landing, then pause.
                    _moving = false;
                    _hopPhase = 0f;
                    _body.Impulse(landingSquash);
                    _pause = Random.Range(pauseSeconds.x, pauseSeconds.y);
                }
            }

            transform.position = _floorPos + Vector3.up * lift;
        }

        private void PickTarget()
        {
            float limit = Mathf.Max(0.1f, _room.WalkRadius - _body.Radius);
            // Short trips feel more alive than crossing the whole room each time.
            for (int tries = 0; tries < 8; tries++)
            {
                Vector2 p = Random.insideUnitCircle * limit;
                var candidate = new Vector3(p.x, _room.FloorY, p.y);
                float d = Vector3.Distance(candidate, _floorPos);
                if (d > 0.3f && d < limit * 1.2f) { _target = candidate; _moving = true; return; }
            }
            _target = Vector3.zero + Vector3.up * _room.FloorY;
            _moving = true;
        }
    }
}
