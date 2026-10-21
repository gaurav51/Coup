using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Coup.Game
{
    /// <summary>
    /// A click sound for one control. Added by the director rather than authored, and
    /// it handles the pointer directly so rebuilding a panel's onClick handlers can
    /// not drop it. A control that is switched off answers with a soft refusal, which
    /// tells the player the button was found but is not available.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiClickSound : MonoBehaviour, IPointerClickHandler
    {
        private Selectable _selectable;

        private void Awake() => _selectable = GetComponent<Selectable>();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData != null && eventData.button != PointerEventData.InputButton.Left) return;
            if (_selectable != null && !_selectable.IsInteractable())
            {
                Sfx.Play(Cue.UiDenied, volume: 0.6f);
                return;
            }
            Sfx.Play(Cue.UiClick);
        }
    }
}
