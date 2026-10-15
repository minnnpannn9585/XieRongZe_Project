using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using L = Qingxi.QingxiLocalization;

namespace Qingxi
{
    /// <summary>Three small authored treatment boards. Parts and equipment are saved scene objects.</summary>
    public sealed class CleanupGame : MonoBehaviour
    {
        [SerializeField] QingxiGame controller;
        [SerializeField] int chapter;
        [SerializeField] CleanupPiece[] pieces;
        [SerializeField] Image[] slotImages;
        [SerializeField] Button primary;
        [SerializeField] Text title;
        [SerializeField] Text instruction;
        [SerializeField] Text progress;
        [SerializeField] Text feedback;
        [SerializeField] Text primaryLabel;
        [SerializeField] CleanupFlow flow;
        CleanupState state;
        Coroutine finishing;
        string message;
        public int Chapter => chapter;
        public CleanupPiece[] Pieces => pieces;
        public bool CanOperate => state != null && finishing == null && controller.CleanupInputEnabled;
        public int PlacedMask => state?.placed ?? 0;
        public Image[] Slots => slotImages;
        public int TrayPosition(int piece) => state.trayOrder[piece];
        public int InstalledPiece(int slot) => chapter == 1 ? ((PlacedMask & (1 << slot)) != 0 ? slot : -1) : state.slotPieces[slot];
        public int Rotation(int index) => state.rotations[index];
        public static int TargetRotation(int index) => new[] { 2, 0, 3, 1 }[index];
        public bool IsDynamicLabel(Text label)
        {
            if (label == title || label == instruction || label == progress || label == feedback || label == primaryLabel) return true;
            if (label.GetComponentInParent<CleanupGame>(true) == this && int.TryParse(label.text, out _)) return true;
            return false;
        }
        public bool Ready
        {
            get
            {
                if (state == null || state.placed != (1 << slotImages.Length) - 1) return false;
                if (chapter == 1)
                    for (int i = 0; i < pieces.Length; i++) if (state.rotations[i] != TargetRotation(i)) return false;
                return true;
            }
        }

        public void Configure(QingxiGame game, int index, CleanupPiece[] parts, Image[] slots,
            Button finishButton, Text heading, Text rule,
            Text count, Text hint, Text finishLabel, CleanupFlow water)
        {
            controller = game; chapter = index; pieces = parts; slotImages = slots;
            primary = finishButton;
            title = heading; instruction = rule; progress = count; feedback = hint;
            primaryLabel = finishLabel; flow = water;
        }

        public void Begin(CleanupState saved)
        {
            if (finishing != null) StopCoroutine(finishing);
            finishing = null;
            state = saved;
            message = null;
            Refresh();
        }

        public void Refresh()
        {
            if (state == null) return;
            title.text = L.Text(chapter == 0 ? "隔板拦截" : chapter == 1 ? "溢流水引流" : "修复矿硐挡墙");
            instruction.text = L.Text(chapter == 0 ? "从五块碎片中选出合适的三块，按形状补齐排污沟的缺口。" : chapter == 1 ?
                "拖入四段管道，点击旋转，连接溢流口与收集池。" : "从五块碎片中选出合适的三块，按形状修复矿硐挡墙。");
            int placed = 0, aligned = 0;
            for (int i = 0; i < pieces.Length; i++)
            {
                int installedSlot = SlotForPiece(i);
                pieces[i].Present(installedSlot >= 0 ? slotImages[installedSlot].rectTransform : null,
                    chapter == 1 ? state.rotations[i] : 0, chapter == 1 ? pieces[i].Home : pieces[state.trayOrder[i]].Home);
            }
            for (int i = 0; i < slotImages.Length; i++)
            {
                bool installed = InstalledPiece(i) >= 0;
                if (installed) placed++;
                bool correct = installed && (chapter != 1 || state.rotations[i] == TargetRotation(i));
                if (correct) aligned++;
                slotImages[i].color = correct ? new Color(.12f,.42f,.35f,.16f) : new Color(.65f,.52f,.30f,.18f);
            }
            progress.text = chapter == 1 ? L.Format("管道 {0} / 4 · 方向正确 {1} / 4", placed, aligned) : chapter == 2 ?
                L.Format("修补板 {0} / 3", placed) : L.Format("隔板 {0} / 3", placed);
            primaryLabel.text = L.Text(chapter == 1 ? "启动抽水泵" : "确认治理成果");
            primary.interactable = Ready && finishing == null;
            feedback.text = L.Text(finishing != null ? "正在控制污染源……" : message ?? (Ready ? "准备就绪，确认完成治理。" :
                chapter == 1 ? "按编号放置，点击管道旋转；浅色轮廓提示方向。" :
                "观察缺口形状再选择碎片；有两块用不上，放错可重试。"));
            flow.SetProgress(placed / (float)slotImages.Length);
        }

