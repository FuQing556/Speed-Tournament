#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Game=SpeedTournament.SpeedTournamentPrototype;

namespace SpeedTournament.Editor
{
    // Runs the actual movement/AI/pickup/hazard loop, not a separate race approximation.
    public static class CircuitValidation
    {
        [Serializable] public sealed class Scenario
        {
            public int role, frameRate, skills, hits, pickups, branches, frames;
            public bool finished, continuous=true, staysOnTrack=true, spritesAlive=true;
            public float seconds, largestStep, maxTrackError;
            public double meanStepMs,p95StepMs,maxStepMs;
            public long allocatedBytes;
        }
        public static Scenario RunScenario(int role,int frameRate=60)
        {
            var game=UnityEngine.Object.FindAnyObjectByType<Game>();if(game==null)throw new Exception("Enter Play mode first");
            game.ResetValidation(role);
            var result=new Scenario{role=role,frameRate=frameRate};var times=new List<double>(12000);
            float dt=1f/frameRate;long bytes=GC.GetAllocatedBytesForCurrentThread();
            for(int f=0;f<frameRate*160&&!game.IsFinished;f++)
            {
                Vector3 before=game.Player.transform.position;
                game.TickRace(dt);
                var p=game.Player;float step=Vector3.Distance(before,p.transform.position);result.largestStep=Mathf.Max(result.largestStep,step);
                if(step>RaceBalance.BaseSpeed*1.8f*dt+TrackModelLaneStep(dt))result.continuous=false;
                Vector3 expected=p.OnBranch?game.Track.BranchPosition(p.ActiveBranch,p.BranchT,p.BranchOffset):game.Track.Position(p.CurrentRoute,p.TotalProgress,p.Lateral);
                float error=Vector3.Distance(expected,p.transform.position);result.maxTrackError=Mathf.Max(result.maxTrackError,error);
                if(error>0.03f||float.IsNaN(p.TotalProgress)||float.IsInfinity(p.TotalProgress))result.staysOnTrack=false;
                if(!p.IsChangingLane&&!p.OnBranch&&Mathf.Abs(p.Lateral-Game.TrackModel.LaneOffset(p.LaneIndex))>0.01f)result.staysOnTrack=false;
                times.Add(game.LastSimulationMilliseconds);result.frames++;
            }
            result.allocatedBytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
            result.finished=game.IsFinished;result.seconds=game.RaceTime;result.skills=game.Player.SkillsUsed;result.hits=game.Player.HitsTaken;result.pickups=game.Player.PickupsCollected;result.branches=game.Player.BranchesTaken;
            times.Sort();double sum=0;foreach(double t in times)sum+=t;result.meanStepMs=sum/times.Count;result.p95StepMs=times[(int)(times.Count*0.95f)];result.maxStepMs=times[times.Count-1];
            for(int i=0;i<6;i++)if(Game.UiArt.Skill(i)==null)result.spritesAlive=false;
            Directory.CreateDirectory("Temp/Validation");File.WriteAllText("Temp/Validation/map_"+game.SelectedMapId+"_role_"+role+"_"+frameRate+"fps.json",JsonUtility.ToJson(result,true));
            return result;
        }
        static float TrackModelLaneStep(float dt)=>Game.TrackModel.LaneSpacing*Mathf.Min(1,dt/RaceBalance.LaneChangeSeconds)*2f;
        public static string Junctions()
        {
            var g=UnityEngine.Object.FindAnyObjectByType<Game>();var t=g.Track;var errors=new List<string>();
            int entered=0,switched=0;float biggest=0;var savings=new List<string>();
            for(int b=0;b<t.BranchCount;b++)
            {
                if(t.GetBranchSourceRoute(b)!=0)continue;
                int first=t.GetBranchEntranceLane(b),width=t.GetBranchWidth(b);
                for(int lane=first;lane<first+width;lane++)
                {
                    g.ResetValidation(4);var p=g.Player;p.SimulationAutopilot=false;
                    p.SetValidationStation(t.GetBranchStart(b)-.002f,lane);
                    for(int f=0;f<90&&!p.OnBranch;f++)g.TickRace(1f/60);
                    if(!p.OnBranch||p.ActiveBranch!=b){errors.Add("Missed entry "+b+" lane "+lane);continue;}entered++;
                    int target=lane==first?lane+1:lane-1;
                    if(width>1)p.RequestLaneChange(target-lane);
                    for(int f=0;f<500&&p.OnBranch;f++)
                    {
                        Vector3 prev=p.transform.position;g.TickRace(1f/60);
                        float jump=Vector3.Distance(prev,p.transform.position);biggest=Mathf.Max(biggest,jump);
                        if(jump>2.6f)errors.Add("Discontinuity "+b+" / "+jump);
                    }
                    if(p.OnBranch||p.CurrentRoute!=1)errors.Add("No branch exit "+b);
                    int expected=t.GetBranchExitLane(b)+(width>1?target:lane)-first;
                    if(p.LaneIndex!=expected)errors.Add("Lane mapping "+b);else if(width>1)switched++;
                    // Stay on this lane until the compulsory six-lane return, checking continuity.
                    int limit=0;while((p.CurrentRoute!=0||p.OnBranch)&&limit++<1000)
                    {Vector3 prev=p.transform.position;g.TickRace(1f/60);float jump=Vector3.Distance(prev,p.transform.position);biggest=Mathf.Max(biggest,jump);if(jump>2.6f)errors.Add("Return discontinuity "+b);}
                    if(p.CurrentRoute!=0||p.OnBranch)errors.Add("Missed return "+b);
                }
                // An unmarked main-road lane must not be captured by a nearby gate.
                g.ResetValidation(0);g.Player.SimulationAutopilot=false;int bypass=first==0?5:0;
                g.Player.SetValidationStation(t.GetBranchStart(b)-.002f,bypass);
                for(int f=0;f<25;f++)g.TickRace(1f/60);
                if(g.Player.OnBranch||g.Player.CurrentRoute!=0)errors.Add("Gate captured bypass lane "+b);
                int ret=b+1;
                float main=t.AheadMeters(0,t.GetBranchStart(b),t.GetBranchEnd(ret));
                float alt=t.GetBranchLength(b)+t.AheadMeters(1,t.GetBranchEnd(b),t.GetBranchStart(ret))+t.GetBranchLength(ret);
                savings.Add("section "+b/2+": main "+main.ToString("F1")+"m / alternate "+alt.ToString("F1")+"m");
            }
            string result=(errors.Count==0?"PASS":string.Join("; ",errors))+"; entries="+entered+"; wide-lane switches="+switched+"; max step="+biggest+"m\n"+string.Join("\n",savings);
            Directory.CreateDirectory("Temp/Validation");File.WriteAllText("Temp/Validation/junctions_map_"+g.SelectedMapId+".txt",result);return result;
        }
        public static string Geometry()
        {
            var game=UnityEngine.Object.FindAnyObjectByType<Game>();var t=game.Track;
            float grade=0,minRadius=99999,innerRadius=99999,maxSeam=0;
            for(int i=0;i<2000;i++)
            {
                float p=i/2000f;Vector3 v=t.TangentMain(p);grade=Mathf.Max(grade,Mathf.Abs(v.y));
                float angle=Vector3.Angle(t.TangentMain(p),t.TangentMain(p+0.001f))*Mathf.Deg2Rad;
                if(angle>0.0001f)minRadius=Mathf.Min(minRadius,Vector3.Distance(t.EvaluateMain(p),t.EvaluateMain(p+0.001f))/angle);
                if(t.InnerIsOpen(p)){angle=Vector3.Angle(t.TangentMain(1,p),t.TangentMain(1,p+0.001f))*Mathf.Deg2Rad;if(angle>0.0001f)innerRadius=Mathf.Min(innerRadius,Vector3.Distance(t.EvaluateRoute(1,p),t.EvaluateRoute(1,p+0.001f))/angle);}
            }
            for(int i=0;i<t.BranchCount;i++)
            {
                for(int lane=0;lane<t.GetBranchWidth(i);lane++)
                {
                    float lateral=(lane-(t.GetBranchWidth(i)-1)*.5f)*Game.TrackModel.LaneSpacing;
                    maxSeam=Mathf.Max(maxSeam,Vector3.Distance(t.BranchPosition(i,0,lateral),t.Position(t.GetBranchSourceRoute(i),t.GetBranchStart(i),Game.TrackModel.LaneOffset(t.GetBranchEntranceLane(i)+lane))));
                    maxSeam=Mathf.Max(maxSeam,Vector3.Distance(t.BranchPosition(i,1,lateral),t.Position(t.GetBranchTargetRoute(i),t.GetBranchEnd(i),Game.TrackModel.LaneOffset(t.GetBranchExitLane(i)+lane))));
                }
            }
            string result="length="+t.ApproxLength+"m; max grade="+grade+"; min outer radius="+minRadius+"; min open inner radius="+innerRadius+"; branch seam="+maxSeam+"m";
            Directory.CreateDirectory("Temp/Validation");File.WriteAllText("Temp/Validation/geometry_map_"+game.SelectedMapId+".txt",result);return result;
        }
        public static string Targeted()
        {
            var g=UnityEngine.Object.FindAnyObjectByType<Game>();var p=g.Player;var errors=new List<string>();
            g.ResetValidation(0);p.SimulationAutopilot=false;
            if(p.LaneIndex!=2)errors.Add("Player not in third lane");
            p.RequestLaneChange(1);g.TickRace(1f/60);if(p.Lateral<=Game.TrackModel.LaneOffset(2))errors.Add("No first-frame response");
            p.RequestLaneChange(1);p.RequestLaneChange(1);p.RequestLaneChange(1);
            for(int i=0;i<20;i++)g.TickRace(1f/60);if(p.LaneIndex!=4)errors.Add("Unbounded lane queue");
            g.TogglePause();int lane=p.LaneIndex;p.RequestLaneChange(1);if(p.LaneIndex!=lane)errors.Add("Input while paused");g.Resume();
            for(int role=0;role<6;role++)
            {
                g.ResetValidation(role);p.SimulationAutopilot=false;p.AddSkillEnergy(1);p.UseSkill();
                g.TickRace(1f/60);if(p.Speed<=RaceBalance.BaseSpeed)errors.Add("Role "+role+" gives no speed benefit");
                if(role==1||role==4){p.ApplySlow(1);p.ApplyStun(1);if(p.IsSlowed||p.IsStunned)errors.Add("Immunity "+role);}
                int casts=p.SkillsUsed;p.UseSkill();if(p.SkillsUsed!=casts)errors.Add("Cast without energy "+role);
                p.AddSkillEnergy(1);p.UseSkill();if(p.SkillsUsed!=casts+1)errors.Add("Hidden cooldown "+role);
                if(p.SkillRemaining>RaceBalance.Duration[role]+.01f)errors.Add("Unbounded duration stacking "+role);
            }
            g.ResetValidation(2);p.SimulationAutopilot=false;p.SetValidationStation(0.18f,1);p.AddSkillEnergy(1);p.UseSkill();
            int cleared=0;foreach(var o in g.ValidationObstacles)if(!o.Available)cleared++;if(cleared<2)errors.Add("Pulse did not clear neighbouring roadblocks");
            g.ResetValidation(3);p.SimulationAutopilot=false;p.SetValidationStation(0.025f,2);p.AddSkillEnergy(1);p.UseSkill();for(int i=0;i<8;i++)g.TickRace(1f/60);if(p.PickupsCollected<2)errors.Add("Magnet did not collect adjacent supplies");
            g.ResetValidation(0);p.SimulationAutopilot=false;p.SetValidationStation(0.19f,1);for(int i=0;i<8;i++)g.TickRace(1f/20);if(p.HitsTaken==0)errors.Add("Swept barrier missed at 20fps");
            g.ResetValidation(1);p.SimulationAutopilot=false;p.AddSkillEnergy(1);p.UseSkill();for(int i=0;i<12;i++)p.BlockHazard();
            if(Mathf.Abs(p.SkillEnergy-.36f)>.001f||p.SkillRemaining>RaceBalance.Duration[1])errors.Add("Shield recovery cap");
            string result=errors.Count==0?"PASS: middle grid; first-frame input; one-slot lane buffer; pause input; six useful skills; immunity; energy-only casting and recasting; bounded shield recovery; pulse clear; gravity pickups; low-FPS hazard sweep":string.Join("; ",errors);
            Directory.CreateDirectory("Temp/Validation");File.WriteAllText("Temp/Validation/targeted.txt",result);return result;
        }
    }
}
#endif
