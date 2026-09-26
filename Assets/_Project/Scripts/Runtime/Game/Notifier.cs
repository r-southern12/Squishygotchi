using System;
using System.Collections.Generic;
using System.IO;
using Squishy.Simulation.Game;
#if UNITY_ANDROID || UNITY_IOS
using Unity.Notifications;
#endif
#if UNITY_ANDROID
using Unity.Notifications.Android;
#endif
using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Cute, calm reminders while the app is closed, in the squishy's own voice with its picture (Android):
    /// a picture of it with a thought bubble of what it wants (low, run out, fading). No tasks or gifts.
    /// Never between 9 pm and 8 am (moved to the morning) and never closer than 3 hours apart, except the
    /// "fading" warning. Scheduled on leaving the app, cleared on return. Nothing leaves the phone.
    /// Also keeps the Android home-screen widget's data up to date.
    /// </summary>
    public static class Notifier
    {
        private const float LowAt = .25f, FadeWarnSeconds = 6 * 3600, GapHours = 3;
        private const int QuietFrom = 21, QuietTo = 8;
        private static bool _ready;
        private static int _nextId;
        public static bool Enabled = true;
        private static readonly Dictionary<string, string> Pictures = new Dictionary<string, string>();

        private static readonly string[] NeedEmoji = { "🍚", "🎾", "💤", "🫧" };

        public static void Init()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (_ready || !Application.isMobilePlatform) return;
            try
            {
                NotificationCenter.Initialize(new NotificationCenterArgs
                {
                    AndroidChannelId = "care",
                    AndroidChannelName = "Care reminders",
                    AndroidChannelDescription = "When your squishy would love some attention",
                    PresentationOptions = NotificationPresentation.Alert | NotificationPresentation.Sound,
                });
                NotificationCenter.RequestPermission();
                _ready = true;
            }
            catch (Exception e) { Debug.LogWarning("Notifications unavailable: " + e.Message); }
#endif
        }

        public static void Clear()
        {
            if (!_ready) return;
#if UNITY_ANDROID || UNITY_IOS
            NotificationCenter.CancelAllScheduledNotifications();
            NotificationCenter.CancelAllDeliveredNotifications();
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            _nextId = 0;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var n = new AndroidJavaClass("com.squishydumpling.widget.SquishyNotify"))
                    n.CallStatic("cancelAll", activity);
            }
            catch (Exception) { }
