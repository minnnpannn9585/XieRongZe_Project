using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Qingxi
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public sealed class UiPress : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        RectTransform rect;
        float rest = 1f;
        float target = 1f;
        float current = 1f;
        float velocity;
        bool over;
        bool down;

        void Awake()
        {
            rect = (RectTransform)transform;
        }

        void Update()
        {
            ApplyTarget();
            current = Mathf.SmoothDamp(current, target, ref velocity, 0.07f, Mathf.Infinity, Time.unscaledDeltaTime);
            rect.localScale = new Vector3(current, current, 1f);
        }

        public void SetRest(float scale)
        {
            rest = scale;
            ApplyTarget();
        }

        void OnDisable()
        {
            over = down = false;
            velocity = 0f;
            target = current = rest;
            if (rect != null) rect.localScale = Vector3.one * rest;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            over = true;
            ApplyTarget();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            over = false;
            down = false;
            ApplyTarget();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            down = true;
            ApplyTarget();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            down = false;
            ApplyTarget();
        }

        void ApplyTarget()
        {
            Button button = GetComponent<Button>();
            if (button != null && !button.IsInteractable())
            {
                target = rest;
                return;
            }

            if (down)
                target = rest * 0.96f;
            else if (over)
                target = rest * 1.035f;
            else
                target = rest;
        }
    }
}
