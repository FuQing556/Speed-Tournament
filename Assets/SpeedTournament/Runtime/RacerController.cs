using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpeedTournament
{
    public sealed partial class SpeedTournamentPrototype
    {
        public sealed class RacerAgent : MonoBehaviour
        {
            SpeedTournamentPrototype game;
            bool isPlayer,onBranch;
            int characterId,laneIndex,startLane,queuedDirection,activeBranchIndex=-1;
            float lateral,laneFrom,laneElapsed=1f,branchDistance,branchLap;
            float boostTimer,padTimer,skillTimer,slowTimer,stunTimer,impactGrace,launchTimer;
            float nextDecision,heldUntil;int heldDirection,shieldRecoveries;
            Transform core,decor,shieldShell,contactShadow,playerMarker,phaseEcho;
            Renderer coreRenderer;TrailRenderer trail;
            Transform jetExhaust;
            public float TotalProgress{get;private set;}
            public float PreviousProgress{get;private set;}
            public Vector3 PreviousPosition{get;private set;}
            public float Nitro{get;private set;}=0.46f;
            public float SkillEnergy{get;private set;}=0.62f;
            public float Speed{get;private set;}
            public int Rank{get;set;}=1;
            public int RacerId{get;private set;}
            public bool OnBranch=>onBranch;
            public int ActiveBranch=>activeBranchIndex;
            public float BranchT=>onBranch?branchDistance/game.Track.GetBranchLength(activeBranchIndex):0f;
            public float BranchOffset=>onBranch?game.Track.BranchLateral(activeBranchIndex,lateral):0f;
            public bool IsPlayer=>isPlayer;
            public int LaneIndex=>laneIndex;
            public float Lateral=>lateral;
            public bool IsChangingLane=>laneElapsed<RaceBalance.LaneChangeSeconds;
            public int CharacterId=>characterId;
            public int BranchesTaken{get;private set;}
            public int CurrentRoute{get;private set;}
            public bool MagnetActive=>characterId==3&&skillTimer>0;
            public bool PhaseActive=>characterId==4&&skillTimer>0;
            public bool ShieldActive=>characterId==1&&skillTimer>0;
            public bool SkillBoostActive=>skillTimer>0;
            public bool LaunchActive=>launchTimer>0;
            public float LaunchRemaining=>launchTimer;
            public bool IsSlowed=>slowTimer>0;
            public bool IsStunned=>stunTimer>0;
            public float SkillRemaining=>skillTimer;
            public bool SkillReady=>characterId>=0&&SkillEnergy>=0.999f;
            public bool SimulationAutopilot{get;set;}
            public int SkillsUsed{get;private set;}
            public int HitsTaken{get;private set;}
            public int PickupsCollected{get;private set;}
            public Vector3 CameraAnchor=>onBranch?game.Track.BranchPosition(activeBranchIndex,BranchT,0):game.Track.Position(CurrentRoute,TotalProgress,0);

            public static RacerAgent Create(SpeedTournamentPrototype game,Transform parent,bool player,int index,int role,Color color,float progress,float lane)
            {
                var go=new GameObject(player?"PLAYER":"AI_"+index);go.transform.SetParent(parent,false);
                var a=go.AddComponent<RacerAgent>();a.game=game;a.isPlayer=player;a.RacerId=index;a.characterId=role;a.TotalProgress=progress;a.PreviousProgress=progress;
                a.lateral=lane;a.laneIndex=Mathf.Clamp(Mathf.RoundToInt(lane/TrackModel.LaneSpacing+2.5f),0,5);
                a.startLane=a.laneIndex;
                a.core=RaceVisuals.Part(go.transform,"Core",PrimitiveType.Sphere,Vector3.zero,Vector3.one*1.85f,RaceVisuals.Material("NeutralCore",new Color(0.68f,0.75f,0.82f)),true).transform;
                a.coreRenderer=a.core.GetComponent<Renderer>();
                a.contactShadow=RaceVisuals.Part(go.transform,"ContactShadow",PrimitiveType.Sphere,new Vector3(0,-0.95f,0),new Vector3(2.3f,0.035f,2.3f),RaceVisuals.Material("Shadow",RaceVisuals.Ink)).transform;
                a.trail=go.AddComponent<TrailRenderer>();a.trail.time=0.20f;a.trail.startWidth=0.5f;a.trail.endWidth=0;a.trail.minVertexDistance=0.5f;
                a.trail.sharedMaterial=RaceVisuals.Material("Trail",RaceVisuals.Cyan);a.trail.emitting=false;a.trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
                a.jetExhaust=CreateJetExhaust(go.transform);
                a.ConfigureCharacter(role);a.Place();a.PreviousPosition=go.transform.position;
                return a;
            }
            public void ConfigureCharacter(int role)
            {
                characterId=Mathf.Clamp(role,-1,5);
                Color color=role<0?new Color(0.7f,0.76f,0.84f):RaceVisuals.Roles[characterId];
                coreRenderer.sharedMaterial=RaceVisuals.Material("CoreRole"+characterId,color);
                if(decor!=null){decor.gameObject.SetActive(false);Destroy(decor.gameObject);}
                decor=new GameObject("CharacterParts").transform;decor.SetParent(transform,false);
                var ink=RaceVisuals.Material("ArmorDark",new Color(0.065f,0.09f,0.16f));var white=RaceVisuals.Material("VisorWhite",new Color(0.96f,0.99f,1f));
                // Rear-facing visor remains readable from the chase camera and is independent of rolling shell.
                RaceVisuals.Part(decor,"Visor",PrimitiveType.Sphere,new Vector3(0,0.22f,-0.78f),new Vector3(1.15f,0.50f,0.32f),ink);
                for(int side=-1;side<=1;side+=2)RaceVisuals.Part(decor,"Eye",PrimitiveType.Cube,new Vector3(side*0.30f,0.25f,-0.96f),new Vector3(0.17f,0.15f,0.08f),white);
                var accent=RaceVisuals.Material("AccentRole"+characterId,color*0.82f);
                if(role==0)for(int s=-1;s<=1;s+=2)RaceVisuals.Part(decor,"Fin",PrimitiveType.Cube,new Vector3(s*0.82f,0.2f,0.12f),new Vector3(0.20f,0.36f,1.0f),ink,true);
                if(role==1)for(int s=-1;s<=1;s+=2)RaceVisuals.Part(decor,"Armor",PrimitiveType.Cube,new Vector3(s*0.83f,0,0),new Vector3(0.35f,0.80f,0.9f),ink,true);
                if(role==2){RaceVisuals.Part(decor,"Antenna",PrimitiveType.Cylinder,new Vector3(0,1.1f,0),new Vector3(0.10f,0.3f,0.10f),ink);RaceVisuals.Part(decor,"AntennaTip",PrimitiveType.Sphere,new Vector3(0,1.43f,0),Vector3.one*0.28f,accent,true);}
                if(role==3)for(int s=-1;s<=1;s+=2)RaceVisuals.Part(decor,"Magnet",PrimitiveType.Cube,new Vector3(s*0.96f,0.1f,0),new Vector3(0.25f,0.68f,0.35f),ink,true);
                if(role==4)for(int s=-1;s<=1;s+=2)RaceVisuals.Part(decor,"PhaseWing",PrimitiveType.Cube,new Vector3(s*0.72f,0.65f,0.25f),new Vector3(0.22f,0.22f,0.8f),ink,true);
                if(role==5)for(int s=-1;s<=1;s+=2)RaceVisuals.Part(decor,"Battery",PrimitiveType.Cylinder,new Vector3(s*0.82f,0.25f,0.25f),new Vector3(0.30f,0.45f,0.3f),ink,true);
                shieldShell=new GameObject("ActiveShield").transform;shieldShell.SetParent(decor,false);
                for(int i=0;i<12;i++){float a=i*Mathf.PI/6;RaceVisuals.Part(shieldShell,"ShieldSegment",PrimitiveType.Cube,new Vector3(Mathf.Sin(a)*1.35f,0,Mathf.Cos(a)*1.35f),new Vector3(0.23f,0.7f,0.23f),RaceVisuals.Material("ShieldCyan",RaceVisuals.Cyan));}
                phaseEcho=RaceVisuals.Part(decor,"PhaseEcho",PrimitiveType.Sphere,new Vector3(0,0,-1.65f),Vector3.one*1.10f,accent,true).transform;
                shieldShell.gameObject.SetActive(false);phaseEcho.gameObject.SetActive(false);
                if(isPlayer)
                {
                    playerMarker=new GameObject("PlayerMarker").transform;playerMarker.SetParent(decor,false);
                    RaceVisuals.Part(playerMarker,"Marker",PrimitiveType.Cube,new Vector3(0,2.10f,0),new Vector3(0.28f,0.28f,0.28f),white,true).transform.localRotation=Quaternion.Euler(0,0,45);
                }
            }
            public void RaceTick(float dt)
            {
                if(!game.IsRacing)return;
                PreviousPosition=transform.position;PreviousProgress=TotalProgress;
                if(!isPlayer||SimulationAutopilot)HandleAi(dt);else HandleKeyboard();
                impactGrace=Mathf.Max(0,impactGrace-dt);
                bool skill=skillTimer>0,boost=boostTimer>0,pad=padTimer>0;
                float target=RaceBalance.BaseSpeed*RaceBalance.SpeedMultiplier(characterId,skill,boost,pad,LaunchActive);
                if(slowTimer>0)target*=0.73f;if(stunTimer>0)target*=0.48f;
                Speed=target;
                if(IsChangingLane)
                {
                    laneElapsed=Mathf.Min(RaceBalance.LaneChangeSeconds,laneElapsed+dt);
                    float t=laneElapsed/RaceBalance.LaneChangeSeconds;
                    lateral=Mathf.Lerp(laneFrom,TrackModel.LaneOffset(laneIndex),1f-(1f-t)*(1f-t));
                    if(!IsChangingLane&&queuedDirection!=0){int q=queuedDirection;queuedDirection=0;BeginLaneChange(q);}
                }
                else lateral=TrackModel.LaneOffset(laneIndex);
                float remaining=Speed*dt;
                // At most one branch boundary can be crossed per fixed 60Hz simulation substep.
                if(onBranch)
                {
                    float length=game.Track.GetBranchLength(activeBranchIndex);branchDistance+=remaining;float bt=Mathf.Clamp01(branchDistance/length);
                    TotalProgress=branchLap+Mathf.Lerp(game.Track.GetBranchStart(activeBranchIndex),game.Track.GetBranchEnd(activeBranchIndex),bt);
                    if(bt>=1)
                    {
                        float extra=branchDistance-length;CurrentRoute=game.Track.GetBranchTargetRoute(activeBranchIndex);
                        int laneShift=game.Track.GetBranchExitLane(activeBranchIndex)-game.Track.GetBranchEntranceLane(activeBranchIndex);
                        laneIndex+=laneShift;lateral+=laneShift*TrackModel.LaneSpacing;laneFrom+=laneShift*TrackModel.LaneSpacing;
                        onBranch=false;activeBranchIndex=-1;
                        TotalProgress=game.Track.Advance(CurrentRoute,TotalProgress,extra);
                    }
                }
                else
                {
                    float next=game.Track.Advance(CurrentRoute,TotalProgress,remaining);float old=TotalProgress;
                    int enter=-1;
                    for(int i=0;i<game.Track.BranchCount;i++)
                    {
                        if(game.Track.GetBranchSourceRoute(i)!=CurrentRoute||!game.Track.BranchAcceptsLane(i,laneIndex))continue;
                        // Intent at the swept crossing selects the fork. laneIndex is the
                        // adjacent target lane; preserve lateral/laneFrom/laneElapsed below
                        // so a last-moment lane change continues smoothly onto the bridge.
                        // Requiring a completed animation here loses the gate permanently.
                        float at=Mathf.Floor(old)+game.Track.GetBranchStart(i);if(at<old)at+=1f;
                        if(at>=old&&at<=next){enter=i;break;}
                    }
                    TotalProgress=next;
                    if(enter>=0)
                    {
                        float distanceTo=game.Track.AheadMeters(CurrentRoute,old,game.Track.GetBranchStart(enter));
                        activeBranchIndex=enter;onBranch=true;branchDistance=Mathf.Max(0,remaining-distanceTo);branchLap=Mathf.Floor(old);
                        TotalProgress=branchLap+Mathf.Lerp(game.Track.GetBranchStart(enter),game.Track.GetBranchEnd(enter),branchDistance/game.Track.GetBranchLength(enter));
                        // Keep the single queued input; branch lane limits apply when it starts.
                        BranchesTaken++;
                        if(isPlayer)game.ShowFeedback(CurrentRoute==0?"进入支线 · "+game.Track.GetBranchWidth(enter)+" 车道":"汇入主道",RaceVisuals.Orange);
                    }
                }
                boostTimer=Mathf.Max(0,boostTimer-dt);padTimer=Mathf.Max(0,padTimer-dt);skillTimer=Mathf.Max(0,skillTimer-dt);launchTimer=Mathf.Max(0,launchTimer-dt);
                slowTimer=Mathf.Max(0,slowTimer-dt);stunTimer=Mathf.Max(0,stunTimer-dt);
                AddNitro(dt*RaceBalance.PassiveNitro);if(characterId>=0)AddSkillEnergy(dt*RaceBalance.PassiveSkill);
                Place();core.Rotate(Vector3.right,Speed*dt*60f,Space.Self);
                trail.emitting=boost||skill||pad||LaunchActive;
                trail.startWidth=LaunchActive?1.1f:.5f;
                bool jet=LaunchActive||boost||(skill&&(characterId==0||characterId==5));
                if(jetExhaust.gameObject.activeSelf!=jet)jetExhaust.gameObject.SetActive(jet);
                if(jet)jetExhaust.localScale=new Vector3(.65f,.65f,(LaunchActive?4.2f:2.8f)*(1+.06f*Mathf.Sin(game.WorldClock*42)));
                shieldShell.gameObject.SetActive(ShieldActive||MagnetActive);if(ShieldActive||MagnetActive)shieldShell.localRotation=Quaternion.Euler(0,TotalProgress*3000f,0);
                phaseEcho.gameObject.SetActive(PhaseActive);
            }
            void Place()
            {
                Vector3 p=onBranch?game.Track.BranchPosition(activeBranchIndex,BranchT,BranchOffset):game.Track.Position(CurrentRoute,TotalProgress,lateral);
                Vector3 tangent=onBranch?game.Track.TangentBranch(activeBranchIndex,BranchT):game.Track.TangentMain(CurrentRoute,TotalProgress);
                transform.SetPositionAndRotation(p,Quaternion.LookRotation(tangent,Vector3.up));
                float hop=IsChangingLane?Mathf.Sin(Mathf.PI*laneElapsed/RaceBalance.LaneChangeSeconds)*0.24f:0;
                core.localPosition=Vector3.up*hop;decor.localPosition=Vector3.up*hop;
            }
            public void RequestLaneChange(int direction)
            {
                if(!game.IsRacing||Time.timeScale<=0||direction==0)return;
                direction=Math.Sign(direction);
                if(IsChangingLane){queuedDirection=direction;return;}
                BeginLaneChange(direction);
            }
            void BeginLaneChange(int direction)
            {
                int low=onBranch?game.Track.GetBranchEntranceLane(activeBranchIndex):0;
                int high=onBranch?low+game.Track.GetBranchWidth(activeBranchIndex)-1:5;
                int next=Mathf.Clamp(laneIndex+direction,low,high);if(next==laneIndex)return;
                laneFrom=lateral;laneIndex=next;laneElapsed=0;
                if(isPlayer)game.Sound?.Play(RaceSound.Cue.Lane);
            }
            void HandleKeyboard()
            {
                var k=Keyboard.current;if(k==null)return;
                bool l=k.aKey.isPressed||k.leftArrowKey.isPressed,r=k.dKey.isPressed||k.rightArrowKey.isPressed;int d=l==r?0:l?-1:1;
                if(d!=0&&(d!=heldDirection||game.RaceTime>=heldUntil)) {RequestLaneChange(d);heldUntil=game.RaceTime+RaceBalance.HoldRepeatSeconds;}
                heldDirection=d;
            }
            void HandleAi(float dt)
            {
                nextDecision-=dt;
                if(nextDecision<=0&&!IsChangingLane)
                {
                    nextDecision=0.16f+RacerId*0.008f;
                    int target=game.ChooseAiLane(this);if(target!=laneIndex)RequestLaneChange(Math.Sign(target-laneIndex));
                }
                if(SkillReady&&(game.DangerAhead(this,60)||Nitro<0.7f||characterId==0||characterId==2||characterId==5))UseSkill();
                if(Nitro>=0.68f&&boostTimer<=.2f&&!IsStunned&&(!game.DangerAhead(this,28)||ShieldActive||PhaseActive))UseNitro();
            }
            public void AddNitro(float n)=>Nitro=Mathf.Clamp01(Nitro+n);
            public void AddSkillEnergy(float n)=>SkillEnergy=Mathf.Clamp01(SkillEnergy+n);
            public void Pickup(bool skill)
            {
                if(skill)AddSkillEnergy(RaceBalance.PickupSkill);else AddNitro(RaceBalance.PickupNitro);PickupsCollected++;
                if(isPlayer){game.Sound?.Play(skill?RaceSound.Cue.SkillPickup:RaceSound.Cue.Pickup);game.ShowFeedback(skill?"技能 +34%":"氮气 +24%",skill?RaceVisuals.Orange:RaceVisuals.Cyan);}
            }
            public bool BlockHazard()
            {
                if(PhaseActive)return true;
                if(!ShieldActive)return false;
                if(shieldRecoveries<3){shieldRecoveries++;AddSkillEnergy(0.12f);AddNitro(0.12f);}
                if(isPlayer){game.Sound?.Play(RaceSound.Cue.Shield);game.ShowFeedback("护盾挡撞 · 能量回收",RaceVisuals.Cyan);}return true;
            }
            public void ApplySlow(float duration)
            {
                if(BlockHazard()||impactGrace>0)return;slowTimer=Mathf.Max(slowTimer,duration);impactGrace=0.45f;HitsTaken++;
                if(isPlayer){game.Sound?.Play(RaceSound.Cue.Hit);game.ShowFeedback("撞到路障 · 减速",RaceVisuals.Danger);}
            }
            public void ApplyStun(float duration)
            {
                if(BlockHazard()||impactGrace>0)return;stunTimer=Mathf.Max(stunTimer,duration);impactGrace=0.65f;HitsTaken++;
                if(isPlayer){game.Sound?.Play(RaceSound.Cue.Bomb);game.ShowFeedback("炸弹冲击 · 仍可变道",RaceVisuals.Danger);}
            }
            public void ApplyPadBoost(){padTimer=Mathf.Max(padTimer,0.9f);AddNitro(0.07f);if(isPlayer)game.Sound?.Play(RaceSound.Cue.PadBoost);}
            public void ApplyLaunchBoost(){launchTimer=RaceBalance.LaunchDuration;}
            public void UseNitro()
            {
                if(isPlayer&&game.HandleStartNitro())return;
                if(!game.IsRacing||Time.timeScale<=0||Nitro<RaceBalance.NitroCost)return;
                Nitro-=RaceBalance.NitroCost;boostTimer=RaceBalance.NitroDuration;if(isPlayer)game.Sound?.Play(RaceSound.Cue.Boost);
            }
            public void UseSkill()
            {
                if(!game.IsRacing||Time.timeScale<=0||!SkillReady)return;
                // A full battery always casts, even while the previous effect is active.
                // Recasting refreshes duration; it never stacks speed or banks unlimited time.
                SkillEnergy=0;skillTimer=RaceBalance.Duration[characterId];SkillsUsed++;shieldRecoveries=0;
                if(characterId==1||characterId==4)slowTimer=stunTimer=0;
                if(characterId==2)game.PulseFrom(this);
                if(characterId==3)AddNitro(0.24f);if(characterId==5)AddNitro(0.34f);
                if(isPlayer){game.Sound?.PlaySkill(characterId);game.ShowFeedback(CharacterNames[characterId]+" · 技能启动",RaceVisuals.Roles[characterId]);}
            }
#if UNITY_EDITOR
            public void ResetValidation(int role)
            {
                TotalProgress=PreviousProgress=0;CurrentRoute=0;laneIndex=startLane;lateral=TrackModel.LaneOffset(laneIndex);laneElapsed=1;queuedDirection=0;
                onBranch=false;activeBranchIndex=-1;branchDistance=0;boostTimer=padTimer=skillTimer=slowTimer=stunTimer=impactGrace=launchTimer=0;
                jetExhaust.gameObject.SetActive(false);trail.Clear();trail.emitting=false;
                Nitro=0.46f;SkillEnergy=0.62f;SkillsUsed=HitsTaken=PickupsCollected=BranchesTaken=0;nextDecision=0;
                if(characterId!=role)ConfigureCharacter(role);
                Place();PreviousPosition=transform.position;
            }
            public void SetValidationStation(float p,int lane,int route=0)
            {
                TotalProgress=PreviousProgress=p;laneIndex=lane;lateral=TrackModel.LaneOffset(lane);laneElapsed=1;queuedDirection=0;CurrentRoute=route;onBranch=false;activeBranchIndex=-1;Place();PreviousPosition=transform.position;
            }
#endif
        }

        public sealed class FollowCamera : MonoBehaviour
        {
            public Transform Target;
            RacerAgent racer;
            Vector3 smoothAnchor;
            public void Snap(){racer=Target==null?null:Target.GetComponent<RacerAgent>();if(racer==null)return;smoothAnchor=racer.CameraAnchor;PositionCamera(1f);}
            void LateUpdate(){if(racer==null)return;PositionCamera(1f-Mathf.Exp(-14f*Time.unscaledDeltaTime));}
            void PositionCamera(float amount)
            {
                // Track-center chase does not chase each lane change and cancel its visible motion.
                smoothAnchor=Vector3.Lerp(smoothAnchor,racer.CameraAnchor,amount);
                Vector3 forward=Target.forward;
                Vector3 position=smoothAnchor-forward*16f+Vector3.up*9.4f;
                transform.position=position;
                transform.rotation=Quaternion.LookRotation(smoothAnchor+forward*22f+Vector3.up*0.4f-position,Vector3.up);
            }
        }
    }
}
