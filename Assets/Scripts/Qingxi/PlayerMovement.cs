using UnityEngine;
using UnityEngine.InputSystem;

namespace Qingxi
{
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float speed = 4.6f;
        [SerializeField] SpriteRenderer illustration;
        Rigidbody2D body;
        bool inputEnabled = true;
        public float Speed => speed;
        public bool InputEnabled
        {
            get => inputEnabled;
            set
            {
                inputEnabled = value;
                if (!value && body != null) body.linearVelocity = Vector2.zero;
            }
        }

        public void Configure(SpriteRenderer renderer)
        {
            illustration = renderer;
        }

        void Awake()
        {
            body = GetComponent<Rigidbody2D>();
        }

        void OnDisable() { if (body != null) body.linearVelocity = Vector2.zero; }

        void FixedUpdate()
        {
            if (body == null) return;
            Vector2 move = InputEnabled && Application.isFocused ? ReadMove() : Vector2.zero;
            body.linearVelocity = move * speed;
            if (illustration == null) return;
            illustration.sortingOrder = WorldDepthSorting.Order(transform.position.y);
        }

        static Vector2 ReadMove()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero;
            Vector2 move = Vector2.zero;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) move.x--;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) move.x++;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) move.y--;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) move.y++;
            return Vector2.ClampMagnitude(move, 1f);
        }
    }
}
