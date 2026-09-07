#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Game=SpeedTournament.SpeedTournamentPrototype;

namespace SpeedTournament.Editor
{
    public static class ForkEntryValidation
    {
        // The classic map has single-lane entrances: test all three laps and repeated input,
        // not just entering a wide sky bridge once from an already centered lane.
        public static string ClassicRepeat()
        {
            var g=UnityEngine.Object.FindAnyObjectByType<Game>();
            if(g==null)throw new InvalidOperationException("Enter Play mode first");
            g.SetValidationMap(0);var track=g.Track;var errors=new List<string>();int cases=0;
            for(int lap=0;lap<3;lap++)
            for(int branch=0;branch<track.BranchCount;branch++)
            {
                if(track.GetBranchSourceRoute(branch)!=0)continue;
                foreach(int fps in new[]{20,30,60})
                foreach(bool boosted in new[]{false,true})
                foreach(float gap in new[]{.004f,.0005f,0f})
                {
                    g.ResetValidation(0);var p=g.Player;p.SimulationAutopilot=false;
                    p.SetValidationStation(lap+track.GetBranchStart(branch)-gap,1);
                    if(boosted){p.AddSkillEnergy(1);p.UseSkill();p.AddNitro(1);p.UseNitro();}
                    p.RequestLaneChange(-1);bool entered=false;
                    for(int f=0;f<fps/2;f++){g.TickRace(1f/fps);if(p.OnBranch&&p.ActiveBranch==branch)entered=true;}
                    if(!entered)errors.Add("Late miss: lap="+lap+" branch="+branch+" fps="+fps+" boost="+boosted+" gap="+gap);
                    cases++;
                }
                for(int lane=0;lane<6;lane++)
                {
                    g.ResetValidation(0);var p=g.Player;p.SimulationAutopilot=false;
                    p.SetValidationStation(lap+track.GetBranchStart(branch)-.05f,lane);
                    bool entered=false;float nextInput=0;
                    for(int f=0;f<150;f++)
                    {
                        float elapsed=f/60f;
                        if(elapsed>=nextInput){p.RequestLaneChange(-1);nextInput+=RaceBalance.HoldRepeatSeconds;}
                        g.TickRace(1f/60);if(p.OnBranch&&p.ActiveBranch==branch)entered=true;
                    }
                    if(!entered)errors.Add("Repeated-input miss: lap="+lap+" branch="+branch+" lane="+lane);
                    cases++;
                }
            }
            string result=(errors.Count==0?"PASS":string.Join("\n",errors))+"\ncases="+cases+"; classic map; three laps; all entrances; late 1->0 and repeated input from all six lanes";
            Directory.CreateDirectory("Temp/Validation");File.WriteAllText("Temp/Validation/classic_repeat_regression.txt",result);return result;
        }
        public static string Run()
        {
            var g=UnityEngine.Object.FindAnyObjectByType<Game>();
            if(g==null)throw new InvalidOperationException("Enter Play mode first");
            var errors=new List<string>();int cases=0;float maxStep=0;
            foreach(int map in new[]{0,1})
            {
                g.SetValidationMap(map);var track=g.Track;
                foreach(int fps in new[]{20,30,60})
                foreach(bool boosted in new[]{false,true})
                for(int branch=0;branch<track.BranchCount;branch++)
                {
                    if(track.GetBranchSourceRoute(branch)!=0)continue;
                    for(int from=0;from<6;from++)
                    for(int direction=-1;direction<=1;direction++)
                    foreach(float gap in new[]{.004f,.0005f,.00005f,0f,-.00005f})
                    {
                        int target=from+direction;if(target<0||target>5)continue;
                        g.ResetValidation(0);var p=g.Player;p.SimulationAutopilot=false;
                        p.SetValidationStation(track.GetBranchStart(branch)-gap,from);
                        if(boosted){p.AddSkillEnergy(1);p.UseSkill();p.AddNitro(1);p.UseNitro();}
                        p.RequestLaneChange(direction);
                        bool expected=gap>=0&&track.BranchAcceptsLane(branch,target);
                        bool entered=false,wrongBranch=false,continuous=true;float dt=1f/fps;
                        for(int f=0;f<fps/2;f++)
                        {
                            Vector3 before=p.transform.position;g.TickRace(dt);
                            float step=Vector3.Distance(before,p.transform.position);maxStep=Mathf.Max(maxStep,step);
                            if(step>RaceBalance.BaseSpeed*1.8f*dt+Game.TrackModel.LaneSpacing*Mathf.Min(1,2*dt/RaceBalance.LaneChangeSeconds))continuous=false;
                            if(p.OnBranch){entered=true;if(p.ActiveBranch!=branch)wrongBranch=true;}
                        }
                        if(entered!=expected||wrongBranch||!continuous)
                            errors.Add("map="+map+" fps="+fps+" boosted="+boosted+" fork="+branch+" lane="+from+"->"+target+" gap="+gap+" expected="+expected+" got="+entered+" continuous="+continuous);
                        if(p.OnBranch&&Mathf.Abs(p.BranchOffset)>track.GetBranchWidth(branch)*Game.TrackModel.LaneSpacing*.5f)
                            errors.Add("Outside bridge after lane animation, branch="+branch);
                        cases++;
                    }
                    // A queued second step is retained and clamped to the actual bridge width.
                    g.ResetValidation(0);g.Player.SimulationAutopilot=false;
                    int first=track.GetBranchEntranceLane(branch),width=track.GetBranchWidth(branch);
                    g.Player.SetValidationStation(track.GetBranchStart(branch)-.00005f,first);
                    if(width>1){g.Player.RequestLaneChange(1);g.Player.RequestLaneChange(1);}
                    for(int f=0;f<fps/2;f++)g.TickRace(1f/fps);
                    int wanted=first+Mathf.Min(2,width-1);
                    if(!g.Player.OnBranch||g.Player.ActiveBranch!=branch||g.Player.LaneIndex!=wanted)errors.Add("Queued input / width bound at fork "+branch);
                    cases++;
                }
            }
            string result=(errors.Count==0?"PASS":string.Join("\n",errors))+"\ncases="+cases+"; largest frame displacement="+maxStep+"m; targets tested: centered / within gate / enter from outside / leave gate / late-after-crossing / queued; 20,30,60fps; base and boosted speed";
            Directory.CreateDirectory("Temp/Validation");File.WriteAllText("Temp/Validation/fork_entry_regression.txt",result);return result;
        }
    }
}
#endif
