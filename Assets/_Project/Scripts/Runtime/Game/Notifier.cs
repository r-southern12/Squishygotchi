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
        private const float LowAt = .4f, FadeWarnSeconds = 6 * 3600, GapHours = 3; // first nudge at 40% (about 6-7 hours away); 25% took 12 hours and was never seen
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

        /// <summary>Whether the phone lets the game post reminders: "allowed", "blocked", "not asked", or "" off a phone.</summary>
        public static string PermissionState()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var st = Unity.Notifications.Android.AndroidNotificationCenter.UserPermissionToPost;
            return st == Unity.Notifications.Android.PermissionStatus.Allowed ? "allowed" : st == Unity.Notifications.Android.PermissionStatus.NotRequested ? "not asked" : "blocked";
#else
            return "";
#endif
        }

        /// <summary>Opens the phone's notification settings for the game (to allow reminders after saying no).</summary>
        public static void OpenSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            Unity.Notifications.Android.AndroidNotificationCenter.OpenNotificationSettings();
#endif
        }

        /// <summary>Admin: a real reminder in ten seconds, to check they reach the phone.</summary>
        public static void SendTest(GameRules rules)
        {
            Init();
            if (Thumbs.Ready) RenderPictures(rules);
            Send(new Plan { when = DateTime.Now.AddSeconds(10), title = rules.Fav.name, text = NeedEmoji[0] + " …?", mood = "low0" });
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
        private static byte[] _droopyPng, _sadPng;

        /// <summary>Every need that will be low when a reminder arrives (the one that triggered it first).</summary>
        private static List<int> NeedsAt(GameRules rules, float comfort, DateTime when, int trigger)
        {
            var s = rules.S;
            float slow = rules.ComfortSlow(comfort);
            float[] decay = { rules.R.decayHunger, rules.R.decayPlay, rules.R.decayRest, rules.R.decayClean };
            double secs = Math.Max(0, (when - DateTime.Now).TotalSeconds), left = s.asleep ? Math.Max(0, (s.sleepUntil - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerSecond) : 0;
            var list = new List<int>();
            if (trigger >= 0) list.Add(trigger);
            for (int k = 0; k < 4; k++)
            {
                if (k == trigger) continue;
                double rate = decay[k] * slow, asleepFor = Math.Min(secs, left);
                double level = s.needs[k] - rate * (asleepFor * rules.R.nightDrain + (secs - asleepFor));
                if (level < LowAt + .1f) list.Add(k);
            }
            return list;
        }

        /// <summary>The picture for a reminder showing these needs (made now if it is a new combination).</summary>
        private static string PictureFor(string kind, List<int> needs, GameRules rules)
        {
            string key = kind + string.Join("", needs);
            if (!Pictures.ContainsKey(key))
            {
                var portrait = kind == "empty" ? _sadPng : _droopyPng;
                if (portrait == null) return kind + needs[0];
                Save(key, Scene(portrait, needs, rules));
            }
            return key;
        }

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
            // Painted, icon-style portraits of this squishy type (a camera render came back blank on phones).
            var stage = rules.LifeStage();
            Save("happy", SquishyArt.Png(f, SquishyArt.Mood.Happy, stage));
            var droopy = SquishyArt.Png(f, SquishyArt.Mood.Droopy, stage);
            Save("droopy", droopy);
            var sad = SquishyArt.Png(f, SquishyArt.Mood.Sad, stage);
            Save("sad", sad);
            for (int k = 0; k < 4; k++)
            {
                Save("low" + k, Scene(droopy, new[] { k }, rules));
                Save("empty" + k, Scene(sad, new[] { k }, rules));
            }
            Save("fade", Scene(sad, null, rules));
            _droopyPng = droopy;
            _sadPng = sad;
        }

        /// <summary>A little postcard: the squishy on the left, a thought bubble of what it wants on the right.</summary>
        /// <summary>
        /// A soft, muted impression of the player's own room behind the squishy: the steamer's wall colour above a warm
        /// floor, with blurred blobs in the colours of the furniture (was a plain beige card).
        /// </summary>
        private static void RoomBackdrop(Three.Canvas2D g, int W, int H, GameRules rules)
        {
            var c = rules.C;
            var skin = c.skins[Mathf.Clamp(rules.S.curSkin, 0, c.skins.Length - 1)];
            var cream = Three.Canvas2D.Css("#F7F0E4");
            Color wall = Color.Lerp(Three.Canvas2D.Css(skin.a), cream, .55f), floor = Color.Lerp(Three.Canvas2D.Css("#E7CFA4"), cream, .35f);
            // The colours of what is in the room (its most common style), as out-of-focus shapes along the floor line.
            var counts = new Dictionary<string, int>();
            foreach (var p in rules.S.items) { var st = p.Style; if (string.IsNullOrEmpty(st)) continue; counts.TryGetValue(st, out int n); counts[st] = n + 1; }
            string best = null; int bn = 0;
            foreach (var kv in counts) if (kv.Value > bn) { best = kv.Key; bn = kv.Value; }
            var style = c.Style(best);
            var pal = style != null ? style.pal : new[] { "#C8674E", "#6F9A74", "#D9A64A", "#8C7BB0", "#F2E3C6" };
            var blobs = new List<(Vector2 p, float r, Color col)>();
            var rnd = new System.Random(7);
            for (int i = 0; i < 6; i++)
                blobs.Add((new Vector2(40 + (float)rnd.NextDouble() * (W - 80), H * .52f + (float)rnd.NextDouble() * H * .22f), 34 + (float)rnd.NextDouble() * 38, Color.Lerp(Three.Canvas2D.Css(pal[i % pal.Length]), cream, .35f)));
            g.FillShader((x, y) =>
            {
                float v = y / H; // 0 at the top
                var col = Color.Lerp(wall, floor, Mathf.SmoothStep(0, 1, (v - .5f) / .2f));
                foreach (var b in blobs)
                {
                    float dx = x - b.p.x, dy = (y - b.p.y) * 1.4f, w = Mathf.Exp(-(dx * dx + dy * dy) / (b.r * b.r)) * .8f;
                    col = Color.Lerp(col, b.col, w);
                }
                float vig = 1 - .12f * Mathf.Pow(Mathf.Abs(x / W - .5f) * 2, 2); // a gentle vignette
                return new Color(col.r * vig, col.g * vig, col.b * vig, 1);
            });
        }

        /// <summary>One need's little picture, centred at (cx, cy) at scale s: a bowl, a ball, a moon, a drop (or a heart for "fading").</summary>
        private static void DrawNeed(Three.Canvas2D g, int need, float cx, float cy, float s, Color bubble)
        {
            if (need == 0)
            {
                var bowl = new List<Vector2>();
                for (int i = 0; i <= 16; i++) { float a = Mathf.PI * i / 16; bowl.Add(new Vector2(cx - 42 * s * Mathf.Cos(a), cy + 34 * s * Mathf.Sin(a))); }
                g.FillEllipse(cx, cy, 40 * s, 12 * s, 0, Three.Canvas2D.Css("#F4EBDD"));
                g.FillPolygon(bowl, Three.Canvas2D.Css("#C8674E"));
                g.StrokeArc(cx - 12 * s, cy - 22 * s, 10 * s, 0, Mathf.PI, Three.Canvas2D.Css("#C9BBA8"), 4 * s);
                g.StrokeArc(cx + 12 * s, cy - 30 * s, 10 * s, Mathf.PI, 2 * Mathf.PI, Three.Canvas2D.Css("#C9BBA8"), 4 * s);
            }
            else if (need == 1)
            {
                g.FillCircle(cx, cy, 38 * s, Three.Canvas2D.Css("#6E9C9A"));
                g.StrokeArc(cx - 52 * s, cy, 40 * s, -.9f, .9f, bubble, 5 * s);
                g.StrokeArc(cx + 52 * s, cy, 40 * s, Mathf.PI - .9f, Mathf.PI + .9f, bubble, 5 * s);
            }
            else if (need == 2)
            {
                g.FillCircle(cx, cy, 38 * s, Three.Canvas2D.Css("#8C7BB0"));
                g.FillCircle(cx + 20 * s, cy - 14 * s, 32 * s, bubble);
                g.FillCircle(cx + 40 * s, cy + 30 * s, 5 * s, Three.Canvas2D.Css("#D9A64A"));
                g.FillCircle(cx - 44 * s, cy - 40 * s, 4 * s, Three.Canvas2D.Css("#D9A64A"));
            }
            else if (need == 3)
            {
                var drop = Three.Canvas2D.Css("#7FB0C9");
                g.FillCircle(cx, cy + 14 * s, 30 * s, drop);
                g.FillPolygon(new[] { new Vector2(cx, cy - 44 * s), new Vector2(cx - 27 * s, cy + 4 * s), new Vector2(cx + 27 * s, cy + 4 * s) }, drop);
                g.FillCircle(cx - 10 * s, cy + 8 * s, 8 * s, bubble);
            }
            else
            {
                var pink = Three.Canvas2D.Css("#E86A92");
                g.FillCircle(cx - 17 * s, cy - 10 * s, 22 * s, pink);
                g.FillCircle(cx + 17 * s, cy - 10 * s, 22 * s, pink);
                g.FillPolygon(new[] { new Vector2(cx - 37 * s, cy - 2 * s), new Vector2(cx + 37 * s, cy - 2 * s), new Vector2(cx, cy + 40 * s) }, pink);
            }
        }

        private static byte[] Scene(byte[] squishyPng, IList<int> needs, GameRules rules)
        {
            if (squishyPng == null) return null;
            const int W = 512, H = 256;
            var g = new Three.Canvas2D(W, H);
            Color bubble = Three.Canvas2D.Css("#FFFFFF");
            RoomBackdrop(g, W, H, rules);
            g.FillCircle(262, 178, 9, bubble);
            g.FillCircle(292, 148, 15, bubble);
            g.FillCircle(384, 104, 80, bubble);
            // One need fills the bubble; two sit side by side; three or four share it in a little grid.
            int n = needs == null ? 0 : needs.Count;
            if (n == 0) DrawNeed(g, -1, 384, 104, 1, bubble);
            else if (n == 1) DrawNeed(g, needs[0], 384, 104, 1, bubble);
            else if (n == 2) { DrawNeed(g, needs[0], 348, 104, .62f, bubble); DrawNeed(g, needs[1], 420, 104, .62f, bubble); }
            else
            {
                var at = new[] { new Vector2(354, 76), new Vector2(414, 76), new Vector2(354, 132), new Vector2(414, 132) };
                if (n == 3) at[2] = new Vector2(384, 132);
                for (int i = 0; i < Mathf.Min(4, n); i++) DrawNeed(g, needs[i], at[i].x, at[i].y, .48f, bubble);
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

        private sealed class Plan { public DateTime when; public string title, text, mood; public bool urgent; public int need = -1; }

        /// <summary>Schedules the reminders from the current state, predicting drain the same way GameRules does.</summary>
        /// <summary>Seconds until a need drops by this much: slower while it sleeps overnight, then the normal rate.</summary>
        private static double Until(double amount, double rate, GameRules rules)
        {
            var s = rules.S;
            if (amount <= 0) return 0;
            if (!s.asleep) return amount / rate;
            double night = rate * rules.R.nightDrain, left = Math.Max(0, (s.sleepUntil - DateTime.UtcNow.Ticks) / (double)TimeSpan.TicksPerSecond);
            return amount <= night * left ? amount / night : left + (amount - night * left) / rate;
        }

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
                    double low = Until(s.needs[k] - LowAt, rate, rules), empty = Until(s.needs[k], rate, rules);
                    if (low > 600 && low < firstLow) { firstLow = low; first = k; }
                    if (empty < firstEmpty) { firstEmpty = empty; emptyK = k; }
                }
                if (first >= 0) plans.Add(new Plan { when = now.AddSeconds(firstLow), title = name, text = NeedEmoji[first] + " …?", mood = "low" + first, need = first });
                if (firstEmpty < double.MaxValue)
                {
                    plans.Add(new Plan { when = now.AddSeconds(Math.Max(600, firstEmpty)), title = name, text = "🥺 " + NeedEmoji[emptyK], mood = "empty" + emptyK, need = emptyK });
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
                if (p.need >= 0)
                {
                    var needs = NeedsAt(rules, comfort, p.when, p.need);
                    string kind = p.mood.StartsWith("empty") ? "empty" : "low";
                    p.mood = PictureFor(kind, needs, rules);
                    var em = new System.Text.StringBuilder();
                    foreach (int k in needs) em.Append(NeedEmoji[k]);
                    p.text = kind == "empty" ? "\U0001F97A " + em : em + " \u2026?";
                }
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
