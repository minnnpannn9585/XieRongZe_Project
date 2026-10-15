using System;
using System.Linq;
using System.Reflection;
using Qingxi;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

namespace Qingxi.Editor
{
    /// <summary>Exercises real drag handlers, puzzle prerequisites, partial saves, scene reload and completion gating.</summary>
    public static class QingxiCleanupChecks
    {
        static int currentChapter = -1;
        static int stage;
        static int evidenceCount;
        static int[] trayOrder;

        public static void ValidateSaves()
        {
            for(int chapter=0;chapter<3;chapter++)
            {
                var state=new CleanupState { chapter=chapter, placed=3 };
                if(chapter!=1)
                    for(int slot=0;slot<2;slot++) state.slotPieces[slot]=CleanupPuzzle.PieceForSlot(chapter,slot);
                state.rotations[0]=2;
                Require(CleanupSave.TryDecode(chapter,JsonUtility.ToJson(state),out var copy) && copy.placed==3 && copy.rotations[0]==2,"Cleanup save did not round trip");
                Require(!CleanupSave.TryDecode(chapter,"{broken",out _),"Malformed cleanup save accepted");
                state.version=3;
                Require(!CleanupSave.TryDecode(chapter,JsonUtility.ToJson(state),out _),"Unknown cleanup save version accepted");
                state.version=2; state.placed=31;
                Require(!CleanupSave.TryDecode(chapter,JsonUtility.ToJson(state),out _),"Impossible installed mask accepted");
                state.placed=1; state.supports=1;
                Require(!CleanupSave.TryDecode(chapter,JsonUtility.ToJson(state),out _),"Supports accepted before repairs");
                state.supports=0; state.rotations[1]=-1;
                Require(!CleanupSave.TryDecode(chapter,JsonUtility.ToJson(state),out _),"Invalid rotation accepted");
                state.rotations[1]=0; state.placed=3; state.trayOrder[1]=0;
                Require(!CleanupSave.TryDecode(chapter,JsonUtility.ToJson(state),out _),"Duplicate tray position accepted");
                if(chapter!=1)
                {
                    string legacy="{\"version\":1,\"chapter\":"+chapter+",\"placed\":7,\"supports\":"+(chapter==2?1:0)+",\"rotations\":[0,0,0,0]}";
                    Require(CleanupSave.TryDecode(chapter,legacy,out var migrated) && migrated.version==2 && migrated.placed==7 && migrated.supports==0,
                        "Legacy puzzle save did not migrate or still requires supports");
                    for(int slot=0;slot<3;slot++) Require(migrated.slotPieces[slot]==CleanupPuzzle.PieceForSlot(chapter,slot),"Legacy installation lost");
                    int distractor=Enumerable.Range(0,5).First(p=>!Enumerable.Range(0,3).Any(s=>CleanupPuzzle.PieceShape(chapter,p)==CleanupPuzzle.SlotShape(chapter,s)));
                    migrated.slotPieces[0]=distractor;
                    Require(!CleanupSave.TryDecode(chapter,JsonUtility.ToJson(migrated),out _),"Distractor accepted in saved slot");
                }
            }
            Require(CleanupSave.TryDecode(1,"{\"version\":1,\"chapter\":1,\"placed\":3,\"supports\":0,\"rotations\":[2,0,0,1]}",out var pipe) &&
                pipe.placed==3 && pipe.rotations[0]==2 && pipe.rotations[3]==1,"Legacy pipe progress changed");
        }

        public static void ValidateScene(QingxiGame game,int chapter)
        {
            var board=game.GetComponentInChildren<CleanupGame>(true);
            Require(board!=null && board.Chapter==chapter,"Missing or wrong cleanup board");
            Require(board.Pieces.Length==(chapter==1?4:5),"Wrong treatment part count");
            Require(board.Slots.Length==(chapter==1?4:3),"Wrong treatment slot count");
            Require(board.Pieces.Select(p=>p.Index).Distinct().Count()==board.Pieces.Length,"Duplicate part index");
            if(chapter==1) foreach(var piece in board.Pieces) Require(piece.Slot!=null,"Missing pipe snap target");
            else
            {
                Require(board.Pieces.Select(p=>p.Kind).Distinct().Count()==5,"Puzzle silhouettes are not distinct");
                Require(board.Pieces.All(p=>p.Slot==null),"Puzzle parts retain preset slot bindings");
                Require(!board.GetComponentsInChildren<Text>(true).Any(t=>int.TryParse(t.text,out _)),"Puzzle still has corresponding number labels");
                Require(!board.GetComponentsInChildren<Button>(true).Any(b=>b.name.StartsWith("BtnSupport")),"Mine reinforcement interaction remains");
                int distractions=0;
                foreach(var piece in board.Pieces)
                {
                    int matches=Enumerable.Range(0,3).Count(s=>board.ShapeFits(piece.Index,s));
                    if(matches==0) distractions++;
                    else Require(matches==1,"Puzzle shape fits multiple different gaps");
                }
                Require(distractions==2,"Puzzle must have exactly two distractors");
                for(int slot=0;slot<3;slot++) Require(board.Pieces.Count(p=>board.ShapeFits(p.Index,slot))==1,"Gap lacks a matching piece");
            }
            foreach(var graphic in board.GetComponentsInChildren<MaskableGraphic>(true))
                Require(graphic.GetComponent<CanvasRenderer>()!=null,"UI artwork has no CanvasRenderer: "+graphic.name);
            var art=board.GetComponentsInChildren<Image>(true).First(x=>x.name=="SourceIllustration");
            Require(art.sprite!=null && AssetDatabase.Contains(art.sprite),"Cleanup art is not persistent");
            Require(art.material.shader.name=="Qingxi/Flat Cleanup UI","Cleanup artwork lost its flat palette");
        }

