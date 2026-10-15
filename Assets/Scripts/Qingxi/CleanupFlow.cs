using UnityEngine;
using UnityEngine.UI;

namespace Qingxi
{
    /// <summary>Moving water marks on an authored channel, with progressive containment and collection feedback.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class CleanupFlow : MaskableGraphic
    {
        [SerializeField] int chapter;
        float progress;
        float finishing;
        public void Configure(int value) { chapter = value; raycastTarget = false; }
        public void SetProgress(float value, float complete = 0) { progress = value; finishing = complete; SetVerticesDirty(); }
        void Update() { if (Application.isPlaying) SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            void Box(float x,float y,float w,float h,Color c)
            {
                int n=vh.currentVertCount;
                vh.AddVert(new Vector3(r.xMin+x*r.width,r.yMin+y*r.height),c,Vector2.zero);
                vh.AddVert(new Vector3(r.xMin+x*r.width,r.yMin+(y+h)*r.height),c,Vector2.zero);
                vh.AddVert(new Vector3(r.xMin+(x+w)*r.width,r.yMin+(y+h)*r.height),c,Vector2.zero);
                vh.AddVert(new Vector3(r.xMin+(x+w)*r.width,r.yMin+y*r.height),c,Vector2.zero);
                vh.AddTriangle(n,n+1,n+2); vh.AddTriangle(n,n+2,n+3);
            }
            var pollution = new Color(.65f,.39f,.25f,1);
            var water = new Color(.23f,.60f,.56f,1);
            float time = Application.isPlaying ? Time.unscaledTime : 0;
            if (chapter == 1)
            {
                // Four elbows connect left -> down -> right -> up -> right, into the holding tank.
                float fill = finishing;
                Box(.81f,.50f,.14f,.34f*fill,water);
                if (fill > 0)
                {
                    Vector2[] path={new Vector2(.035f,.72f),new Vector2(.22f,.72f),new Vector2(.22f,.43f),
                        new Vector2(.54f,.43f),new Vector2(.54f,.72f),new Vector2(.82f,.72f)};
                    for(int i=0;i<12;i++)
                    {
                        float t=Mathf.Repeat(time*.24f+i/12f,1)*5;
                        int segment=Mathf.Min(4,(int)t);
                        Vector2 p=Vector2.Lerp(path[segment],path[segment+1],t-segment);
                        Box(p.x-.009f,p.y-.012f,.018f,.024f,water);
                    }
                }
            }
            else
            {
                float length = Mathf.Lerp(.82f,.06f,progress);
                Box(.06f,.49f,length,.23f*(1-finishing),pollution);
                for(int i=0;i<9;i++)
                {
                    float t=Mathf.Repeat(time*.18f+i/9f,1);
                    if(t*.82f<length && finishing<1) Box(.06f+t*.82f,.53f+(i%3)*.065f,.035f,.012f,new Color(.85f,.69f,.43f,1-finishing));
                }
            }
        }
    }
}
