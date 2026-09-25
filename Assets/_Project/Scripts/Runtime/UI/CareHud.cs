using System;
using System.Collections.Generic;
using Squishy.Simulation.Care;
using Squishy.Simulation.Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Squishy.Runtime.UI
{
    /// <summary>
    /// PLACEHOLDER UI built in code: four need bars, condition, the squishy's speech bubble,
    /// a whole-room toggle and the death card. The UI pass replaces it with designed screens.
    /// </summary>
    public sealed class CareHud : MonoBehaviour
    {
        private static readonly Color Cream = new Color(1f, 0.976f, 0.937f, 0.94f);
        private static readonly Color Ink = new Color(0.2f, 0.15f, 0.11f);
        private static readonly Color Track = new Color(0.86f, 0.8f, 0.72f);

        private Font _font;
        private RectTransform _canvas;
        private readonly Dictionary<NeedKind, RectTransform> _fills = new Dictionary<NeedKind, RectTransform>();
        private readonly Dictionary<NeedKind, Image> _fillImages = new Dictionary<NeedKind, Image>();
        private Text _stage, _speed, _bubbleText;
        private RectTransform _bubble;
        private Image _bubbleBg;
        private GameObject _deathCard;
        private Text _deathTitle, _deathBody;
        private RectTransform _deathButtons;

        public event Action WholeRoomPressed;

        public void Build()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            var canvasGo = new GameObject("HUD");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            _canvas = canvasGo.GetComponent<RectTransform>();

            // Top panel with the four needs.
            var top = Panel(_canvas, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(24f, -230f), new Vector2(-24f, -60f), Cream);
            _stage = Label(top, "", 38, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -64f), new Vector2(-28f, -14f));
            _speed = Label(top, "", 34, TextAnchor.UpperRight, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(28f, -64f), new Vector2(-28f, -14f));
            var needs = CareSim.AllNeeds;
            for (int i = 0; i < needs.Length; i++)
            {
                float x0 = i / 4f, x1 = (i + 1) / 4f;
                var cell = MakeRect(top, new Vector2(x0, 0f), new Vector2(x1, 0f), new Vector2(18f, 16f), new Vector2(-18f, 100f));
                Label(cell, needs[i].ToString(), 30, TextAnchor.UpperCenter, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -40f), Vector2.zero);
                var track = Panel(cell, Vector2.zero, new Vector2(1f, 0f), new Vector2(0f, 8f), new Vector2(0f, 38f), Track);
                var fill = Panel(track, Vector2.zero, new Vector2(1f, 1f), Vector2.zero, Vector2.zero, Color.white);
                _fills[needs[i]] = fill;
                _fillImages[needs[i]] = fill.GetComponent<Image>();
            }

            // Speech bubble that follows the squishy.
            _bubble = Panel(_canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-220f, -40f), new Vector2(220f, 40f), Cream);
            _bubbleBg = _bubble.GetComponent<Image>();
            _bubbleText = Label(_bubble, "", 34, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            _bubble.gameObject.SetActive(false);

            // Whole-room toggle.
            var room = MakeButton(_canvas, "Room", new Vector2(1f, 0f), new Vector2(-250f, 60f), new Vector2(-40f, 170f));
            room.onClick.AddListener(() => { if (WholeRoomPressed != null) WholeRoomPressed(); });

            // Death card, hidden until needed.
            var card = Panel(_canvas, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-440f, -420f), new Vector2(440f, 420f), Cream);
            _deathCard = card.gameObject;
            _deathTitle = Label(card, "", 52, TextAnchor.UpperCenter, new Vector2(0f, 1f), Vector2.one, new Vector2(40f, -140f), new Vector2(-40f, -50f));
            _deathBody = Label(card, "", 34, TextAnchor.UpperCenter, new Vector2(0f, 1f), Vector2.one, new Vector2(50f, -360f), new Vector2(-50f, -160f));
            _deathButtons = MakeRect(card, Vector2.zero, new Vector2(1f, 0f), new Vector2(60f, 50f), new Vector2(-60f, 420f));
            var layout = _deathButtons.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.LowerCenter;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;
            _deathCard.SetActive(false);
        }

        public void Refresh(CareState care, CareDef def, int comfort, float timeScale, Camera cam, Vector3 bubbleWorldPos, string bubble, bool urgent)
        {
            foreach (var need in CareSim.AllNeeds)
            {
                float v = care.Get(need);
                _fills[need].anchorMax = new Vector2(Mathf.Max(0.001f, v), 1f);
                _fillImages[need].color = v > def.happyAbove ? new Color(0.44f, 0.6f, 0.45f)
                    : v > def.droopyAbove ? new Color(0.85f, 0.65f, 0.29f)
                    : v >= def.criticalBelow ? new Color(0.88f, 0.48f, 0.22f) : new Color(0.78f, 0.25f, 0.18f);
            }
            _stage.text = CareSim.Stage(care, def) + "  ·  Comfort " + comfort + "  ·  Gen " + care.generation;
            _speed.text = timeScale > 1f ? "×" + timeScale.ToString("0") + " (F)" : "";

            bool show = !string.IsNullOrEmpty(bubble) && cam != null;
            if (show)
            {
                Vector3 sp = cam.WorldToScreenPoint(bubbleWorldPos);
                show = sp.z > 0f;
                if (show)
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvas, sp, null, out var local);
                    _bubble.anchoredPosition = local + new Vector2(0f, 60f);
                    _bubbleText.text = bubble;
                    _bubbleBg.color = urgent ? new Color(1f, 0.86f, 0.8f, 0.95f) : Cream;
                }
            }
            _bubble.gameObject.SetActive(show);
        }

        public void ShowDeath(string name, NeedKind cause, IList<KeyValuePair<string, string>> choices, Action<string> onChoose)
        {
            _deathTitle.text = name + " has gone";
            _deathBody.text = "Left " + CauseWord(cause) + " for too long.\nIts tombstone stays in the room, and all your things carry over.\n\nChoose your next favourite:";
            foreach (Transform child in _deathButtons) Destroy(child.gameObject);
            foreach (var choice in choices)
            {
                string id = choice.Key;
                var b = MakeButton(_deathButtons, choice.Value, new Vector2(0.5f, 0.5f), new Vector2(-300f, -55f), new Vector2(300f, 55f));
                b.GetComponent<RectTransform>().sizeDelta = new Vector2(600f, 110f);
                b.onClick.AddListener(() => onChoose(id));
            }
            _deathCard.SetActive(true);
        }

        public void HideDeath()
        {
            _deathCard.SetActive(false);
        }

        private static string CauseWord(NeedKind need)
        {
            switch (need)
            {
                case NeedKind.Hunger: return "hungry";
                case NeedKind.Play: return "bored";
                case NeedKind.Rest: return "exhausted";
                default: return "grubby";
            }
        }

        // ---- Tiny UI helpers ----------------------------------------------------------

        private static RectTransform MakeRect(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("Rect", typeof(RectTransform));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            return rt;
        }

        private static RectTransform Panel(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Color color)
        {
            var rt = MakeRect(parent, anchorMin, anchorMax, offsetMin, offsetMax);
            rt.gameObject.AddComponent<Image>().color = color;
            return rt;
        }

        private Text Label(RectTransform parent, string text, int size, TextAnchor align, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = MakeRect(parent, anchorMin, anchorMax, offsetMin, offsetMax);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.color = Ink;
            t.alignment = align;
            t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private Button MakeButton(RectTransform parent, string text, Vector2 anchor, Vector2 offsetMin, Vector2 offsetMax)
        {
            var rt = Panel(parent, anchor, anchor, offsetMin, offsetMax, new Color(0.72f, 0.36f, 0.27f));
            var b = rt.gameObject.AddComponent<Button>();
            var label = Label(rt, text, 38, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label.color = new Color(1f, 0.97f, 0.93f);
            return b;
        }
    }
}
