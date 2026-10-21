using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Coup.Game.UI
{
    /// <summary>Visual pointer state only; does not consume clicks or change navigation.</summary>
    public sealed class LobbyPressFeedback : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Button _button;
        public bool Pressed { get; private set; }
        public bool Hovered { get; private set; }
        public bool Available => _button != null && _button.IsInteractable();
        private void Awake() => _button = GetComponent<Button>();
        public void OnPointerEnter(PointerEventData e) { Hovered = true; }
        public void OnPointerExit(PointerEventData e) { Hovered = false; Pressed = false; }
        public void OnPointerDown(PointerEventData e) { if (e.button == PointerEventData.InputButton.Left && Available) Pressed = true; }
        public void OnPointerUp(PointerEventData e) { Pressed = false; }
        private void OnDisable() { Pressed = false; Hovered = false; }
        private void OnApplicationFocus(bool focused) { if (!focused) { Pressed = false; Hovered = false; } }
    }
}
