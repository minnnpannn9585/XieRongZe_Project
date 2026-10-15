using UnityEngine;
using UnityEngine.UI;

namespace Qingxi
{
    /// <summary>One editable investigation location with its artwork, entrance, label and physical footprint.</summary>
    public sealed class InvestigationSite : MonoBehaviour
    {
        [SerializeField, Range(0, 4)] int index;
        [SerializeField] Transform entrance;
        [SerializeField] Transform artwork;
        [SerializeField] CanvasGroup label;
        [SerializeField] Text title;
        [SerializeField] Text status;
        [SerializeField] float noticeRange = 3.4f;
        [SerializeField] float interactionRange = 2.15f;
        Vector3 originalScale = Vector3.one;
        public int Index => index;
        public Vector2 Entrance => entrance != null ? entrance.position : transform.position;

        public void Configure(int area, Transform door, Transform visual, CanvasGroup plaque, Text nameText, Text stateText)
        {
            index = area;
            entrance = door;
            artwork = visual;
            label = plaque;
            title = nameText;
            status = stateText;
        }

        public void Initialize(InvestigationSession session, Font font)
        {
            if (artwork != null) originalScale = artwork.localScale;
            if (title != null)
            {
                title.text = QingxiLocalization.Text(session.Case.Areas[index].ShortName);
                if (font != null) title.font = font;
            }
            if (status != null && font != null) status.font = font;
            RefreshProgress(session);
        }

        public bool CanInteract(Vector2 player)
        {
            return Vector2.Distance(player, transform.position) <= interactionRange || Vector2.Distance(player, Entrance) <= 1.45f;
        }

        public void RefreshProgress(InvestigationSession session)
        {
            if (status == null || index < 0 || index >= session.Case.Areas.Length) return;
            if (title != null) title.text = QingxiLocalization.Text(session.Case.Areas[index].ShortName);
            AreaProgress progress = session.GetProgress(index);
            status.text = QingxiLocalization.Text(progress.DeepDone ? "已深查" : progress.SurfaceDone ? "已观察" : "未调查");
            status.color = progress.DeepDone ? new Color(0.12f, 0.42f, 0.35f) : new Color(0.3f, 0.37f, 0.32f);
        }

        public void UpdatePresentation(Vector2 player, bool selected, bool locked)
        {
            bool visible = Vector2.Distance(player, transform.position) <= noticeRange || Vector2.Distance(player, Entrance) <= noticeRange || (selected && locked);
            if (label != null) label.alpha = Mathf.MoveTowards(label.alpha, visible ? 1f : 0f, Time.deltaTime * 6f);
            if (artwork != null)
            {
                float scale = selected && !locked ? 1.035f : 1f;
                artwork.localScale = Vector3.Lerp(artwork.localScale, originalScale * scale, Time.deltaTime * 12f);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.75f, 0.55f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, interactionRange);
            Gizmos.color = new Color(1f, 0.7f, 0.25f, 0.8f);
            Gizmos.DrawWireSphere(Entrance, 1.45f);
        }
    }
}