#endif
        }

        private static bool _pending;

        /// <summary>Renders the pictures once the thumbnail camera exists, then refreshes the widget.</summary>
        public static void Retry(GameRules rules, float comfort)
        {
            if (!_pending || !Thumbs.Ready) return;
            RenderPictures(rules);
            PushWidget(rules, comfort);
        }

        /// <summary>Asks for the mood pictures (widget) and thought-bubble scenes (notifications) to be re-rendered.</summary>
        public static void RefreshPictures(GameRules rules)
        {
            if (!Application.isMobilePlatform) return;
            _pending = true; // rendered from the game loop (a render during start-up comes back blank)
        }

        private static void RenderPictures(GameRules rules)
        {
            _pending = false;
            var f = rules.Fav;
            var stage = rules.LifeStage();
            Save("happy", Thumbs.SquishyPng(f, stage, 0, 0, false));
            var droopy = Thumbs.SquishyPng(f, stage, .2f, .25f, false);
            Save("droopy", droopy);
            var sad = Thumbs.SquishyPng(f, stage, .7f, .35f, true);
            Save("sad", sad);
            for (int k = 0; k < 4; k++)
            {
                Save("low" + k, Scene(droopy, k));
                Save("empty" + k, Scene(sad, k));
            }
            Save("fade", Scene(sad, -1));
        }

        /// <summary>A little postcard: the squishy on the left, a thought bubble of what it wants on the right.</summary>
        private static byte[] Scene(byte[] squishyPng, int need)
        {
            if (squishyPng == null) return null;
            const int W = 512, H = 256;
            var g = new Three.Canvas2D(W, H);
            Color bubble = Three.Canvas2D.Css("#FFFFFF");
            g.FillRect(0, 0, W, H, Three.Canvas2D.Css("#F7F0E4"));
            g.FillCircle(262, 178, 9, bubble);
            g.FillCircle(292, 148, 15, bubble);
            g.FillCircle(384, 104, 80, bubble);
            float cx = 384, cy = 104;
            if (need == 0)
            {
                var bowl = new List<Vector2>();
                for (int i = 0; i <= 16; i++) { float a = Mathf.PI * i / 16; bowl.Add(new Vector2(cx - 42 * Mathf.Cos(a), cy + 34 * Mathf.Sin(a))); }
                g.FillEllipse(cx, cy, 40, 12, 0, Three.Canvas2D.Css("#F4EBDD"));
                g.FillPolygon(bowl, Three.Canvas2D.Css("#C8674E"));
                g.StrokeArc(cx - 12, cy - 22, 10, 0, Mathf.PI, Three.Canvas2D.Css("#C9BBA8"), 4);
                g.StrokeArc(cx + 12, cy - 30, 10, Mathf.PI, 2 * Mathf.PI, Three.Canvas2D.Css("#C9BBA8"), 4);
            }
            else if (need == 1)
            {
                g.FillCircle(cx, cy, 38, Three.Canvas2D.Css("#6E9C9A"));
                g.StrokeArc(cx - 52, cy, 40, -.9f, .9f, bubble, 5);
                g.StrokeArc(cx + 52, cy, 40, Mathf.PI - .9f, Mathf.PI + .9f, bubble, 5);
            }
            else if (need == 2)
            {
                g.FillCircle(cx, cy, 38, Three.Canvas2D.Css("#8C7BB0"));
                g.FillCircle(cx + 20, cy - 14, 32, bubble);
                g.FillCircle(cx + 40, cy + 30, 5, Three.Canvas2D.Css("#D9A64A"));
                g.FillCircle(cx - 44, cy - 40, 4, Three.Canvas2D.Css("#D9A64A"));
            }
            else if (need == 3)
            {
                var drop = Three.Canvas2D.Css("#7FB0C9");
                g.FillCircle(cx, cy + 14, 30, drop);
                g.FillPolygon(new[] { new Vector2(cx, cy - 44), new Vector2(cx - 27, cy + 4), new Vector2(cx + 27, cy + 4) }, drop);
                g.FillCircle(cx - 10, cy + 8, 8, bubble);
            }
            else
            {
                var pink = Three.Canvas2D.Css("#E86A92");
                g.FillCircle(cx - 17, cy - 10, 22, pink);
                g.FillCircle(cx + 17, cy - 10, 22, pink);
                g.FillPolygon(new[] { new Vector2(cx - 37, cy - 2), new Vector2(cx + 37, cy - 2), new Vector2(cx, cy + 40) }, pink);
            }
            var bg = g.ToTexture(true, false, false, "scene", true);
            var sq = new Texture2D(2, 2);
            sq.LoadImage(squishyPng);
            var px = bg.GetPixels();
            const int X0 = 8, Y0 = 4, S = 248;
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                Color c = sq.GetPixelBilinear((x + .5f) / S, (y + .5f) / S);
                if (c.a <= 0) continue;
                int i = (Y0 + y) * W + X0 + x;
                Color d = px[i];
                px[i] = new Color(Mathf.Lerp(d.r, c.r, c.a), Mathf.Lerp(d.g, c.g, c.a), Mathf.Lerp(d.b, c.b, c.a), 1);
            }
            bg.SetPixels(px);
            bg.Apply();
            var png = bg.EncodeToPNG();
            UnityEngine.Object.Destroy(bg);
            UnityEngine.Object.Destroy(sq);
            return png;
        }

        private static void Save(string mood, byte[] png)
        {
            if (png == null) return;
            string path = Path.Combine(Application.persistentDataPath, "squishy_" + mood + ".png");
            try { File.WriteAllBytes(path, png); Pictures[mood] = path; }
            catch (Exception e) { Debug.LogWarning("Could not save picture: " + e.Message); }
        }

        private sealed class Plan { public DateTime when; public string title, text, mood; public bool urgent; }

        /// <summary>Schedules the reminders from the current state, predicting drain the same way GameRules does.</summary>
        public static void Schedule(GameRules rules, float comfort)
        {
            if (_pending && Thumbs.Ready) RenderPictures(rules);
            PushWidget(rules, comfort);
            if (!_ready) return;
            Clear();
            if (!Enabled) return;
            var s = rules.S;
            if (s.dead) return;
            string name = rules.Fav.name;
            var now = DateTime.Now;
            var plans = new List<Plan>();
            if (!s.tucked)
            {
                float slow = rules.ComfortSlow(comfort);
                float[] decay = { rules.R.decayHunger, rules.R.decayPlay, rules.R.decayRest, rules.R.decayClean };
                int first = -1, emptyK = 0;
                double firstLow = double.MaxValue, firstEmpty = double.MaxValue;
                for (int k = 0; k < 4; k++)
                {
                    double rate = decay[k] * slow;
                    if (rate <= 0) continue;
                    double low = (s.needs[k] - LowAt) / rate, empty = s.needs[k] / rate;
                    if (low > 600 && low < firstLow) { firstLow = low; first = k; }
                    if (empty < firstEmpty) { firstEmpty = empty; emptyK = k; }
                }
                if (first >= 0) plans.Add(new Plan { when = now.AddSeconds(firstLow), title = name, text = NeedEmoji[first] + " …?", mood = "low" + first });
                if (firstEmpty < double.MaxValue)
                {
                    plans.Add(new Plan { when = now.AddSeconds(Math.Max(600, firstEmpty)), title = name, text = "🥺 " + NeedEmoji[emptyK], mood = "empty" + emptyK });
                    double fade = firstEmpty + Math.Max(0, rules.R.deathSeconds - s.deathClock) - FadeWarnSeconds;
                    if (fade > 600) plans.Add(new Plan { when = now.AddSeconds(fade), title = name, text = "💛 …", mood = "fade", urgent = true });
                }
            }
            // Tasks and gifts never notify: only the squishy itself asking for care does.

            // Calm policy: quiet hours move to the morning; keep at least 3 hours between reminders.
            foreach (var p in plans) if (!p.urgent) p.when = OutOfQuietHours(p.when);
            plans.Sort((a, b) => a.when.CompareTo(b.when));
            DateTime last = DateTime.MinValue;
            foreach (var p in plans)
            {
                if (!p.urgent && (p.when - last).TotalHours < GapHours) continue;
                Send(p);
                last = p.when;
            }
        }

        private static DateTime OutOfQuietHours(DateTime t)
        {
            if (t.Hour >= QuietFrom) return t.Date.AddDays(1).AddHours(QuietTo);
            if (t.Hour < QuietTo) return t.Date.AddHours(QuietTo);
            return t;
        }

        private static void Send(Plan p)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Native picture notification: the squishy's thought-bubble scene fills it (custom layout).
            string pic;
            Pictures.TryGetValue(p.mood, out pic);
            long ms = new DateTimeOffset(p.when).ToUnixTimeMilliseconds();
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var n = new AndroidJavaClass("com.squishydumpling.widget.SquishyNotify"))
                    n.CallStatic("schedule", activity, ++_nextId, ms, p.title, p.text, pic);
            }
            catch (Exception e) { Debug.LogWarning("Notification failed: " + e.Message); }
