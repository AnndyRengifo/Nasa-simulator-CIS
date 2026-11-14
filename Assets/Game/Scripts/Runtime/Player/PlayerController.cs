using System;
using Sol3.Game.Art;
using Sol3.Game.Audio;
using Sol3.Game.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sol3.Game.Player
{
    /// <summary>
    /// Astronauta del jugador: movimiento con WASD / flechas / mando / joystick táctil, colisiones con
    /// las paredes, animación de caminar en cuatro direcciones e interacción (E) con la máquina cercana.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        public const float InteractRange = 1.45f;
        public static PlayerController Instance { get; private set; }

        /// <summary>Entrada de los controles táctiles en pantalla.</summary>
        public static Vector2 TouchMove;
        public static bool TouchInteract;

        public float Speed = 3.6f;
        public bool InputEnabled = true;
        public Machine Nearby { get; private set; }
        public event Action<Machine> Interacted;

        Rigidbody2D m_Body;
        SpriteRenderer m_Renderer;
        Facing m_Facing = Facing.Down;
        bool m_FlipRight;
        float m_AnimTime, m_StepTimer;
        Vector2 m_Move;
        Machine[] m_Machines = Array.Empty<Machine>();
        static readonly Color Accent = Palette.Mars;

        public static PlayerController Create(Vector2 position, Machine[] machines)
        {
            var go = new GameObject("Player");
            go.transform.position = position;
            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            var collider = go.AddComponent<CapsuleCollider2D>();
            collider.direction = CapsuleDirection2D.Horizontal;
            collider.size = new Vector2(0.55f, 0.32f);
            collider.offset = new Vector2(0f, 0.12f);
            var player = go.AddComponent<PlayerController>();
            player.m_Machines = machines;
            return player;
        }

        void Awake()
        {
            Instance = this;
            m_Body = GetComponent<Rigidbody2D>();
            m_Renderer = BaseBuilder.Sprite(transform, "Sprite", Sprites.Astronaut(Facing.Down, 0, Accent), Vector2.zero, 0);
            var glow = BaseBuilder.Sprite(transform, "Marker", Sprites.Soft(32), new Vector2(0f, 0.08f), Sorting.Floor + 10, false, Palette.Mars.WithAlpha(0.35f));
            glow.transform.localScale = new Vector3(1.4f, 0.6f, 1f);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Teleport(Vector2 position)
        {
            m_Body.position = position;
            transform.position = position;
            m_Body.linearVelocity = Vector2.zero;
        }

        void Update()
        {
            m_Move = InputEnabled ? ReadMove() : Vector2.zero;
            if (m_Move.sqrMagnitude > 1f)
                m_Move.Normalize();

            Animate();
            FindNearby();

            if (InputEnabled && (InteractPressed() || TouchInteract) && Nearby != null)
                Interacted?.Invoke(Nearby);
            TouchInteract = false;
        }

        void FixedUpdate() => m_Body.linearVelocity = m_Move * Speed;

        static Vector2 ReadMove()
        {
            var move = TouchMove;
            var k = Keyboard.current;
            if (k != null)
            {
                if (k.aKey.isPressed || k.leftArrowKey.isPressed) move.x -= 1f;
                if (k.dKey.isPressed || k.rightArrowKey.isPressed) move.x += 1f;
                if (k.sKey.isPressed || k.downArrowKey.isPressed) move.y -= 1f;
                if (k.wKey.isPressed || k.upArrowKey.isPressed) move.y += 1f;
            }
            var pad = Gamepad.current;
            if (pad != null)
                move += pad.leftStick.ReadValue();
            return move;
        }

        static bool InteractPressed()
        {
            var k = Keyboard.current;
            if (k != null && (k.eKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame))
                return true;
            var pad = Gamepad.current;
            return pad != null && pad.buttonSouth.wasPressedThisFrame;
        }

        void Animate()
        {
            bool moving = m_Move.sqrMagnitude > 0.01f;
            if (moving)
            {
                if (Mathf.Abs(m_Move.x) > Mathf.Abs(m_Move.y) * 1.1f)
                {
                    m_Facing = Facing.Side;
                    m_FlipRight = m_Move.x > 0f;
                }
                else
                {
                    m_Facing = m_Move.y > 0f ? Facing.Up : Facing.Down;
                }
                m_AnimTime += Time.deltaTime * 8f;
                m_StepTimer -= Time.deltaTime;
                if (m_StepTimer <= 0f)
                {
                    m_StepTimer = 0.32f;
                    GameAudio.Instance?.Play(Sfx.Step, 0.35f);
                }
            }
            else
            {
                m_AnimTime = 0f;
            }
            int frame = moving ? 1 + (int)m_AnimTime % 4 : 0;
            m_Renderer.sprite = Sprites.Astronaut(m_Facing, frame, Accent);
            m_Renderer.flipX = m_Facing == Facing.Side && m_FlipRight;
            m_Renderer.sortingOrder = Sorting.ForY(transform.position.y);
        }

        void FindNearby()
        {
            Machine best = null;
            float bestDistance = InteractRange;
            Vector2 feet = transform.position;
            foreach (var machine in m_Machines)
            {
                float d = Vector2.Distance(feet, machine.InteractionPoint);
                if (d < bestDistance)
                {
                    best = machine;
                    bestDistance = d;
                }
            }
            if (best != Nearby)
            {
                if (Nearby != null) Nearby.SetHighlighted(false);
                if (best != null) best.SetHighlighted(true);
                Nearby = best;
            }
        }
    }
}
