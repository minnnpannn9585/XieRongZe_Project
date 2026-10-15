using UnityEngine;

namespace Qingxi
{
    [RequireComponent(typeof(Camera))]
    public sealed class CameraFollow : MonoBehaviour
    {
        [SerializeField] Transform target;
        [SerializeField] Camera viewCamera;
        [SerializeField] Vector2 overviewPosition;
        [SerializeField] Vector2 mapSize = new Vector2(22f, 16f);
        [SerializeField] float explorationSize = 4.8f;
        [SerializeField] float overviewSize = 8f;
        bool overview;
        public Camera ViewCamera => viewCamera;
        public Vector2 MapSize => mapSize;

        public void Configure(Transform player, Camera camera, Vector2 mapCenter)
        {
            target = player;
            viewCamera = camera;
            overviewPosition = mapCenter;
        }
        public void ShowOverview(bool visible) { overview = visible; }

        void LateUpdate()
        {
            if (target == null || viewCamera == null) return;
            float blend = 1f - Mathf.Pow(0.001f, Time.deltaTime);
            viewCamera.orthographicSize = Mathf.Lerp(viewCamera.orthographicSize, overview ? overviewSize : explorationSize, blend);
            Vector2 focus = overview ? overviewPosition : (Vector2)target.position;
            float halfHeight = viewCamera.orthographicSize;
            float halfWidth = halfHeight * viewCamera.aspect;
            float limitX = Mathf.Max(0f, mapSize.x * 0.5f - halfWidth);
            float limitY = Mathf.Max(0f, mapSize.y * 0.5f - halfHeight);
            focus.x = Mathf.Clamp(focus.x, -limitX, limitX);
            focus.y = Mathf.Clamp(focus.y, -limitY, limitY);
            Vector3 next = Vector3.Lerp(transform.position, new Vector3(focus.x, focus.y, -10f), blend);
            next.x = Mathf.Clamp(next.x, -limitX, limitX);
            next.y = Mathf.Clamp(next.y, -limitY, limitY);
            transform.position = next;
        }
    }
}
