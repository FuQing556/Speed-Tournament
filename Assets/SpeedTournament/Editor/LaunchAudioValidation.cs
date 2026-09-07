#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Game=SpeedTournament.SpeedTournamentPrototype;

namespace SpeedTournament.Editor
{
    public static class LaunchAudioValidation
    {
        public static string Run()
        {
            var g=UnityEngine.Object.FindAnyObjectByType<Game>();
            if(g==null)throw new InvalidOperationException("Enter Play mode first");
            var errors=new List<string>();int cases=0;g.SetValidationMap(0);
            foreach(int fps in new[]{20,30,60})
            for(int role=0;role<6;role++)
            foreach(float timing in new[]{-.6f,-.35f,-.2f,-.001f,0f,.05f,.20f,.201f,.35f})
            {
                g.ResetCountdownValidation(role);var p=g.Player;
                if(timing<0)g.TickCountdown(3+timing);
                else {g.TickCountdown(3);if(timing>0)g.TickRace(timing);}
                float energy=p.Nitro;Vector3 at=p.transform.position;
                p.UseNitro();bool expected=timing>=-RaceBalance.LaunchEarlyWindow&&timing<=RaceBalance.LaunchLateWindow;
                if(timing<0)
                {
                    if(p.LaunchActive||p.transform.position!=at||g.RaceTime!=0)errors.Add("Moved/launched before GO");
                    g.TickCountdown(g.Countdown+.001f);
                }
                if(p.LaunchActive!=expected||g.LaunchTriggered!=expected)errors.Add("Timing mismatch: role="+role+" fps="+fps+" timing="+timing);
                if(timing<=RaceBalance.LaunchLateWindow&&Mathf.Abs(p.Nitro-energy)>.00001f)errors.Add("Launch/early input spent energy");
                if(timing>RaceBalance.LaunchLateWindow&&Mathf.Abs(p.Nitro-(energy-RaceBalance.NitroCost))>.0001f)errors.Add("Ordinary nitro not available after window");
                g.TickRace(1f/fps);
                if(expected&&Mathf.Abs(p.Speed-RaceBalance.BaseSpeed*(1+RaceBalance.LaunchBonus))>.001f)errors.Add("Launch speed wrong");
                cases++;
            }
            // No launch without an input, and an early press cannot pre-arm a later window.
            g.ResetCountdownValidation();g.Player.UseNitro();g.TickCountdown(3);
            if(g.LaunchTriggered||g.Player.LaunchActive)errors.Add("Early input auto-launched");cases++;
            // Pause freezes both the timing window and incoming touch/keyboard requests.
            g.ResetCountdownValidation();g.TickCountdown(2.8f);Time.timeScale=0;
            float remaining=g.Countdown;g.Player.UseNitro();g.TickCountdown(2);
            if(g.LaunchArmed||g.Countdown!=remaining)errors.Add("Paused input/countdown advanced");Time.timeScale=1;cases++;
            g.Player.UseNitro();g.TickCountdown(.21f);g.TickRace(.05f);
            float duration=g.Player.LaunchRemaining,energyBefore=g.Player.Nitro;
            for(int i=0;i<12;i++)g.Player.UseNitro();
            if(Mathf.Abs(g.Player.LaunchRemaining-duration)>.00001f||g.Player.Nitro!=energyBefore)errors.Add("Spam refreshed launch/spent nitro");cases++;
            g.TickRace(.18f);energyBefore=g.Player.Nitro;g.Player.UseNitro();
            if(Mathf.Abs(g.Player.Nitro-(energyBefore-RaceBalance.NitroCost))>.0001f)errors.Add("Launch added ordinary nitro cooldown");
            g.Player.AddSkillEnergy(1);g.Player.UseSkill();g.TickRace(.02f);
            if(g.Player.Speed>RaceBalance.BaseSpeed*(1+RaceBalance.MaximumSpeedBonus)+.001f)errors.Add("Speed cap exceeded");
            g.TickRace(1.3f);if(g.Player.LaunchActive)errors.Add("Launch did not expire");cases++;
            // Real uGUI pointer-down path; a later click/release must not repeat it.
            var inputs=UnityEngine.Object.FindObjectsByType<PressRaceAction>();
            foreach(var input in inputs)
            {
                if(input.name!="Nitro")continue;
                g.ResetCountdownValidation();g.TickCountdown(2.8f);
                var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left};
                ExecuteEvents.Execute(input.gameObject,data,ExecuteEvents.pointerDownHandler);
                if(!g.LaunchArmed)errors.Add("Touch-down did not arm start");
                g.TickCountdown(.21f);g.TickRace(.25f);energyBefore=g.Player.Nitro;
                input.GetComponent<Button>().onClick.Invoke();
                if(g.Player.Nitro!=energyBefore)errors.Add("Release fired a second action");cases++;
            }
            var sound=g.Sound;var stats=new StringBuilder();var fingerprints=new HashSet<double>();
            foreach(RaceSound.Cue cue in Enum.GetValues(typeof(RaceSound.Cue)))
            {
                var clip=sound.GetCueClip(cue);var samples=new float[clip.samples*clip.channels];clip.GetData(samples,0);
                float peak=0;double sum=0,signature=0;
                for(int i=0;i<samples.Length;i++){peak=Mathf.Max(peak,Mathf.Abs(samples[i]));sum+=samples[i]*samples[i];signature+=samples[i]*(i%31);}
                if(float.IsNaN(peak)||peak>.721f||peak<.70f||sum<=0)errors.Add("Invalid audio samples: "+cue);
                if(cue>=RaceSound.Cue.Gale&&!fingerprints.Add(signature))errors.Add("Duplicate role sound: "+cue);
                stats.AppendLine(cue+": seconds="+clip.length.ToString("F2")+" peak="+peak.ToString("F3")+" rms="+Math.Sqrt(sum/samples.Length).ToString("F3"));cases++;
            }
            sound.BeginCountdown();float original=sound.MusicBaseVolume;
            sound.PlaySkill(0);sound.TickMix(.10f);
            if(sound.MusicVolume>=original*.40f)errors.Add("BGM failed to duck");
            sound.SetPaused(true);float duck=sound.DuckRemaining,volume=sound.MusicVolume;
            sound.TickMix(2);sound.PlaySkill(1);
            if(sound.DuckRemaining!=duck||sound.MusicVolume!=volume)errors.Add("Paused mix advanced");
            sound.SetPaused(false);sound.TickMix(3);
            if(Mathf.Abs(sound.MusicVolume-original)>.002f)errors.Add("BGM failed to recover");
            if(sound.GetComponentsInChildren<AudioSource>().Length!=4)errors.Add("Audio voice budget changed");
            sound.SetPaused(true);cases++;
            string result=(errors.Count==0?"PASS":string.Join("\n",errors))+"\ncases="+cases+"; six roles; 20/30/60fps; early/late/no input; free one-shot launch; spam; pause; speed cap; uGUI touch-down; audio peak/roles/duck/pause/recovery\n"+stats;
            Directory.CreateDirectory("Temp/Validation");File.WriteAllText("Temp/Validation/launch_audio_regression.txt",result);return result;
        }
    }
}
#endif
