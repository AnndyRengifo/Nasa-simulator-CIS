using Sol3.Game.World;
using UnityEngine;

namespace Sol3.Game.Player
{
    /// <summary>Cámara ortográfica que sigue al jugador con suavizado, limitada al área de la base.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        public Transform Target;
        public float Smooth = 6f;
        public float Size = 5.6f;
        float m_Shake;
        Camera m_Camera;

        void Awake()
        {
            m_Camera = GetComponent<Camera>();
            m_Camera.orthographic = true;
            m_Camera.orthographicSize = Size;
        }

        public void Shake(float amount) => m_Shake = Mathf.Max(m_Shake, amount);

        public void SnapToTarget()
        {
            if (Target != null)
                transform.position = Clamp(Target.position);
        }

        void LateUpdate()
        {
            if (Target == null)
                return;
            m_Camera.orthographicSize = Mathf.Lerp(m_Camera.orthographicSize, Size, Time.unscaledDeltaTime * 3f);
            var goal = Clamp(Target.position + new Vector3(0f, 0.4f, 0f));
            var pos = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-Smooth * Time.deltaTime));
            if (m_Shake > 0f)
            {
                pos += (Vector3)(Random.insideUnitCircle * m_Shake * 0.12f);
                m_Shake = Mathf.MoveTowards(m_Shake, 0f, Time.deltaTime * 2f);
            }
            pos.z = -10f;
            transform.position = pos;
        }

        Vector3 Clamp(Vector3 p)
        {
            float halfH = m_Camera.orthographicSize;
            float halfW = halfH * m_Camera.aspect;
            const float margin = 1.5f;
            float minX = -margin + halfW, maxX = BaseLayout.Width + margin - halfW;
            float minY = -margin + halfH, maxY = BaseLayout.Height + margin - halfH;
            p.x = minX > maxX ? BaseLayout.Width * 0.5f : Mathf.Clamp(p.x, minX, maxX);
            p.y = minY > maxY ? BaseLayout.Height * 0.5f : Mathf.Clamp(p.y, minY, maxY);
            p.z = -10f;
            return p;
        }
    }
}
