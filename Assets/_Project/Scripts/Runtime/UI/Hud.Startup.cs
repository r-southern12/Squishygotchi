using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;
using static Squishy.Runtime.UI.Css;

namespace Squishy.Runtime.UI
{
    /// <summary>
    /// The studio animation after the Unity splash (the user's video, 4 Oct 2026): silent, centred at most of the
    /// screen's width, then it fades away. A tap skips it.
    /// It starts on the splash's own dark colour (there was a white flash while it loaded), only plays once it's ready
    /// (it lagged), and fits whole inside its frame (it was cropped). While it covers the screen the room isn't drawn
    /// (covering(true)), so the phone has nothing else to do.
    /// </summary>
    public sealed partial class Hud
    {
        private static readonly Color SplashDark = new Color(.1216f, .1216f, .1255f, 1); // Player Settings' splash background

        public void PlayStartupVideo(Action<bool> covering)
        {
            var clip = Resources.Load<VideoClip>("Video/startup");
            if (clip == null || clip.width == 0) return;

            var overlay = new VisualElement();
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.backgroundColor = SplashDark;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.pickingMode = PickingMode.Position; // the game underneath waits
            Root.Add(overlay);
            covering?.Invoke(true);

            float w = Width * .8f, h = w * clip.height / clip.width;
            if (h > Height * .85f) { h = Height * .85f; w = h * clip.width / clip.height; }
            var rt = new RenderTexture((int)clip.width, (int)clip.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            var pic = new Image { image = rt, scaleMode = ScaleMode.ScaleToFit }.Size(w, h).In(overlay); // the whole frame, never cropped
            pic.pickingMode = PickingMode.Ignore;
            pic.style.opacity = 0; // until its first frame is in

            var go = new GameObject("Startup video");
            var vp = go.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.clip = clip;
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.audioOutputMode = VideoAudioOutputMode.None; // never its sound
            vp.isLooping = false;
            vp.skipOnDrop = false; // every frame shown, not skipped to catch up
            vp.waitForFirstFrame = true;
            vp.sendFrameReadyEvents = true;

            bool done = false, shown = false;
            IVisualElementScheduledItem keepTop = null;
            void Finish()
            {
                if (done) return;
                done = true;
                keepTop?.Pause();
                covering?.Invoke(false);
                Tw.Run(overlay, .4f, u => overlay.style.opacity = 1 - u);
                overlay.schedule.Execute(() =>
                {
                    overlay.RemoveFromHierarchy();
                    if (go != null) UnityEngine.Object.Destroy(go);
                    rt.Release();
                    UnityEngine.Object.Destroy(rt);
                }).StartingIn(450);
            }
            vp.frameReady += (src, idx) =>
            {
                if (done || shown) return;
                shown = true;
                vp.sendFrameReadyEvents = false;
                // The background eases from the splash's dark to the video's own edge colour as the video fades in.
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var px = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                px.ReadPixels(new Rect(4, rt.height - 5, 1, 1), 0, 0);
                px.Apply();
                RenderTexture.active = prev;
                var edge = px.GetPixel(0, 0);
                edge.a = 1;
                UnityEngine.Object.Destroy(px);
                Tw.Run(pic, .35f, u =>
                {
                    pic.style.opacity = u;
                    overlay.style.backgroundColor = Color.Lerp(SplashDark, edge, u);
                });
            };
            vp.prepareCompleted += _ =>
            {
                // Give the game's first heavy frames a moment to pass before it starts, so it plays smoothly.
                overlay.schedule.Execute(() => { if (!done) vp.Play(); }).StartingIn(250);
            };
            vp.loopPointReached += _ => Finish();
            vp.errorReceived += (_, __) => Finish();
            overlay.RegisterCallback<PointerDownEvent>(_ => Finish());
            // Stays on top of whatever the game shows underneath, and never holds things up if the video won't play.
            keepTop = overlay.schedule.Execute(() => overlay.BringToFront()).Every(100);
            overlay.schedule.Execute(Finish).StartingIn((long)((clip.length + 4) * 1000));
            vp.Prepare();
        }
    }
}
