using UnityEngine;
using UnityEngine.UI;

namespace Qingxi
{
    /// <summary>Editable flat-color vector art for movable treatment equipment; no runtime textures.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CleanupShape : MaskableGraphic
    {
        public enum Shape { Barrier, Elbow, PanelA, PanelB, PanelC, Support, Triangle, Step, House, Trapezoid, Diamond, Notch }
        [SerializeField] Shape shape;
        public Shape Kind => shape;
        public void Configure(Shape value) { shape = value; raycastTarget = false; SetVerticesDirty(); }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            void Polygon(Color tint, params Vector2[] points)
            {
                int start = vh.currentVertCount;
                foreach (var p in points) vh.AddVert(new Vector3(r.xMin + p.x * r.width, r.yMin + p.y * r.height), tint, Vector2.zero);
                for (int i = 1; i < points.Length - 1; i++) vh.AddTriangle(start, start + i, start + i + 1);
            }
            void Box(float x, float y, float w, float h, Color tint) => Polygon(tint,
                new Vector2(x,y), new Vector2(x,y+h), new Vector2(x+w,y+h), new Vector2(x+w,y));
            Color light = Color.Lerp(color, Color.white, 0.25f); light.a = color.a;
            Color dark = Color.Lerp(color, Color.black, 0.22f); dark.a = color.a;
            switch (shape)
            {
                case Shape.Elbow:
                    Box(.33f,.33f,.34f,.67f,dark); Box(.33f,.33f,.67f,.34f,dark);
                    Box(.40f,.40f,.20f,.60f,color); Box(.40f,.40f,.60f,.20f,color);
                    Box(.28f,.86f,.44f,.09f,light); Box(.86f,.28f,.09f,.44f,light); break;
                case Shape.Barrier:
                    Box(.18f,.03f,.64f,.84f,color); Box(.10f,.05f,.10f,.84f,dark); Box(.80f,.05f,.10f,.84f,dark);
                    Box(.28f,.86f,.44f,.10f,dark); Box(.28f,.55f,.44f,.05f,light); Box(.28f,.28f,.44f,.05f,light); break;
                case Shape.PanelA:
                    Polygon(color,new Vector2(.07f,.05f),new Vector2(.07f,.70f),new Vector2(.32f,.95f),new Vector2(.93f,.95f),new Vector2(.93f,.05f));
                    Box(.23f,.20f,.54f,.07f,light); Box(.23f,.53f,.54f,.07f,light); break;
                case Shape.PanelB:
                    Polygon(color,new Vector2(.07f,.05f),new Vector2(.07f,.95f),new Vector2(.62f,.95f),new Vector2(.62f,.72f),new Vector2(.93f,.72f),new Vector2(.93f,.05f));
                    Box(.23f,.20f,.54f,.07f,light); Box(.23f,.53f,.34f,.07f,light); break;
                case Shape.PanelC:
                    Polygon(color,new Vector2(.07f,.05f),new Vector2(.07f,.95f),new Vector2(.72f,.95f),new Vector2(.93f,.55f),new Vector2(.93f,.05f));
                    Box(.23f,.20f,.54f,.07f,light); Box(.23f,.53f,.44f,.07f,light); break;
                case Shape.Support:
                    Polygon(color,new Vector2(.05f,.05f),new Vector2(.05f,.22f),new Vector2(.86f,.95f),new Vector2(.99f,.95f),new Vector2(.99f,.78f),new Vector2(.20f,.05f)); break;
                case Shape.Triangle:
                    Polygon(color,new Vector2(.06f,.06f),new Vector2(.50f,.95f),new Vector2(.94f,.06f)); break;
                case Shape.Step:
                    Box(.07f,.06f,.34f,.89f,color); Box(.41f,.06f,.52f,.34f,color); break;
                case Shape.House:
                    Polygon(color,new Vector2(.07f,.06f),new Vector2(.07f,.59f),new Vector2(.50f,.95f),new Vector2(.93f,.59f),new Vector2(.93f,.06f)); break;
                case Shape.Trapezoid:
                    Polygon(color,new Vector2(.06f,.06f),new Vector2(.27f,.94f),new Vector2(.73f,.94f),new Vector2(.94f,.06f)); break;
                case Shape.Diamond:
                    Polygon(color,new Vector2(.06f,.50f),new Vector2(.50f,.95f),new Vector2(.94f,.50f),new Vector2(.50f,.05f)); break;
                case Shape.Notch:
                    Box(.07f,.06f,.86f,.40f,color); Box(.07f,.46f,.25f,.49f,color); Box(.68f,.46f,.25f,.49f,color); break;
            }
        }
    }
}
