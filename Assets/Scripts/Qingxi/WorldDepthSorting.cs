using UnityEngine;

namespace Qingxi
{
    /// <summary>Keeps illustration overlap correct when its scene object is moved in the editor or at runtime.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class WorldDepthSorting : MonoBehaviour
    {
        [SerializeField] SpriteRenderer target;
        [SerializeField] Transform sortOrigin;
        [SerializeField] int orderOffset;

        public static int Order(float y) => 100 - Mathf.RoundToInt(y * 12f);

        public void Configure(SpriteRenderer renderer, Transform origin, int offset = 0)
        {
            target = renderer;
            sortOrigin = origin;
            orderOffset = offset;
            Refresh();
        }

        void OnEnable() { Refresh(); }
        void OnValidate() { Refresh(); }
        void LateUpdate() { Refresh(); }

        void Refresh()
        {
            if (target == null) return;
            Transform origin = sortOrigin != null ? sortOrigin : transform;
            int desired = Order(origin.position.y) + orderOffset;
            if (target.sortingOrder != desired) target.sortingOrder = desired;
        }
    }
}
