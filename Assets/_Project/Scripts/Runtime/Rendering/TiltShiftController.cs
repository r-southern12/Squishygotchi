using UnityEngine;

namespace Squishy.Runtime.Rendering
{
    /// <summary>
    /// Keeps the tilt-shift focus band on the target (the squishy) and fades flashes.
    /// Writes global shader values, so the material asset isn't modified while playing.
    /// </summary>
    public sealed class TiltShiftController : MonoBehaviour
    {
        private static readonly int FocusY = Shader.PropertyToID("_FocusY");
        private static readonly int FlashId = Shader.PropertyToID("_Flash");

        [SerializeField] private Camera targetCamera;
        [SerializeField] private float focusFollowSpeed = 6f;
        [SerializeField] private float flashFadeSpeed = 3f;

        private Transform _focus;
        private float _focusHeight;
        private float _focusY = 0.5f, _flash;

        public void SetFocus(Transform target, float heightOffset)
        {
            _focus = target;
            _focusHeight = heightOffset;
        }

        /// <summary>White flash, e.g. when a steamer lid blows off.</summary>
        public void Flash(float strength)
        {
            _flash = Mathf.Max(_flash, strength);
        }

        private void LateUpdate()
        {
            if (targetCamera == null) return;
            float goal = 0.5f;
            if (_focus != null)
            {
                Vector3 vp = targetCamera.WorldToViewportPoint(_focus.position + Vector3.up * _focusHeight);
                if (vp.z > 0f) goal = Mathf.Clamp01(vp.y);
            }
            _focusY = Mathf.Lerp(_focusY, goal, Mathf.Min(1f, Time.deltaTime * focusFollowSpeed));
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * flashFadeSpeed);
            Shader.SetGlobalFloat(FocusY, _focusY);
            Shader.SetGlobalFloat(FlashId, _flash);
        }

        private void OnDisable()
        {
            Shader.SetGlobalFloat(FlashId, 0f);
        }
    }
}
