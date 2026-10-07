using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Qingxi
{
    /// <summary>
    /// Still views of the three maps, shown beside the main menu.
    /// </summary>
    public sealed class QingxiPoster : MonoBehaviour
    {
        readonly List<Camera> mutedCameras = new List<Camera>();

        Sprite square;
        Sprite disc;
        Camera posterCamera;
        Transform camTransform;

        public void Build(Font font)
        {
            square = SquareSprite();
            disc = DiscSprite();
            BuildCamera();
            BuildTown(3.35f);
            BuildLake(0f);
            BuildCreek(-3.35f);
            Caption(new Vector2(0f, 1.7f), "清溪小镇", font);
            Caption(new Vector2(0f, -1.55f), "清溪水库", font);
            Caption(new Vector2(0f, -4.98f), "清溪山溪", font);
            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
                canvases[i].worldCamera = posterCamera;
        }

        void OnEnable()
        {
            if (posterCamera == null)
                return;

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == posterCamera || !cameras[i].enabled)
                    continue;
                cameras[i].enabled = false;
                mutedCameras.Add(cameras[i]);
            }

            posterCamera.enabled = true;
        }

        void OnDisable()
        {
            if (posterCamera != null)
                posterCamera.enabled = false;
            for (int i = 0; i < mutedCameras.Count; i++)
            {
                if (mutedCameras[i] != null)
                    mutedCameras[i].enabled = true;
            }

            mutedCameras.Clear();
        }

        void LateUpdate()
        {
            if (camTransform == null)
                return;

            float sway = Mathf.Sin(Time.unscaledTime * 0.35f) * 0.12f;
            camTransform.position = new Vector3(5.13f + sway, 0f, -10f);
        }

        void BuildCamera()
        {
            var go = new GameObject("PosterCamera");
            go.transform.SetParent(transform, false);
            posterCamera = go.AddComponent<Camera>();
            posterCamera.orthographic = true;
            posterCamera.orthographicSize = 5.55f;
            posterCamera.clearFlags = CameraClearFlags.SolidColor;
            posterCamera.backgroundColor = new Color(0.847f, 0.886f, 0.855f, 1f);
            posterCamera.depth = 12f;
            posterCamera.enabled = false;
            if (go.GetComponent<UniversalAdditionalCameraData>() == null)
                go.AddComponent<UniversalAdditionalCameraData>();
            camTransform = go.transform;
            camTransform.position = new Vector3(5.13f, 0f, -10f);
        }

        void BuildTown(float y)
        {
            Color grass = new Color(0.62f, 0.74f, 0.50f, 1f);
            Color water = new Color(0.38f, 0.64f, 0.74f, 1f);
            Color dirt = new Color(0.84f, 0.74f, 0.52f, 1f);
            Color leaf = new Color(0.28f, 0.50f, 0.32f, 1f);
            Paint("Ground", grass, new Vector2(0f, y), new Vector2(7.6f, 2.55f), -40);
            Paint("River", water, new Vector2(-1.15f, y), new Vector2(0.7f, 2.35f), -30);
            Paint("Current", new Color(0.26f, 0.50f, 0.62f, 1f), new Vector2(-1.15f, y), new Vector2(0.22f, 2.1f), -29);
            Paint("Road", dirt, new Vector2(1.15f, y - 0.15f), new Vector2(3.4f, 0.38f), -28);
            Paint("Bridge", new Color(0.62f, 0.42f, 0.24f, 1f), new Vector2(-1.15f, y - 0.15f), new Vector2(1.7f, 0.42f), -27);
            Tree(new Vector2(-2.7f, y + 0.35f), 0.85f, leaf);
            Tree(new Vector2(2.85f, y + 0.25f), 0.7f, leaf);
            Hut(new Vector2(1.35f, y + 0.35f), new Vector2(1.15f, 0.62f), new Color(0.78f, 0.62f, 0.40f, 1f), new Color(0.55f, 0.28f, 0.20f, 1f));
            Paint("Reed", new Color(0.24f, 0.48f, 0.28f, 1f), new Vector2(-1.7f, y + 0.55f), new Vector2(0.08f, 0.36f), -20);
        }

        void BuildLake(float y)
        {
            Color sand = new Color(0.86f, 0.80f, 0.60f, 1f);
            Color water = new Color(0.30f, 0.58f, 0.66f, 1f);
            Color algae = new Color(0.42f, 0.70f, 0.36f, 0.95f);
            Blob("Beach", sand, new Vector2(0f, y + 0.1f), new Vector2(6.6f, 2.15f), -40);
            Blob("Lake", water, new Vector2(-0.15f, y + 0.22f), new Vector2(4.8f, 1.55f), -30);
            Blob("Deep", new Color(0.16f, 0.40f, 0.52f, 1f), new Vector2(-0.35f, y + 0.28f), new Vector2(2.4f, 0.85f), -29);
            Blob("Bloom", algae, new Vector2(-0.7f, y + 0.45f), new Vector2(1.5f, 0.45f), -28);
            Paint("Dam", new Color(0.68f, 0.70f, 0.68f, 1f), new Vector2(0f, y - 0.62f), new Vector2(4.2f, 0.18f), -27);
            Blob("Buoy", new Color(0.90f, 0.32f, 0.20f, 1f), new Vector2(1.15f, y + 0.15f), new Vector2(0.22f, 0.22f), -26);
            Tree(new Vector2(2.7f, y + 0.15f), 0.62f, new Color(0.34f, 0.52f, 0.34f, 1f));
        }

        void BuildCreek(float y)
        {
            Color dry = new Color(0.56f, 0.52f, 0.36f, 1f);
            Color rust = new Color(0.70f, 0.38f, 0.24f, 1f);
            Color pine = new Color(0.22f, 0.38f, 0.26f, 1f);
            Paint("Ground", dry, new Vector2(0f, y), new Vector2(7.6f, 2.55f), -40);
            Blob("Hill", new Color(0.46f, 0.48f, 0.36f, 1f), new Vector2(2.15f, y + 0.25f), new Vector2(2.8f, 1.7f), -35);
            Blob("CreekA", rust, new Vector2(1.4f, y + 0.55f), new Vector2(1.8f, 0.42f), -30);
            Blob("CreekB", rust, new Vector2(0.15f, y + 0.05f), new Vector2(0.42f, 0.9f), -30);
            Blob("CreekC", rust, new Vector2(-1.15f, y - 0.4f), new Vector2(1.7f, 0.4f), -30);
            Blob("RockA", new Color(0.50f, 0.48f, 0.44f, 1f), new Vector2(0.85f, y + 0.35f), new Vector2(0.45f, 0.34f), -24);
            Blob("RockB", new Color(0.42f, 0.40f, 0.36f, 1f), new Vector2(-1.8f, y + 0.15f), new Vector2(0.38f, 0.28f), -24);
            Paint("Terrace", new Color(0.50f, 0.62f, 0.34f, 1f), new Vector2(-2.5f, y - 0.55f), new Vector2(1.1f, 0.32f), -32);
            Tree(new Vector2(-2.85f, y + 0.4f), 0.55f, pine);
            Paint("Mouth", new Color(0.10f, 0.10f, 0.11f, 1f), new Vector2(2.2f, y + 0.05f), new Vector2(0.42f, 0.32f), -22);
        }

        void Hut(Vector2 pos, Vector2 size, Color wall, Color roof)
        {
            Paint("Wall", wall, pos, size, -22);
            Paint("Roof", roof, pos + new Vector2(0f, size.y * 0.28f), new Vector2(size.x * 1.12f, size.y * 0.42f), -21);
            Paint("Door", new Color(0.28f, 0.18f, 0.12f, 1f), pos + new Vector2(0f, -size.y * 0.22f), new Vector2(0.16f, size.y * 0.32f), -20);
        }

        void Tree(Vector2 pos, float canopy, Color leaves)
        {
            Paint("Trunk", new Color(0.42f, 0.30f, 0.18f, 1f), pos + new Vector2(0f, -0.16f), new Vector2(0.12f, 0.28f), -23);
            Blob("Canopy", leaves, pos + new Vector2(0f, 0.12f), new Vector2(canopy, canopy * 0.9f), -22);
        }

        void Caption(Vector2 pos, string text, Font font)
        {
            var canvasGo = new GameObject("Caption", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.transform.position = new Vector3(pos.x, pos.y, 0f);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 40;
            RectTransform rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 46f);
            rect.localScale = Vector3.one * 0.011f;

            var plaque = new GameObject("Plaque", typeof(RectTransform));
            plaque.transform.SetParent(canvasGo.transform, false);
            var plaqueRect = plaque.GetComponent<RectTransform>();
            plaqueRect.anchorMin = Vector2.zero;
            plaqueRect.anchorMax = Vector2.one;
            plaqueRect.offsetMin = Vector2.zero;
            plaqueRect.offsetMax = Vector2.zero;
            var image = plaque.AddComponent<Image>();
            image.sprite = square;
            image.color = new Color(0.984f, 0.973f, 0.949f, 0.92f);
            image.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(canvasGo.transform, false);
            var labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 28;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.11f, 0.16f, 0.14f, 1f);
            label.text = text;
            label.raycastTarget = false;
        }

        void Paint(string name, Color color, Vector2 pos, Vector2 size, int order)
        {
            Draw(name, color, pos, size, order, square);
        }

        void Blob(string name, Color color, Vector2 pos, Vector2 size, int order)
        {
            Draw(name, color, pos, size, order, disc);
        }

        void Draw(string name, Color color, Vector2 pos, Vector2 size, int order, Sprite sprite)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            Material unlit = SpriteMaterial();
            if (unlit != null)
                renderer.sharedMaterial = unlit;
        }

        static Material spriteMaterial;

        static Material SpriteMaterial()
        {
            if (spriteMaterial != null)
                return spriteMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader == null)
                return null;

            spriteMaterial = new Material(shader);
            spriteMaterial.hideFlags = HideFlags.HideAndDontSave;
            return spriteMaterial;
        }

        static Sprite SquareSprite()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static Sprite DiscSprite()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float radius = (size - 1) * 0.5f;
            var center = new Vector2(radius, radius);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center) / radius;
                    float alpha = distance <= 0.92f ? 1f : Mathf.Clamp01((1f - distance) / 0.08f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            tex.hideFlags = HideFlags.HideAndDontSave;
            Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
