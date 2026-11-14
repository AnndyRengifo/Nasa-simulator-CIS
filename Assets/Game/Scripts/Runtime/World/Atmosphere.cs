using Sol3.Game.Art;
using Sol3.Game.Sim;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Sol3.Game.World
{
    /// <summary>
    /// Iluminación del ciclo día/noche: luz global según la luz solar, luces interiores que se encienden
    /// de noche, parpadeo en apagones, tono ámbar durante la actividad solar y pulso rojo de alarma.
    /// </summary>
    public sealed class Atmosphere : MonoBehaviour
    {
        BaseBuilder m_Base;
        Camera m_Camera;
        float m_Alarm, m_Time;

        static readonly Color DayColor = Palette.Hex("#FFE9D6");
        static readonly Color DuskColor = Palette.Hex("#E7A27A");
        static readonly Color NightColor = Palette.Hex("#5C6F9E");
        static readonly Color StormColor = Palette.Hex("#F0B060");

        public void Init(BaseBuilder builder, Camera camera)
        {
            m_Base = builder;
            m_Camera = camera;
        }

        public void PulseAlarm(float seconds) => m_Alarm = Mathf.Max(m_Alarm, seconds);

        public void Tick(ColonySimulation sim, float dt)
        {
            if (m_Base == null || m_Base.GlobalLight == null)
                return;
            var s = sim.State;
            m_Time += dt;
            m_Alarm = Mathf.Max(0f, m_Alarm - dt);

            float daylight = s.Daylight;
            var sky = daylight > 0.35f ? Color.Lerp(DuskColor, DayColor, (daylight - 0.35f) / 0.65f) : Color.Lerp(NightColor, DuskColor, daylight / 0.35f);
            if (s.StormActive)
                sky = Color.Lerp(sky, StormColor, 0.45f + 0.1f * Mathf.Sin(m_Time * 2f));
            float intensity = Mathf.Lerp(0.32f, 0.95f, daylight);
            if (m_Alarm > 0f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(m_Time * 7f);
                sky = Color.Lerp(sky, Palette.Alert, 0.35f * pulse);
            }
            m_Base.GlobalLight.color = Color.Lerp(m_Base.GlobalLight.color, sky, dt * 2f);
            m_Base.GlobalLight.intensity = Mathf.Lerp(m_Base.GlobalLight.intensity, intensity, dt * 2f);

            float interior = s.Blackout ? (Mathf.PerlinNoise(m_Time * 6f, 0.3f) > 0.62f ? 0.35f : 0.05f) : Mathf.Lerp(0.95f, 0.45f, daylight);
            foreach (var pair in m_Base.RoomLights)
            {
                var light = pair.Value;
                float target = interior;
                if (s.ShelterActive && pair.Key == RoomId.Shelter)
                    target = Mathf.Max(target, 1f);
                light.intensity = Mathf.Lerp(light.intensity, target, dt * 3f);
            }

            if (m_Camera != null)
                m_Camera.backgroundColor = Color.Lerp(Palette.Hex("#0B0F18"), Palette.Hex("#3A2418"), daylight);
        }
    }
}
