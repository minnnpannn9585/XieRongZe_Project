using UnityEngine;

namespace Qingxi
{
    /// <summary>The menu's saved illustration and camera. No procedural scenery is created at runtime.</summary>
    public sealed class QingxiPoster : MonoBehaviour
    {
        [SerializeField] Camera posterCamera;
        [SerializeField] Vector3 cameraCenter = new Vector3(5.13f, 0f, -10f);
        [SerializeField] float sway = 0.06f;

        public void Configure(Camera camera, Vector3 center)
        {
            posterCamera = camera;
            cameraCenter = center;
        }

        void LateUpdate()
        {
            if (posterCamera == null) return;
            posterCamera.transform.position = cameraCenter + Vector3.right * (Mathf.Sin(Time.unscaledTime * 0.25f) * sway);
        }
    }
}
