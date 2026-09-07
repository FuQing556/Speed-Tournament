using UnityEngine;

namespace SpeedTournament
{
    public sealed class RaceSound : MonoBehaviour
    {
        public enum Cue { Click,Lane,Pickup,SkillPickup,Boost,Hit,Bomb,Shield,Skill,Count,Go,Finish,
            Launch,PadBoost,Gale,Guard,Pulse,Gravity,Phase,Overload }
        AudioSource music,effects,jet,action;
        AudioClip[] clips;
        RaceAudioConfig config;
        double scheduledAt,pendingDelay;
        bool paused;
        float nextLaneCue,nextPickupCue,nextPadCue,duckRemaining,baseMusicVolume;
        public float MusicVolume=>music!=null?music.volume:0;
        public float MusicBaseVolume=>baseMusicVolume;
        public float DuckRemaining=>duckRemaining;
        public bool IsPaused=>paused;
        public AudioClip GetCueClip(Cue cue)=>clips[(int)cue];

        public static RaceSound Create(Transform parent)
        {
            var go=new GameObject("RaceAudio");go.transform.SetParent(parent,false);var a=go.AddComponent<RaceSound>();
            a.config=Resources.Load<RaceAudioConfig>("RaceAudio");
            a.music=a.Source("Music",64);a.music.loop=true;
            a.effects=a.Source("Details",80);a.jet=a.Source("Jet",16);a.action=a.Source("Ability",8);
            float volume=a.config!=null?a.config.effectsVolume:.78f;
            a.effects.volume=a.jet.volume=a.action.volume=volume;
            a.clips=new AudioClip[System.Enum.GetValues(typeof(Cue)).Length];
            for(int i=0;i<a.clips.Length;i++)a.clips[i]=Synthesize((Cue)i);
            return a;
        }
        AudioSource Source(string name,int priority)
        {
            var go=new GameObject(name);go.transform.SetParent(transform,false);
            var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.priority=priority;return source;
        }
        void ResetMix()
        {
            paused=false;pendingDelay=scheduledAt=0;duckRemaining=0;
            nextLaneCue=nextPickupCue=nextPadCue=0;
            music.Stop();effects.Stop();jet.Stop();action.Stop();
        }
        public void Menu()
        {
            ResetMix();music.clip=config!=null?config.menuMusic:null;
            baseMusicVolume=config!=null?config.menuVolume:.16f;music.volume=baseMusicVolume;
            if(music.clip!=null)music.Play();
        }
        public void BeginCountdown()
        {
            ResetMix();music.clip=config!=null?config.raceMusic:null;
            baseMusicVolume=config!=null?config.raceVolume:.7f;music.volume=baseMusicVolume;
            if(music.clip==null)return;
            scheduledAt=AudioSettings.dspTime+(config!=null?config.raceStartDelay:.5f);music.PlayScheduled(scheduledAt);
        }
        public void RaceStarted(){}
        public void SetPaused(bool value)
        {
            if(value==paused)return;paused=value;
            if(value)
            {
                pendingDelay=System.Math.Max(0,scheduledAt-AudioSettings.dspTime);
                if(pendingDelay>0)music.Stop();else music.Pause();
                effects.Pause();jet.Pause();action.Pause();
            }
            else
            {
                if(pendingDelay>0&&music.clip!=null){scheduledAt=AudioSettings.dspTime+pendingDelay;music.PlayScheduled(scheduledAt);pendingDelay=0;}
                else music.UnPause();
                effects.UnPause();jet.UnPause();action.UnPause();
            }
        }
        void Update()=>TickMix(Time.unscaledDeltaTime);
        public void TickMix(float dt)
        {
            if(paused||dt<=0)return;
            duckRemaining=Mathf.Max(0,duckRemaining-dt);
            float factor=duckRemaining>0?(config!=null?config.criticalMusicFactor:.28f):1f;
            float target=baseMusicVolume*factor;
            float seconds=target<music.volume?.018f:(config!=null?config.musicRecoverySeconds:.28f);
            music.volume=Mathf.Lerp(music.volume,target,1-Mathf.Exp(-dt/Mathf.Max(.01f,seconds)));
            if(scheduledAt>0&&scheduledAt<=AudioSettings.dspTime)scheduledAt=0;
        }
        public void PlaySkill(int role)=>Play(role switch
        {
            0=>Cue.Gale,1=>Cue.Guard,2=>Cue.Pulse,3=>Cue.Gravity,4=>Cue.Phase,5=>Cue.Overload,_=>Cue.Skill
        });
        public void Play(Cue cue)
        {
            if(paused||effects==null)return;
            float now=Time.unscaledTime;
            if(cue==Cue.Lane){if(now<nextLaneCue)return;nextLaneCue=now+.07f;}
            if(cue==Cue.Pickup||cue==Cue.SkillPickup){if(now<nextPickupCue)return;nextPickupCue=now+.045f;}
            if(cue==Cue.PadBoost){if(now<nextPadCue)return;nextPadCue=now+.12f;}
            bool isJet=cue==Cue.Boost||cue==Cue.Launch||cue==Cue.PadBoost;
            bool important=isJet||cue==Cue.Skill||cue==Cue.Count||cue==Cue.Go||cue==Cue.Finish||cue>=Cue.Gale;
            AudioSource source=isJet?jet:important?action:effects;
            float gain=important?.60f:cue==Cue.Lane?.055f:cue==Cue.Pickup?.10f:cue==Cue.SkillPickup?.13f:cue==Cue.Click?.085f:.20f;
            // Three bounded SFX voices. Pickups/lanes cannot steal a skill or accumulate
            // dozens of OneShot tails when several resources are crossed in one frame.
            source.Stop();source.clip=clips[(int)cue];
            source.volume=(config!=null?config.effectsVolume:.78f)*gain;source.Play();
            if(important)duckRemaining=Mathf.Max(duckRemaining,source.clip.length+.10f);
        }
        static AudioClip Synthesize(Cue cue)
        {
            const int hz=22050;
            float duration=cue switch
            {
                Cue.Lane=>.055f,Cue.Hit=>.16f,Cue.Bomb=>.32f,Cue.Boost=>.70f,Cue.Launch=>.90f,
                Cue.PadBoost=>.30f,Cue.Gale=>.65f,Cue.Guard=>.62f,Cue.Pulse=>.55f,Cue.Gravity=>.72f,
                Cue.Phase=>.58f,Cue.Overload=>.65f,Cue.Skill=>.48f,Cue.Go=>.32f,Cue.Finish=>.8f,_=>.14f
            };
            var samples=new float[Mathf.CeilToInt(duration*hz)];
            uint rng=0x12345678;float phase=0,bodyPhase=0,fastNoise=0,slowNoise=0,peak=0;
            for(int i=0;i<samples.Length;i++)
            {
                float t=i/(float)hz,u=t/duration;
                float frequency=cue switch
                {
                    Cue.Lane=>Mathf.Lerp(300,750,u),Cue.Pickup=>Mathf.Lerp(900,1450,u),Cue.SkillPickup=>Mathf.Lerp(1200,1900,u),
                    Cue.Count=>700,Cue.Go=>1400,Cue.Shield=>Mathf.Lerp(520,1100,u),
                    Cue.Boost or Cue.PadBoost or Cue.Gale=>Mathf.Lerp(420,1750,Mathf.Sqrt(u)),
                    Cue.Launch=>Mathf.Lerp(300,2200,Mathf.Sqrt(u)),
                    Cue.Guard=>Mathf.Lerp(950,460,u),Cue.Pulse=>Mathf.Lerp(1400,280,Mathf.Repeat(u*3,1)),
                    Cue.Gravity=>620+200*Mathf.Sin(t*22),Cue.Phase=>Mathf.Lerp(1900,420,u),
                    Cue.Overload=>u<.25f?490:u<.5f?740:u<.75f?980:1470,
                    Cue.Skill=>Mathf.Lerp(400,1400,u),Cue.Finish=>u<.33f?523:u<.66f?659:784,_=>Mathf.Lerp(210,90,u)
                };
                phase+=2*Mathf.PI*frequency/hz;bodyPhase+=2*Mathf.PI*Mathf.Lerp(180,70,u)/hz;
                rng^=rng<<13;rng^=rng>>17;rng^=rng<<5;float noise=(rng&65535)/32768f-1;
                fastNoise+=.68f*(noise-fastNoise);slowNoise+=.12f*(noise-slowNoise);
                float band=fastNoise-slowNoise; // audible air texture on small speakers, not just bass
                float tone=Mathf.Sin(phase)*.65f+Mathf.Sin(phase*2)*.16f;
                bool thrust=cue==Cue.Boost||cue==Cue.Launch||cue==Cue.PadBoost||cue==Cue.Gale;
                if(thrust)tone=.23f*tone+.18f*Mathf.Sin(bodyPhase)+.85f*band*(.5f+.5f*Mathf.Sin(Mathf.PI*u));
                if(cue==Cue.Guard)tone=.45f*Mathf.Sin(phase)+.25f*Mathf.Sin(phase*1.414f)+.15f*Mathf.Sin(phase*2.76f);
                if(cue==Cue.Pulse)tone=(tone*.5f+band*.5f)*(.30f+.70f*Mathf.Pow(1-Mathf.Repeat(u*3,1),.45f));
                if(cue==Cue.Gravity)tone*=.65f+.35f*Mathf.Sin(t*34);
                if(cue==Cue.Phase)tone=.38f*tone+.60f*band;
                if(cue==Cue.Overload)tone=.60f*tone+.18f*Mathf.Sin(phase*3);
                if(cue==Cue.Hit||cue==Cue.Bomb)tone=tone*.35f+band*.75f;
                float envelope=Mathf.Min(1,t/.012f)*Mathf.Pow(1-u,thrust?.85f:1.25f);
                samples[i]=tone*envelope;peak=Mathf.Max(peak,Mathf.Abs(samples[i]));
            }
            // Equal peak headroom per cue; volume is set by category once, not three
            // unrelated attenuators. No synthesis or buffer allocations on button presses.
            float normalize=peak>0?.72f/peak:0;
            for(int i=0;i<samples.Length;i++)samples[i]*=normalize;
            var clip=AudioClip.Create("Synth_"+cue,samples.Length,1,hz,false);clip.SetData(samples,0);return clip;
        }
        void OnDestroy(){if(clips!=null)foreach(var c in clips)if(c!=null)Destroy(c);}
    }
}