        int SlotForPiece(int piece)
        {
            if (chapter == 1) return (state.placed & (1 << piece)) != 0 ? piece : -1;
            for (int slot = 0; slot < state.slotPieces.Length; slot++) if (state.slotPieces[slot] == piece) return slot;
            return -1;
        }
        public bool ShapeFits(int piece, int slot) => chapter == 1 ? piece == slot :
            pieces[piece].Kind == slotImages[slot].GetComponentInChildren<CleanupShape>(true).Kind;

        bool Near(int slot, Vector2 screen, Camera camera)
        {
            var target = slotImages[slot].rectTransform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(target, screen, camera, out var point)) return false;
            Rect expanded = target.rect; expanded.xMin -= 24; expanded.xMax += 24; expanded.yMin -= 24; expanded.yMax += 24;
            return expanded.Contains(point);
        }
        public void PreviewDrop(int index, Vector2 screen, Camera camera)
        {
            if (!CanOperate || index < 0 || index >= pieces.Length) return;
            if (chapter == 1)
            {
                for (int i = 0; i < slotImages.Length; i++)
                    slotImages[i].color = i == index && Near(i,screen,camera) ? new Color(.12f,.42f,.35f,.40f) : new Color(.65f,.52f,.30f,.18f);
                return;
            }
            for (int i = 0; i < slotImages.Length; i++)
                slotImages[i].color = Near(i,screen,camera) ?
                    ShapeFits(index, i) && (InstalledPiece(i) < 0 || InstalledPiece(i) == index) ? new Color(.12f,.42f,.35f,.40f) : new Color(.65f,.29f,.22f,.32f) :
                    new Color(.65f,.52f,.30f,.18f);
        }
        public bool Drop(int index, Vector2 screen, Camera camera)
        {
            if (!CanOperate || index < 0 || index >= pieces.Length) return false;
            int target = -1;
            float distance = float.MaxValue;
            if (chapter == 1) target = Near(index,screen,camera) ? index : -1;
            else for (int i = 0; i < slotImages.Length; i++)
            {
                if (!Near(i, screen, camera)) continue;
                float candidate = Vector2.Distance(screen, RectTransformUtility.WorldToScreenPoint(camera, slotImages[i].rectTransform.position));
                if (candidate < distance) { distance = candidate; target = i; }
            }
            bool accepted = target >= 0 && ShapeFits(index, target) && (InstalledPiece(target) < 0 || InstalledPiece(target) == index);
            if (accepted)
            {
                if (chapter != 1)
                {
                    int previous = SlotForPiece(index);
                    if (previous >= 0 && previous != target) { state.slotPieces[previous] = -1; state.placed &= ~(1 << previous); }
                    state.slotPieces[target] = index;
                }
                state.placed |= 1 << target;
                CleanupSave.Store(state); message = "安装到位。";
            }
            else message = chapter == 1 ? "位置不匹配，请放到对应轮廓上。" : "形状不匹配，请试试其他碎片或缺口。";
            Refresh();
            return accepted;
        }
        public void Rotate(int index)
        {
            if (!CanOperate || chapter != 1 || index < 0 || index >= pieces.Length) return;
            state.rotations[index] = (state.rotations[index] + 1) % 4;
            message = null;
            CleanupSave.Store(state); Refresh();
        }
        public void ResetBoard()
        {
            if (!CanOperate) return;
            state = CleanupSave.Begin(chapter); message = "已重新摆放，调查结论保持不变。"; Refresh();
        }
        public void Confirm()
        {
            if (!CanOperate || !Ready) return;
            finishing = StartCoroutine(Finish());
            Refresh();
        }
        IEnumerator Finish()
        {
            // Keep solved parts in the save throughout the short feedback animation, including an early exit.
            float elapsed = 0;
            while (elapsed < 1.1f)
            {
                elapsed += Time.unscaledDeltaTime;
                flow.SetProgress(1, Mathf.Clamp01(elapsed / 1.1f));
                yield return null;
            }
            controller.CompleteCleanup();
            finishing = null;
        }
        void OnDisable()
        {
            if (finishing != null) StopCoroutine(finishing);
            finishing = null;
        }
    }
}
