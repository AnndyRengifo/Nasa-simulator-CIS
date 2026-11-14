using System;
using Sol3.Game.Art;
using Sol3.Game.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sol3.Game.UI
{
    /// <summary>Utilidades para construir la interfaz (uGUI + TextMeshPro) por código con el estilo del juego.</summary>
    public static class UIKit
    {
        public static readonly Color PanelColor = Palette.Hex("#18283A").WithAlpha(0.94f);
        public static readonly Color PanelLight = Palette.Hex("#30465D");
        public static readonly Color Muted = Palette.Hex("#9FB3C4");

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.layer = 5;
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static Image Panel(Transform parent, Color color, string name = "Panel", float roundness = 1.6f)
        {
            var image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.sprite = Sprites.Panel();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = roundness;
            image.color = color;
            return image;
        }

        public static Image Outline(Transform parent, Color color, float roundness = 1.6f)
        {
            var image = Rect(parent, "Outline").gameObject.AddComponent<Image>();
            image.sprite = Sprites.PanelOutline();
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = roundness;
            image.color = color;
            image.raycastTarget = false;
            image.rectTransform.Stretch();
            return image;
        }

        public static Image Plain(Transform parent, Color color, string name = "Image")
        {
            var image = Rect(parent, name).gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        public static Image Icon(Transform parent, Icon icon, Color color, float size)
        {
            var image = Rect(parent, "Icon_" + icon).gameObject.AddComponent<Image>();
            image.sprite = Sprites.Icon(icon);
            image.color = color;
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.rectTransform.sizeDelta = new Vector2(size, size);
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = size;
            element.preferredHeight = element.minHeight = size;
            return image;
        }

        public static TextMeshProUGUI Text(Transform parent, string text, float size, Color? color = null,
            TextAlignmentOptions align = TextAlignmentOptions.Left, FontStyles style = FontStyles.Normal, string name = "Text")
        {
            var t = Rect(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color ?? Palette.SoftWhite;
            t.alignment = align;
            t.fontStyle = style;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.raycastTarget = false;
            t.richText = true;
            return t;
        }

        public static Button Button(Transform parent, string label, Action onClick, Color? color = null, float fontSize = 22f, float height = 54f, Icon? icon = null)
        {
            var bg = Panel(parent, color ?? Palette.Steel, "Button_" + label, 2.2f);
            var button = bg.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                GameAudio.Instance?.Play(Sfx.Click, 0.6f);
                onClick?.Invoke();
            });

            var row = bg.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.childAlignment = TextAnchor.MiddleCenter;
            row.padding = new RectOffset(16, 16, 6, 6);
            row.spacing = 10f;
            row.childControlWidth = row.childControlHeight = true;
            row.childForceExpandWidth = row.childForceExpandHeight = false;
            if (icon.HasValue)
                Icon(bg.transform, icon.Value, Palette.SoftWhite, fontSize * 1.2f);
            var text = Text(bg.transform, label, fontSize, Palette.SoftWhite, TextAlignmentOptions.Center, FontStyles.Bold);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var element = bg.gameObject.AddComponent<LayoutElement>();
            element.minHeight = element.preferredHeight = height;
            return button;
        }

        public static Button IconButton(Transform parent, Icon icon, Action onClick, float size = 52f)
        {
            var bg = Panel(parent, Palette.Steel.WithAlpha(0.92f), "IconButton_" + icon, 2.2f);
            bg.rectTransform.sizeDelta = new Vector2(size, size);
            var button = bg.gameObject.AddComponent<Button>();
            button.onClick.AddListener(() =>
            {
                GameAudio.Instance?.Play(Sfx.Click, 0.6f);
                onClick?.Invoke();
            });
            var image = Icon(bg.transform, icon, Palette.SoftWhite, size * 0.62f);
            image.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.62f, size * 0.62f));
            var element = bg.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = size;
            element.preferredHeight = element.minHeight = size;
            return button;
        }

        public static void SetButtonLabel(Button button, string label)
        {
            var text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.text = label;
        }

        public static VerticalLayoutGroup VList(Transform target, float spacing = 10f, int padding = 0, bool expandWidth = true)
        {
            var layout = target.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = expandWidth;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup HList(Transform target, float spacing = 10f, int padding = 0)
        {
            var layout = target.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.childAlignment = TextAnchor.MiddleLeft;
            return layout;
        }

        public static RectTransform Row(Transform parent, float spacing = 10f, float height = -1f)
        {
            var row = Rect(parent, "Row");
            HList(row, spacing);
            if (height > 0f)
            {
                var element = row.gameObject.AddComponent<LayoutElement>();
                element.minHeight = element.preferredHeight = height;
            }
            return row;
        }

        public static LayoutElement Size(Component c, float width = -1f, float height = -1f, float flexibleWidth = -1f)
        {
            var element = c.GetComponent<LayoutElement>();
            if (element == null) element = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f) element.preferredWidth = element.minWidth = width;
            if (height >= 0f) element.preferredHeight = element.minHeight = height;
            if (flexibleWidth >= 0f) element.flexibleWidth = flexibleWidth;
            return element;
        }

        /// <summary>Vista con desplazamiento vertical; devuelve el contenedor del contenido (con lista vertical).</summary>
        public static RectTransform Scroll(Transform parent, float spacing = 10f)
        {
            var view = Rect(parent, "Scroll");
            var mask = view.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0.001f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30f;
            var content = Rect(view, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = content.offsetMax = Vector2.zero;
            VList(content, spacing, 0);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = view;
            var element = view.gameObject.AddComponent<LayoutElement>();
            element.flexibleHeight = 1f;
            element.flexibleWidth = 1f;
            return content;
        }

        public static Bar Bar(Transform parent, Color fill, float height = 12f)
        {
            var bg = Panel(parent, Palette.Ink.WithAlpha(0.9f), "Bar", 4f);
            var bar = bg.gameObject.AddComponent<Bar>();
            var f = Panel(bg.transform, fill, "Fill", 4f);
            f.raycastTarget = false;
            f.rectTransform.anchorMin = Vector2.zero;
            f.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            f.rectTransform.offsetMin = new Vector2(2, 2);
            f.rectTransform.offsetMax = new Vector2(-2, -2);
            bar.Fill = f;
            Size(bg, -1f, height);
            return bar;
        }

        /// <summary>Etiqueta de origen de los datos: REAL (verde) o SIMULACIÓN (ámbar).</summary>
        public static RectTransform Tag(Transform parent, string label, Color color)
        {
            var bg = Panel(parent, color.WithAlpha(0.22f), "Tag", 4f);
            var layout = HList(bg.transform, 0f, 0);
            layout.padding = new RectOffset(10, 10, 3, 3);
            Text(bg.transform, label, 15f, color, TextAlignmentOptions.Center, FontStyles.Bold).textWrappingMode = TextWrappingModes.NoWrap;
            var fitter = bg.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return bg.rectTransform;
        }

        public static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);
    }

    public sealed class Bar : MonoBehaviour
    {
        public Image Fill;
        float m_Value = -1f;

        public void Set(float value01, Color? color = null)
        {
            value01 = Mathf.Clamp01(value01);
            if (!Mathf.Approximately(value01, m_Value))
            {
                m_Value = value01;
                Fill.rectTransform.anchorMax = new Vector2(Mathf.Max(0.02f, value01), 1f);
            }
            if (color.HasValue)
                Fill.color = color.Value;
        }
    }

    /// <summary>Botón que informa mientras se mantiene pulsado (válvulas, joystick...).</summary>
    public sealed class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public bool Held { get; private set; }
        public void OnPointerDown(PointerEventData eventData) => Held = true;
        public void OnPointerUp(PointerEventData eventData) => Held = false;
        public void OnPointerExit(PointerEventData eventData) => Held = false;
        void OnDisable() => Held = false;
    }

    /// <summary>Deslizador horizontal simple (0-1) con arrastre y clic.</summary>
    public sealed class SimpleSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public float Value = 0.5f;
        public RectTransform Handle;
        public Action<float> Changed;

        public static SimpleSlider Create(Transform parent, Color color, float height = 46f)
        {
            var track = UIKit.Panel(parent, Palette.Ink.WithAlpha(0.95f), "Slider", 3f);
            UIKit.Size(track, -1f, height);
            var line = UIKit.Panel(track.transform, color.WithAlpha(0.45f), "Line", 6f);
            line.raycastTarget = false;
            line.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            line.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            line.rectTransform.offsetMin = new Vector2(16, -3);
            line.rectTransform.offsetMax = new Vector2(-16, 3);
            var handle = UIKit.Panel(track.transform, color, "Handle", 2f);
            handle.raycastTarget = false;
            handle.rectTransform.anchorMin = handle.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            handle.rectTransform.sizeDelta = new Vector2(30, height - 8);
            var slider = track.gameObject.AddComponent<SimpleSlider>();
            slider.Handle = handle.rectTransform;
            slider.Apply();
            return slider;
        }

        public void SetValue(float value)
        {
            Value = Mathf.Clamp01(value);
            Apply();
        }

        void Apply()
        {
            if (Handle == null) return;
            Handle.anchorMin = Handle.anchorMax = new Vector2(Mathf.Lerp(0.04f, 0.96f, Value), 0.5f);
            Handle.anchoredPosition = Vector2.zero;
        }

        public void OnPointerDown(PointerEventData eventData) => Track(eventData);
        public void OnDrag(PointerEventData eventData) => Track(eventData);

        void Track(PointerEventData eventData)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, eventData.position, eventData.pressEventCamera, out var local))
                return;
            float t = Mathf.InverseLerp(rt.rect.xMin, rt.rect.xMax, local.x);
            SetValue(Mathf.InverseLerp(0.04f, 0.96f, t));
            Changed?.Invoke(Value);
        }
    }
}
