using UnityEngine;
using UnityEngine.UI;

namespace SpeedTournament
{
    public sealed partial class SpeedTournamentPrototype
    {
        void Supply(int route,float p,int lane,bool skill,int branch=-1)
            =>pickups.Add(PickupOrb.Create(courseRoot,track,route,p,TrackModel.LaneOffset(lane),skill,branch));
        void Hazard(int route,float p,int lane,ObstacleKind kind,int branch=-1)
            =>obstacles.Add(TrackObstacle.Create(courseRoot,track,route,p,lane,kind,6,branch));

        void BuildSkyPickups()
        {
            // Thirteen authored encounter blocks: supply, speed choice, barrier, bomb, recovery.
            // Fixed station spacing leaves ~40m between different hazard decisions.
            for(int block=0;block<13;block++)
            {
                float start=.022f+block*.075f;
                for(int c=0;c<3;c++)
                {
                    Supply(0,start,(block%2)+c*2,c==1);
                    Supply(0,start+.061f,(1-block%2)+c*2,c!=1);
                }
            }
            for(int s=0;s<track.SectionCount;s++)
            {
                float a=track.SectionStart(s),b=track.ReturnStations[s];
                float[] rows={.10f,.34f,.61f,.89f};
                for(int row=0;row<rows.Length;row++)for(int c=0;c<3;c++)
                    Supply(1,Mathf.Lerp(a,b,rows[row]),row%2+c*2,(row+c)%2==0);
            }
            for(int branch=0;branch<track.BranchCount;branch++)
            {
                int first=track.GetBranchEntranceLane(branch),width=track.GetBranchWidth(branch);
                for(int row=0;row<2;row++)for(int lane=first;lane<first+width;lane++)
                    Supply(track.GetBranchSourceRoute(branch),Mathf.Lerp(track.GetBranchStart(branch),track.GetBranchEnd(branch),row==0?.28f:.84f),lane,(lane+row)%2==0,branch);
            }
        }
        void BuildSkyObstacles()
        {
            for(int block=0;block<13;block++)
            {
                float start=.022f+block*.075f;
                AddBoostPair(0,start+.010f,block%2==0?3:1);
                AddTriple(0,start+.029f,block%2==0?0:3);
                Hazard(0,start+.052f,block%2==0?3:2,ObstacleKind.Mascot);
            }
            for(int s=0;s<track.SectionCount;s++)
            {
                float a=track.SectionStart(s),b=track.ReturnStations[s];
                AddTriple(1,Mathf.Lerp(a,b,.24f),0);AddTriple(1,Mathf.Lerp(a,b,.54f),3);
                AddTriple(1,Mathf.Lerp(a,b,.78f),0);
                Hazard(1,Mathf.Lerp(a,b,.41f),4,ObstacleKind.Mascot);
                Hazard(1,Mathf.Lerp(a,b,.68f),1,ObstacleKind.Mascot);
                AddBoostPair(1,Mathf.Lerp(a,b,.94f),3);
            }
            for(int branch=0;branch<track.BranchCount;branch++)
            {
                int first=track.GetBranchEntranceLane(branch),width=track.GetBranchWidth(branch),route=track.GetBranchSourceRoute(branch);
                float p=Mathf.Lerp(track.GetBranchStart(branch),track.GetBranchEnd(branch),.57f);
                if(route==0)Hazard(route,p,first+width/2,ObstacleKind.Mascot,branch);
                else for(int lane=2;lane<4;lane++)Hazard(route,p,lane,ObstacleKind.BoostPad,branch);
            }
        }

        public sealed partial class RaceHud
        {
            GameObject mapPanel;
            Text mapDescription;
            readonly System.Collections.Generic.List<Button> mapButtons=new();
            void BuildMapChoice(Transform parent)
            {
                mapPanel=Panel("MapSelect",parent,new Color(.015f,.025f,.065f,1),Vector2.zero,Vector2.one).gameObject;
                Text title=Label("Title",mapPanel.transform,"选择赛道",54,TextAnchor.MiddleCenter,Color.white);
                SetAnchors(title.rectTransform,new Vector2(.1f,.82f),new Vector2(.9f,.94f));
                for(int i=0;i<CircuitCatalog.Maps.Length;i++)
                {
                    int id=i;float x=.13f+i*.40f;
                    var button=ButtonRect("Map_"+i,mapPanel.transform,CircuitCatalog.Maps[i].Name,Color.white,()=>game.SelectMap(id));
                    UsePillArt(button.GetComponent<Image>());SetAnchors(button.GetComponent<RectTransform>(),new Vector2(x,.61f),new Vector2(x+.34f,.76f));
                    mapButtons.Add(button);
                    var subtitle=Label("MapHint_"+i,mapPanel.transform,i==0?"CLASSIC / 经典":"SKYWAY / 立体",26,TextAnchor.MiddleCenter,RaceVisuals.Cyan);
                    SetAnchors(subtitle.rectTransform,new Vector2(x,.54f),new Vector2(x+.34f,.60f));
                }
                mapDescription=Label("MapDescription",mapPanel.transform,"",30,TextAnchor.MiddleCenter,new Color(.84f,.91f,1));
                SetAnchors(mapDescription.rectTransform,new Vector2(.10f,.25f),new Vector2(.90f,.49f));
                Button back=ButtonRect("BackToCharacters",mapPanel.transform,"返回角色",Color.white,()=>{mapPanel.SetActive(false);characterPanel.SetActive(true);});
                UsePillArt(back.GetComponent<Image>());SetAnchors(back.GetComponent<RectTransform>(),new Vector2(.22f,.10f),new Vector2(.46f,.21f));
                Button start=ButtonRect("StartRace",mapPanel.transform,"开始比赛",Color.white,game.ConfirmMap);
                UsePillArt(start.GetComponent<Image>());SetAnchors(start.GetComponent<RectTransform>(),new Vector2(.54f,.10f),new Vector2(.78f,.21f));
                RefreshMapChoice();mapPanel.SetActive(false);
            }
            public void ShowMapChoice(){characterPanel.SetActive(false);mapPanel.SetActive(true);RefreshMapChoice();}
            public void HideMapChoice()=>mapPanel.SetActive(false);
            public void RefreshMapChoice()
            {
                if(mapDescription==null)return;var map=CircuitCatalog.Maps[game.SelectedMapId];
                mapDescription.text=map.Description+"\n本图记录："+(game.BestTime>0?FormatTime(game.BestTime):"暂无");
                for(int i=0;i<mapButtons.Count;i++)mapButtons[i].GetComponent<Image>().color=i==game.SelectedMapId?Color.white:new Color(.45f,.53f,.68f,.85f);
            }
            public void InvalidateMap(){foreach(var m in GetComponentsInChildren<MiniMapGraphic>(true))m.SetVerticesDirty();nextRefreshTime=0;}
        }
    }
}
