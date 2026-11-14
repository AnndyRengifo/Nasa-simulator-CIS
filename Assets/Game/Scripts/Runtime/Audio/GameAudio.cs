using System.Collections.Generic;
using UnityEngine;

namespace Sol3.Game.Audio
{
    public enum Sfx
    {
        Click, Step, Success, Fail, Alarm, Warning, Repair, Transmit, Door, Day, Night, Pickup, Spark,
    }

    /// <summary>
    /// Audio del juego: música de día y de noche con fundido cruzado, ambiente de estación y efectos.
    /// Todo se sintetiza al arrancar. El silencio se guarda como preferencia (LocalStorage/PlayerPrefs).
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        const string MuteKey = "SOL3.Muted";

        public static GameAudio Instance { get; private set; }

        AudioSource m_MusicA, m_MusicB, m_Ambience, m_Sfx;
        AudioClip m_Day, m_Night;
        readonly Dictionary<Sfx, AudioClip> m_Clips = new Dictionary<Sfx, AudioClip>();
        bool m_NightMusic;
        float m_Blend;
        float m_MusicVolume = 0.55f;

        public bool Muted { get; private set; }

        public static GameAudio Create()
        {
            if (Instance != null)
                return Instance;
            var go = new GameObject("GameAudio");
            DontDestroyOnLoad(go);
            return go.AddComponent<GameAudio>();
        }

        void Awake()
        {
            Instance = this;
            m_MusicA = Source(true, m_MusicVolume);
            m_MusicB = Source(true, 0f);
            m_Ambience = Source(true, 0.18f);
            m_Sfx = Source(false, 0.8f);

            m_Day = Synth.DayTheme();
            m_Night = Synth.NightTheme();
            m_Ambience.clip = Synth.StationHum();

            m_Clips[Sfx.Click] = Synth.Tones("click", new[] { 1760f }, 0.04f, 60f, 0.35f);
            m_Clips[Sfx.Step] = Synth.Noise("step", 0.07f, 0.12f, 55f, 0.18f, 1);
            m_Clips[Sfx.Success] = Synth.Tones("success", new[] { 523f, 659f, 784f, 1047f }, 0.09f, 7f, 0.5f);
            m_Clips[Sfx.Fail] = Synth.Tones("fail", new[] { 392f, 311f }, 0.18f, 6f, 0.45f);
            m_Clips[Sfx.Alarm] = Synth.Tones("alarm", new[] { 620f, 480f, 620f, 480f }, 0.28f, 2.5f, 0.4f);
            m_Clips[Sfx.Warning] = Synth.Tones("warning", new[] { 880f, 1320f }, 0.14f, 5f, 0.4f);
            m_Clips[Sfx.Repair] = Synth.Sweep("repair", 300f, 1200f, 0.5f, 0.4f);
            m_Clips[Sfx.Transmit] = Synth.Tones("transmit", new[] { 1200f, 1500f, 1200f, 1800f }, 0.07f, 25f, 0.35f);
            m_Clips[Sfx.Door] = Synth.Noise("door", 0.35f, 0.05f, 7f, 0.25f, 2);
            m_Clips[Sfx.Day] = Synth.Tones("day", new[] { 523f, 784f, 1047f }, 0.22f, 3f, 0.35f);
            m_Clips[Sfx.Night] = Synth.Tones("night", new[] { 392f, 311f, 262f }, 0.3f, 2.5f, 0.35f);
            m_Clips[Sfx.Pickup] = Synth.Tones("pickup", new[] { 880f, 1175f }, 0.07f, 12f, 0.4f);
            m_Clips[Sfx.Spark] = Synth.Noise("spark", 0.25f, 0.6f, 14f, 0.3f, 4);

            m_MusicA.clip = m_Day;
            m_MusicB.clip = m_Night;
            m_MusicA.Play();
            m_MusicB.Play();
            m_Ambience.Play();
            SetMuted(PlayerPrefs.GetInt(MuteKey, 0) == 1);
        }

        AudioSource Source(bool loop, float volume)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.loop = loop;
            source.playOnAwake = false;
            source.volume = volume;
            source.spatialBlend = 0f;
            return source;
        }

        void Update()
        {
            m_Blend = Mathf.MoveTowards(m_Blend, m_NightMusic ? 1f : 0f, Time.unscaledDeltaTime / 4f);
            m_MusicA.volume = m_MusicVolume * (1f - m_Blend);
            m_MusicB.volume = m_MusicVolume * m_Blend;
        }

        public void SetNight(bool night) => m_NightMusic = night;

        public void SetAmbience(bool on)
        {
            if (on && !m_Ambience.isPlaying) m_Ambience.Play();
            else if (!on && m_Ambience.isPlaying) m_Ambience.Stop();
        }

        public void Play(Sfx sfx, float volume = 1f)
        {
            if (m_Clips.TryGetValue(sfx, out var clip))
                m_Sfx.PlayOneShot(clip, volume);
        }

        public void SetMuted(bool muted)
        {
            Muted = muted;
            AudioListener.volume = muted ? 0f : 1f;
            PlayerPrefs.SetInt(MuteKey, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void ToggleMute() => SetMuted(!Muted);
    }
}
