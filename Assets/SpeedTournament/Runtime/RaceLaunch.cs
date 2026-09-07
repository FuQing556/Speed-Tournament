using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpeedTournament
{
    public sealed partial class SpeedTournamentPrototype
    {
        bool launchSequence,launchArmed,launchTriggered;
        public bool LaunchArmed=>launchArmed;
        public bool LaunchTriggered=>launchTriggered;
        public bool LaunchWindowOpen=>launchSequence&&!finished&&!choosingCharacter&&
            (racing?raceTime<=RaceBalance.LaunchLateWindow:countdown<=RaceBalance.LaunchEarlyWindow);
        void ResetLaunch(bool enabled){launchSequence=enabled;launchArmed=launchTriggered=false;}

        // Returns true while this press belongs to the start sequence, including early/duplicate
        // presses. Those must never spend the ordinary nitro battery or bank multiple launches.
        public bool HandleStartNitro()
        {
            if(!launchSequence)return false;
            if(finished||choosingCharacter||Time.timeScale<=0)return true;
            if(!racing)
            {
                if(LaunchWindowOpen)launchArmed=true;
                return true;
            }
            if(raceTime>RaceBalance.LaunchLateWindow)return false;
            TriggerLaunch();return true;
        }
        void TriggerLaunch()
        {
            if(launchTriggered||!racing)return;
            launchTriggered=true;launchArmed=false;player.ApplyLaunchBoost();
            Sound?.Play(RaceSound.Cue.Launch); // Dedicated start HUD owns the success message.
        }
        public void TickCountdown(float dt)
        {
            if(racing||finished||choosingCharacter||Time.timeScale<=0||dt<=0)return;
            countdown=Mathf.Max(0,countdown-dt);
            int stage=Mathf.CeilToInt(countdown);
            if(stage!=lastCountdown){lastCountdown=stage;Sound?.Play(stage==0?RaceSound.Cue.Go:RaceSound.Cue.Count);}
            if(countdown<=0)
            {
                racing=true;Sound?.RaceStarted();
                if(launchArmed)TriggerLaunch();
            }
        }
#if UNITY_EDITOR
        public void ResetCountdownValidation(int role=0)
        {
            ResetValidation(role);racing=false;countdown=3;lastCountdown=3;ResetLaunch(true);
            player.SimulationAutopilot=false;
        }
#endif
        static Transform CreateJetExhaust(Transform parent)
        {
            // Two shared 16-triangle cones per racer; no particles, lights or runtime spawning.
            var mesh=new Mesh{name="JetCone"};var v=new Vector3[10];var indices=new int[48];
            v[0]=Vector3.back;v[9]=Vector3.zero;
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI*.25f;v[i+1]=new Vector3(Mathf.Cos(a),Mathf.Sin(a),0);
                int next=(i+1)%8+1,k=i*6;indices[k]=0;indices[k+1]=next;indices[k+2]=i+1;
                indices[k+3]=9;indices[k+4]=i+1;indices[k+5]=next;
            }
            mesh.vertices=v;mesh.triangles=indices;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.UploadMeshData(true);
            var root=new GameObject("JetExhaust").transform;root.SetParent(parent,false);root.localPosition=new Vector3(0,0,-.75f);
            root.gameObject.AddComponent<GeneratedMeshOwner>().OwnedMesh=mesh;
            for(int i=0;i<2;i++)
            {
                var go=new GameObject(i==0?"CyanFlame":"WhiteCore",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
                go.transform.localScale=i==0?Vector3.one:new Vector3(.30f,.30f,1.12f);
                go.transform.localPosition=i==0?Vector3.zero:new Vector3(0,0,-.015f);
                go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<MeshRenderer>();
                renderer.sharedMaterial=RaceVisuals.Material(i==0?"JetCyan":"JetWhite",i==0?RaceVisuals.Cyan:Color.white);
                renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
            }
            root.gameObject.SetActive(false);return root;
        }
        public sealed partial class RaceHud
        {
            Text launchHint,launchButtonHint;
            void BuildLaunchHint(Transform parent)
            {
                launchHint=Label("LaunchHint",parent,"",30,TextAnchor.MiddleCenter,RaceVisuals.Cyan);
                SetAnchors(launchHint.rectTransform,new Vector2(.27f,.33f),new Vector2(.73f,.41f));
                launchButtonHint=Label("LaunchButtonHint",parent,"起步",23,TextAnchor.MiddleCenter,RaceVisuals.Cyan);
                SetAnchors(launchButtonHint.rectTransform,new Vector2(.86f,.262f),new Vector2(.975f,.31f));
                launchHint.raycastTarget=launchButtonHint.raycastTarget=false;
            }
            void RefreshLaunchHint()
            {
                bool counting=!game.IsChoosingCharacter&&!game.IsRacing&&!game.IsFinished;
                bool go=game.launchSequence&&game.IsRacing&&!game.IsFinished&&game.RaceTime<.55f;
                countdownText.gameObject.SetActive(counting||go);
                countdownText.text=go?"GO!":game.Countdown>2?"3":game.Countdown>1?"2":"1";
                countdownText.color=game.LaunchWindowOpen||go?RaceVisuals.Cyan:Color.white;
                bool visible=counting||game.LaunchWindowOpen||(game.LaunchTriggered&&game.Player.LaunchActive&&!game.IsFinished);
                launchHint.gameObject.SetActive(visible);
                if(visible)launchHint.text=game.LaunchTriggered?"起步喷射！":game.LaunchArmed?"已就绪 · 等待 GO":game.LaunchWindowOpen?"现在点氮气！":"GO 亮起时点氮气 · 免费起步喷射";
                launchButtonHint.gameObject.SetActive(counting||game.LaunchWindowOpen);
                launchButtonHint.text=game.LaunchTriggered?"喷射中":game.LaunchArmed?"已就绪":game.LaunchWindowOpen?"点击起步":"起步";
            }
        }
    }
    // Fire on touch-down, not after lifting the finger. onClick is intentionally unbound;
    // pointer release cannot fire a second time. Keyboard/UI Submit remains supported.
    public sealed class PressRaceAction:MonoBehaviour,IPointerDownHandler,ISubmitHandler
    {
        SpeedTournamentPrototype game;bool nitro;
        public void Setup(SpeedTournamentPrototype owner,bool isNitro){game=owner;nitro=isNitro;}
        void Fire()
        {
            var button=GetComponent<Button>();if(game==null||!button.IsInteractable())return;
            if(nitro)game.Player.UseNitro();else game.Player.UseSkill();
        }
        public void OnPointerDown(PointerEventData data){if(data.button==PointerEventData.InputButton.Left)Fire();}
        public void OnSubmit(BaseEventData data)=>Fire();
    }
}
