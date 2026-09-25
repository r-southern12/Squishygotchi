using Squishy.Runtime.Rendering;
using UnityEngine;

namespace Squishy.Runtime.SquishyPet
{
    /// <summary>
    /// The squishy's look: dumpling mesh, face, finish material and a squash-and-stretch spring.
    /// Movement lives in <see cref="SquishyWander"/>; this only draws.
    /// </summary>
    public sealed class SquishyBody : MonoBehaviour
    {
        [SerializeField] private Material bodyMaterial;
        [SerializeField] private Material faceMaterial;
        [Tooltip("Body radius at Mini size. Other sizes multiply this by the size tier's relative scale.")]
        [SerializeField] private float miniRadius = 0.18f;

        [Header("Squash spring")]
        [SerializeField] private float stiffness = 180f;
        [SerializeField] private float damping = 9f;
        [SerializeField] private float maxSquash = 0.35f;

        private Transform _visual;
        private Material _instanceMaterial;
        private float _squash, _squashVelocity;

        public float Radius { get; private set; }

        public void Build(float relativeScale, Color color, float smoothness)
        {
            if (_visual != null) Destroy(_visual.gameObject);
            Radius = miniRadius * relativeScale;

            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);

            _instanceMaterial = new Material(bodyMaterial);
            _instanceMaterial.SetColor("_BaseColor", color);
            _instanceMaterial.SetFloat("_Smoothness", smoothness);

            var body = new GameObject("Body");
            body.transform.SetParent(_visual, false);
            body.transform.localScale = Vector3.one * Radius;
            body.AddComponent<MeshFilter>().sharedMesh = MeshKit.Dumpling();
            body.AddComponent<MeshRenderer>().sharedMaterial = _instanceMaterial;

            // Placeholder face: two eyes on the front (+Z). Character design is still an open question.
            var eyeMesh = MeshKit.RoundedBox(Vector3.one, 0.5f, 4);
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = new GameObject("Eye");
                eye.transform.SetParent(body.transform, false);
                eye.transform.localPosition = new Vector3(side * 0.3f, 0.62f, 0.92f);
                eye.transform.localScale = new Vector3(0.12f, 0.16f, 0.08f);
                eye.AddComponent<MeshFilter>().sharedMesh = eyeMesh;
                var r = eye.AddComponent<MeshRenderer>();
                r.sharedMaterial = faceMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        /// <summary>Kick the spring. Positive squashes (landing), negative stretches (take-off).</summary>
        public void Impulse(float amount)
        {
            _squashVelocity += amount;
        }

        private void LateUpdate()
        {
            if (_visual == null) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 20f);
            _squashVelocity += (-stiffness * _squash - damping * _squashVelocity) * dt;
            _squash = Mathf.Clamp(_squash + _squashVelocity * dt, -maxSquash, maxSquash);

            // Keep volume roughly constant: flatter means wider.
            float y = 1f - _squash;
            float xz = 1f / Mathf.Sqrt(Mathf.Max(0.2f, y));
            _visual.localScale = new Vector3(xz, y, xz);
        }
    }
}
