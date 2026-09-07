using System;
using UnityEngine;

namespace SpeedTournament
{
    public sealed partial class SpeedTournamentPrototype
    {
        public enum ObstacleKind { Billboard, Mascot, MovingGate, BoostPad }
        public sealed class PickupOrb : MonoBehaviour
        {
            public int RouteIndex{get;private set;}
            public bool IsSkill{get;private set;}
            public float Progress{get;private set;}
            public int LaneIndex{get;private set;}
            public int BranchIndex{get;private set;}=-1;
            public bool Available=>visual.gameObject.activeSelf;
            readonly int[] collectedLap={-1,-1,-1,-1,-1,-1};
            Transform visual; Vector3 basePosition;bool near;
            public static PickupOrb Create(Transform parent,TrackModel track,int route,float progress,float lane,bool skill,int branch=-1)
            {
                var root=new GameObject(skill?"SkillBattery":"NitroCrystal");root.transform.SetParent(parent,false);
                root.transform.SetPositionAndRotation(track.Position(route,progress,lane),Quaternion.LookRotation(track.TangentMain(route,progress)));
                var p=root.AddComponent<PickupOrb>();p.RouteIndex=route;p.Progress=progress;p.IsSkill=skill;p.LaneIndex=Mathf.Clamp(Mathf.RoundToInt(lane/TrackModel.LaneSpacing+2.5f),0,5);p.basePosition=root.transform.position;
                p.BranchIndex=branch;
                if(branch>=0){float t=Mathf.InverseLerp(track.GetBranchStart(branch),track.GetBranchEnd(branch),progress);root.transform.SetPositionAndRotation(track.BranchPosition(branch,t,track.BranchLateral(branch,lane)),Quaternion.LookRotation(track.TangentBranch(branch,t)));}
                p.visual=new GameObject("Visual").transform;p.visual.SetParent(root.transform,false);
                var color=RaceVisuals.Material(skill?"PickupAmber":"PickupBlue",skill?RaceVisuals.Orange:RaceVisuals.Cyan);
                if(skill)
                {
                    RaceVisuals.Part(p.visual,"Battery",PrimitiveType.Cube,new Vector3(0,0.6f,0),new Vector3(0.82f,1.1f,0.65f),color,true);
                    RaceVisuals.Part(p.visual,"BatteryCap",PrimitiveType.Cube,new Vector3(0,1.23f,0),new Vector3(0.35f,0.15f,0.4f),RaceVisuals.Material("ItemInk",RaceVisuals.Ink));
                }
                else RaceVisuals.Part(p.visual,"Diamond",PrimitiveType.Cube,new Vector3(0,0.6f,0),Vector3.one*0.76f,color,true).transform.localRotation=Quaternion.Euler(0,0,45);
                RaceVisuals.Part(p.visual,"GroundMarker",PrimitiveType.Cylinder,new Vector3(0,-0.98f,0),new Vector3(1.3f,0.025f,1.3f),color);
                return p;
            }
            public void SetVisible(bool visible,bool playerCollected)
            {
                near=visible;bool active=visible&&!playerCollected;if(visual.gameObject.activeSelf!=active)visual.gameObject.SetActive(active);
            }
            public bool Collected(RacerAgent r)=>collectedLap[r.RacerId]==Mathf.FloorToInt(r.TotalProgress);
            public void Animate(float time)
            {
                if(!near||!visual.gameObject.activeSelf)return;
                visual.localPosition=Vector3.up*(0.10f*Mathf.Sin(time*2.8f+Progress*12f));
            }
            public bool TryCollect(RacerAgent racer,TrackModel track)
            {
                if(!OnPath(racer)||Collected(racer))return false;
                float width=racer.MagnetActive?TrackModel.LaneSpacing*2.15f:1.14f;
                if(Mathf.Abs(racer.Lateral-TrackModel.LaneOffset(LaneIndex))>width)return false;
                float along=BranchIndex<0?track.AheadMeters(RouteIndex,racer.PreviousProgress,Progress):(Progress-Mathf.Repeat(racer.PreviousProgress,1))*BranchScale(track);
                float travel=BranchIndex<0?track.AheadMeters(RouteIndex,racer.PreviousProgress,racer.TotalProgress):(racer.TotalProgress-racer.PreviousProgress)*BranchScale(track);
                if(along<0||along>travel+(racer.MagnetActive?10f:1.6f))return false;
                collectedLap[racer.RacerId]=Mathf.FloorToInt(racer.TotalProgress);racer.Pickup(IsSkill);
                if(racer.IsPlayer)visual.gameObject.SetActive(false);return true;
            }
            public void Reactivate(){for(int i=0;i<collectedLap.Length;i++)collectedLap[i]=-1;}
            public bool OnPath(RacerAgent r)=>BranchIndex>=0?r.OnBranch&&r.ActiveBranch==BranchIndex:!r.OnBranch&&r.CurrentRoute==RouteIndex;
            float BranchScale(TrackModel t)=>t.GetBranchLength(BranchIndex)/(t.GetBranchEnd(BranchIndex)-t.GetBranchStart(BranchIndex));
            public float Ahead(RacerAgent r,TrackModel t)=>!OnPath(r)?float.PositiveInfinity:BranchIndex<0?t.AheadMeters(RouteIndex,r.TotalProgress,Progress):(Progress-Mathf.Repeat(r.TotalProgress,1))*BranchScale(t);
        }
        public sealed class TrackObstacle : MonoBehaviour
        {
            public int RouteIndex{get;private set;}
            public float Progress{get;private set;}
            public int LaneIndex{get;private set;}
            public ObstacleKind Kind{get;private set;}
            public int BranchIndex{get;private set;}=-1;
            public bool Available=>disabledUntil<=raceClock;
            readonly int[] triggeredLap={-1,-1,-1,-1,-1,-1};
            Transform visual,burst;
            Vector3 fixedPosition,right;
            float disabledUntil,raceClock,movingLateral;bool near;
            public float Lateral=>TrackModel.LaneOffset(LaneIndex)+movingLateral;
            public static TrackObstacle Create(Transform parent,TrackModel track,int route,float progress,int lane,ObstacleKind kind,int racerCount,int branch=-1)
            {
                var go=new GameObject("R"+route+"_"+kind+"_"+progress.ToString("F3")+"_L"+lane);go.transform.SetParent(parent,false);
                go.transform.SetPositionAndRotation(track.MainLanePoint(route,progress,TrackModel.LaneOffset(lane)),Quaternion.LookRotation(track.TangentMain(route,progress)));
                var o=go.AddComponent<TrackObstacle>();o.RouteIndex=route;o.Progress=progress;o.LaneIndex=lane;o.Kind=kind;o.fixedPosition=go.transform.position;o.right=go.transform.right;
                o.BranchIndex=branch;
                if(branch>=0){float t=Mathf.InverseLerp(track.GetBranchStart(branch),track.GetBranchEnd(branch),progress);go.transform.SetPositionAndRotation(track.BranchPosition(branch,t,track.BranchLateral(branch,TrackModel.LaneOffset(lane)))-Vector3.up*1.03f,Quaternion.LookRotation(track.TangentBranch(branch,t)));o.fixedPosition=go.transform.position;o.right=go.transform.right;}
                o.visual=new GameObject("Visual").transform;o.visual.SetParent(go.transform,false);o.BuildVisual();
                return o;
            }
            void BuildVisual()
            {
                Material red=RaceVisuals.Material("DangerRed",RaceVisuals.Danger),ink=RaceVisuals.Material("ItemInk",RaceVisuals.Ink),yellow=RaceVisuals.Material("HazardYellow",new Color(1f,0.82f,0.17f)),white=RaceVisuals.Material("OffWhite",new Color(0.90f,0.96f,1f));
                if(Kind==ObstacleKind.BoostPad)
                {
                    var cyan=RaceVisuals.Material("PadCyan",RaceVisuals.Cyan);
                    RaceVisuals.Part(visual,"PadBase",PrimitiveType.Cube,new Vector3(0,0.045f,0),new Vector3(2.8f,0.07f,5.8f),ink);
                    RaceVisuals.Part(visual,"PadInset",PrimitiveType.Cube,new Vector3(0,0.09f,0),new Vector3(2.55f,0.04f,5.4f),cyan);
                    for(int i=0;i<3;i++)RaceVisuals.Chevron(visual,new Vector3(0,0.13f,-1.8f+i*1.8f),1.6f,white);
                }
                else if(Kind==ObstacleKind.Mascot)
                {
                    RaceVisuals.Part(visual,"Bomb",PrimitiveType.Sphere,new Vector3(0,1,0),Vector3.one*1.65f,ink,true);
                    RaceVisuals.Part(visual,"Fuse",PrimitiveType.Cylinder,new Vector3(0,1.98f,0),new Vector3(0.13f,0.26f,0.13f),yellow);
                    RaceVisuals.Part(visual,"FuseSpark",PrimitiveType.Sphere,new Vector3(0,2.28f,0),Vector3.one*0.32f,red,true);
                    Cross(visual,new Vector3(0,1,-0.82f),red);
                    RaceVisuals.Part(visual,"DangerFootprint",PrimitiveType.Cylinder,new Vector3(0,0.04f,0),new Vector3(2.3f,0.02f,2.3f),red);
                }
                else
                {
                    RaceVisuals.Part(visual,"Barrier",PrimitiveType.Cube,new Vector3(0,1.22f,0),new Vector3(2.38f,2.30f,0.48f),red,true);
                    for(int i=-1;i<=1;i++)
                    {
                        var stripe=RaceVisuals.Part(visual,"WarningStripe",PrimitiveType.Cube,new Vector3(i*0.72f,0.33f,-0.26f),new Vector3(0.28f,0.35f,0.06f),yellow);stripe.transform.localRotation=Quaternion.Euler(0,0,-30);
                    }
                    Cross(visual,new Vector3(0,1.45f,-0.30f),white);
                    if(Kind==ObstacleKind.MovingGate)RaceVisuals.Part(visual,"AlertLamp",PrimitiveType.Sphere,new Vector3(0,2.55f,0),Vector3.one*0.30f,yellow,true);
                }
                burst=new GameObject("PooledBurst").transform;burst.SetParent(transform,false);
                for(int i=0;i<5;i++)RaceVisuals.Part(burst,"Spark",PrimitiveType.Cube,new Vector3(Mathf.Sin(i*1.26f)*0.9f,1+Mathf.Cos(i*1.26f)*0.7f,0),Vector3.one*0.28f,yellow);
                burst.gameObject.SetActive(false);
            }
            static void Cross(Transform parent,Vector3 p,Material mat)
            {
                for(int s=-1;s<=1;s+=2)RaceVisuals.Part(parent,"Cross",PrimitiveType.Cube,p,new Vector3(0.18f,0.94f,0.08f),mat).transform.localRotation=Quaternion.Euler(0,0,s*45);
            }
            public void SetVisible(bool value){near=value;bool active=value&&Available;if(visual.gameObject.activeSelf!=active)visual.gameObject.SetActive(active);}
            public void Animate(float time)
            {
                raceClock=time;
                if(Kind==ObstacleKind.MovingGate)
                {
                    // Only moves inside its own lane; the two neighbouring lanes remain safe.
                    movingLateral=Mathf.Sin(time*1.8f+Progress*24f)*0.35f;
                    if(near)transform.position=fixedPosition+right*movingLateral;
                }
                if(!Available)
                {
                    float since=8f-(disabledUntil-raceClock);bool flash=near&&since<0.30f;
                    if(burst.gameObject.activeSelf!=flash)burst.gameObject.SetActive(flash);
                    if(flash)burst.localScale=Vector3.one*(1f+since*5);
                }
                else if(near&&!visual.gameObject.activeSelf)visual.gameObject.SetActive(true);
            }
            public bool TryTrigger(RacerAgent racer,TrackModel track)
            {
                if(!Available||!OnPath(racer))return false;
                int lap=Mathf.FloorToInt(racer.TotalProgress);if(triggeredLap[racer.RacerId]==lap)return false;
                if(Mathf.Abs(racer.Lateral-Lateral)>1.36f)return false;
                float travel=BranchIndex<0?track.AheadMeters(RouteIndex,racer.PreviousProgress,racer.TotalProgress):(racer.TotalProgress-racer.PreviousProgress)*BranchScale(track);
                float ahead=BranchIndex<0?track.AheadMeters(RouteIndex,racer.PreviousProgress,Progress):(Progress-Mathf.Repeat(racer.PreviousProgress,1))*BranchScale(track);
                float depth=Kind==ObstacleKind.BoostPad?2.8f:0.7f;
                if(ahead<0||ahead>travel+depth)return false;
                triggeredLap[racer.RacerId]=lap;
                if(Kind==ObstacleKind.BoostPad)racer.ApplyPadBoost();
                else if(racer.PhaseActive){} // Phase passes through without consuming the obstacle.
                else if(racer.ShieldActive){racer.BlockHazard();Disable();}
                else if(Kind==ObstacleKind.Mascot){racer.ApplyStun(0.55f);Disable();}
                else racer.ApplySlow(0.85f);
                return true;
            }
            public bool OnPath(RacerAgent r)=>BranchIndex>=0?r.OnBranch&&r.ActiveBranch==BranchIndex:!r.OnBranch&&r.CurrentRoute==RouteIndex;
            float BranchScale(TrackModel t)=>t.GetBranchLength(BranchIndex)/(t.GetBranchEnd(BranchIndex)-t.GetBranchStart(BranchIndex));
            public float Ahead(RacerAgent r,TrackModel t)=>!OnPath(r)?float.PositiveInfinity:BranchIndex<0?t.AheadMeters(RouteIndex,r.TotalProgress,Progress):(Progress-Mathf.Repeat(r.TotalProgress,1))*BranchScale(t);
            public void Disable()
            {
                if(Kind==ObstacleKind.BoostPad)return;disabledUntil=raceClock+8f;visual.gameObject.SetActive(false);
            }
#if UNITY_EDITOR
            public void ResetValidation(){for(int i=0;i<triggeredLap.Length;i++)triggeredLap[i]=-1;disabledUntil=raceClock=0;burst.gameObject.SetActive(false);}
#endif
        }
    }
}
