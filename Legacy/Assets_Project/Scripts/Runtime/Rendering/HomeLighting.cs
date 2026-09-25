using UnityEngine;
using UnityEngine.Rendering;

namespace Squishy.Runtime.Rendering
{
    /// <summary>
    /// The prototype's home lighting: warm hemisphere ambient, a strong warm key light with shadows
    /// from the front-left, a faint cool fill from behind-right, and a tan background.
    /// </summary>
    [System.Serializable]
    public class HomeLighting
    {
        public Color background = Hex("#CFB38C");
        public Color hemisphereSky = Hex("#FFF1DC");
        public Color hemisphereGround = Hex("#8A6445");
        public float hemisphereIntensity = 0.62f;
        public Color keyColor = Hex("#FFE6C8");
        public float keyIntensity = 1.9f;
        public Vector3 keyFrom = new Vector3(-5f, 11f, 6f);
        public Color fillColor = Hex("#D8E4FF");
        public float fillIntensity = 0.3f;
        public Vector3 fillFrom = new Vector3(6f, 5f, -3f);

        public void Apply(Camera camera)
        {
            if (camera != null)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = background;
            }

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Scale(hemisphereSky, hemisphereIntensity);
            RenderSettings.ambientEquatorColor = Scale(Color.Lerp(hemisphereSky, hemisphereGround, 0.5f), hemisphereIntensity);
            RenderSettings.ambientGroundColor = Scale(hemisphereGround, hemisphereIntensity);

            Light key = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude))
                if (l.type == LightType.Directional && l.name != "Fill") { key = l; break; }
            if (key == null) key = new GameObject("Sun").AddComponent<Light>();
            Setup(key, keyColor, keyIntensity, keyFrom, LightShadows.Soft);

            var fillGo = GameObject.Find("Fill");
            var fill = fillGo != null ? fillGo.GetComponent<Light>() : new GameObject("Fill").AddComponent<Light>();
            Setup(fill, fillColor, fillIntensity, fillFrom, LightShadows.None);
        }

        private static void Setup(Light light, Color color, float intensity, Vector3 from, LightShadows shadows)
        {
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.shadows = shadows;
            light.shadowStrength = 1f;
            light.transform.rotation = Quaternion.LookRotation(-from.normalized);
        }

        /// <summary>Scales a colour's brightness in linear space, as three.js does with light intensity.</summary>
        private static Color Scale(Color c, float k)
        {
            return (c.linear * k).gamma;
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
