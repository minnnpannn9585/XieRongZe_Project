using UnityEngine;

namespace Qingxi
{
    /// <summary>Saved bridge/pier footprint and approach points. Collision is baked into the scene.</summary>
    [DisallowMultipleComponent]
    public sealed class WalkableBridge : MonoBehaviour
    {
        [SerializeField] Vector2 deckSize;
        [SerializeField] bool pier;
        public Vector2 DeckSize => deckSize;
        public bool IsPier => pier;
        public Vector2 ApproachA => transform.TransformPoint(new Vector3(-deckSize.x / 2f - 0.45f, 0, 0));
        public Vector2 ApproachB => transform.TransformPoint(new Vector3(deckSize.x / 2f + (pier ? -0.55f : 0.45f), 0, 0));

        public void Configure(Vector2 size, bool isPier) { deckSize = size; pier = isPier; }
        public bool ContainsDeck(Vector2 world)
        {
            Vector3 p = transform.InverseTransformPoint(world);
            return Mathf.Abs(p.x) <= deckSize.x / 2f && Mathf.Abs(p.y) <= deckSize.y / 2f;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.75f, 0.2f);
            Gizmos.DrawLine(ApproachA, ApproachB);
            Gizmos.DrawWireSphere(ApproachA, 0.32f);
            Gizmos.DrawWireSphere(ApproachB, 0.32f);
        }
    }
}