        public static bool Advance(QingxiGame game,int chapter)
        {
            if(currentChapter!=chapter) { currentChapter=chapter; stage=0; }
            var board=game.GetComponentInChildren<CleanupGame>(true);
            switch(stage++)
            {
                case 0:
                    Require(CleanupSave.TryLoad(chapter,out _),"Correct verdict did not start pending treatment");
                    Require((PlayerPrefs.GetInt("Qingxi.ClearedMask") & (1<<chapter))==0,"Quiz alone cleared the chapter");
                    Require(PlayerPrefs.GetInt("Qingxi.UnlockedThrough")==chapter,"Quiz alone unlocked a chapter");
                    evidenceCount=Get<InvestigationSession>(game,"session").Clues.Count;
                    Press(game,"BtnRestart"); return false;
                case 1:
                    Require(board.gameObject.activeInHierarchy && game.CleanupInputEnabled,"Treatment did not open");
                    Require(!Get<TownExplorer>(game,"explorer").Player.InputEnabled,"Player moves behind treatment UI");
                    Canvas.ForceUpdateCanvases();
                    foreach(var graphic in board.GetComponentsInChildren<CleanupShape>())
                        Require(graphic.canvasRenderer!=null && !graphic.canvasRenderer.cull,"Treatment artwork is not rendered");
                    PolishChecks.Capture("cleanup-"+chapter+"-start-en");
                    Require(!board.Ready,"Empty treatment board is complete");
                    board.Confirm();
                    Require(CleanupSave.TryLoad(chapter,out _),"Empty confirm advanced the chapter");
                    // Wrong drops return to the tray; they do not count as installations.
                    Drag(board.Pieces[0],new Vector2(-100,-100));
                    Require(board.PlacedMask==0,"Wrong drop installed a part");
                    if(chapter!=1)
                    {
                        foreach(var piece in board.Pieces.Where(p=>!Enumerable.Range(0,3).Any(s=>board.ShapeFits(p.Index,s))))
                            for(int slot=0;slot<3;slot++)
                            {
                                Drag(piece,ScreenPoint(board.Slots[slot].rectTransform));
                                Require(board.PlacedMask==0,"Distractor was accepted");
                            }
                        Drag(MatchingPiece(board,0),ScreenPoint(board.Slots[1].rectTransform));
                        Require(board.PlacedMask==0,"Wrong silhouette accepted");
                    }
                    Place(board,0); Place(board,1);
                    Require(board.PlacedMask==3,"Valid drag did not snap");
                    if(chapter==1) board.Rotate(0);
                    trayOrder=board.Pieces.Select(p=>board.TrayPosition(p.Index)).ToArray();
                    Require(CleanupSave.TryLoad(chapter,out var saved) && saved.placed==3,"Partial progress not saved");
                    Press(game,"BtnCleanupMenu"); return false;
                case 2:
                    Require(game.gameObject.scene.name==QingxiGame.MenuSceneName,"Save-and-return did not reach menu");
                    QingxiSceneChecks.CheckRuntime("MainMenu");
                    game.PerformAction(QingxiUiAction.Action.EnterCase,chapter); return false;
                case 3:
                    Require(game.CleanupInputEnabled && board.PlacedMask==3,"Reload did not resume treatment directly");
                    Require(Get<InvestigationSession>(game,"session").Clues.Count==evidenceCount,"Pending treatment lost evidence");
                    if(chapter==1) Require(board.Rotation(0)==1,"Pipe rotation lost on resume");
                    Require(board.Pieces.Select(p=>board.TrayPosition(p.Index)).SequenceEqual(trayOrder),"Shuffled tray changed on resume");
                    for(int slot=0;slot<2;slot++) Require(board.InstalledPiece(slot)==MatchingPiece(board,slot).Index,"Placed shape identity lost on resume");
                    // A bad re-drop must preserve an already installed part.
                    Drag(MatchingPiece(board,0),new Vector2(-100,-100));
                    Require(board.PlacedMask==3,"Bad re-drop removed an installed part");
                    Place(board,0);
                    LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("zh-Hans");
                    return false;
                case 4:
                    Require(board.PlacedMask==3 && board.GetComponentsInChildren<Text>().Any(t=>t.text==(chapter==0?"隔板拦截":chapter==1?"溢流水引流":"修复矿硐挡墙")),"Treatment language switch failed or reset progress");
                    PolishChecks.Capture("cleanup-"+chapter+"-zh");
                    if(chapter!=1)
                    {
                        Press(game,"BtnCleanupReset");
                        PolishChecks.Capture("cleanup-"+chapter+"-start-zh");
                    }
                    LocalizationSettings.SelectedLocale=LocalizationSettings.AvailableLocales.GetLocale("en"); return false;
                case 5:
                    Require(board.GetComponentsInChildren<Text>().Any(t=>t.text==QingxiLocalization.Text(chapter==0?"隔板拦截":chapter==1?"溢流水引流":"修复矿硐挡墙")),"Treatment English title missing");
                    Press(game,"BtnCleanupReset");
                    Require(board.PlacedMask==0 && CleanupSave.TryLoad(chapter,out var reset) && reset.placed==0,"Reset did not persist");
                    for(int slot=0;slot<board.Slots.Length;slot++) Place(board,slot);
                    if(chapter==1) Require(!board.Ready,"Misaligned pipe route accepted");
                    if(chapter!=1)
                    {
                        Require(board.Ready,"Three matching shapes should complete the puzzle without extra steps");
                        Require(board.Pieces.Count(p=>Enumerable.Range(0,3).All(s=>board.InstalledPiece(s)!=p.Index))==2,"Distractors should remain unused");
                    }
                    return false;
                case 6:
                    if(chapter==1)
                    {
                        foreach(var piece in board.Pieces)
                        {
                            int turns=(CleanupGame.TargetRotation(piece.Index)-board.Rotation(piece.Index)+4)%4;
                            for(int turn=0;turn<turns;turn++)
                                ExecuteEvents.Execute<IPointerClickHandler>(piece.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
                        }
                    }
                    Require(board.Ready,"Solved treatment board not ready");
                    Require((PlayerPrefs.GetInt("Qingxi.ClearedMask") & (1<<chapter))==0,"Parts alone cleared chapter before confirmation");
                    QingxiSceneChecks.CheckRuntime(QingxiGame.SceneNameForCase(chapter));
                    PolishChecks.Capture("cleanup-"+chapter+"-en");
                    Press(game,"BtnCleanupPrimary"); return false;
                case 7:
                    if(game.CleanupInputEnabled) { PolishChecks.Capture("cleanup-"+chapter+"-flow-en"); stage--; return false; }
                    Require(!CleanupSave.TryLoad(chapter,out _),"Completed treatment save remained");
                    Require(!InvestigationSave.TryLoad(chapter,out _),"Completed investigation save remained");
                    Require((PlayerPrefs.GetInt("Qingxi.ClearedMask") & (1<<chapter))!=0,"Treatment did not clear chapter");
                    Require(PlayerPrefs.GetInt("Qingxi.UnlockedThrough")==Mathf.Min(chapter+1,2),"Treatment did not unlock next chapter");
                    Debug.Log("CLEANUP CHECK: chapter "+chapter+" / drag raycasts / silhouette matching / distractors / shuffled-tray resume / rotations / reset / bilingual UI / completion gate passed.");
                    return true;
                default: return true;
            }
        }

        static CleanupPiece MatchingPiece(CleanupGame board,int slot) => board.Pieces.First(p=>board.ShapeFits(p.Index,slot));
        static Vector2 ScreenPoint(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null,rect.position);
        static void Place(CleanupGame board,int slot)
        {
            Canvas.ForceUpdateCanvases();
            Drag(MatchingPiece(board,slot),ScreenPoint(board.Slots[slot].rectTransform));
        }
        static void Drag(CleanupPiece piece,Vector2 end)
        {
            Canvas.ForceUpdateCanvases();
            Vector2 start=RectTransformUtility.WorldToScreenPoint(null,piece.Rect.position);
            var e=new PointerEventData(EventSystem.current) { position=start, pressPosition=start, button=PointerEventData.InputButton.Left };
            var hits=new System.Collections.Generic.List<RaycastResult>(); EventSystem.current.RaycastAll(e,hits);
            Require(hits.Count>0 && hits[0].gameObject==piece.gameObject,"Part is obscured and cannot receive a drag: "+piece.name);
            e.pointerPressRaycast=hits[0];
            ExecuteEvents.Execute<IBeginDragHandler>(piece.gameObject,e,ExecuteEvents.beginDragHandler);
            e.position=end;
            ExecuteEvents.Execute<IDragHandler>(piece.gameObject,e,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute<IEndDragHandler>(piece.gameObject,e,ExecuteEvents.endDragHandler);
        }
        static void Press(QingxiGame game,string name)
        {
            var button=game.GetComponentsInChildren<Button>(true).First(b=>b.name==name);
            Require(button.isActiveAndEnabled && button.interactable,"Button unavailable: "+name);
            button.onClick.Invoke();
        }
        static T Get<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(obj);
        static void Require(bool value,string message) { if(!value) throw new InvalidOperationException(message); }
    }
}
