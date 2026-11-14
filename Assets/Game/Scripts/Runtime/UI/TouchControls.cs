using Sol3.Game.Art;
using Sol3.Game.Player;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sol3.Game.UI
{
    /// <summary>Joystick virtual y botón ACCIÓN para pantallas táctiles (Android y navegadores móviles).</summary>
    public sealed class TouchControls : MonoBehaviour
    {
        GameObject m_Root;
        bool m_Enabled;

        public void Build(RectTransform parent)
        {
            m_Enabled = Application.isMobilePlatform || Touchscreen.current != null;
            m_Root = UIKit.Rect(parent, "Touch").Stretch().gameObject;

            var pad = UIKit.Panel(m_Root.transform, Palette.Ink.WithAlpha(0.35f), "Joystick", 0.35f);
            pad.rectTransform.Place(Vector2.zero, new Vector2(0.5f, 0.5f), new Vector2(150f, 150f), new Vector2(210f, 210f));
            UIKit.Outline(pad.transform, Palette.Cyan.WithAlpha(0.4f), 0.35f);
            var knob = UIKit.Panel(pad.transform, Palette.SoftWhite.WithAlpha(0.6f), "Knob", 0.75f);
            knob.raycastTarget = false;
            knob.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(86f, 86f));
            var stick = pad.gameObject.AddComponent<VirtualStick>();
            stick.Knob = knob.rectTransform;

            var action = UIKit.Panel(m_Root.transform, Palette.Mars.WithAlpha(0.75f), "Action", 0.5f);
            action.rectTransform.Place(new Vector2(1f, 0f), new Vector2(0.5f, 0.5f), new Vector2(-110f, 230f), new Vector2(140f, 140f));
            var label = UIKit.Text(action.transform, "ACCIÓN", 22f, Palette.SoftWhite, TextAlignmentOptions.Center, FontStyles.Bold);
            label.rectTransform.Stretch();
            var button = action.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() => PlayerController.TouchInteract = true);
            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (m_Root != null && m_Root.activeSelf != (visible && m_Enabled))
                m_Root.SetActive(visible && m_Enabled);
        }
    }

    public sealed class VirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform Knob;
        const float Radius = 80f;

        public void OnPointerDown(PointerEventData e) => OnDrag(e);

        public void OnDrag(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local))
                return;
            var v = Vector2.ClampMagnitude(local, Radius);
            Knob.anchoredPosition = v;
            var move = v / Radius;
            PlayerController.TouchMove = move.magnitude < 0.15f ? Vector2.zero : move;
        }

        public void OnPointerUp(PointerEventData e)
        {
            Knob.anchoredPosition = Vector2.zero;
            PlayerController.TouchMove = Vector2.zero;
        }

        void OnDisable()
        {
            if (Knob != null) Knob.anchoredPosition = Vector2.zero;
            PlayerController.TouchMove = Vector2.zero;
        }
    }
}
