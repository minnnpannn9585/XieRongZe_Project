using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Qingxi
{
    /// <summary>Coordinates the authored world; movement, camera and sites live on their own objects.</summary>
    public sealed class TownExplorer : MonoBehaviour
    {
        [SerializeField] int caseIndex;
        [SerializeField] PlayerMovement player;
        [SerializeField] CameraFollow cameraFollow;
        [SerializeField] InvestigationSite[] sites = Array.Empty<InvestigationSite>();
        [SerializeField] Vector2 spawnPosition;
        InvestigationSession session;
        int nearby = -1;
        bool presenting;
        bool inputEnabled = true;
        bool movementLocked;

        public Action<int> OnOpen;
        public Action OnClose;
        public int CaseIndex => caseIndex;
        public PlayerMovement Player => player;
        public Vector2 Spawn => spawnPosition;
        public InvestigationSite[] Sites => sites;
        public Camera WorldCamera => cameraFollow != null ? cameraFollow.ViewCamera : null;
        public bool InputEnabled
        {
            get => inputEnabled;
            set { inputEnabled = value; ApplyInput(); }
        }
        public bool MovementLocked
        {
            get => movementLocked;
            set { movementLocked = value; ApplyInput(); }
        }
        public string Prompt => MovementLocked ? QingxiLocalization.Text("按 E 离开") :
            nearby < 0 || session == null ? QingxiLocalization.Text("WASD / 方向键移动  ·  E 调查  ·  N 笔记") :
            QingxiLocalization.Format("按 E 调查  {0}", QingxiLocalization.Text(session.Case.Areas[nearby].Name));

        public void Configure(int chapter, PlayerMovement movement, CameraFollow follow, InvestigationSite[] locations)
        {
            caseIndex = chapter;
            player = movement;
            spawnPosition = movement.transform.position;
            cameraFollow = follow;
            sites = locations;
        }

        /// <summary>Binds case state to objects already saved in the scene. Creates no objects or textures.</summary>
        public void Initialize(InvestigationSession activeSession, Font font)
        {
            session = activeSession ?? throw new ArgumentNullException(nameof(activeSession));
            if (player == null || cameraFollow == null || sites.Length != session.Case.Areas.Length)
            {
                Debug.LogError("场景缺少玩家、相机或调查点，请运行 Qingxi / Rebuild Authored Scenes。", this);
                enabled = false;
                return;
            }
            foreach (InvestigationSite site in sites)
                if (site != null) site.Initialize(session, font);
            ApplyInput();
            RefreshMarks();
        }

        public void PresentLandmark()
        {
            presenting = true;
            MovementLocked = true;
            if (cameraFollow != null) cameraFollow.ShowOverview(true);
        }

        public void ResumeExplore()
        {
            presenting = false;
            MovementLocked = false;
            if (cameraFollow != null) cameraFollow.ShowOverview(false);
        }

        public void RefreshMarks()
        {
            if (session == null) return;
            foreach (InvestigationSite site in sites)
                if (site != null) site.RefreshProgress(session);
        }

        void OnEnable() { ApplyInput(); }
        void OnDisable() { if (player != null) player.InputEnabled = false; }

        void ApplyInput()
        {
            if (player != null) player.InputEnabled = inputEnabled && !movementLocked && !presenting && isActiveAndEnabled;
        }

        void Update()
        {
            if (player == null || session == null || presenting || !InputEnabled) return;
            if (!MovementLocked) nearby = FindNearby();
            foreach (InvestigationSite site in sites)
                if (site != null) site.UpdatePresentation(player.transform.position, site.Index == nearby, MovementLocked);
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame) return;
            if (MovementLocked) OnClose?.Invoke();
            else if (nearby >= 0) OnOpen?.Invoke(nearby);
        }

        int FindNearby()
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            foreach (InvestigationSite site in sites)
            {
                if (site == null || !site.CanInteract(player.transform.position)) continue;
                float distance = Vector2.Distance(player.transform.position, site.transform.position);
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = site.Index;
            }
            return best;
        }
    }
}
