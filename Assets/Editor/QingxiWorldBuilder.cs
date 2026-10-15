using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Qingxi.Editor
{
    /// <summary>Authoring only. Run explicitly to bake editable illustrations, prefabs and colliders into scenes.</summary>
    public static class QingxiWorldBuilder
    {
        const string ArtFolder = "Assets/Art/Illustrations";
        const string PrefabFolder = "Assets/Prefabs/World";
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static Material illustrationMaterial;
        static readonly string[][] LandmarkKeys =
        {
            new[] { "river_dock", "livestock_farm", "fish_pond", "recycling_yard", "village_houses" },
            new[] { "reservoir_dock", "fish_cages", "fertilizer_factory", "lakeside_restaurant", "water_intake" },
            new[] { "creek_sampling", "mountain_guesthouse", "stone_quarry", "mine_entrance", "irrigation_fields" }
        };
        static readonly string[] MapKeys = { "river_town_map", "reservoir_map", "mountain_creek_map" };
        static readonly Vector2[][] Positions =
        {
            new[] { new Vector2(1.8f, -0.4f), new Vector2(-4.2f, 3.8f), new Vector2(-7.15f, 0.2f), new Vector2(7.35f, 1.15f), new Vector2(5f, -4.55f) },
            new[] { new Vector2(-3.1f, -2.1f), new Vector2(-3f, 3.1f), new Vector2(-8.2f, 3.5f), new Vector2(-8.1f, -1.7f), new Vector2(3.8f, -6.4f) },
            new[] { new Vector2(0.55f, 0.15f), new Vector2(-7.35f, 2.85f), new Vector2(-2.45f, 4.85f), new Vector2(6.35f, 2.25f), new Vector2(-4.7f, -4.15f) }
        };
        static readonly Vector2[] Spawns = { new Vector2(4.55f, -6.05f), new Vector2(-3.6f, -6.2f), new Vector2(2.35f, -6.25f) };
        static readonly Color[] Horizons = { new Color(0.73f, 0.78f, 0.56f), new Color(0.76f, 0.8f, 0.62f), new Color(0.66f, 0.7f, 0.5f) };

        public static TownExplorer Build(int caseIndex, Font font)
        {
            if (caseIndex < 0 || caseIndex >= QingxiCases.Count) throw new ArgumentOutOfRangeException(nameof(caseIndex));
            PrepareAssets();
            CaseDefinition definition = QingxiCases.Get(caseIndex);
            var root = new GameObject("World · " + definition.PlaceName);
            var explorer = root.AddComponent<TownExplorer>();
            Transform scenery = Child(root.transform, "Scenery · 地貌与植被");
            Transform locations = Child(root.transform, "Investigation Sites · 调查地点");
            Transform boundary = Child(root.transform, "Map Boundary · 地图边界");

            Artwork(scenery, "Landscape · 地貌插画", MapKeys[caseIndex], Vector2.zero, new Vector2(22f, 16f), -1000, false, true);
            Boundary(boundary, new Vector2(0f, 8.15f), new Vector2(22.8f, 0.5f));
            Boundary(boundary, new Vector2(0f, -8.15f), new Vector2(22.8f, 0.5f));
            Boundary(boundary, new Vector2(-11.15f, 0f), new Vector2(0.5f, 16.8f));
            Boundary(boundary, new Vector2(11.15f, 0f), new Vector2(0.5f, 16.8f));

            PlayerMovement player = MakePlayer(root.transform, Spawns[caseIndex]);
            Camera camera = MakeCamera(root.transform, "World Camera · 跟随相机", Horizons[caseIndex], 4.8f);
            camera.transform.position = new Vector3(0f, -3.2f, -10f);
            var follow = camera.gameObject.AddComponent<CameraFollow>();
            follow.Configure(player.transform, camera, new Vector2(0f, 0.3f));
            var sites = new InvestigationSite[definition.Areas.Length];
            for (int i = 0; i < sites.Length; i++)
                sites[i] = MakeSite(locations, caseIndex, i, definition.Areas[i], font, camera);
            Decorate(scenery, caseIndex);
            explorer.Configure(caseIndex, player, follow, sites);
            QingxiWaterBuilder.Build(explorer);
            return explorer;
        }

        public static QingxiPoster BuildMenu(Font font)
        {
            PrepareAssets();
            var root = new GameObject("Menu Illustration · 清溪全景");
            var poster = root.AddComponent<QingxiPoster>();
            Camera camera = MakeCamera(root.transform, "Menu Camera", new Color(0.85f, 0.89f, 0.84f), 5.5f);
            Vector3 center = new Vector3(0f, 0f, -10f);
            camera.transform.position = center;
            poster.Configure(camera, center);
            string panoramaKey = FindSprite("menu_panorama", false) != null ? "menu_panorama" :
                FindSprite("qingxi_panorama", false) != null ? "qingxi_panorama" : null;
            if (panoramaKey != null)
            {
                // The portrait occupies the menu's open left half, preserving the illustration's aspect ratio.
                Artwork(root.transform, "Qingxi Panorama · 清溪全景插画", panoramaKey, new Vector2(-4.9f, 0f), new Vector2(9.8f, 12.25f), -1000, false);
            }
            else
            {
                for (int i = 0; i < 3; i++)
                {
                    Transform panel = Child(root.transform, QingxiCases.Get(i).PlaceName + " · 插画");
                    Vector2 at = new Vector2(-5f, 3.5f - i * 3.5f);
                    Artwork(panel, "Landscape", MapKeys[i], at, new Vector2(9f, 3.45f), -1000 + i, false, true);
                    Artwork(panel, "Landmark", LandmarkKeys[i][1], at + new Vector2(-0.1f, -1.2f), new Vector2(2.9f, 2.9f), 0, true);
                }
            }
            return poster;
        }

        static void PrepareAssets()
        {
            Directory.CreateDirectory(PrefabFolder);
            Directory.CreateDirectory(ArtFolder);
            AssetDatabase.Refresh();
            sprites.Clear();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtFolder }))
                foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                    if (asset is Sprite sprite) sprites[sprite.name] = sprite;
            string materialPath = ArtFolder + "/IllustrationUnlit.mat";
            illustrationMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (illustrationMaterial == null)
            {
                Shader shader = Shader.Find("Qingxi/Flat Illustration");
                if (shader == null) throw new InvalidOperationException("Missing sprite shader.");
                illustrationMaterial = new Material(shader);
                AssetDatabase.CreateAsset(illustrationMaterial, materialPath);
            }
            else illustrationMaterial.shader = Shader.Find("Qingxi/Flat Illustration");
            illustrationMaterial.SetFloat("_Flatness", 1f);
            illustrationMaterial.SetFloat("_PaletteSmoothness", 2f);
            EditorUtility.SetDirty(illustrationMaterial);
        }

        static Sprite FindSprite(string key, bool required = true)
        {
            if (sprites.TryGetValue(key, out Sprite sprite)) return sprite;
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtFolder + "/" + key + ".png");
            if (sprite == null && required) throw new InvalidOperationException("Missing illustration sprite: " + key);
            return sprite;
        }

        static PlayerMovement MakePlayer(Transform parent, Vector2 spawn)
        {
            var source = new GameObject("Player · 环境调查员");
            var body = source.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = source.AddComponent<CircleCollider2D>();
            collider.radius = 0.32f;
            SpriteRenderer artwork = Artwork(source.transform, "Illustration · 调查员", "player_investigator", Vector2.zero, new Vector2(0.95f, 1.3f), 100, true);
            var movement = source.AddComponent<PlayerMovement>();
            movement.Configure(artwork);
            QingxiPlayerAnimationBuilder.ConfigurePlayer(movement);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabFolder + "/Player.prefab");
            UnityEngine.Object.DestroyImmediate(source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = spawn;
            return instance.GetComponent<PlayerMovement>();
        }

        static InvestigationSite MakeSite(Transform parent, int chapter, int index, AreaDefinition area, Font font, Camera camera)
        {
            string key = LandmarkKeys[chapter][index];
            var source = new GameObject("Site " + (index + 1) + " · " + area.ShortName);
            SpriteRenderer art = Artwork(source.transform, "Illustration · 地点插画", key, new Vector2(0f, -0.15f), new Vector2(3.2f, 3.2f), 100, true);
            source.AddComponent<WorldDepthSorting>().Configure(art, source.transform);
            Transform entrance = Child(source.transform, "Entrance · 调查入口");
            entrance.localPosition = new Vector3(0f, -0.85f, 0f);
            Transform footprint = Child(source.transform, "Footprint · 建筑碰撞");
            var collider = footprint.gameObject.AddComponent<BoxCollider2D>();
            collider.offset = new Vector2(0f, 0.6f);
            collider.size = new Vector2(2.05f, 1.05f);
            float labelHeight = art.bounds.max.y - source.transform.position.y + 0.45f;
            CanvasGroup label = MakeLabel(source.transform, area.ShortName, font, labelHeight, out Text title, out Text status);
            var site = source.AddComponent<InvestigationSite>();
            site.Configure(index, entrance, art.transform, label, title, status);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabFolder + "/Site_" + key + ".prefab");
            UnityEngine.Object.DestroyImmediate(source);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.transform.localPosition = Positions[chapter][index];
            instance.GetComponentInChildren<SpriteRenderer>().sortingOrder = WorldDepthSorting.Order(instance.transform.position.y);
            instance.GetComponentInChildren<Canvas>().worldCamera = camera;
            return instance.GetComponent<InvestigationSite>();
        }

        static CanvasGroup MakeLabel(Transform parent, string name, Font font, float height, out Text title, out Text status)
        {
            var go = new GameObject("Label · 地名与调查进度", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            go.transform.localScale = Vector3.one * 0.01f;
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 2000;
            ((RectTransform)go.transform).sizeDelta = new Vector2(220f, 72f);
            var group = go.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var plaque = new GameObject("Plaque", typeof(RectTransform), typeof(Image));
            plaque.transform.SetParent(go.transform, false);
            Stretch((RectTransform)plaque.transform, Vector2.zero, Vector2.one);
            Image image = plaque.GetComponent<Image>();
            image.color = new Color(1f, 0.98f, 0.91f, 0.95f);
            image.raycastTarget = false;
            title = LabelText(go.transform, "Name", name, font, 28, new Vector2(0f, 0.44f), Vector2.one);
            title.fontStyle = FontStyle.Bold;
            status = LabelText(go.transform, "Progress", "未调查", font, 21, Vector2.zero, new Vector2(1f, 0.44f));
            return group;
        }

        static Text LabelText(Transform parent, string name, string value, Font font, int size, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, min, max);
            var text = go.GetComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.14f, 0.25f, 0.22f);
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static void Decorate(Transform parent, int chapter)
        {
            string tree = chapter == 2 ? "pine_tree" : "broadleaf_tree";
            if (FindSprite(tree, false) == null) return;
            var source = new GameObject(tree + " · 植被");
            SpriteRenderer artwork = Artwork(source.transform, "Illustration", tree, Vector2.zero, new Vector2(1.65f, 2.1f), 0, true);
            source.AddComponent<WorldDepthSorting>().Configure(artwork, source.transform);
            var trunk = source.AddComponent<CircleCollider2D>();
            trunk.radius = 0.32f;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabFolder + "/" + tree + ".prefab");
            UnityEngine.Object.DestroyImmediate(source);
            Vector2[] positions = { new Vector2(-9.4f, 5.6f), new Vector2(-8.2f, 5.95f), new Vector2(9.15f, -2.3f), new Vector2(-9.2f, -6.5f), new Vector2(8.7f, 5.7f) };
            for (int i = 0; i < positions.Length; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.transform.localPosition = positions[i];
                instance.transform.localScale = Vector3.one * (0.85f + i % 3 * 0.12f);
                instance.GetComponentInChildren<SpriteRenderer>().sortingOrder = WorldDepthSorting.Order(positions[i].y);
            }

            Vector2[][] reeds =
            {
                new[] { new Vector2(4.8f, 4.7f), new Vector2(2.3f, -3.7f) },
                new[] { new Vector2(-3.6f, 0.5f), new Vector2(8.5f, 4.8f) },
                new[] { new Vector2(3.4f, 2.1f), new Vector2(-6.4f, -6.4f) }
            };
            Vector2[][] rocks =
            {
                new[] { new Vector2(-5.1f, -6.6f), new Vector2(8.8f, -5.8f) },
                new[] { new Vector2(-9.6f, -4.4f), new Vector2(7f, -6.4f) },
                new[] { new Vector2(3.8f, 4.4f), new Vector2(8.2f, 0.1f) }
            };
            PlaceIllustratedProps(parent, "reeds", new Vector2(0.8f, 1f), reeds[chapter], 0f);
            PlaceIllustratedProps(parent, "rocks", new Vector2(1.1f, 0.85f), rocks[chapter], 0.26f);
        }

        static void PlaceIllustratedProps(Transform parent, string key, Vector2 size, Vector2[] positions, float radius)
        {
            if (FindSprite(key, false) == null) return;
            var source = new GameObject(key + " · 环境插画");
            SpriteRenderer artwork = Artwork(source.transform, "Illustration", key, Vector2.zero, size, 0, true);
            source.AddComponent<WorldDepthSorting>().Configure(artwork, source.transform);
            if (radius > 0f) source.AddComponent<CircleCollider2D>().radius = radius;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, PrefabFolder + "/" + key + ".prefab");
            UnityEngine.Object.DestroyImmediate(source);
            foreach (Vector2 position in positions)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.transform.localPosition = position;
                instance.GetComponentInChildren<SpriteRenderer>().sortingOrder = WorldDepthSorting.Order(position.y);
            }
        }

        static SpriteRenderer Artwork(Transform parent, string name, string key, Vector2 position, Vector2 size, int order, bool bottom, bool stretch = false)
        {
            Sprite sprite = FindSprite(key);
            Transform go = Child(parent, name);
            var renderer = go.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sharedMaterial = illustrationMaterial;
            renderer.sortingOrder = order;
            Vector3 bounds = sprite.bounds.size;
            float xScale = size.x / bounds.x;
            float yScale = stretch ? size.y / bounds.y : xScale;
            if (!stretch && bounds.y * yScale > size.y) xScale = yScale = size.y / bounds.y;
            go.localScale = new Vector3(xScale, yScale, 1f);
            Vector3 anchor = bottom ? new Vector3(sprite.bounds.center.x, sprite.bounds.min.y, 0f) : sprite.bounds.center;
            go.localPosition = (Vector3)position - Vector3.Scale(anchor, go.localScale);
            return renderer;
        }

        static Camera MakeCamera(Transform parent, string name, Color background, float size)
        {
            Transform go = Child(parent, name);
            var camera = go.gameObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = size;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.depth = 0f;
            camera.tag = "MainCamera";
            go.gameObject.AddComponent<UniversalAdditionalCameraData>();
            go.gameObject.AddComponent<AudioListener>();
            return camera;
        }

        static Transform Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static void Boundary(Transform parent, Vector2 position, Vector2 size)
        {
            Transform wall = Child(parent, "Boundary");
            wall.localPosition = position;
            wall.gameObject.AddComponent<BoxCollider2D>().size = size;
        }
    }
}
