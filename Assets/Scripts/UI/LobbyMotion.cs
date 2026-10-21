using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>Presentation motion on runtime instances, always relative to authored poses.</summary>
    public sealed class LobbyMotion : MonoBehaviour
    {
        private sealed class Item
        {
            public RectTransform rect;
            public Vector2 position;
            public Vector3 scale;
            public CanvasGroup group;
            public LobbyPressFeedback feedback;
            public float delay, age, press = 1f, offset;
            public bool wasVisible, pulse, floating;
        }
        private readonly List<Item> _items = new List<Item>();
        private LoginScreen _login;
        private PlatformPanels _panels;
        private bool _ready;

        private void Start()
        {
            _login = GetComponent<LoginScreen>();
            _panels = GetComponent<PlatformPanels>();
            Add("Hud", 0f, 14f);
            Add("Hall/CurrentGameCards", .08f, -12f, floating: true);
            Add("Actions/SeasonPass", .12f, -16f);
            Add("Actions/RankedTier", .17f, -16f);
            Add("Actions/Play", .22f, -22f, pulse: true);
            Add("Actions/Casual", .28f, -18f);
            Add("Actions/Ranked", .32f, -18f);
            Add("Actions/Event", .36f, -18f);
            Add("Actions/Collection", .40f, -16f);
            Add("Actions/Shop", .44f, -16f);
            Add("Actions/Missions", .48f, -16f);
            // Pointer handlers only report visual state; Button keeps all its callbacks.
            foreach (var path in new[] { "Hud/Profile", "Hud/Currency", "Hud/Gems", "Hud/Settings" })
                Add(path, 0f, 0f);
            _ready = true;
        }

        private void Add(string path, float delay, float offset, bool pulse = false, bool floating = false)
        {
            var rect = transform.Find(path) as RectTransform;
            if (rect == null) return;
            var button = rect.GetComponent<Button>();
            var feedback = button != null ? rect.gameObject.AddComponent<LobbyPressFeedback>() : null;
            _items.Add(new Item { rect = rect, position = rect.anchoredPosition,
                scale = rect.localScale, group = UIFactory.Group(rect.gameObject), feedback = feedback,
                delay = delay, offset = offset, pulse = pulse, floating = floating });
        }

        private void LateUpdate()
        {
            if (!_ready) return;
            var hidden = _login != null && (_login.IsVisible || _login.IsStarting);
            var covered = _panels != null && _panels.IsOpen;
            var reduced = PlayerPrefs.GetInt("Coup.ReducedMotion", 0) != 0;
            var dt = Mathf.Min(Time.unscaledDeltaTime, .1f);
            foreach (var item in _items)
            {
                if (item.rect == null) continue;
                if (hidden || !item.rect.gameObject.activeInHierarchy) {
                    Restore(item);
                    item.wasVisible = false;
                    continue;
                }
                if (!item.wasVisible) { item.age = 0f; item.wasVisible = true; }
                item.age += dt;
                var t = reduced ? 1f : Mathf.Clamp01((item.age - item.delay) / .42f);
                var ease = 1f - Mathf.Pow(1f - t, 3f);
                item.group.alpha = ease;
                var idle = !covered && !reduced && t >= 1f;
                var y = item.offset * (1f - ease);
                if (idle && item.floating) y += Mathf.Sin((item.age - item.delay - .42f) * 1.15f) * 3f;
                item.rect.anchoredPosition = item.position + new Vector2(0f, y);
                var desired = 1f;
                if (!covered && item.feedback != null && item.feedback.Available)
                    desired = item.feedback.Pressed ? .965f : item.feedback.Hovered ? 1.018f : 1f;
                item.press = Mathf.Lerp(item.press, desired, 1f - Mathf.Exp(-18f * dt));
                var breathing = idle && item.pulse ? Mathf.Sin((item.age - item.delay - .42f) * 1.8f) * .008f : 0f;
                item.rect.localScale = item.scale * ((1f - .035f * (1f - ease) + breathing) * item.press);
            }
        }

        private static void Restore(Item item)
        {
            if (item.rect == null) return;
            item.rect.anchoredPosition = item.position;
            item.rect.localScale = item.scale;
            item.group.alpha = 1f;
            item.press = 1f;
        }

        private void OnDisable() { foreach (var item in _items) Restore(item); }
    }
}
