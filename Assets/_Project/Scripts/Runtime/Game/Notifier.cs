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
    /// a need getting low, a need run out, fading before neglect kills, a new task, a gift steamer.
    /// Never between 9 pm and 8 am (moved to the morning) and never closer than 3 hours apart, except the
    /// "fading" warning. Scheduled on leaving the app, cleared on return. Nothing leaves the phone.
    /// Also keeps the Android home-screen widget's data up to date.
    /// </summary>
    public static class Notifier
    {
        private const float LowAt = .25f, FadeWarnSeconds = 6 * 3600, GapHours = 3;
        private const int QuietFrom = 21, QuietTo = 8;
        private static bool _ready;
        public static bool Enabled = true;
        private static readonly Dictionary<string, string> Pictures = new Dictionary<string, string>();

        private static readonly string[] LowTitle = { "my tummy's rumbling… 🍚", "will you play with me? 🎾", "I'm so sleepy… 💤", "I feel a bit grubby 🫧" };
        private static readonly string[] NeedWord = { "food", "play", "sleep", "bath" };

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
        }

        /// <summary>Renders the squishy's mood pictures (happy, droopy, sad) for the widget and notifications.</summary>
        public static void RefreshPictures(GameRules rules)
        {
            if (!Application.isMobilePlatform) return;
            var f = rules.Fav;
            var stage = rules.LifeStage();
            Save("happy", Thumbs.SquishyPng(f, stage, 0, 0, false));
            Save("droopy", Thumbs.SquishyPng(f, stage, .2f, .25f, false));
            Save("sad", Thumbs.SquishyPng(f, stage, .7f, .35f, true));
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
                if (first >= 0) plans.Add(new Plan { when = now.AddSeconds(firstLow), title = name + ": " + LowTitle[first], text = "Pop in when you can 💛", mood = "droopy" });
                if (firstEmpty < double.MaxValue)
                {
                    plans.Add(new Plan { when = now.AddSeconds(Math.Max(600, firstEmpty)), title = name + " really misses you 🥺", text = "They'd love some " + NeedWord[emptyK] + ".", mood = "sad" });
                    double fade = firstEmpty + Math.Max(0, rules.R.deathSeconds - s.deathClock) - FadeWarnSeconds;
                    if (fade > 600) plans.Add(new Plan { when = now.AddSeconds(fade), title = name + " is fading…", text = "Please come back soon.", mood = "sad", urgent = true });
                }
            }
            TimeSpan? task = null;
            foreach (var t in s.tasks)
                if (!rules.TaskReady(t)) { var w = rules.TaskWait(t); if (!task.HasValue || w < task.Value) task = w; }
            if (task.HasValue) plans.Add(new Plan { when = now.Add(task.Value), title = name + ": a new care task is here ✨", text = "Tasks earn coins and free steamers.", mood = "happy" });
            if (!rules.GiftReady()) plans.Add(new Plan { when = now.Add(rules.GiftWait()), title = name + ": a gift steamer arrived! 🎁", text = "Come and open it.", mood = "happy" });

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
#if UNITY_ANDROID
            var n = new AndroidNotification { Title = p.title, Text = p.text, FireTime = p.when };
            if (Pictures.TryGetValue(p.mood, out var pic))
            {
                n.LargeIcon = pic;
                n.BigPicture = new BigPictureStyle { Picture = pic, LargeIcon = pic, ShowWhenCollapsed = false };
            }
            AndroidNotificationCenter.SendNotification(n, "care");
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
