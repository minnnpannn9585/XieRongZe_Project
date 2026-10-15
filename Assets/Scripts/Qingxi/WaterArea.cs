using UnityEngine;

namespace Qingxi
{
    /// <summary>Authored water collision, following the source illustration and cut around walkways.</summary>
    [RequireComponent(typeof(PolygonCollider2D)), DisallowMultipleComponent]
    public sealed class WaterArea : MonoBehaviour
    {
        [SerializeField] SpriteRenderer source;
        [SerializeField] bool terrain;
        public SpriteRenderer Source => source;
        public bool IsTerrain => terrain;
        public PolygonCollider2D Barrier => GetComponent<PolygonCollider2D>();
        public void Configure(SpriteRenderer illustration, bool isTerrain) { source = illustration; terrain = isTerrain; }
    }
}
