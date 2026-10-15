using UnityEngine;
using UnityEngine.UI;

namespace Qingxi
{
    /// <summary>Saved button behavior. Set its action and optional case/suspect index in the Inspector.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class QingxiUiAction : MonoBehaviour
    {
        public enum Action
        {
            EnterCase,
            BeginInvestigation,
            ReturnToMenu,
            OpenDeduction,
            ToggleJournal,
            CloseJournal,
            CloseInteract,
            ObserveSurface,
            InvestigateDeep,
            SelectSuspect,
            BackToInvestigation,
            ConfirmSuspect,
            ContinueAfterResult,
            CleanupPrimary,
            ResetCleanup
        }

        [SerializeField] QingxiGame controller;
        [SerializeField] Action action;
        [SerializeField, Tooltip("关卡或推理选项的索引；其他动作不使用此值。")]
        int index = -1;
        Button button;

        public void Configure(QingxiGame owner, Action buttonAction, int argument = -1)
        {
            controller = owner;
            action = buttonAction;
            index = argument;
        }

        void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(Invoke);
        }

        void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(Invoke);
        }

        public void Invoke()
        {
            if (controller != null) controller.PerformAction(action, index);
        }
    }
}
