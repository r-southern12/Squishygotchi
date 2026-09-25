using Squishy.Runtime.Room;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace Squishy.Runtime.CameraRig
{
    /// <summary>
    /// Home camera. Follows the squishy close up; zoom (pinch / scroll) blends out to the whole room.
    /// Drag turns (with inertia) and tilts. Tab or the HUD's Room button toggles follow / whole room; taps are passed on.
    /// Distances are fitted to the screen width, so portrait phones of any shape frame the same.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class FollowCamera : MonoBehaviour
    {
        [Header("Framing")]
        [Tooltip("Half-width of the view around the squishy at zoom 0, as multiples of its radius, plus a margin.")]
        [SerializeField] private float followMargin = 0.45f;
        [SerializeField] private float followRadiusFactor = 2.5f;
        [SerializeField] private float wholeRoomFactor = 1.12f;
        [SerializeField] private float followElevation = 42f;
        [SerializeField] private float wholeRoomElevation = 56f;
        [SerializeField] private Vector2 tiltOffsetRange = new Vector2(-18f, 28f);

        [Header("Feel")]
        [SerializeField] private float turnPerPixel = 0.5f;
        [SerializeField] private float tiltPerPixel = 0.15f;
        [SerializeField] private float inertiaDecay = 0.02f;
        [SerializeField] private float pinchPixelsForFullZoom = 500f;
        [SerializeField] private float scrollStep = 0.1f;
        [SerializeField] private float zoomSmoothing = 4f;
        [SerializeField] private float followSmoothing = 3f;

        private Camera _camera;
        private SteamerRoom _room;
        private Transform _target;
        private float _targetRadius;

        private float _yaw = 180f, _yawVelocity, _tiltOffset;
        private float _zoom, _zoomGoal;
        private Vector3 _focus;
        private bool _dragging;
        private float _pinchStart, _zoomAtPinchStart;

        public void Init(SteamerRoom room, Transform target, float targetRadius)
        {
            _camera = GetComponent<Camera>();
            _room = room;
            _target = target;
            _targetRadius = targetRadius;
            _focus = FocusPoint();
            Apply(1f);
        }

        private void OnEnable() { EnhancedTouchSupport.Enable(); }
        private void OnDisable() { EnhancedTouchSupport.Disable(); }

        private void LateUpdate()
        {
            if (_room == null) return;
            float dt = Time.deltaTime;
            HandleInput(dt);

            if (!_dragging)
            {
                _yawVelocity *= Mathf.Pow(inertiaDecay, dt);
                _yaw += _yawVelocity * dt;
            }
            _zoom = Mathf.Lerp(_zoom, _zoomGoal, Mathf.Min(1f, dt * zoomSmoothing));
            Apply(Mathf.Min(1f, dt * followSmoothing));
            _room.UpdateCutaway(transform.position, dt);
        }

        private void Apply(float followLerp)
        {
            _focus = Vector3.Lerp(_focus, FocusPoint(), followLerp);

            float nearHalf = followMargin + _targetRadius * followRadiusFactor;
            float half = Mathf.Lerp(nearHalf, _room.Radius * wholeRoomFactor, _zoom);
            float elevation = Mathf.Lerp(followElevation, wholeRoomElevation, _zoom) + _tiltOffset;
            float distance = FitDistance(half);

            Quaternion rot = Quaternion.Euler(elevation, _yaw, 0f);
            transform.position = _focus - rot * Vector3.forward * distance;
            transform.rotation = rot;
        }

        private Vector3 FocusPoint()
        {
            Vector3 squishy = _target != null ? _target.position + Vector3.up * _targetRadius : Vector3.zero;
            Vector3 centre = _room.transform.position + Vector3.up * 0.3f;
            // Follow on the floor plane only, so hops don't bob the camera.
            squishy.y = _room.FloorY + _targetRadius;
            return Vector3.Lerp(squishy, centre, _zoom);
        }

        /// <summary>Distance at which a width of 2 * halfWidth fills the screen horizontally.</summary>
        private float FitDistance(float halfWidth)
        {
            float vFov = _camera.fieldOfView * Mathf.Deg2Rad;
            float hFov = 2f * Mathf.Atan(Mathf.Tan(vFov * 0.5f) * _camera.aspect);
            return halfWidth / Mathf.Tan(hFov * 0.5f);
        }

        private void HandleInput(float dt)
        {
            var touches = Touch.activeTouches;
            if (touches.Count >= 2)
            {
                float span = Vector2.Distance(touches[0].screenPosition, touches[1].screenPosition);
                if (touches[1].began || _pinchStart <= 0f) { _pinchStart = span; _zoomAtPinchStart = _zoomGoal; }
                _zoomGoal = Mathf.Clamp01(_zoomAtPinchStart - (span - _pinchStart) / pinchPixelsForFullZoom);
                _dragging = false;
                return;
            }
            _pinchStart = 0f;

            Vector2 delta, position;
            bool pressed, began, ended;
            int touchId = -1;
            if (touches.Count == 1)
            {
                var t = touches[0];
                touchId = t.touchId;
                position = t.screenPosition;
                delta = t.delta;
                began = t.began;
                ended = t.ended;
                pressed = !ended;
            }
            else
            {
                var mouse = Mouse.current;
                if (mouse == null) return;
                position = mouse.position.ReadValue();
                delta = mouse.delta.ReadValue();
                began = mouse.leftButton.wasPressedThisFrame;
                ended = mouse.leftButton.wasReleasedThisFrame;
                pressed = mouse.leftButton.isPressed;
                float scroll = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f && !PointerOverUi(-1)) _zoomGoal = Mathf.Clamp01(_zoomGoal - Mathf.Sign(scroll) * scrollStep);
            }

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.tabKey.wasPressedThisFrame) ToggleWholeRoom();

            if (began)
            {
                _pressOverUi = PointerOverUi(touchId);
                _pressPosition = position;
                _pressTime = Time.unscaledTime;
                _moved = false;
            }
            if (_pressOverUi)
            {
                _dragging = false;
                return;
            }

            // Only turn once the finger has really moved, so taps don't nudge the camera.
            if (pressed && (position - _pressPosition).magnitude > tapSlopPixels) _moved = true;
            _dragging = pressed && _moved;

            // Screen-size independent: treat drag deltas as fractions of a 1080-wide screen.
            float scale = 1080f / Mathf.Max(1f, Screen.width);
            if (_dragging && dt > 0f)
            {
                _yawVelocity = delta.x * scale * turnPerPixel / dt;
                _yaw += delta.x * scale * turnPerPixel;
                _tiltOffset = Mathf.Clamp(_tiltOffset - delta.y * scale * tiltPerPixel, tiltOffsetRange.x, tiltOffsetRange.y);
            }

            if (ended && !_moved && Time.unscaledTime - _pressTime < tapMaxSeconds && Tapped != null) Tapped(position);
        }

        private static bool PointerOverUi(int touchId)
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return touchId >= 0 ? es.IsPointerOverGameObject(touchId) : es.IsPointerOverGameObject();
        }

        /// <summary>A quick press and release without dragging, in screen pixels.</summary>
        public event System.Action<Vector2> Tapped;

        [Header("Taps")]
        [SerializeField] private float tapSlopPixels = 14f;
        [SerializeField] private float tapMaxSeconds = 0.4f;
        private bool _pressOverUi, _moved;
        private Vector2 _pressPosition;
        private float _pressTime;

        public void ToggleWholeRoom()
        {
            _zoomGoal = _zoomGoal > 0.5f ? 0f : 1f;
        }
    }
}