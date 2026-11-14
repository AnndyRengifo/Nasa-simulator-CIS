using System;
using System.Collections.Generic;
using Sol3.Game.Art;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sol3.Game.UI
{
    /// <summary>Ventana modal: fondo oscurecido, panel con título, botón de cierre y cuerpo con lista vertical.</summary>
    public sealed class Window
    {
        public RectTransform Root;
        public RectTransform Panel;
        public RectTransform Body;
        public TextMeshProUGUI Title;
        public bool Closable;
        public Action Closed;
        public Action<Window> Builder;
        internal WindowManager Owner;
        public bool IsOpen => Root != null;

        public void Close()
        {
            if (Root == null) return;
            UnityEngine.Object.Destroy(Root.gameObject);
            Root = null;
            Owner.Remove(this);
            Closed?.Invoke();
        }

        public void Clear()
        {
            for (int i = Body.childCount - 1; i >= 0; i--)
            {
                var child = Body.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
        }

        public void Rebuild()
        {
            if (Root == null || Builder == null) return;
            Clear();
            Builder(this);
        }
    }

    public sealed class WindowManager : MonoBehaviour
    {
        readonly List<Window> m_Open = new List<Window>();
        RectTransform m_Layer;

        public bool AnyOpen => m_Open.Count > 0;
        public Window Top => m_Open.Count > 0 ? m_Open[m_Open.Count - 1] : null;

        public void Init(RectTransform layer) => m_Layer = layer;

        public Window Open(string title, Vector2 size, Action<Window> builder, bool closable = true, Icon? icon = null, Color? accent = null)
        {
            var window = new Window { Closable = closable, Builder = builder, Owner = this };
            var root = UIKit.Rect(m_Layer, "Window_" + title).Stretch();
            var dim = root.gameObject.AddComponent<Image>();
            dim.color = Palette.Ink.WithAlpha(0.72f);

            var panel = UIKit.Panel(root, UIKit.PanelColor, "Panel");
            panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            UIKit.Outline(panel.transform, (accent ?? Palette.Cyan).WithAlpha(0.55f));

            var header = UIKit.Rect(panel.transform, "Header");
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.offsetMin = new Vector2(24f, -66f);
            header.offsetMax = new Vector2(-16f, -12f);
            UIKit.HList(header, 12f);
            if (icon.HasValue)
                UIKit.Icon(header, icon.Value, accent ?? Palette.Cyan, 32f);
            window.Title = UIKit.Text(header, title, 28f, Palette.SoftWhite, TextAlignmentOptions.Left, FontStyles.Bold, "Title");
            UIKit.Size(window.Title, -1f, 44f, 1f);
            if (closable)
            {
                var close = UIKit.Button(header, "Cerrar", window.Close, Palette.Steel, 18f, 44f);
                UIKit.Size(close, 110f, 44f);
            }

            var line = UIKit.Plain(panel.transform, (accent ?? Palette.Cyan).WithAlpha(0.35f), "Line");
            line.rectTransform.anchorMin = new Vector2(0f, 1f);
            line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.offsetMin = new Vector2(24f, -72f);
            line.rectTransform.offsetMax = new Vector2(-24f, -70f);

            var body = UIKit.Rect(panel.transform, "Body").Stretch(24f, 24f, 84f, 22f);
            UIKit.VList(body, 12f);

            window.Root = root;
            window.Panel = panel.rectTransform;
            window.Body = body;
            m_Open.Add(window);
            builder?.Invoke(window);
            return window;
        }

        internal void Remove(Window window) => m_Open.Remove(window);

        public void CloseAll()
        {
            for (int i = m_Open.Count - 1; i >= 0; i--)
            {
                var w = m_Open[i];
                w.Closed = null;
                w.Close();
            }
            m_Open.Clear();
        }

        /// <summary>Cierra la ventana superior si se puede (tecla Escape / Atrás). Devuelve true si cerró algo.</summary>
        public bool CloseTop()
        {
            var top = Top;
            if (top == null || !top.Closable)
                return false;
            top.Close();
            return true;
        }
    }
}