#elif UNITY_IOS
            var n = new Notification { Title = p.title, Text = p.text };
            NotificationCenter.ScheduleNotification(n, new NotificationDateTimeSchedule(p.when));
#endif
        }

        /// <summary>Hands the widget what it needs to work out the mood by itself while the app is closed.</summary>
        private static void PushWidget(GameRules rules, float comfort)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                var s = rules.S;
                float slow = rules.ComfortSlow(comfort);
                float[] decay = { rules.R.decayHunger, rules.R.decayPlay, rules.R.decayRest, rules.R.decayClean };
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var prefs = activity.Call<AndroidJavaObject>("getSharedPreferences", "squishy_widget", 0))
                using (var ed = prefs.Call<AndroidJavaObject>("edit"))
                {
                    ed.Call<AndroidJavaObject>("putString", "name", rules.Fav.name).Dispose();
                    ed.Call<AndroidJavaObject>("putBoolean", "dead", s.dead).Dispose();
                    ed.Call<AndroidJavaObject>("putBoolean", "tucked", s.tucked).Dispose();
                    ed.Call<AndroidJavaObject>("putLong", "saved", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).Dispose();
                    for (int k = 0; k < 4; k++)
                    {
                        ed.Call<AndroidJavaObject>("putFloat", "need" + k, s.needs[k]).Dispose();
                        ed.Call<AndroidJavaObject>("putFloat", "rate" + k, decay[k] * slow).Dispose();
                    }
                    foreach (var kv in Pictures) ed.Call<AndroidJavaObject>("putString", "img_" + kv.Key, kv.Value).Dispose();
                    ed.Call("apply");
                    using (var w = new AndroidJavaClass("com.squishydumpling.widget.SquishyWidget")) w.CallStatic("refresh", activity);
                }
            }
            catch (Exception e) { Debug.LogWarning("Widget update failed: " + e.Message); }
#endif
        }
    }
}
