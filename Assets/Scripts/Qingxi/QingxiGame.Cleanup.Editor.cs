#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;

namespace Qingxi
{
    public sealed partial class QingxiGame
    {
        /// <summary>Adds only the treatment panel; existing world objects, bridges and UI stay authored in place.</summary>
        public void EditorInstallCleanup()
        {
            if (sceneCaseIndex < 0) return;
            uiFont = LoadUiFont();
            EditorBuildCleanup(transform.Find("Canvas"), sceneCaseIndex);
            EditorConfigureLocalization();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        void EditorBuildCleanup(Transform parent, int chapter)
        {
            Transform old = parent.Find("Cleanup");
            if (old != null) DestroyImmediate(old.gameObject);
            var root = CreatePanel(parent, "Cleanup", new Color(.08f,.15f,.12f,.72f));
            cleanupGame = root.AddComponent<CleanupGame>();
            var card = CreateImage(root.transform,"Paper",Panel,new Vector2(.035f,.04f),new Vector2(.965f,.96f),true);
            CreateText(card.transform,"Kicker","现场治理",18,Gold,TextAnchor.MiddleLeft,new Vector2(.035f,.92f),new Vector2(.55f,.97f));
            Text heading=CreateText(card.transform,"Heading","",40,Paper,TextAnchor.MiddleLeft,new Vector2(.035f,.835f),new Vector2(.82f,.925f),FontStyle.Bold);
            Text instruction=CreateText(card.transform,"Instruction","",23,Muted,TextAnchor.MiddleLeft,new Vector2(.035f,.77f),new Vector2(.96f,.835f));
            string[] images={"river_cleanup_simple","reservoir_cleanup_simple","mine_cleanup_simple"};
            var art=CreateImage(card.transform,"SourceIllustration",Color.white,new Vector2(.035f,.255f),new Vector2(.395f,.755f),false);
            art.sprite=UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Cleanup/"+images[chapter]+".png");
            art.preserveAspect=true;
            art.material=UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Cleanup/FlatCleanupUI.mat");
            CreateText(card.transform,"SourceCaption",chapter==0?"养殖场排污沟":chapter==1?"化肥厂应急收集区":"废弃矿硐入口",22,Gold,
                TextAnchor.MiddleLeft,new Vector2(.035f,.195f),new Vector2(.40f,.255f),FontStyle.Bold);
            CreateText(card.transform,"SourceNote","先控制污染源，再交由后续处理。",20,Muted,TextAnchor.UpperLeft,new Vector2(.035f,.125f),new Vector2(.39f,.19f));

            Image board=CreateImage(card.transform,"TreatmentBoard",new Color(.90f,.91f,.84f),new Vector2(.44f,.205f),new Vector2(.965f,.76f),false);
            // The pale channel is a schematic operation area, kept deliberately simpler than the world map.
            var land=new Color(.78f,.72f,.56f);
            var dark=new Color(.16f,.28f,.23f);
            var blue=new Color(.25f,.55f,.55f);
            if(chapter!=1)
            {
                CreateImage(board.transform,"ChannelBank",land,new Vector2(.035f,.445f),new Vector2(.965f,.765f),false);
                CreateImage(board.transform,"Channel",new Color(.72f,.79f,.74f),new Vector2(.035f,.49f),new Vector2(.965f,.72f),false);
                CreateText(board.transform,"Inlet","污染源",17,dark,TextAnchor.MiddleLeft,new Vector2(.035f,.80f),new Vector2(.40f,.90f));
                CreateText(board.transform,"Outlet",chapter==0?"河流":"收集区",17,dark,TextAnchor.MiddleRight,new Vector2(.70f,.80f),new Vector2(.965f,.90f));
            }
            else
            {
                void Track(Vector2 min,Vector2 max) => CreateImage(board.transform,"FixedPipe",new Color(.53f,.60f,.53f),min,max,false);
                Track(new Vector2(.035f,.702f),new Vector2(.22f,.738f));
                Track(new Vector2(.21f,.43f),new Vector2(.23f,.72f));
                Track(new Vector2(.22f,.412f),new Vector2(.54f,.448f));
                Track(new Vector2(.53f,.43f),new Vector2(.55f,.72f));
                Track(new Vector2(.54f,.702f),new Vector2(.82f,.738f));
                CreateImage(board.transform,"HoldingTankRim",dark,new Vector2(.79f,.48f),new Vector2(.97f,.88f),false);
                CreateImage(board.transform,"HoldingTank",new Color(.77f,.85f,.78f),new Vector2(.81f,.50f),new Vector2(.95f,.86f),false);
                CreateText(board.transform,"Inlet","溢流口",17,dark,TextAnchor.MiddleLeft,new Vector2(.025f,.82f),new Vector2(.25f,.96f));
                CreateText(board.transform,"TankLabel","收集池",17,dark,TextAnchor.MiddleCenter,new Vector2(.78f,.88f),new Vector2(.98f,.99f));
            }
            var flowRect=CreateRect(board.transform,"WaterFlow",Vector2.zero,Vector2.one);
            var water=flowRect.gameObject.AddComponent<CleanupFlow>(); water.Configure(chapter);
            CreateImage(board.transform,"MaterialsTray",new Color(.96f,.94f,.88f),new Vector2(.02f,.015f),new Vector2(.98f,.29f),false);
            CreateText(board.transform,"TrayLabel","拖动材料",16,Muted,TextAnchor.UpperLeft,new Vector2(.035f,.22f),new Vector2(.30f,.29f));

            int count=chapter==1?4:5;
            var pieces=new CleanupPiece[count];
            var slots=new Image[chapter==1?4:3];
            Vector2[] pipeLocations={new Vector2(.22f,.72f),new Vector2(.22f,.43f),new Vector2(.54f,.43f),new Vector2(.54f,.72f)};
            Vector2 size=chapter==1?new Vector2(116,116):new Vector2(148,112);
            for(int i=0;i<slots.Length;i++)
            {
                var shape=CleanupPuzzle.SlotShape(chapter,i);
                Vector2 target=chapter==1?pipeLocations[i]:new Vector2(.27f+i*.24f,.60f);
                Image slot=CreateImage(board.transform,"Slot_"+i,new Color(.65f,.52f,.30f,.18f),target,target,false);
                slot.rectTransform.sizeDelta=size+new Vector2(14,14);
                var ghost=CreateRect(slot.transform,"ShapeGuide",new Vector2(.04f,.04f),new Vector2(.96f,.96f)).gameObject.AddComponent<CleanupShape>();
                ghost.Configure(shape); ghost.color=new Color(.35f,.44f,.35f,chapter==1?.23f:.38f);
                if(chapter==1) ghost.rectTransform.localRotation=Quaternion.Euler(0,0,-90*CleanupGame.TargetRotation(i));
                if(chapter==1)
                {
                    var number=CreateText(slot.transform,"SlotNumber",(i+1).ToString(),18,Muted,TextAnchor.LowerCenter,new Vector2(0,1),new Vector2(1,1));
                    number.rectTransform.sizeDelta=new Vector2(0,30);
                }
                slots[i]=slot;
            }
            for(int i=0;i<count;i++)
            {
                var shape=CleanupPuzzle.PieceShape(chapter,i);
                Vector2 home=new Vector2(chapter==1?.19f+i*.21f:.13f+i*.185f,.13f);
                Image part=CreateImage(board.transform,"Part_"+i,new Color(1,1,1,.6f),home,home,true);
                part.rectTransform.sizeDelta=size;
                part.gameObject.AddComponent<CanvasGroup>();
                var graphic=CreateRect(part.transform,"EquipmentArt",new Vector2(.03f,.03f),new Vector2(.97f,.97f)).gameObject.AddComponent<CleanupShape>();
                graphic.Configure(shape); graphic.color=blue;
                if(chapter==1)
                {
                    var label=CreateText(part.transform,"PartNumber",(i+1).ToString(),18,Paper,TextAnchor.MiddleCenter,new Vector2(.03f,.03f),new Vector2(.25f,.25f),FontStyle.Bold);
                }
                pieces[i]=part.gameObject.AddComponent<CleanupPiece>();
                pieces[i].Configure(cleanupGame,i,home,chapter==1?slots[i].rectTransform:null,graphic.rectTransform);
            }

            Text progress=CreateText(card.transform,"Progress","",21,Gold,TextAnchor.MiddleLeft,new Vector2(.44f,.15f),new Vector2(.96f,.205f),FontStyle.Bold);
            Text hint=CreateText(card.transform,"Feedback","",18,Muted,TextAnchor.MiddleLeft,new Vector2(.44f,.10f),new Vector2(.96f,.15f));
            Button back=CreateButton(card.transform,"BtnCleanupMenu","保存并返回",Card,Paper,21,new Vector2(.035f,.035f),new Vector2(.24f,.10f));
            BindAction(back,QingxiUiAction.Action.ReturnToMenu);
            Button reset=CreateButton(card.transform,"BtnCleanupReset","重新摆放",Card,Paper,21,new Vector2(.44f,.025f),new Vector2(.65f,.09f));
            BindAction(reset,QingxiUiAction.Action.ResetCleanup);
            Button primary=CreateButton(card.transform,"BtnCleanupPrimary","",Gold,Ink,23,new Vector2(.69f,.025f),new Vector2(.965f,.09f));
            BindAction(primary,QingxiUiAction.Action.CleanupPrimary);
            cleanupGame.Configure(this,chapter,pieces,slots,primary,heading,instruction,progress,hint,primary.GetComponentInChildren<Text>(),water);
            // Dynamic text is populated from the state on entry, never baked as localization keys.
            cleanupGame.Begin(new CleanupState { chapter=chapter });
            var group=root.GetComponent<CanvasGroup>(); group.alpha=0; group.interactable=group.blocksRaycasts=false;
            root.SetActive(false);
        }
    }
}
#endif
