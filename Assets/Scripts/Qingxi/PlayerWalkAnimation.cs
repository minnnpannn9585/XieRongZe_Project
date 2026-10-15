using UnityEngine;

namespace Qingxi
{
    /// <summary>Drives the four-frame Animator from measured physics movement, never visual bobbing.</summary>
    [RequireComponent(typeof(PlayerMovement), typeof(Rigidbody2D)), DisallowMultipleComponent]
    public sealed class PlayerWalkAnimation : MonoBehaviour
    {
        static readonly int WalkingParameter = Animator.StringToHash("IsWalking");
        static readonly int SpeedParameter = Animator.StringToHash("MoveSpeed");
        [SerializeField] Animator animator;
        [SerializeField] SpriteRenderer illustration;
        [SerializeField, Min(0.01f)] float movementThreshold = 0.05f;
        Rigidbody2D body;
        PlayerMovement movement;
        Vector2 previousPosition;
        Vector2 actualVelocity;
        public Animator Animator => animator;
        public bool IsWalking { get; private set; }

        public void Configure(Animator controller, SpriteRenderer renderer)
        {
            animator = controller;
            illustration = renderer;
        }

        void OnEnable()
        {
            body = GetComponent<Rigidbody2D>();
            movement = GetComponent<PlayerMovement>();
            previousPosition = body.position;
            actualVelocity = Vector2.zero;
        }

        void FixedUpdate()
        {
            Vector2 position = body.position;
            Vector2 delta = position - previousPosition;
            previousPosition = position;
            // Scene placement and teleporting are not footsteps.
            actualVelocity = delta.magnitude <= movement.Speed * Time.fixedDeltaTime * 2f
                ? delta / Time.fixedDeltaTime : Vector2.zero;
        }

        void Update()
        {
            if (animator == null || animator.runtimeAnimatorController == null) return;
            float threshold = movementThreshold * movementThreshold;
            IsWalking = movement.InputEnabled && actualVelocity.sqrMagnitude > threshold && body.linearVelocity.sqrMagnitude > threshold;
            animator.SetBool(WalkingParameter, IsWalking);
            animator.SetFloat(SpeedParameter, IsWalking ? Mathf.Clamp(actualVelocity.magnitude / movement.Speed, 0.1f, 1.5f) : 1f);
            if (IsWalking && illustration != null && Mathf.Abs(actualVelocity.x) > movementThreshold)
                illustration.flipX = actualVelocity.x < 0;
        }

        void OnDisable()
        {
            IsWalking = false;
            actualVelocity = Vector2.zero;
            if (animator != null && animator.isActiveAndEnabled && animator.isInitialized)
                animator.SetBool(WalkingParameter, false);
        }
    }
}
