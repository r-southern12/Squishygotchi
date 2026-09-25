using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Squishy.Runtime.Three
{
    /// <summary>
    /// three.js-style scene building: groups and meshes placed with the prototype's literal numbers.
    /// Everything lives under the mirrored world root, so positions, radians and scales copy across unchanged.
    /// </summary>
    public static class Node
    {
        private static readonly Dictionary<Material, Material> NoReceive = new Dictionary<Material, Material>();

        public static Transform Group(Transform parent, string name = "g", float x = 0, float y = 0, float z = 0)
        {
            var t = new GameObject(name).transform;
            if (parent != null) t.SetParent(parent, false);
            t.localPosition = new Vector3(x, y, z);
            return t;
        }

        /// <summary>mesh(geo, mat, x, y, z, shadow): shadow=false turns off casting and receiving, as in three.js.</summary>
        public static Transform Mesh(Transform parent, Mesh geo, Material mat, float x = 0, float y = 0, float z = 0, bool shadow = true, bool? receive = null)
        {
            var go = new GameObject(geo.name);
            var t = go.transform;
            if (parent != null) t.SetParent(parent, false);
            t.localPosition = new Vector3(x, y, z);
            go.AddComponent<MeshFilter>().sharedMesh = geo;
            var r = go.AddComponent<MeshRenderer>();
            r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.sharedMaterial = (receive ?? shadow) ? mat : Unreceiving(mat);
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return t;
        }

        public static Material Unreceiving(Material mat)
        {
            if (NoReceive.TryGetValue(mat, out var m)) return m;
            m = new Material(mat) { name = mat.name + " (no shadows)" };
            m.SetFloat("_ReceiveShadows", 0f);
            return NoReceive[mat] = m;
        }

        /// <summary>three.js Euler (radians, order XYZ).</summary>
        public static void Rot(Transform t, float x, float y, float z)
        {
            t.localRotation = Quaternion.AngleAxis(x * Mathf.Rad2Deg, Vector3.right) * Quaternion.AngleAxis(y * Mathf.Rad2Deg, Vector3.up) * Quaternion.AngleAxis(z * Mathf.Rad2Deg, Vector3.forward);
        }

        public static Transform RotX(this Transform t, float a) { Rot(t, a, 0, 0); return t; }
        public static Transform RotY(this Transform t, float a) { Rot(t, 0, a, 0); return t; }
        public static Transform RotZ(this Transform t, float a) { Rot(t, 0, 0, a); return t; }
        public static Transform Scale(this Transform t, float x, float y, float z) { t.localScale = new Vector3(x, y, z); return t; }
        public static Transform ScaleY(this Transform t, float y) { t.localScale = new Vector3(1, y, 1); return t; }

        /// <summary>Box3.setFromObject: bounds of every mesh under root, in the space of <paramref name="space"/>.</summary>
        public static Bounds LocalBounds(Transform root, Transform space)
        {
            bool any = false;
            var b = new Bounds();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
                var mb = mf.sharedMesh.bounds;
                var m = space.worldToLocalMatrix * mf.transform.localToWorldMatrix;
                for (int k = 0; k < 8; k++)
                {
                    var p = m.MultiplyPoint3x4(mb.center + Vector3.Scale(mb.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1)));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        public static void Destroy(Transform t)
        {
            if (t == null) return;
            if (Application.isPlaying) Object.Destroy(t.gameObject); else Object.DestroyImmediate(t.gameObject);
        }

        public static void SetLayer(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            foreach (Transform c in t) SetLayer(c, layer);
        }
    }
}
