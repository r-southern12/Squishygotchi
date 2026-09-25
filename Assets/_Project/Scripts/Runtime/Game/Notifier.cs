using System;
using Squishy.Simulation.Game;
#if UNITY_ANDROID || UNITY_IOS
using Unity.Notifications;
#endif
using UnityEngine;

namespace Squishy.Runtime.Game
{
    /// <summary>
    /// Local reminders while the app is closed: first need getting low, a need running empty, fading before
    /// neglect kills, and a care task coming back. Scheduled on leaving the app, cleared on return.
    /// No server and no data leaves the phone.
    /// </summary>
    public static class Notifier
    {
        private const float LowAt = .25f, FadeWarnSeconds = 6 * 3600;
        private static bool _ready;
        private static readonly string[] Low = { "is getting hungry", "is getting bored", "is getting sleepy", "is getting grubby" };

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
                    AndroidChannelDescription = "When your squishy needs you",
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

        /// <summary>Schedules reminders from the current state, predicting drain the same way GameRules does.</summary>
        public static void Schedule(GameRules rules, float comfort)
        {
            if (!_ready) return;
            Clear();
            var s = rules.S;
            if (s.dead) return;
            string name = rules.Fav.name;
            var now = DateTime.Now;
            if (!s.tucked)
            {
                float slow = rules.ComfortSlow(comfort);
                float[] decay = { rules.R.decayHunger, rules.R.decayPlay, rules.R.decayRest, rules.R.decayClean };
                int first = -1;
                double firstLow = double.MaxValue, firstEmpty = double.MaxValue;
                for (int k = 0; k < 4; k++)
                {
                    double rate = decay[k] * slow;
                    if (rate <= 0) continue;
                    double low = (s.needs[k] - LowAt) / rate, empty = s.needs[k] / rate;
                    if (low > 60 && low < firstLow) { firstLow = low; first = k; }
                    if (empty < firstEmpty) firstEmpty = empty;
                }
                if (first >= 0) Send(name + " " + Low[first], "Pop in and look after them.", now.AddSeconds(firstLow));
                if (firstEmpty < double.MaxValue)
                {
                    Send(name + " needs you!", "One of their needs has run out.", now.AddSeconds(Math.Max(60, firstEmpty)));
                    double fade = firstEmpty + Math.Max(0, rules.R.deathSeconds - s.deathClock) - FadeWarnSeconds;
                    if (fade > 60) Send(name + " is fading…", "Please come back soon.", now.AddSeconds(fade));
                }
            }
            TimeSpan? task = null;
            foreach (var t in s.tasks)
                if (!rules.TaskReady(t)) { var w = rules.TaskWait(t); if (!task.HasValue || w < task.Value) task = w; }
            if (task.HasValue) Send("A new care task is ready", "Finish tasks for coins and free steamers.", now.Add(task.Value));
        }

        private static void Send(string title, string text, DateTime when)
        {
#if UNITY_ANDROID || UNITY_IOS
            var n = new Notification { Title = title, Text = text };
            NotificationCenter.ScheduleNotification(n, new NotificationDateTimeSchedule(when));
#endif
        }
    }
}
