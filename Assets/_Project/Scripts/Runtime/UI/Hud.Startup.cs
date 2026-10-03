using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;
using static Squishy.Runtime.UI.Css;

namespace Squishy.Runtime.UI
{
    /// <summary>
    /// The studio animation after the Unity splash (the user's video, 4 Oct 2026): silent, centred at most of the
    /// screen's width on a matching background, then it fades away. A tap skips it.
    /// </summary>
    public sealed partial class Hud
    {
        public void PlayStartupVideo()
        {
            var clip = Resources.Load<VideoClip>("Video/startup");
            if (clip == null || clip.width == 0) return;

            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = C("#F7F0E4");
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.pickingMode = PickingMode.Position; // the game underneath waits
            Root.Add(overlay);

            float w = Width * .8f, h = w * clip.height / clip.width;
            if (h > Height * .85f) { h = Height * .85f; w = h * clip.width / clip.height; }
            var pic = new VisualElement().Size(w, h).In(overlay);
            pic.pickingMode = PickingMode.Ignore;
            pic.style.opacity = 0; // until its first frame is in

            var rt = new RenderTexture((int)clip.width, (int)clip.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            pic.style.backgroundImage = Background.FromRenderTexture(rt);
            var go = new GameObject("Startup video");
            var vp = go.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.clip = clip;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.audioOutputMode = VideoAudioOutputMode.None; // never its sound
            vp.isLooping = false;
            vp.skipOnDrop = true;
            vp.timeUpdateMode = VideoTimeUpdateMode.UnscaledGameTime;
            vp.sendFrameReadyEvents = true;

            bool done = false;
            IVisualElementScheduledItem keepTop = null;
            void Finish()
            {
                if (done) return;
                done = true;
                keepTop?.Pause();
                Tw.Run(overlay, .4f, u => overlay.style.opacity = 1 - u);
                overlay.schedule.Execute(() =>
                {
                    overlay.RemoveFromHierarchy();
                    if (vp != null) Object.Destroy(go);
                    rt.Release();
                    Object.Destroy(rt);
                }).StartingIn(450);
            }
            vp.frameReady += (src, idx) =>
            {
                if (done || pic.style.opacity.value > 0) return;
                // The background takes the video's own edge colour, so it sits on the screen seamlessly.
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                px.ReadPixels(new Rect(4, rt.height - 5, 1, 1), 0, 0);
                px.Apply();
                RenderTexture.active = prev;
                var edge = px.GetPixel(0, 0);
                edge.a = 1;
                Object.Destroy(px);
                overlay.style.backgroundColor = edge;
                pic.style.opacity = 1;
                vp.sendFrameReadyEvents = false;
            };
            vp.loopPointReached += _ => Finish();
            vp.errorReceived += (_, __) => Finish();
            overlay.RegisterCallback<PointerDownEvent>(_ => Finish());
            // Stays on top of whatever the game shows underneath, and never holds things up if the video won't play.
            keepTop = overlay.schedule.Execute(() => overlay.BringToFront()).Every(100);
            overlay.schedule.Execute(Finish).StartingIn((long)((clip.length + 3) * 1000));
            vp.Play();
        }
    }
}
