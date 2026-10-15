using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Qingxi
{
    /// <summary>A saved draggable part. Moving over a matching slot previews snapping; bad drops return home.</summary>
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class CleanupPiece : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        [SerializeField] CleanupGame owner;
        [SerializeField] int index;
        [SerializeField] Vector2 home;
        [SerializeField] RectTransform slot;
        [SerializeField] RectTransform artwork;
        bool dragging;
        int lastDragFrame = -2;
        public int Index => index;
        public RectTransform Slot => slot;
        public Vector2 Home => home;
        public CleanupShape.Shape Kind => artwork.GetComponent<CleanupShape>().Kind;
        public RectTransform Rect => (RectTransform)transform;

        public void Configure(CleanupGame game, int id, Vector2 homeAnchor, RectTransform target, RectTransform art)
        { owner = game; index = id; home = homeAnchor; slot = target; artwork = art; }

        public void Present(RectTransform target, int rotation, Vector2 trayAnchor)
        {
            dragging = false;
            GetComponent<CanvasGroup>().blocksRaycasts = true;
            Rect.anchorMin = Rect.anchorMax = target != null ? target.anchorMin : trayAnchor;
            Rect.anchoredPosition = Vector2.zero;
            artwork.localRotation = Quaternion.Euler(0, 0, -90 * rotation);
            GetComponent<Image>().color = target != null ? new Color(0.12f, 0.42f, 0.35f, 0.06f) : new Color(1, 1, 1, 0.6f);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || !owner.CanOperate) return;
            dragging = true;
            Rect.anchorMin = Rect.anchorMax = Vector2.one * 0.5f;
            GetComponent<CanvasGroup>().blocksRaycasts = false;
            transform.SetAsLastSibling();
            OnDrag(e);
        }
        public void OnDrag(PointerEventData e)
        {
            if (!dragging) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform.parent,
                e.position, e.pressEventCamera, out var point)) Rect.anchoredPosition = point;
            owner.PreviewDrop(index, e.position, e.pressEventCamera);
        }
        public void OnEndDrag(PointerEventData e)
        {
            if (!dragging) return;
            dragging = false;
            lastDragFrame = Time.frameCount;
            GetComponent<CanvasGroup>().blocksRaycasts = true;
            owner.Drop(index, e.position, e.pressEventCamera);
        }
        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            if (Time.frameCount == lastDragFrame) return;
            owner.Rotate(index);
        }
        void OnDisable()
        {
            dragging = false;
            GetComponent<CanvasGroup>().blocksRaycasts = true;
        }
    }
}
