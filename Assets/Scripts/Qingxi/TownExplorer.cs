using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Qingxi
{
    /// <summary>
    /// Top-down town. Walk with WASD and press E beside a place to investigate.
    /// </summary>
    public sealed class TownExplorer : MonoBehaviour
    {
        const float MoveSpeed = 4.6f;
        const float InteractRange = 2.15f;

        static readonly Vector2[] TownSpots =
        {
            new Vector2(1.8f, 0.35f),
            new Vector2(3.3f, 5.35f),
            new Vector2(-7.15f, 1.85f),
            new Vector2(7.35f, 1.15f),
            new Vector2(-6.15f, -4.55f),
        };

        static readonly Vector2[] LakeSpots =
        {
            new Vector2(1.15f, -0.7f),
            new Vector2(-3.7f, 3.35f),
            new Vector2(-8.75f, 1.55f),
            new Vector2(8.05f, 3.55f),
            new Vector2(3.45f, -5.9f),
        };

        static readonly Vector2[] CreekSpots =
        {
            new Vector2(0.55f, 0.15f),
            new Vector2(-7.35f, 2.85f),
            new Vector2(-2.45f, 5.45f),
            new Vector2(6.35f, 2.25f),
            new Vector2(-4.7f, -4.15f),
        };

        readonly struct Stamp
        {
            public readonly Vector2 Door;
            public readonly float LabelLift;
            public readonly float Notice;

            public Stamp(Vector2 door, float labelLift, float notice)
            {
                Door = door;
                LabelLift = labelLift;
                Notice = notice;
            }
        }

        sealed class Place
        {
            public int Index;
            public Transform Root;
            public Vector2 DoorLocal;
            public float Notice;
            public CanvasGroup Label;
            public Text Status;
            public SpriteRenderer[] Parts;
            public Color[] PartColors;

            public Vector2 Door
            {
                get { return Root.TransformPoint(DoorLocal); }
            }
        }

        readonly List<Place> places = new List<Place>();
        readonly List<Camera> mutedCameras = new List<Camera>();

        InvestigationSession session;
        Rigidbody2D body;
        Transform player;
        Transform playerVisual;
        SpriteRenderer[] playerSprites;
        float facing = 1f;
        Camera townCamera;
        Transform camTransform;
        Sprite square;
        Sprite disc;
        int nearby = -1;
        bool presenting;
        Vector2 landmark;
        const float RoamSize = 5.5f;
        const float PresentSize = 8.6f;

        public bool MovementLocked { get; set; }
        public Action<int> OnOpen;
        public Action OnClose;

        public string Prompt
        {
            get
            {
                if (MovementLocked)
                    return "按 E 离开";
                if (nearby < 0)
                    return "WASD 移动    ·    靠近场所后按 E";
                string name = session.Case.Areas[nearby].Name;
                return "按 E 调查  " + name;
            }
        }

        public void Build(InvestigationSession activeSession, Font font)
        {
            session = activeSession;
            square = SquareSprite();
            disc = DiscSprite();
            BuildScenery();
            BuildPlaces(font);
            BuildPlayer();
            BuildCamera();
            BindWorldCanvases();
        }

        void OnEnable()
        {
            if (townCamera == null)
                return;

            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            for (int i = 0; i < cameras.Length; i++)
            {
                if (cameras[i] == townCamera || !cameras[i].enabled)
                    continue;
                cameras[i].enabled = false;
                mutedCameras.Add(cameras[i]);
            }

            townCamera.enabled = true;
        }

        void OnDisable()
        {
            if (townCamera != null)
                townCamera.enabled = false;
            for (int i = 0; i < mutedCameras.Count; i++)
            {
                if (mutedCameras[i] != null)
                    mutedCameras[i].enabled = true;
            }

            mutedCameras.Clear();
        }

        public void PresentLandmark()
        {
            presenting = true;
            MovementLocked = true;
            switch (MapId())
            {
                case 1:
                    landmark = new Vector2(0.2f, 0.7f);
                    break;
                case 2:
                    landmark = new Vector2(1.4f, 1.1f);
                    break;
                default:
                    landmark = new Vector2(0.8f, 1.5f);
                    break;
            }
        }

        public void ResumeExplore()
        {
            presenting = false;
            MovementLocked = false;
        }

        void Update()
        {
            if (player == null || presenting)
                return;

            nearby = FindNearby();
            RefreshLabels();
            RefreshHighlight();
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                if (MovementLocked)
                    OnClose?.Invoke();
                else if (nearby >= 0)
                    OnOpen?.Invoke(nearby);
            }
        }

        void FixedUpdate()
        {
            if (body == null)
                return;

            Vector2 move = MovementLocked ? Vector2.zero : ReadMove();
            body.linearVelocity = move * MoveSpeed;
            if (playerVisual == null)
                return;

            float moving = Mathf.Clamp01(body.linearVelocity.magnitude / MoveSpeed);
            float bob = Mathf.Sin(Time.time * 11f) * 0.05f * moving;
            playerVisual.localPosition = new Vector3(0f, bob, 0f);
            if (move.x < -0.1f)
                facing = -1f;
            else if (move.x > 0.1f)
                facing = 1f;
            playerVisual.localScale = new Vector3(facing, 1f, 1f);
        }

        void LateUpdate()
        {
            if (player == null || camTransform == null)
                return;

            Vector2 focus = presenting ? landmark : (Vector2)player.position;
            Vector3 desired = new Vector3(focus.x, focus.y, -10f);
            float blend = 1f - Mathf.Pow(0.001f, Time.deltaTime);
            camTransform.position = Vector3.Lerp(camTransform.position, desired, blend);
            float size = presenting ? PresentSize : RoamSize;
            townCamera.orthographicSize = Mathf.Lerp(townCamera.orthographicSize, size, blend);
            int order = Order(player.position.y);
            for (int i = 0; i < playerSprites.Length; i++)
                playerSprites[i].sortingOrder = order + i;
        }

        public void RefreshMarks()
        {
            for (int i = 0; i < places.Count; i++)
            {
                AreaProgress progress = session.GetProgress(places[i].Index);
                if (progress.DeepDone)
                {
                    places[i].Status.text = "已深查";
                    places[i].Status.color = new Color(0.12f, 0.42f, 0.35f, 1f);
                }
                else if (progress.SurfaceDone)
                {
                    places[i].Status.text = "已观察";
                    places[i].Status.color = new Color(0.11f, 0.16f, 0.14f, 1f);
                }
                else
                {
                    places[i].Status.text = "未调查";
                    places[i].Status.color = new Color(0.37f, 0.42f, 0.39f, 1f);
                }
            }
        }

        int MapId()
        {
            if (session.Case.ChapterName == "水库藻华")
                return 1;
            if (session.Case.ChapterName == "山溪赤浊")
                return 2;
            return 0;
        }

        Vector2 SpotFor(int index)
        {
            switch (MapId())
            {
                case 1: return LakeSpots[index];
                case 2: return CreekSpots[index];
                default: return TownSpots[index];
            }
        }

        Vector3 Entrance()
        {
            switch (MapId())
            {
                case 1: return new Vector3(7.6f, -4.35f, 0f);
                case 2: return new Vector3(2.35f, -6.25f, 0f);
                default: return new Vector3(4.55f, -6.05f, 0f);
            }
        }

        Color Horizon()
        {
            switch (MapId())
            {
                case 1: return new Color(0.62f, 0.74f, 0.68f, 1f);
                case 2: return new Color(0.52f, 0.48f, 0.36f, 1f);
                default: return new Color(0.56f, 0.68f, 0.48f, 1f);
            }
        }

        void BuildScenery()
        {
            Wall(new Vector2(0f, 8.15f), new Vector2(22.4f, 0.5f));
            Wall(new Vector2(0f, -8.15f), new Vector2(22.4f, 0.5f));
            Wall(new Vector2(-11.15f, 0f), new Vector2(0.5f, 16.6f));
            Wall(new Vector2(11.15f, 0f), new Vector2(0.5f, 16.6f));
            switch (MapId())
            {
                case 1:
                    BuildLakeMap();
                    break;
                case 2:
                    BuildCreekMap();
                    break;
                default:
                    BuildTownMap();
                    break;
            }
        }

        void BuildTownMap()
        {
            Color dirt = new Color(0.84f, 0.74f, 0.52f, 1f);
            Color leaf = new Color(0.28f, 0.5f, 0.32f, 1f);
            Paint(transform, "Ground", new Color(0.62f, 0.74f, 0.5f, 1f), Vector2.zero, new Vector2(22f, 16f), -80);
            Paint(transform, "River", new Color(0.38f, 0.64f, 0.74f, 1f), new Vector2(-0.35f, 0.15f), new Vector2(2.35f, 15f), -70);
            Paint(transform, "Current", new Color(0.26f, 0.5f, 0.62f, 1f), new Vector2(-0.35f, 0.15f), new Vector2(0.55f, 14.2f), -69);
            Paint(transform, "Bank", new Color(0.8f, 0.72f, 0.5f, 1f), new Vector2(1.05f, 0.15f), new Vector2(0.38f, 15f), -68);
            Paint(transform, "Street", dirt, new Vector2(2.7f, 0.1f), new Vector2(1.15f, 12.4f), -60);
            Paint(transform, "WestRoad", dirt, new Vector2(-2.4f, 1.7f), new Vector2(7.2f, 1.05f), -60);
            Paint(transform, "EastRoad", dirt, new Vector2(4.8f, 1.15f), new Vector2(5.4f, 1.0f), -60);
            Paint(transform, "SouthRoad", dirt, new Vector2(-1.6f, -4.55f), new Vector2(8.6f, 1.0f), -60);
            Paint(transform, "Gate", dirt, new Vector2(4.55f, -5.3f), new Vector2(1.05f, 1.8f), -60);
            Paint(transform, "Bridge", new Color(0.62f, 0.42f, 0.24f, 1f), new Vector2(-0.35f, 1.7f), new Vector2(3.5f, 1.2f), -58);
            Paint(transform, "Plank", new Color(0.5f, 0.34f, 0.18f, 1f), new Vector2(-0.35f, 1.7f), new Vector2(3.2f, 0.12f), -57);
            Reed(new Vector2(-1.7f, 4.6f));
            Reed(new Vector2(-1.55f, 2.4f));
            Reed(new Vector2(-1.75f, -2.2f));
            Reed(new Vector2(0.9f, -3.4f));
            TreeAt(new Vector2(-9.3f, 6.3f), 1.05f, leaf);
            TreeAt(new Vector2(-8.4f, 5.5f), 0.8f, leaf);
            TreeAt(new Vector2(-9.1f, -6.3f), 0.95f, leaf);
            TreeAt(new Vector2(9.1f, 5.7f), 0.9f, leaf);
            TreeAt(new Vector2(9.3f, -3.6f), 0.75f, new Color(0.22f, 0.42f, 0.28f, 1f));
            TreeAt(new Vector2(-2.2f, 6.6f), 0.85f, leaf);
            TreeAt(new Vector2(0.4f, 6.7f), 0.7f, leaf);
        }

        void BuildLakeMap()
        {
            Color shore = new Color(0.88f, 0.82f, 0.66f, 1f);
            Color water = new Color(0.3f, 0.58f, 0.66f, 1f);
            Color deep = new Color(0.16f, 0.4f, 0.52f, 1f);
            Color algae = new Color(0.42f, 0.7f, 0.36f, 0.92f);
            Paint(transform, "Ground", new Color(0.76f, 0.78f, 0.62f, 1f), Vector2.zero, new Vector2(22f, 16f), -80);
            Blob(transform, "Beach", new Color(0.86f, 0.8f, 0.6f, 1f), new Vector2(0.1f, 0.85f), new Vector2(12.4f, 9.1f), -74);
            Blob(transform, "Lake", water, new Vector2(0.05f, 1.15f), new Vector2(9.6f, 6.9f), -70);
            Blob(transform, "Deep", deep, new Vector2(-0.15f, 1.45f), new Vector2(5.8f, 4.1f), -69);
            Blob(transform, "BloomA", algae, new Vector2(-1.4f, 2.3f), new Vector2(2.2f, 1.1f), -68);
            Blob(transform, "BloomB", algae, new Vector2(1.5f, 0.6f), new Vector2(1.6f, 0.7f), -68);
            Paint(transform, "Tributary", water, new Vector2(-6.3f, 0.55f), new Vector2(3.6f, 0.55f), -71);
            Paint(transform, "Dam", new Color(0.68f, 0.7f, 0.68f, 1f), new Vector2(0.35f, -2.95f), new Vector2(8.4f, 0.48f), -66);
            Paint(transform, "Spill", new Color(0.45f, 0.62f, 0.7f, 1f), new Vector2(0.2f, -2.95f), new Vector2(0.7f, 0.7f), -65);
            Paint(transform, "SouthRoad", shore, new Vector2(2.4f, -4.35f), new Vector2(11.4f, 0.95f), -60);
            Paint(transform, "EastRoad", shore, new Vector2(6.15f, -0.35f), new Vector2(1.05f, 8.0f), -60);
            Paint(transform, "WestRoad", shore, new Vector2(-6.45f, 0.9f), new Vector2(1.05f, 7.2f), -60);
            Paint(transform, "NorthRoad", shore, new Vector2(-1.8f, 5.35f), new Vector2(9.2f, 0.95f), -60);
            Paint(transform, "Pier", new Color(0.62f, 0.48f, 0.3f, 1f), new Vector2(-4.6f, 2.15f), new Vector2(2.2f, 0.28f), -64);
            Blob(transform, "Buoy", new Color(0.9f, 0.32f, 0.2f, 1f), new Vector2(2.1f, 2.4f), new Vector2(0.28f, 0.28f), -67);
            Reed(new Vector2(-4.8f, 4.3f));
            Reed(new Vector2(4.5f, 3.6f));
            Reed(new Vector2(-5.1f, -0.8f));
            TreeAt(new Vector2(-9.4f, 6.4f), 0.8f, new Color(0.34f, 0.52f, 0.34f, 1f));
            TreeAt(new Vector2(9.3f, 6.2f), 0.7f, new Color(0.34f, 0.52f, 0.34f, 1f));
            TreeAt(new Vector2(-9.2f, -6.2f), 0.85f, new Color(0.3f, 0.48f, 0.32f, 1f));
        }

        void BuildCreekMap()
        {
            Color trail = new Color(0.7f, 0.62f, 0.46f, 1f);
            Color rust = new Color(0.7f, 0.38f, 0.24f, 1f);
            Color pine = new Color(0.22f, 0.38f, 0.26f, 1f);
            Paint(transform, "Ground", new Color(0.56f, 0.52f, 0.36f, 1f), Vector2.zero, new Vector2(22f, 16f), -80);
            Blob(transform, "Meadow", new Color(0.58f, 0.66f, 0.4f, 1f), new Vector2(-6.6f, 2.4f), new Vector2(5.4f, 4.2f), -78);
            Blob(transform, "Hill", new Color(0.46f, 0.48f, 0.36f, 1f), new Vector2(6.4f, 1.7f), new Vector2(7.4f, 8.4f), -76);
            Blob(transform, "Ridge", new Color(0.4f, 0.4f, 0.32f, 1f), new Vector2(7.6f, 3.4f), new Vector2(3.2f, 2.4f), -75);
            Blob(transform, "CreekA", rust, new Vector2(5.2f, 4.6f), new Vector2(2.4f, 0.7f), -70);
            Blob(transform, "CreekB", rust, new Vector2(3.2f, 2.6f), new Vector2(0.7f, 2.6f), -70);
            Blob(transform, "CreekC", rust, new Vector2(1.2f, 0.7f), new Vector2(2.6f, 0.65f), -70);
            Blob(transform, "CreekD", rust, new Vector2(-1.1f, -1.5f), new Vector2(0.65f, 2.5f), -70);
            Blob(transform, "CreekE", rust, new Vector2(-3.3f, -3.5f), new Vector2(2.8f, 0.6f), -70);
            Paint(transform, "Foam", new Color(0.94f, 0.9f, 0.84f, 0.85f), new Vector2(3.1f, 3.5f), new Vector2(0.28f, 0.12f), -68);
            Paint(transform, "SouthTrail", trail, new Vector2(2.35f, -3.2f), new Vector2(1.05f, 6.2f), -60);
            Paint(transform, "BendTrail", trail, new Vector2(-1.2f, 0.9f), new Vector2(5.2f, 0.9f), -60);
            Paint(transform, "HillTrail", trail, new Vector2(3.6f, 2.2f), new Vector2(4.6f, 0.85f), -60);
            Paint(transform, "QuarryTrail", trail, new Vector2(-2.5f, 3.2f), new Vector2(1.0f, 4.2f), -60);
            Paint(transform, "FieldTrail", trail, new Vector2(-3.2f, -3.6f), new Vector2(4.2f, 0.85f), -60);
            Paint(transform, "TerraceA", new Color(0.5f, 0.62f, 0.34f, 1f), new Vector2(-6.6f, -5.4f), new Vector2(2.2f, 0.7f), -74);
            Paint(transform, "TerraceB", new Color(0.62f, 0.66f, 0.32f, 1f), new Vector2(-4.2f, -6.1f), new Vector2(1.8f, 0.55f), -74);
            RockAt(new Vector2(3.6f, 4.4f), 0.7f, true);
            RockAt(new Vector2(4.6f, 3.3f), 0.5f, false);
            RockAt(new Vector2(-0.6f, 2.6f), 0.55f, true);
            RockAt(new Vector2(8.6f, 5.5f), 0.8f, true);
            RockAt(new Vector2(8.2f, -1.4f), 0.6f, true);
            RockAt(new Vector2(-3.6f, 6.5f), 0.65f, true);
            TreeAt(new Vector2(-9.2f, 5.6f), 0.7f, pine);
            TreeAt(new Vector2(-8.3f, 4.6f), 0.55f, pine);
            TreeAt(new Vector2(-9.1f, 1.0f), 0.6f, pine);
            TreeAt(new Vector2(-8.8f, -6.1f), 0.65f, pine);
            TreeAt(new Vector2(9.4f, 6.3f), 0.5f, pine);
        }

        void Reed(Vector2 pos)
        {
            int order = Order(pos.y);
            Paint(transform, "Reed", new Color(0.24f, 0.48f, 0.28f, 1f), pos, new Vector2(0.1f, 0.42f), order);
            Paint(transform, "ReedB", new Color(0.32f, 0.55f, 0.3f, 1f), pos + new Vector2(0.14f, -0.05f), new Vector2(0.08f, 0.32f), order);
        }

        void TreeAt(Vector2 pos, float canopy, Color leaves)
        {
            var tree = new GameObject("Tree");
            tree.transform.SetParent(transform, false);
            tree.transform.position = pos;
            int order = Order(pos.y);
            Paint(tree.transform, "Trunk", new Color(0.42f, 0.3f, 0.18f, 1f), new Vector2(0f, -0.16f), new Vector2(0.18f, 0.38f), order - 1);
            Blob(tree.transform, "Canopy", leaves, new Vector2(0f, 0.2f), new Vector2(canopy, canopy * 0.9f), order);
            var trunk = tree.AddComponent<CircleCollider2D>();
            trunk.radius = canopy * 0.28f;
        }

        void RockAt(Vector2 pos, float size, bool blocking)
        {
            Blob(transform, "Rock", new Color(0.5f, 0.48f, 0.44f, 1f), pos, new Vector2(size, size * 0.75f), Order(pos.y));
            if (blocking)
                SolidCircle(transform, pos, size * 0.3f);
        }

        void BuildPlaces(Font font)
        {
            for (int i = 0; i < session.Case.Areas.Length; i++)
            {
                AreaDefinition area = session.Case.Areas[i];
                Vector2 spot = SpotFor(i);
                var root = new GameObject("Place_" + area.ShortName);
                root.transform.SetParent(transform, false);
                root.transform.position = spot;

                int order = Order(spot.y);
                Stamp stamp = PaintPlace(root.transform, area.ShortName, order);
                CanvasGroup label = CreateLabel(root.transform, font, area.ShortName, spot.y, stamp.LabelLift, out Text status);
                SpriteRenderer[] parts = root.GetComponentsInChildren<SpriteRenderer>();
                var colors = new Color[parts.Length];
                for (int p = 0; p < parts.Length; p++)
                    colors[p] = parts[p].color;

                places.Add(new Place
                {
                    Index = i,
                    Root = root.transform,
                    DoorLocal = stamp.Door,
                    Notice = stamp.Notice,
                    Label = label,
                    Status = status,
                    Parts = parts,
                    PartColors = colors
                });
            }

            RefreshMarks();
        }

        Stamp PaintPlace(Transform root, string shortName, int order)
        {
            switch (shortName)
            {
                case "河流": return PaintRiver(root, order);
                case "养殖场": return PaintFarm(root, order);
                case "鱼塘": return PaintFishPond(root, order);
                case "回收站": return PaintRecycle(root, order);
                case "居民社区": return PaintHouses(root, order);
                case "库区": return PaintReservoir(root, order);
                case "网箱": return PaintCages(root, order);
                case "化肥厂": return PaintFactory(root, order);
                case "餐馆": return PaintRestaurant(root, order);
                case "取水口": return PaintIntake(root, order);
                case "山溪": return PaintCreek(root, order);
                case "农家乐": return PaintFarmstay(root, order);
                case "采石场": return PaintQuarry(root, order);
                case "矿硐": return PaintMine(root, order);
                case "灌区": return PaintIrrigation(root, order);
                default: return PaintFallback(root, order);
            }
        }

        Stamp PaintRiver(Transform root, int order)
        {
            Color water = new Color(0.40f, 0.52f, 0.42f, 1f);
            Color oil = new Color(0.72f, 0.66f, 0.38f, 0.9f);
            Color wood = new Color(0.58f, 0.40f, 0.22f, 1f);
            Color post = new Color(0.36f, 0.24f, 0.14f, 1f);
            Blob(root, "Turbid", water, new Vector2(-1.05f, 0.12f), new Vector2(1.85f, 1.35f), order - 2);
            Blob(root, "Oil", oil, new Vector2(-1.2f, 0.28f), new Vector2(0.7f, 0.28f), order - 1);
            Paint(root, "ChipA", new Color(0.85f, 0.88f, 0.9f, 1f), new Vector2(-0.7f, -0.05f), new Vector2(0.16f, 0.1f), order, null, 24f);
            Paint(root, "ChipB", new Color(0.25f, 0.32f, 0.34f, 1f), new Vector2(-1.35f, -0.12f), new Vector2(0.14f, 0.08f), order, null, -16f);
            Paint(root, "Plank", wood, new Vector2(0.2f, -0.05f), new Vector2(1.15f, 0.16f), order);
            Paint(root, "Plank2", wood, new Vector2(0.2f, -0.28f), new Vector2(1.15f, 0.16f), order);
            Paint(root, "PostA", post, new Vector2(-0.28f, -0.16f), new Vector2(0.1f, 0.46f), order + 1);
            Paint(root, "PostB", post, new Vector2(0.68f, -0.16f), new Vector2(0.1f, 0.46f), order + 1);
            Solid(root, new Vector2(-1.05f, 0.12f), new Vector2(1.35f, 0.95f));
            return new Stamp(new Vector2(0.2f, -0.95f), 1.2f, 2.7f);
        }

        Stamp PaintFarm(Transform root, int order)
        {
            Color mud = new Color(0.62f, 0.5f, 0.32f, 1f);
            Color wall = new Color(0.78f, 0.62f, 0.4f, 1f);
            Color roof = new Color(0.55f, 0.28f, 0.2f, 1f);
            Color pig = new Color(0.9f, 0.62f, 0.58f, 1f);
            Blob(root, "Yard", mud, new Vector2(0f, -0.05f), new Vector2(2.7f, 1.9f), order - 3);
            Yard(root, new Vector2(0f, -0.05f), new Vector2(2.55f, 1.75f), order - 1);
            Hut(root, new Vector2(0.05f, 0.42f), new Vector2(2.15f, 0.82f), wall, roof, order, true);
            Blob(root, "Silo", new Color(0.72f, 0.74f, 0.7f, 1f), new Vector2(1.05f, -0.28f), new Vector2(0.58f, 0.58f), order + 1);
            Blob(root, "SiloCap", new Color(0.45f, 0.48f, 0.46f, 1f), new Vector2(1.05f, -0.28f), new Vector2(0.28f, 0.28f), order + 2);
            SolidCircle(root, new Vector2(1.05f, -0.28f), 0.3f);
            Blob(root, "PigA", pig, new Vector2(-0.75f, -0.35f), new Vector2(0.42f, 0.26f), order);
            Blob(root, "PigB", pig, new Vector2(-0.28f, -0.48f), new Vector2(0.36f, 0.22f), order);
            Paint(root, "Trough", new Color(0.42f, 0.32f, 0.2f, 1f), new Vector2(-0.5f, -0.15f), new Vector2(0.7f, 0.12f), order);
            return new Stamp(new Vector2(-0.35f, -1.2f), 1.55f, 3.2f);
        }

        Stamp PaintFishPond(Transform root, int order)
        {
            Color water = new Color(0.36f, 0.66f, 0.7f, 1f);
            Color deep = new Color(0.18f, 0.42f, 0.52f, 1f);
            Color fish = new Color(0.93f, 0.58f, 0.28f, 1f);
            Color reed = new Color(0.25f, 0.5f, 0.28f, 1f);
            Blob(root, "Pond", water, new Vector2(0f, 0.22f), new Vector2(2.5f, 1.8f), order - 2);
            Blob(root, "Deep", deep, new Vector2(0.08f, 0.28f), new Vector2(1.45f, 1.0f), order - 1);
            Paint(root, "FishA", fish, new Vector2(-0.4f, 0.4f), new Vector2(0.32f, 0.14f), order, null, 16f);
            Paint(root, "FishB", fish, new Vector2(0.35f, 0.15f), new Vector2(0.28f, 0.12f), order, null, -20f);
            Paint(root, "FishC", new Color(0.95f, 0.9f, 0.85f, 1f), new Vector2(-0.05f, 0.55f), new Vector2(0.18f, 0.1f), order, null, 40f);
            Paint(root, "ReedA", reed, new Vector2(-1.05f, 0.55f), new Vector2(0.1f, 0.4f), order + 1);
            Paint(root, "ReedB", reed, new Vector2(1.0f, 0.15f), new Vector2(0.1f, 0.36f), order + 1);
            Paint(root, "ReedC", reed, new Vector2(-0.85f, -0.15f), new Vector2(0.1f, 0.32f), order + 1);
            Paint(root, "Walk", new Color(0.6f, 0.42f, 0.24f, 1f), new Vector2(0f, -0.78f), new Vector2(1.3f, 0.2f), order + 1);
            Solid(root, new Vector2(0f, 0.28f), new Vector2(1.8f, 1.15f));
            return new Stamp(new Vector2(0f, -1.22f), 1.5f, 3.15f);
        }

        Stamp PaintRecycle(Transform root, int order)
        {
            Color dirt = new Color(0.7f, 0.66f, 0.54f, 1f);
            Blob(root, "Lot", dirt, new Vector2(0f, 0.05f), new Vector2(2.6f, 1.85f), order - 3);
            Yard(root, new Vector2(0f, 0.05f), new Vector2(2.45f, 1.7f), order - 1);
            Blob(root, "PileBlue", new Color(0.28f, 0.48f, 0.75f, 1f), new Vector2(-0.7f, 0.4f), new Vector2(0.7f, 0.5f), order);
            Blob(root, "PileGreen", new Color(0.3f, 0.58f, 0.38f, 1f), new Vector2(0.05f, 0.5f), new Vector2(0.55f, 0.42f), order);
            Blob(root, "PileYellow", new Color(0.86f, 0.72f, 0.28f, 1f), new Vector2(-0.35f, 0.05f), new Vector2(0.6f, 0.4f), order);
            SolidCircle(root, new Vector2(-0.7f, 0.4f), 0.32f);
            SolidCircle(root, new Vector2(0.05f, 0.5f), 0.24f);
            Hut(root, new Vector2(0.85f, 0.35f), new Vector2(0.85f, 0.62f), new Color(0.75f, 0.78f, 0.76f, 1f), new Color(0.35f, 0.48f, 0.5f, 1f), order, false);
            Paint(root, "BinA", new Color(0.2f, 0.55f, 0.38f, 1f), new Vector2(-0.85f, -0.35f), new Vector2(0.22f, 0.28f), order + 1);
            Paint(root, "BinB", new Color(0.2f, 0.4f, 0.7f, 1f), new Vector2(-0.55f, -0.35f), new Vector2(0.22f, 0.28f), order + 1);
            return new Stamp(new Vector2(-0.15f, -1.15f), 1.45f, 3.15f);
        }

        Stamp PaintHouses(Transform root, int order)
        {
            Paint(root, "Lane", new Color(0.82f, 0.74f, 0.58f, 1f), new Vector2(0f, -0.15f), new Vector2(0.35f, 1.7f), order - 2);
            Paint(root, "LaneE", new Color(0.82f, 0.74f, 0.58f, 1f), new Vector2(0.15f, 0.15f), new Vector2(1.5f, 0.28f), order - 2);
            Hut(root, new Vector2(-0.75f, 0.42f), new Vector2(0.85f, 0.68f), new Color(0.9f, 0.86f, 0.78f, 1f), new Color(0.62f, 0.28f, 0.22f, 1f), order, true);
            Hut(root, new Vector2(0.62f, 0.5f), new Vector2(0.78f, 0.62f), new Color(0.86f, 0.9f, 0.86f, 1f), new Color(0.28f, 0.48f, 0.42f, 1f), order, false);
            Hut(root, new Vector2(0.72f, -0.28f), new Vector2(0.72f, 0.58f), new Color(0.93f, 0.88f, 0.8f, 1f), new Color(0.45f, 0.48f, 0.52f, 1f), order, true);
            Hut(root, new Vector2(-0.62f, -0.2f), new Vector2(0.8f, 0.6f), new Color(0.88f, 0.8f, 0.7f, 1f), new Color(0.7f, 0.48f, 0.28f, 1f), order, false);
            Blob(root, "Tree", new Color(0.28f, 0.5f, 0.32f, 1f), new Vector2(-1.2f, -0.72f), new Vector2(0.42f, 0.42f), order + 3);
            return new Stamp(new Vector2(0.05f, -1.2f), 1.4f, 3.25f);
        }

        Stamp PaintReservoir(Transform root, int order)
        {
            Color water = new Color(0.32f, 0.66f, 0.6f, 1f);
            Color algae = new Color(0.45f, 0.72f, 0.38f, 0.95f);
            Blob(root, "Lake", water, new Vector2(-0.55f, 0.18f), new Vector2(2.5f, 1.75f), order - 2);
            Blob(root, "BloomA", algae, new Vector2(-0.85f, 0.4f), new Vector2(0.9f, 0.42f), order - 1);
            Blob(root, "BloomB", algae, new Vector2(-0.2f, 0.05f), new Vector2(0.65f, 0.3f), order - 1);
            Blob(root, "Buoy", new Color(0.9f, 0.35f, 0.22f, 1f), new Vector2(-1.15f, -0.15f), new Vector2(0.18f, 0.18f), order);
            Paint(root, "Pier", new Color(0.68f, 0.7f, 0.68f, 1f), new Vector2(0.7f, -0.55f), new Vector2(0.9f, 0.35f), order);
            Paint(root, "Rail", new Color(0.75f, 0.78f, 0.8f, 1f), new Vector2(0.7f, -0.38f), new Vector2(0.9f, 0.06f), order + 1);
            Solid(root, new Vector2(-0.55f, 0.22f), new Vector2(1.7f, 1.15f));
            return new Stamp(new Vector2(0.7f, -1.15f), 1.4f, 3.05f);
        }

        Stamp PaintCages(Transform root, int order)
        {
            Color water = new Color(0.34f, 0.6f, 0.66f, 1f);
            Color frame = new Color(0.85f, 0.72f, 0.4f, 1f);
            Blob(root, "Bay", water, new Vector2(0f, 0.25f), new Vector2(2.4f, 1.7f), order - 2);
            Cage(root, new Vector2(-0.45f, 0.55f), 0.62f, frame, order);
            Cage(root, new Vector2(0.4f, 0.55f), 0.62f, frame, order);
            Cage(root, new Vector2(-0.45f, 0.0f), 0.62f, frame, order);
            Cage(root, new Vector2(0.4f, 0.0f), 0.62f, frame, order);
            Blob(root, "BuoyA", new Color(0.9f, 0.4f, 0.2f, 1f), new Vector2(-0.95f, 0.9f), new Vector2(0.16f, 0.16f), order + 1);
            Blob(root, "BuoyB", new Color(0.9f, 0.4f, 0.2f, 1f), new Vector2(0.9f, -0.25f), new Vector2(0.16f, 0.16f), order + 1);
            Paint(root, "Boat", new Color(0.55f, 0.32f, 0.18f, 1f), new Vector2(0.95f, -0.55f), new Vector2(0.55f, 0.22f), order + 1);
            Paint(root, "Feed", new Color(0.72f, 0.58f, 0.32f, 1f), new Vector2(-1.0f, -0.45f), new Vector2(0.28f, 0.22f), order + 1);
            Solid(root, new Vector2(0f, 0.28f), new Vector2(1.55f, 1.15f));
            return new Stamp(new Vector2(0f, -1.15f), 1.5f, 3.15f);
        }

        Stamp PaintFactory(Transform root, int order)
        {
            Color hall = new Color(0.62f, 0.66f, 0.68f, 1f);
            Color roof = new Color(0.28f, 0.42f, 0.58f, 1f);
            Hut(root, new Vector2(-0.3f, 0.32f), new Vector2(1.7f, 0.95f), hall, roof, order, false);
            Blob(root, "SiloA", new Color(0.86f, 0.74f, 0.28f, 1f), new Vector2(0.95f, 0.5f), new Vector2(0.62f, 0.62f), order + 2);
            Blob(root, "SiloB", new Color(0.8f, 0.68f, 0.24f, 1f), new Vector2(0.95f, -0.15f), new Vector2(0.5f, 0.5f), order + 2);
            SolidCircle(root, new Vector2(0.95f, 0.5f), 0.32f);
            SolidCircle(root, new Vector2(0.95f, -0.15f), 0.26f);
            Paint(root, "Stack", new Color(0.45f, 0.48f, 0.5f, 1f), new Vector2(-0.95f, 0.72f), new Vector2(0.22f, 0.55f), order + 2);
            Blob(root, "Smoke", new Color(0.75f, 0.78f, 0.78f, 0.7f), new Vector2(-0.85f, 1.1f), new Vector2(0.4f, 0.22f), order + 3);
            Paint(root, "Pipe", new Color(0.4f, 0.44f, 0.46f, 1f), new Vector2(-1.15f, 0.05f), new Vector2(0.7f, 0.1f), order + 1);
            Paint(root, "Hazard", new Color(0.9f, 0.75f, 0.15f, 1f), new Vector2(-0.3f, -0.35f), new Vector2(1.3f, 0.1f), order + 1);
            return new Stamp(new Vector2(-0.55f, -1.15f), 1.65f, 3.2f);
        }

        Stamp PaintRestaurant(Transform root, int order)
        {
            Hut(root, new Vector2(0f, 0.4f), new Vector2(1.45f, 0.85f), new Color(0.93f, 0.86f, 0.74f, 1f), new Color(0.62f, 0.24f, 0.22f, 1f), order, true);
            Paint(root, "AwningA", new Color(0.78f, 0.22f, 0.2f, 1f), new Vector2(-0.28f, -0.12f), new Vector2(0.28f, 0.22f), order + 2);
            Paint(root, "AwningB", new Color(0.96f, 0.94f, 0.9f, 1f), new Vector2(0f, -0.12f), new Vector2(0.28f, 0.22f), order + 2);
            Paint(root, "AwningC", new Color(0.78f, 0.22f, 0.2f, 1f), new Vector2(0.28f, -0.12f), new Vector2(0.28f, 0.22f), order + 2);
            Blob(root, "TableA", new Color(0.55f, 0.36f, 0.22f, 1f), new Vector2(-0.7f, -0.55f), new Vector2(0.36f, 0.36f), order);
            Blob(root, "TableB", new Color(0.55f, 0.36f, 0.22f, 1f), new Vector2(0.65f, -0.5f), new Vector2(0.36f, 0.36f), order);
            Blob(root, "Pot", new Color(0.3f, 0.55f, 0.32f, 1f), new Vector2(0.95f, 0.15f), new Vector2(0.28f, 0.28f), order + 2);
            return new Stamp(new Vector2(0f, -1.12f), 1.4f, 2.95f);
        }

        Stamp PaintIntake(Transform root, int order)
        {
            Color concrete = new Color(0.72f, 0.74f, 0.72f, 1f);
            Paint(root, "Pad", concrete, new Vector2(0f, 0.05f), new Vector2(2.1f, 1.35f), order - 2);
            Blob(root, "Intake", new Color(0.28f, 0.52f, 0.62f, 1f), new Vector2(-0.15f, 0.28f), new Vector2(0.95f, 0.95f), order - 1);
            Blob(root, "Rim", concrete, new Vector2(-0.15f, 0.28f), new Vector2(1.15f, 1.15f), order - 2);
            Paint(root, "Pipe", new Color(0.45f, 0.5f, 0.52f, 1f), new Vector2(-0.15f, 0.95f), new Vector2(0.22f, 0.7f), order);
            Hut(root, new Vector2(0.85f, 0.15f), new Vector2(0.7f, 0.55f), concrete, new Color(0.4f, 0.48f, 0.52f, 1f), order, false);
            Paint(root, "RailN", new Color(0.82f, 0.84f, 0.86f, 1f), new Vector2(-0.15f, 0.72f), new Vector2(0.9f, 0.06f), order + 1);
            SolidCircle(root, new Vector2(-0.15f, 0.28f), 0.5f);
            return new Stamp(new Vector2(-0.15f, -1.05f), 1.35f, 2.95f);
        }

        Stamp PaintCreek(Transform root, int order)
        {
            Color water = new Color(0.72f, 0.4f, 0.26f, 1f);
            Color foam = new Color(0.94f, 0.92f, 0.88f, 0.9f);
            Color rock = new Color(0.55f, 0.54f, 0.5f, 1f);
            Blob(root, "BendA", water, new Vector2(-0.55f, 0.55f), new Vector2(1.3f, 0.55f), order - 2);
            Blob(root, "BendB", water, new Vector2(0.15f, 0.1f), new Vector2(0.55f, 0.9f), order - 2);
            Blob(root, "BendC", water, new Vector2(0.45f, -0.4f), new Vector2(1.15f, 0.48f), order - 2);
            Blob(root, "Rust", new Color(0.62f, 0.28f, 0.16f, 1f), new Vector2(0.2f, 0.15f), new Vector2(0.45f, 0.28f), order - 1);
            Paint(root, "Foam", foam, new Vector2(-0.2f, 0.62f), new Vector2(0.22f, 0.1f), order - 1);
            Blob(root, "RockA", rock, new Vector2(-0.95f, 0.35f), new Vector2(0.55f, 0.42f), order);
            Blob(root, "RockB", rock, new Vector2(0.85f, 0.45f), new Vector2(0.48f, 0.4f), order);
            Blob(root, "RockC", new Color(0.42f, 0.4f, 0.38f, 1f), new Vector2(0.55f, -0.15f), new Vector2(0.36f, 0.3f), order);
            SolidCircle(root, new Vector2(-0.95f, 0.35f), 0.26f);
            SolidCircle(root, new Vector2(0.85f, 0.45f), 0.22f);
            Paint(root, "Log", new Color(0.5f, 0.34f, 0.18f, 1f), new Vector2(-0.15f, -0.72f), new Vector2(0.9f, 0.16f), order + 1);
            return new Stamp(new Vector2(-0.55f, -1.15f), 1.3f, 3.0f);
        }

        Stamp PaintFarmstay(Transform root, int order)
        {
            Blob(root, "Court", new Color(0.78f, 0.68f, 0.46f, 1f), new Vector2(0f, -0.1f), new Vector2(2.4f, 1.7f), order - 3);
            Hut(root, new Vector2(0f, 0.42f), new Vector2(1.65f, 0.8f), new Color(0.9f, 0.82f, 0.66f, 1f), new Color(0.72f, 0.55f, 0.28f, 1f), order, true);
            Paint(root, "Banner", new Color(0.75f, 0.22f, 0.18f, 1f), new Vector2(0f, 0.95f), new Vector2(0.7f, 0.16f), order + 3);
            Paint(root, "RowA", new Color(0.32f, 0.55f, 0.28f, 1f), new Vector2(-0.85f, -0.15f), new Vector2(0.55f, 0.1f), order);
            Paint(root, "RowB", new Color(0.32f, 0.55f, 0.28f, 1f), new Vector2(-0.85f, -0.32f), new Vector2(0.55f, 0.1f), order);
            Blob(root, "Table", new Color(0.55f, 0.36f, 0.2f, 1f), new Vector2(0.7f, -0.45f), new Vector2(0.4f, 0.4f), order);
            Blob(root, "Table2", new Color(0.55f, 0.36f, 0.2f, 1f), new Vector2(0.15f, -0.55f), new Vector2(0.32f, 0.32f), order);
            return new Stamp(new Vector2(-0.2f, -1.15f), 1.5f, 3.15f);
        }

        Stamp PaintQuarry(Transform root, int order)
        {
            Color rock = new Color(0.62f, 0.63f, 0.6f, 1f);
            Color cut = new Color(0.48f, 0.5f, 0.48f, 1f);
            Color dust = new Color(0.74f, 0.7f, 0.58f, 1f);
            Blob(root, "Dust", dust, new Vector2(0.1f, 0f), new Vector2(2.6f, 1.9f), order - 3);
            Paint(root, "StepA", rock, new Vector2(-0.15f, 0.55f), new Vector2(2.2f, 0.48f), order - 1);
            Paint(root, "StepB", cut, new Vector2(0.25f, 0.12f), new Vector2(1.7f, 0.4f), order);
            Paint(root, "StepC", rock, new Vector2(0.45f, -0.22f), new Vector2(1.15f, 0.32f), order + 1);
            Solid(root, new Vector2(-0.15f, 0.55f), new Vector2(2.05f, 0.42f));
            Solid(root, new Vector2(0.25f, 0.12f), new Vector2(1.5f, 0.34f));
            Blob(root, "Gravel", new Color(0.55f, 0.54f, 0.5f, 1f), new Vector2(-0.95f, -0.25f), new Vector2(0.55f, 0.4f), order);
            Paint(root, "Cab", new Color(0.9f, 0.72f, 0.15f, 1f), new Vector2(-0.85f, -0.55f), new Vector2(0.4f, 0.32f), order + 2);
            Paint(root, "Arm", new Color(0.35f, 0.36f, 0.34f, 1f), new Vector2(-0.4f, -0.35f), new Vector2(0.55f, 0.08f), order + 2, null, -18f);
            return new Stamp(new Vector2(0.45f, -1.15f), 1.4f, 3.2f);
        }

        Stamp PaintMine(Transform root, int order)
        {
            Color hill = new Color(0.5f, 0.52f, 0.4f, 1f);
            Color rust = new Color(0.68f, 0.34f, 0.2f, 1f);
            Blob(root, "Hill", hill, new Vector2(0.05f, 0.35f), new Vector2(2.35f, 1.45f), order - 2);
            Paint(root, "Stain", rust, new Vector2(0.05f, -0.15f), new Vector2(0.35f, 0.7f), order - 1);
            Blob(root, "Mouth", new Color(0.08f, 0.08f, 0.09f, 1f), new Vector2(0.05f, 0.05f), new Vector2(0.7f, 0.55f), order);
            Paint(root, "PostL", new Color(0.45f, 0.3f, 0.16f, 1f), new Vector2(-0.28f, 0.02f), new Vector2(0.1f, 0.62f), order + 1);
            Paint(root, "PostR", new Color(0.45f, 0.3f, 0.16f, 1f), new Vector2(0.38f, 0.02f), new Vector2(0.1f, 0.62f), order + 1);
            Paint(root, "Lintel", new Color(0.4f, 0.26f, 0.14f, 1f), new Vector2(0.05f, 0.32f), new Vector2(0.8f, 0.1f), order + 2);
            Blob(root, "Rubble", new Color(0.45f, 0.4f, 0.34f, 1f), new Vector2(0.7f, -0.35f), new Vector2(0.45f, 0.28f), order);
            Solid(root, new Vector2(0.05f, 0.48f), new Vector2(2.0f, 1.05f));
            return new Stamp(new Vector2(0.05f, -1.05f), 1.5f, 3.15f);
        }

        Stamp PaintIrrigation(Transform root, int order)
        {
            Color paddy = new Color(0.42f, 0.68f, 0.36f, 1f);
            Color ditch = new Color(0.38f, 0.6f, 0.72f, 1f);
            Color young = new Color(0.7f, 0.62f, 0.28f, 1f);
            Paint(root, "FieldA", paddy, new Vector2(-0.55f, 0.45f), new Vector2(1.05f, 0.7f), order - 2);
            Paint(root, "FieldB", young, new Vector2(0.6f, 0.45f), new Vector2(1.05f, 0.7f), order - 2);
            Paint(root, "FieldC", paddy, new Vector2(-0.55f, -0.35f), new Vector2(1.05f, 0.7f), order - 2);
            Paint(root, "FieldD", new Color(0.55f, 0.7f, 0.34f, 1f), new Vector2(0.6f, -0.35f), new Vector2(1.05f, 0.7f), order - 2);
            Paint(root, "DitchV", ditch, new Vector2(0.02f, 0.05f), new Vector2(0.12f, 1.6f), order - 1);
            Paint(root, "DitchH", ditch, new Vector2(0.02f, 0.05f), new Vector2(2.2f, 0.1f), order - 1);
            Hut(root, new Vector2(0f, -0.95f), new Vector2(0.55f, 0.4f), new Color(0.74f, 0.76f, 0.74f, 1f), new Color(0.4f, 0.5f, 0.52f, 1f), order, false);
            return new Stamp(new Vector2(0.7f, -1.35f), 1.45f, 3.3f);
        }

        Stamp PaintFallback(Transform root, int order)
        {
            Hut(root, Vector2.zero, new Vector2(1.4f, 1f), new Color(0.8f, 0.78f, 0.72f, 1f), new Color(0.45f, 0.38f, 0.3f, 1f), order, false);
            return new Stamp(new Vector2(0f, -1.05f), 1.2f, 2.8f);
        }

        void Hut(Transform parent, Vector2 pos, Vector2 size, Color wall, Color roof, int order, bool chimney)
        {
            Paint(parent, "Shadow", new Color(0f, 0f, 0f, 0.14f), pos + new Vector2(0.06f, -0.08f), size + new Vector2(0.16f, 0.08f), order - 1);
            Paint(parent, "Wall", wall, pos, size, order);
            Paint(parent, "Roof", roof, pos + new Vector2(0f, size.y * 0.22f), new Vector2(size.x * 1.08f, size.y * 0.48f), order + 1);
            Paint(parent, "Door", new Color(0.28f, 0.18f, 0.12f, 1f), pos + new Vector2(0f, -size.y * 0.28f), new Vector2(0.22f, size.y * 0.34f), order + 2);
            if (chimney)
                Paint(parent, "Chimney", new Color(0.45f, 0.32f, 0.28f, 1f), pos + new Vector2(size.x * 0.28f, size.y * 0.42f), new Vector2(0.14f, 0.28f), order + 3);
            Solid(parent, pos, size * 0.88f);
        }

        void Yard(Transform parent, Vector2 pos, Vector2 size, int order)
        {
            Color rail = new Color(0.58f, 0.46f, 0.28f, 1f);
            float left = pos.x - size.x * 0.5f;
            float right = pos.x + size.x * 0.5f;
            float bottom = pos.y - size.y * 0.5f;
            float top = pos.y + size.y * 0.5f;
            float seg = (size.x - 0.75f) * 0.5f;
            Paint(parent, "RailN", rail, new Vector2(pos.x, top), new Vector2(size.x, 0.07f), order);
            Paint(parent, "RailW", rail, new Vector2(left, pos.y), new Vector2(0.07f, size.y), order);
            Paint(parent, "RailE", rail, new Vector2(right, pos.y), new Vector2(0.07f, size.y), order);
            Paint(parent, "RailSW", rail, new Vector2(left + seg * 0.5f, bottom), new Vector2(seg, 0.07f), order);
            Paint(parent, "RailSE", rail, new Vector2(right - seg * 0.5f, bottom), new Vector2(seg, 0.07f), order);
        }

        void Cage(Transform parent, Vector2 pos, float size, Color frame, int order)
        {
            Paint(parent, "CageN", frame, pos + new Vector2(0f, size * 0.5f), new Vector2(size, 0.06f), order);
            Paint(parent, "CageS", frame, pos + new Vector2(0f, -size * 0.5f), new Vector2(size, 0.06f), order);
            Paint(parent, "CageW", frame, pos + new Vector2(-size * 0.5f, 0f), new Vector2(0.06f, size), order);
            Paint(parent, "CageE", frame, pos + new Vector2(size * 0.5f, 0f), new Vector2(0.06f, size), order);
            Paint(parent, "Net", new Color(0.9f, 0.9f, 0.85f, 0.45f), pos, new Vector2(size * 0.7f, 0.04f), order, null, 40f);
        }

        CanvasGroup CreateLabel(Transform parent, Font font, string title, float y, float lift, out Text status)
        {
            var canvasGo = new GameObject("Label", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            canvasGo.transform.localPosition = new Vector3(0f, lift, 0f);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingOrder = Order(y) + 30;
            RectTransform rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(220f, 90f);
            rect.localScale = Vector3.one * 0.012f;
            var group = canvasGo.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;

            var plaque = new GameObject("Plaque", typeof(RectTransform));
            plaque.transform.SetParent(canvasGo.transform, false);
            var plaqueRect = plaque.GetComponent<RectTransform>();
            plaqueRect.anchorMin = Vector2.zero;
            plaqueRect.anchorMax = Vector2.one;
            plaqueRect.offsetMin = Vector2.zero;
            plaqueRect.offsetMax = Vector2.zero;
            var image = plaque.AddComponent<Image>();
            image.sprite = square;
            image.color = new Color(0.98f, 0.97f, 0.94f, 0.92f);
            image.raycastTarget = false;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(canvasGo.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 0.42f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.offsetMin = Vector2.zero;
            titleRect.offsetMax = Vector2.zero;
            var titleText = titleGo.AddComponent<Text>();
            titleText.font = font;
            titleText.fontSize = 32;
            titleText.fontStyle = FontStyle.Bold;
            titleText.alignment = TextAnchor.MiddleCenter;
            titleText.color = new Color(0.11f, 0.16f, 0.14f, 1f);
            titleText.text = title;
            titleText.raycastTarget = false;

            var statusGo = new GameObject("Status", typeof(RectTransform));
            statusGo.transform.SetParent(canvasGo.transform, false);
            var statusRect = statusGo.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(1f, 0.42f);
            statusRect.offsetMin = Vector2.zero;
            statusRect.offsetMax = Vector2.zero;
            status = statusGo.AddComponent<Text>();
            status.font = font;
            status.fontSize = 24;
            status.alignment = TextAnchor.MiddleCenter;
            status.color = new Color(0.37f, 0.42f, 0.39f, 1f);
            status.text = "未调查";
            status.raycastTarget = false;
            return group;
        }

        void BuildPlayer()
        {
            var go = new GameObject("Explorer");
            go.transform.SetParent(transform, false);
            go.transform.position = Entrance();
            player = go.transform;
            body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = 0.32f;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            playerVisual = visual.transform;
            Paint(visual.transform, "Shadow", new Color(0f, 0f, 0f, 0.18f), new Vector2(0f, -0.28f), new Vector2(0.7f, 0.28f), 0);
            Paint(visual.transform, "Body", new Color(0.16f, 0.24f, 0.22f, 1f), new Vector2(0f, -0.02f), new Vector2(0.62f, 0.72f), 1);
            Paint(visual.transform, "Head", new Color(0.93f, 0.80f, 0.66f, 1f), new Vector2(0f, 0.38f), new Vector2(0.42f, 0.42f), 2);
            Paint(visual.transform, "Mark", new Color(0.12f, 0.42f, 0.35f, 1f), new Vector2(0f, 0.02f), new Vector2(0.7f, 0.16f), 3);
            playerSprites = visual.GetComponentsInChildren<SpriteRenderer>();
        }

        void BuildCamera()
        {
            var go = new GameObject("TownCamera");
            go.transform.SetParent(transform, false);
            townCamera = go.AddComponent<Camera>();
            townCamera.orthographic = true;
            townCamera.orthographicSize = 5.5f;
            townCamera.clearFlags = CameraClearFlags.SolidColor;
            townCamera.backgroundColor = Horizon();
            townCamera.depth = 10f;
            townCamera.enabled = false;
            if (go.GetComponent<UniversalAdditionalCameraData>() == null)
                go.AddComponent<UniversalAdditionalCameraData>();
            camTransform = go.transform;
            Vector3 entrance = Entrance();
            camTransform.position = new Vector3(entrance.x, entrance.y, -10f);
        }

        void BindWorldCanvases()
        {
            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < canvases.Length; i++)
                canvases[i].worldCamera = townCamera;
        }

        int FindNearby()
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            Vector2 position = player.position;
            for (int i = 0; i < places.Count; i++)
            {
                float center = Vector2.Distance(position, places[i].Root.position);
                float door = Vector2.Distance(position, places[i].Door);
                if (center > InteractRange && door > 1.45f)
                    continue;
                if (center < bestDistance)
                {
                    bestDistance = center;
                    best = places[i].Index;
                }
            }

            return best;
        }

        void RefreshLabels()
        {
            Vector2 position = player.position;
            for (int i = 0; i < places.Count; i++)
            {
                Place place = places[i];
                float center = Vector2.Distance(position, place.Root.position);
                float door = Vector2.Distance(position, place.Door);
                bool show = center <= place.Notice || door <= place.Notice;
                if (MovementLocked && place.Index == nearby)
                    show = true;
                float target = show ? 1f : 0f;
                place.Label.alpha = Mathf.MoveTowards(place.Label.alpha, target, Time.deltaTime * 6f);
            }
        }

        void RefreshHighlight()
        {
            for (int i = 0; i < places.Count; i++)
            {
                Place place = places[i];
                bool hot = !MovementLocked && place.Index == nearby;
                for (int p = 0; p < place.Parts.Length; p++)
                {
                    Color color = place.PartColors[p];
                    if (hot)
                    {
                        color.r = Mathf.Lerp(color.r, 1f, 0.22f);
                        color.g = Mathf.Lerp(color.g, 1f, 0.22f);
                        color.b = Mathf.Lerp(color.b, 1f, 0.22f);
                    }

                    place.Parts[p].color = color;
                }

                float scale = hot ? 1.03f : 1f;
                place.Root.localScale = Vector3.Lerp(place.Root.localScale, new Vector3(scale, scale, 1f), 0.2f);
            }
        }

        static Vector2 ReadMove()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return Vector2.zero;

            float x = 0f;
            float y = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
                x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
                x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
                y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
                y += 1f;
            Vector2 move = new Vector2(x, y);
            if (move.sqrMagnitude > 1f)
                move.Normalize();
            return move;
        }

        SpriteRenderer Blob(Transform parent, string name, Color color, Vector2 localPos, Vector2 size, int order)
        {
            return Paint(parent, name, color, localPos, size, order, disc, 0f);
        }

        void Solid(Transform parent, Vector2 localPos, Vector2 size)
        {
            var go = new GameObject("Solid");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        void SolidCircle(Transform parent, Vector2 localPos, float radius)
        {
            var go = new GameObject("Solid");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var collider = go.AddComponent<CircleCollider2D>();
            collider.radius = radius;
        }

        SpriteRenderer Paint(Transform parent, string name, Color color, Vector2 localPos, Vector2 size, int order, Sprite sprite, float rotation)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            go.transform.localScale = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite == null ? square : sprite;
            renderer.color = color;
            renderer.sortingOrder = order;
            Material unlit = SpriteMaterial();
            if (unlit != null)
                renderer.sharedMaterial = unlit;
            return renderer;
        }

        SpriteRenderer Paint(Transform parent, string name, Color color, Vector2 localPos, Vector2 size, int order)
        {
            return Paint(parent, name, color, localPos, size, order, null, 0f);
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

        void Wall(Vector2 position, Vector2 size)
        {
            var go = new GameObject("Wall");
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var collider = go.AddComponent<BoxCollider2D>();
            collider.size = size;
        }

        static int Order(float y)
        {
            return 200 - Mathf.RoundToInt(y * 10f);
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
            const int size = 96;
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
