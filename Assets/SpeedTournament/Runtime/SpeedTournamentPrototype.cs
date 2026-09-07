using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SpeedTournament
{
    [DisallowMultipleComponent]
    public sealed partial class SpeedTournamentPrototype : MonoBehaviour
    {
        private const int LapsToFinish = 3, RacerCount = 6;
        string RecordKey=>CircuitCatalog.Maps[selectedMapId].RecordKey;
        private readonly List<RacerAgent> racers=new();
        private readonly List<PickupOrb> pickups=new();
        private readonly List<TrackObstacle> obstacles=new();
        private TrackModel track;
        private RacerAgent player;
        private RaceHud hud;
        private FollowCamera followCamera;
        private Transform runtimeRoot,courseRoot;
        private float countdown=3f, raceTime, bestTime, visibilityAt, worldClock;
        private bool racing, finished, practiceMode, choosingCharacter=true;
        private int selectedCharacterId, lastCountdown=4;
        private int selectedMapId;
        public int SelectedMapId=>selectedMapId;
        public RaceSound Sound{get;private set;}
        public RacerAgent Player=>player;
        public IReadOnlyList<RacerAgent> Racers=>racers;
        public TrackModel Track=>track;
        public float RaceTime=>raceTime;
        public float WorldClock=>worldClock;
        public float BestTime=>bestTime;
        public int Laps=>LapsToFinish;
        public bool IsRacing=>racing;
        public bool IsFinished=>finished;
        public bool IsPracticeMode=>practiceMode;
        public bool IsChoosingCharacter=>choosingCharacter;
        public int SelectedCharacterId=>selectedCharacterId;
        public float Countdown=>countdown;
        public double LastSimulationMilliseconds{get;private set;}
        public bool ManualSimulation{get;set;}
        private bool suppressRecords;
        public static readonly string[] CharacterNames={"疾风","重盾","脉冲","引力","幻影","蓄能"};
        public static readonly string[] CharacterDescriptions=RaceBalance.Descriptions;

        private void Awake()
        {
            Application.targetFrameRate=60;Application.runInBackground=true;
            Screen.orientation=ScreenOrientation.LandscapeLeft;QualitySettings.vSyncCount=0;Screen.sleepTimeout=SleepTimeout.NeverSleep;
            BuildPrototype();
        }
        private void OnDisable(){Time.timeScale=1f;}
        private void Update()
        {
            if(ManualSimulation)return;
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame&&!hud.TryCloseGuide()&&!choosingCharacter)TogglePause();
            if(finished||Time.timeScale<=0||choosingCharacter){hud.Refresh();return;}
            // Edge-triggered actions are polled once per rendered frame, never once per
            // simulation substep. This also allows Space during the countdown.
            if(!player.SimulationAutopilot&&Keyboard.current!=null)
            {
                if(Keyboard.current.spaceKey.wasPressedThisFrame)player.UseNitro();
                if(racing&&Keyboard.current.leftCtrlKey.wasPressedThisFrame)player.UseSkill();
            }
            if(!racing)
            {
                TickCountdown(Time.unscaledDeltaTime);
                hud.Refresh();return;
            }
            TickRace(Mathf.Min(Time.deltaTime,0.10f));
            hud.Refresh();
        }
        // Public stepping supports reproducible offline validation through the exact gameplay code.
        public void TickRace(float dt)
        {
            if(!racing||finished)return;
            long start=System.Diagnostics.Stopwatch.GetTimestamp();
            int steps=Mathf.Max(1,Mathf.CeilToInt(dt/(1f/60f)));float sub=dt/steps;
            for(int step=0;step<steps;step++)
            {
                worldClock+=sub;if(!practiceMode)raceTime+=sub;
                for(int i=0;i<racers.Count;i++)racers[i].RaceTick(sub);
                UpdatePickups();UpdateObstacles();UpdateRanking();
                if(!practiceMode&&player.TotalProgress>=LapsToFinish&&player.CurrentRoute==0&&!player.OnBranch){FinishRace();break;}
            }
            if(worldClock>=visibilityAt){visibilityAt=worldClock+0.15f;RefreshVisibility();}
            LastSimulationMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;
        }
        private void BuildPrototype()
        {
            Time.timeScale=1;selectedMapId=Mathf.Clamp(PlayerPrefs.GetInt("SpeedTournament.SelectedMap",0),0,1);
            selectedCharacterId=Mathf.Clamp(PlayerPrefs.GetInt("SpeedTournament.SelectedCharacter",0),0,5);
            var old=GameObject.Find("__SpeedTournamentRuntime");if(old!=null){old.SetActive(false);Destroy(old);}
            runtimeRoot=new GameObject("__SpeedTournamentRuntime").transform;
            runtimeRoot.gameObject.AddComponent<PerformanceProbe>();
            ConfigureLighting();BuildCourse();BuildCamera();
            Sound=RaceSound.Create(runtimeRoot);Sound.Menu();
            hud=RaceHud.Create(this,runtimeRoot);RefreshVisibility();
        }
        private void BuildCourse()
        {
            if(courseRoot!=null){courseRoot.gameObject.SetActive(false);Destroy(courseRoot.gameObject);}
            racers.Clear();pickups.Clear();obstacles.Clear();
            courseRoot=new GameObject("Course").transform;courseRoot.SetParent(runtimeRoot,false);
            track=new TrackModel(courseRoot,selectedMapId);track.BuildWorld();
            BuildRacers();BuildPickups();BuildObstacles();bestTime=PlayerPrefs.GetFloat(RecordKey,0);
            if(followCamera!=null){followCamera.Target=player.transform;followCamera.Snap();}
        }
        public void SelectMap(int id)
        {
            if(!choosingCharacter)return;id=Mathf.Clamp(id,0,CircuitCatalog.Maps.Length-1);
            if(id!=selectedMapId){selectedMapId=id;BuildCourse();RefreshVisibility();hud.InvalidateMap();}
            if(!ManualSimulation)PlayerPrefs.SetInt("SpeedTournament.SelectedMap",selectedMapId);
            hud.RefreshMapChoice();Sound?.Play(RaceSound.Cue.Click);
        }
        private void ConfigureLighting()
        {
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(0.45f,0.50f,0.62f);
            RenderSettings.fog=false;
            var sun=FindAnyObjectByType<Light>();if(sun!=null)sun.shadows=LightShadows.None;
        }
        private void BuildRacers()
        {
            var random=new System.Random(Environment.TickCount);
            int[] roles={selectedCharacterId,-1,-1,-1,-1,-1},slots={1,2,3,4,5};
            for(int i=4;i>0;i--){int j=random.Next(i+1);(slots[i],slots[j])=(slots[j],slots[i]);}
            int count=random.Next(0,6);for(int i=0;i<count;i++)roles[slots[i]]=random.Next(0,6);
            for(int i=0;i<6;i++)
            {
                int lane=i==0?2:i<=2?i-1:i;
                var r=RacerAgent.Create(this,courseRoot,i==0,i,roles[i],Color.white,0,TrackModel.LaneOffset(lane));
                racers.Add(r);if(i==0)player=r;
            }
        }
        private void BuildCamera()
        {
            Camera cam=Camera.main;if(cam==null){cam=new GameObject("Main Camera").AddComponent<Camera>();cam.tag="MainCamera";}
            if(cam.GetComponent<AudioListener>()==null)cam.gameObject.AddComponent<AudioListener>();
            cam.transform.SetParent(runtimeRoot,true);cam.clearFlags=CameraClearFlags.SolidColor;
            // Keep complete horizon silhouettes. Road/item distance culling remains unchanged;
            // approach arrows and scenery are spatially batched to bound the extra draw cost.
            cam.backgroundColor=new Color(0.032f,0.055f,0.105f);cam.fieldOfView=62;cam.nearClipPlane=0.25f;cam.farClipPlane=520;cam.allowHDR=false;
            var data=cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if(data!=null){data.renderPostProcessing=false;data.renderShadows=false;data.requiresColorTexture=false;data.requiresDepthTexture=false;}
            followCamera=cam.GetComponent<FollowCamera>()??cam.gameObject.AddComponent<FollowCamera>();followCamera.Target=player.transform;followCamera.Snap();
        }
        private void BuildPickups()
        {
            if(track.Definition.Elevated){BuildSkyPickups();return;}
            // Supplies precede decisions and follow hazards; never occupy a hazard's approach.
            float[] mainRows={0.027f,0.060f,0.087f,0.165f,0.205f,0.240f,0.335f,0.375f,0.405f,0.490f,0.535f,0.650f,0.690f,0.721f,0.800f,0.842f,0.945f,0.975f};
            for(int row=0;row<mainRows.Length;row++)
                for(int col=0;col<3;col++){int lane=(row%2)+col*2;pickups.Add(PickupOrb.Create(courseRoot,track,0,mainRows[row],TrackModel.LaneOffset(lane),(row+col)%3==0));}
            for(int s=0;s<track.SectionCount;s++)
            {
                float a=track.SectionStart(s),b=track.ReturnStations[s];
                for(int row=0;row<4;row++)for(int col=0;col<3;col++)
                {int lane=(row%2)+col*2;float p=Mathf.Lerp(a,b,0.10f+row*0.24f);pickups.Add(PickupOrb.Create(courseRoot,track,1,p,TrackModel.LaneOffset(lane),(row+col)%3==0));}
            }
        }
        private void AddTriple(int route,float p,int first){for(int lane=first;lane<first+3;lane++)obstacles.Add(TrackObstacle.Create(courseRoot,track,route,p,lane,ObstacleKind.Billboard,6));}
        private void AddBoostPair(int route,float p,int first){for(int lane=first;lane<first+2;lane++)obstacles.Add(TrackObstacle.Create(courseRoot,track,route,p,lane,ObstacleKind.BoostPad,6));}
        private void BuildObstacles()
        {
            if(track.Definition.Elevated){BuildSkyObstacles();return;}
            // Main line: low-risk sweeping pairs; shortcut: three readable alternating rows.
            float[] boosts={0.040f,0.180f,0.223f,0.355f,0.505f,0.555f,0.670f,0.815f,0.860f,0.960f};
            int[] lanes={2,3,4,1,3,4,1,3,4,2};
            for(int i=0;i<boosts.Length;i++)AddBoostPair(0,boosts[i],lanes[i]);
            AddTriple(0,0.192f,0);AddTriple(0,0.525f,0);AddTriple(0,0.830f,0);
            float[] bombRows={.125f,.285f,.440f,.610f,.760f,.900f};
            for(int i=0;i<bombRows.Length;i++)obstacles.Add(TrackObstacle.Create(courseRoot,track,0,bombRows[i],2+i%2,ObstacleKind.Mascot,6));
            for(int s=0;s<track.SectionCount;s++)
            {
                float a=track.SectionStart(s),b=track.ReturnStations[s];
                AddTriple(1,Mathf.Lerp(a,b,0.25f),0);
                AddTriple(1,Mathf.Lerp(a,b,0.52f),3);
                AddTriple(1,Mathf.Lerp(a,b,0.78f),0);
                obstacles.Add(TrackObstacle.Create(courseRoot,track,1,Mathf.Lerp(a,b,0.39f),5,ObstacleKind.Mascot,6));
                obstacles.Add(TrackObstacle.Create(courseRoot,track,1,Mathf.Lerp(a,b,0.66f),0,ObstacleKind.MovingGate,6));
                AddBoostPair(1,Mathf.Lerp(a,b,0.89f),3);
            }
        }
        public void PulseFrom(RacerAgent source)
        {
            int count=0;
            for(int i=0;i<racers.Count;i++)
            {
                var r=racers[i];if(r==source||r.CurrentRoute!=source.CurrentRoute||r.OnBranch!=source.OnBranch)continue;
                if(source.OnBranch&&r.ActiveBranch!=source.ActiveBranch)continue;
                float delta=r.TotalProgress-source.TotalProgress;
                if(delta>0&&delta*track.ApproxLength<RaceBalance.PulseRange&&Mathf.Abs(r.Lateral-source.Lateral)<=TrackModel.LaneSpacing*1.25f){r.ApplySlow(1.2f);count++;}
            }
            for(int i=0;i<obstacles.Count;i++)
            {
                var o=obstacles[i];if(!o.OnPath(source)||!o.Available||o.Kind==ObstacleKind.BoostPad)continue;
                float ahead=o.Ahead(source,track);
                if(ahead>=0&&ahead<=RaceBalance.PulseRange&&Mathf.Abs(o.Lateral-source.Lateral)<=TrackModel.LaneSpacing*1.25f){o.Disable();count++;}
            }
            source.AddSkillEnergy(Mathf.Min(count,3)*.08f);
            if(source.IsPlayer)ShowFeedback("脉冲清场 · 命中 "+count,RaceVisuals.Danger);
        }
        private void UpdatePickups()
        {
            for(int i=0;i<pickups.Count;i++)
            {
                var p=pickups[i];p.Animate(worldClock);
                for(int j=0;j<racers.Count;j++)if((p.transform.position-racers[j].transform.position).sqrMagnitude<400f)p.TryCollect(racers[j],track);
            }
        }
        private void UpdateObstacles()
        {
            for(int i=0;i<obstacles.Count;i++)
            {
                var o=obstacles[i];o.Animate(worldClock);
                for(int j=0;j<racers.Count;j++)if((o.transform.position-racers[j].transform.position).sqrMagnitude<400f)o.TryTrigger(racers[j],track);
            }
        }
        private void RefreshVisibility()
        {
            Vector3 p=player.transform.position;
            for(int i=0;i<pickups.Count;i++)pickups[i].SetVisible((pickups[i].transform.position-p).sqrMagnitude<150f*150f,pickups[i].Collected(player));
            for(int i=0;i<obstacles.Count;i++)obstacles[i].SetVisible((obstacles[i].transform.position-p).sqrMagnitude<170f*170f);
            track.UpdateVisibility(p);
        }
        private void UpdateRanking()
        {
            // The racer list remains stable so personal pickup/collision bookkeeping cannot swap.
            for(int i=0;i<racers.Count;i++){int rank=1;for(int j=0;j<racers.Count;j++)if(racers[j].TotalProgress>racers[i].TotalProgress+0.00001f||(Mathf.Abs(racers[j].TotalProgress-racers[i].TotalProgress)<0.00001f&&j<i))rank++;racers[i].Rank=rank;}
        }
        public bool DangerAhead(RacerAgent r,float meters)
        {
            for(int i=0;i<obstacles.Count;i++){var o=obstacles[i];float ahead=o.Ahead(r,track);if(ahead>=0&&ahead<meters&&o.Available&&o.Kind!=ObstacleKind.BoostPad&&Mathf.Abs(o.Lateral-r.Lateral)<1.6f)return true;}
            return false;
        }
        public int ChooseAiLane(RacerAgent r)
        {
            int best=r.LaneIndex;float bestScore=float.NegativeInfinity;
            for(int lane=0;lane<6;lane++)
            {
                if(r.OnBranch&&!track.BranchAcceptsLane(r.ActiveBranch,lane))continue;
                float score=-Mathf.Abs(lane-r.LaneIndex)*0.65f;float lateral=TrackModel.LaneOffset(lane);
                for(int i=0;i<obstacles.Count;i++)
                {
                    var o=obstacles[i];if(!o.Available||Mathf.Abs(o.Lateral-lateral)>1.4f)continue;
                    float ahead=o.Ahead(r,track);if(ahead<0||ahead>85f)continue;
                    if(o.Kind==ObstacleKind.BoostPad)score+=2.1f*(1-ahead/100f);
                    else if(!r.ShieldActive&&!r.PhaseActive)score-=12f*(1-ahead/100f);
                }
                for(int i=0;i<pickups.Count;i++){var p=pickups[i];float ahead=p.Ahead(r,track);if(p.LaneIndex==lane&&!p.Collected(r)&&ahead>=0&&ahead<55f)score+=0.9f;}
                if(!r.OnBranch&&r.CurrentRoute==0&&(r.CharacterId==1||r.CharacterId==4||(!r.IsPlayer&&r.RacerId%3==0)))
                    for(int i=0;i<track.SectionCount;i++){float ahead=track.AheadMeters(0,r.TotalProgress,track.EntryStations[i]);int first=track.Definition.EntryLanes[i],last=first+track.Definition.Widths[i]-1;if(ahead<85&&ahead>3)score-=Mathf.Abs(lane-Mathf.Clamp(lane,first,last))*.85f;}
                if(score>bestScore){bestScore=score;best=lane;}
            }
            return best;
        }
        private void FinishRace()
        {
            finished=true;racing=false;
            if(!ManualSimulation&&!suppressRecords&&(bestTime<=0||raceTime<bestTime)){bestTime=raceTime;PlayerPrefs.SetFloat(RecordKey,bestTime);PlayerPrefs.Save();}
            Sound?.Play(RaceSound.Cue.Finish);hud.ShowFinish();
        }
        public void TogglePause(){if(finished||choosingCharacter)return;bool pause=Time.timeScale>0;Time.timeScale=pause?0:1;Sound?.SetPaused(pause);hud.SetPauseVisible(pause);}
        public void Resume(){Time.timeScale=1;Sound?.SetPaused(false);hud.SetPauseVisible(false);}
        public void RestartRace(){Time.timeScale=1;UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);}
        public void ContinuePractice(){finished=false;racing=true;practiceMode=true;Time.timeScale=1;Sound?.SetPaused(false);hud.HideFinish();}
        public void RequestLaneChange(int direction)=>player?.RequestLaneChange(direction);
        public void SelectCharacter(int id)
        {
            if(!choosingCharacter)return;selectedCharacterId=Mathf.Clamp(id,0,5);
            if(!ManualSimulation)PlayerPrefs.SetInt("SpeedTournament.SelectedCharacter",selectedCharacterId);
            player.ConfigureCharacter(selectedCharacterId);hud.RefreshCharacterChoice();Sound?.Play(RaceSound.Cue.Click);
        }
        public void ConfirmCharacter()
        {
            if(!choosingCharacter)return;hud.ShowMapChoice();Sound?.Play(RaceSound.Cue.Click);
        }
        public void ConfirmMap()
        {
            if(!choosingCharacter)return;choosingCharacter=false;countdown=3f;lastCountdown=3;ResetLaunch(true);
            if(!ManualSimulation)PlayerPrefs.Save();hud.HideCharacterChoice();hud.HideMapChoice();Sound?.BeginCountdown();Sound?.Play(RaceSound.Cue.Count);
        }
        public void ShowFeedback(string message,Color color){if(hud!=null&&!ManualSimulation)hud.ShowFeedback(message,color);}
        public void StartSimulation(int role)
        {
            ManualSimulation=true;SelectCharacter(role);ConfirmMap();countdown=0;racing=true;ResetLaunch(false);player.SimulationAutopilot=true;
        }
#if UNITY_EDITOR
        public void ResetValidation(int role,bool live=false)
        {
            ManualSimulation=!live;suppressRecords=true;Time.timeScale=1;finished=false;racing=true;choosingCharacter=false;practiceMode=false;countdown=0;raceTime=worldClock=visibilityAt=0;
            ResetLaunch(false);
            selectedCharacterId=role;
            for(int i=0;i<racers.Count;i++)racers[i].ResetValidation(i==0?role:(i-1)%6);
            player.SimulationAutopilot=true;
            foreach(var p in pickups)p.Reactivate();foreach(var o in obstacles)o.ResetValidation();
            hud.HideFinish();hud.HideCharacterChoice();hud.HideMapChoice();hud.SetPauseVisible(false);hud.RefreshCharacterChoice();
            Sound.SetPaused(true);followCamera.Snap();RefreshVisibility();
        }
        public IReadOnlyList<TrackObstacle> ValidationObstacles=>obstacles;
        public IReadOnlyList<PickupOrb> ValidationPickups=>pickups;
        public void SetValidationMap(int id){ManualSimulation=true;choosingCharacter=true;SelectMap(id);}
#endif

        public static class UiArt
        {
            private static Sprite panelFrame;
            private static Sprite pillButton;
            private static Sprite[] controlSprites;
            private static Sprite[] skillSprites;
            private static Sprite[] energySprites;
            private static Sprite[] vfxSprites;
            private static Sprite phaseVfx;
            private static Material additiveMaterial;

            [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
            private static void ResetRuntimeCaches()
            {
                panelFrame = null;
                pillButton = null;
                controlSprites = null;
                skillSprites = null;
                energySprites = null;
                vfxSprites = null;
                phaseVfx = null;
                additiveMaterial = null;
            }

            public static Sprite PanelFrame => GetSingle(ref panelFrame, "UI/v2/panel_frame_v2", new Rect(0.025f, 0.06f, 0.95f, 0.88f), new Vector4(170f, 145f, 170f, 145f));
            public static Sprite PillButton => GetSingle(ref pillButton, "UI/v2/clean_button_v2", new Rect(0.015f, 0.25f, 0.97f, 0.52f), new Vector4(105f, 70f, 105f, 70f));
            public static Sprite Control(int index) => GetAtlas(ref controlSprites, "UI/v2/control_buttons_v2", 3, 2)[Mathf.Clamp(index, 0, 5)];
            public static Sprite Skill(int index) => GetAtlas(ref skillSprites, "UI/v2/skill_icons_v2", 3, 2)[Mathf.Clamp(index, 0, 5)];
            public static Sprite Energy(int index) => GetEnergySprites()[Mathf.Clamp(index, 0, 1)];
            public static Sprite Vfx(int index)
            {
                index = Mathf.Clamp(index, 0, 5);
                if (index == 4)
                    return GetSingle(ref phaseVfx, "UI/v2/phase_ball_vfx_v2", new Rect(0f, 0f, 1f, 1f), Vector4.zero);
                return GetAtlas(ref vfxSprites, "UI/v2/skill_vfx_v2", 3, 2)[index];
            }

            public static Material AdditiveMaterial
            {
                get
                {
                    if (additiveMaterial == null)
                    {
                        Shader shader = Shader.Find("SpeedTournament/AdditiveSprite") ?? Shader.Find("Sprites/Default");
                        additiveMaterial = new Material(shader) { name = "SkillVfx_Additive" };
                    }
                    return additiveMaterial;
                }
            }

            private static Sprite[] GetAtlas(ref Sprite[] cache, string resourcePath, int columns, int rows)
            {
                int spriteCount = columns * rows;
                if (cache != null && cache.Length == spriteCount && cache[0] != null) return cache;
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                cache = new Sprite[spriteCount];
                if (texture == null) return cache;
                float cellWidth = texture.width / (float)columns;
                float cellHeight = texture.height / (float)rows;
                for (int row = 0; row < rows; row++)
                {
                    for (int column = 0; column < columns; column++)
                    {
                        int index = row * columns + column;
                        float y = texture.height - (row + 1) * cellHeight;
                        cache[index] = Sprite.Create(texture, new Rect(column * cellWidth, y, cellWidth, cellHeight), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                        cache[index].name = $"{texture.name}_{index:00}";
                    }
                }
                return cache;
            }

            private static Sprite GetSingle(ref Sprite cache, string resourcePath, Rect normalizedRect, Vector4 border)
            {
                if (cache != null) return cache;
                Texture2D texture = Resources.Load<Texture2D>(resourcePath);
                if (texture == null) return null;
                Rect rect = new Rect(
                    normalizedRect.x * texture.width,
                    normalizedRect.y * texture.height,
                    normalizedRect.width * texture.width,
                    normalizedRect.height * texture.height);
                cache = Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                cache.name = texture.name;
                return cache;
            }

            private static Sprite[] GetEnergySprites()
            {
                if (energySprites != null && energySprites.Length == 2 && energySprites[0] != null) return energySprites;
                Texture2D texture = Resources.Load<Texture2D>("UI/v2/energy_meter_v2");
                energySprites = new Sprite[2];
                if (texture == null) return energySprites;
                Rect frameRect = new Rect(texture.width * 0.04f, texture.height * 0.55f, texture.width * 0.92f, texture.height * 0.34f);
                Rect fillRect = new Rect(texture.width * 0.04f, texture.height * 0.105f, texture.width * 0.92f, texture.height * 0.34f);
                energySprites[0] = Sprite.Create(texture, frameRect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                energySprites[1] = Sprite.Create(texture, fillRect, new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                energySprites[0].name = texture.name + "_Frame";
                energySprites[1].name = texture.name + "_Fill";
                return energySprites;
            }
        }


        public sealed class PerformanceProbe : MonoBehaviour
        {
            private float sampleStartedAt;
            private int sampledFrames;
            private float worstFrameMs;

            public float AverageFps { get; private set; }
            public float WorstFrameMs { get; private set; }

            private void OnEnable()
            {
                sampleStartedAt = Time.unscaledTime;
                sampledFrames = 0;
                worstFrameMs = 0f;
            }

            private void Update()
            {
                sampledFrames++;
                worstFrameMs = Mathf.Max(worstFrameMs, Time.unscaledDeltaTime * 1000f);
                float elapsed = Time.unscaledTime - sampleStartedAt;
                if (elapsed < 2f) return;
                AverageFps = sampledFrames / elapsed;
                WorstFrameMs = worstFrameMs;
                sampleStartedAt = Time.unscaledTime;
                sampledFrames = 0;
                worstFrameMs = 0f;
            }
        }

        public sealed partial class RaceHud : MonoBehaviour
        {
            private SpeedTournamentPrototype game;
            private Text rankText, lapText, timerText, bestText, countdownText, finishText, nitroValueText, skillValueText, feedbackText, skillButtonGlyph;
            private Image nitroFill, skillFill, nitroButtonImage, skillButtonImage;
            private GameObject pausePanel, finishPanel, characterPanel;
            private Text selectedCharacterText, characterDescriptionText;
            private readonly List<Button> characterButtons = new();
            private readonly List<Image> characterIcons = new();
            private MiniMapGraphic minimap;
            private Font font;
            private Sprite circleSprite;
            private float nextRefreshTime;
            private float feedbackUntil;
            private int lastNitroPercent = -1;
            private int lastSkillPercent = -1;
            private string lastSkillState;

            public static RaceHud Create(SpeedTournamentPrototype game, Transform parent)
            {
                var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(RaceHud));
                go.transform.SetParent(parent, false);
                var hud = go.GetComponent<RaceHud>();
                hud.game = game;
                hud.Build();
                return hud;
            }

            private void Build()
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                circleSprite = CreateCircleSprite(128);
                Canvas canvas = GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                var scaler = GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                EnsureEventSystem();

                RectTransform safe = Panel("SafeArea", transform, Color.clear, Vector2.zero, Vector2.one).rectTransform;
                safe.gameObject.AddComponent<SafeAreaFitter>();

                var rankGroup = Panel("RankGroup", safe, Color.white, new Vector2(0.025f, 0.825f), new Vector2(0.205f, 0.965f));
                UsePillArt(rankGroup);
                rankText = Label("Rank", rankGroup.transform, "1", 72, TextAnchor.MiddleCenter, new Color(1f, 0.48f, 0.20f));
                rankText.rectTransform.anchorMin = new Vector2(0.08f, 0.05f); rankText.rectTransform.anchorMax = new Vector2(0.39f, 0.95f);
                lapText = Label("Lap", rankGroup.transform, "1/3", 35, TextAnchor.MiddleCenter, new Color(0.82f, 0.96f, 1f));
                lapText.rectTransform.anchorMin = new Vector2(0.40f, 0.05f); lapText.rectTransform.anchorMax = new Vector2(0.92f, 0.95f);

                var miniPanel = Panel("MiniMapPanel", safe, new Color(0.03f, 0.04f, 0.12f, 0.48f), new Vector2(0.025f, 0.49f), new Vector2(0.25f, 0.80f));
                miniPanel.sprite = circleSprite;
                miniPanel.preserveAspect = true;
                minimap = new GameObject("MiniMap", typeof(RectTransform), typeof(CanvasRenderer), typeof(MiniMapGraphic)).GetComponent<MiniMapGraphic>();
                minimap.transform.SetParent(miniPanel.transform, false);
                Stretch(minimap.rectTransform, 24f);
                minimap.Game = game;
                var markers = new GameObject("MiniMapMarkers", typeof(RectTransform), typeof(CanvasRenderer), typeof(MiniMapGraphic)).GetComponent<MiniMapGraphic>();
                markers.transform.SetParent(miniPanel.transform, false);
                Stretch(markers.rectTransform, 24f);
                markers.Game = game;
                markers.MarkersOnly = true;
                minimap = markers;

                var timeGroup = Panel("TimeGroup", safe, Color.white, new Vector2(0.72f, 0.835f), new Vector2(0.975f, 0.965f));
                UsePillArt(timeGroup);
                timerText = Label("Timer", timeGroup.transform, "00:00.00", 44, TextAnchor.UpperCenter, Color.white);
                timerText.rectTransform.anchorMin = new Vector2(0.04f, 0.38f); timerText.rectTransform.anchorMax = new Vector2(0.76f, 0.94f);
                bestText = Label("Best", timeGroup.transform, "BEST --:--.--", 22, TextAnchor.MiddleCenter, new Color(0.35f, 0.95f, 1f));
                bestText.rectTransform.anchorMin = new Vector2(0.04f, 0.06f); bestText.rectTransform.anchorMax = new Vector2(0.76f, 0.42f);
                Button pause = CircleButton("Pause", timeGroup.transform, "Ⅱ", new Color(0.95f, 0.55f, 0.18f), game.TogglePause);
                pause.transform.Find("Glyph").gameObject.SetActive(!UseControlArt(pause.GetComponent<Image>(), 2));
                SetAnchors(pause.GetComponent<RectTransform>(), new Vector2(0.79f, 0.10f), new Vector2(0.97f, 0.90f));

                var leftPad = Panel("Steering", safe, Color.clear, new Vector2(0.025f, 0.055f), new Vector2(0.27f, 0.34f));
                Button left = ButtonRect("Left", leftPad.transform, "◀", new Color(0.12f, 0.58f, 0.88f, 0.70f), null);
                left.transform.Find("Glyph").gameObject.SetActive(!UseControlArt(left.GetComponent<Image>(), 0));
                SetAnchors(left.GetComponent<RectTransform>(), new Vector2(0.01f, 0.04f), new Vector2(0.49f, 0.96f));
                left.gameObject.AddComponent<HoldSteer>().Setup(game, -1f);
                Button right = ButtonRect("Right", leftPad.transform, "▶", new Color(1f, 0.62f, 0.12f, 0.70f), null);
                right.transform.Find("Glyph").gameObject.SetActive(!UseControlArt(right.GetComponent<Image>(), 1));
                SetAnchors(right.GetComponent<RectTransform>(), new Vector2(0.51f, 0.04f), new Vector2(0.99f, 0.96f));
                right.gameObject.AddComponent<HoldSteer>().Setup(game, 1f);

                Button skill = CircleButton("Skill", safe, "★", new Color(1f, 0.42f, 0.10f), null);
                skill.gameObject.AddComponent<PressRaceAction>().Setup(game,false);
                SetAnchors(skill.GetComponent<RectTransform>(), new Vector2(0.75f, 0.12f), new Vector2(0.86f, 0.31f));
                skillButtonImage = skill.GetComponent<Image>();
                skillButtonGlyph = skill.transform.Find("Glyph").GetComponent<Text>();
                Sprite initialSkillSprite = UiArt.Skill(game.SelectedCharacterId);
                if (initialSkillSprite != null)
                {
                    skillButtonImage.sprite = initialSkillSprite;
                    skillButtonImage.color = Color.white;
                    skillButtonImage.preserveAspect = true;
                }
                skillButtonGlyph.gameObject.SetActive(initialSkillSprite == null);
                Button nitro = CircleButton("Nitro", safe, "⚡", new Color(0.10f, 0.72f, 1f), null);
                nitro.gameObject.AddComponent<PressRaceAction>().Setup(game,true);
                SetAnchors(nitro.GetComponent<RectTransform>(), new Vector2(0.86f, 0.055f), new Vector2(0.975f, 0.255f));
                nitroButtonImage = nitro.GetComponent<Image>();
                nitro.transform.Find("Glyph").gameObject.SetActive(!UseControlArt(nitroButtonImage, 3));

                var skillMeter = Panel("SkillMeter", safe, new Color(0.03f, 0.04f, 0.12f, 0.92f), new Vector2(0.405f, 0.092f), new Vector2(0.595f, 0.148f));
                UseEnergyArt(skillMeter, 0);
                skillFill = Panel("Fill", skillMeter.transform, new Color(1f, 0.42f, 0.12f, 0.95f), Vector2.zero, Vector2.one);
                UseEnergyArt(skillFill, 1);
                skillFill.type = Image.Type.Filled; skillFill.fillMethod = Image.FillMethod.Horizontal;
                skillValueText = Label("Value", skillMeter.transform, "技能 62%", 19, TextAnchor.MiddleCenter, Color.white);
                SetAnchors(skillValueText.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.96f));

                var meter = Panel("NitroMeter", safe, new Color(0.03f, 0.04f, 0.12f, 0.92f), new Vector2(0.405f, 0.027f), new Vector2(0.595f, 0.083f));
                UseEnergyArt(meter, 0);
                nitroFill = Panel("Fill", meter.transform, new Color(0.10f, 0.85f, 1f, 0.92f), Vector2.zero, Vector2.one);
                UseEnergyArt(nitroFill, 1);
                nitroFill.type = Image.Type.Filled; nitroFill.fillMethod = Image.FillMethod.Horizontal;
                nitroValueText = Label("Value", meter.transform, "氮气 46%", 19, TextAnchor.MiddleCenter, Color.white);
                SetAnchors(nitroValueText.rectTransform, new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.96f));

                feedbackText = Label("Feedback", safe, string.Empty, 32, TextAnchor.MiddleCenter, Color.white);
                SetAnchors(feedbackText.rectTransform, new Vector2(0.34f, 0.18f), new Vector2(0.66f, 0.25f));
                feedbackText.gameObject.SetActive(false);

                countdownText = Label("Countdown", safe, "3", 116, TextAnchor.MiddleCenter, Color.white);
                SetAnchors(countdownText.rectTransform, new Vector2(0.38f, 0.38f), new Vector2(0.62f, 0.68f));
                BuildLaunchHint(safe);

                pausePanel = Modal(safe, "PAUSED", "继续", game.Resume, "重新开始", game.RestartRace);
                pausePanel.SetActive(false);
                finishPanel = Modal(safe, "FINISH", "再来一局", game.RestartRace, "继续练习", game.ContinuePractice);
                finishText = finishPanel.transform.Find("Card/Title").GetComponent<Text>();
                finishPanel.SetActive(false);
                BuildCharacterChoice(safe);
                BuildMapChoice(safe);
                RefreshCharacterChoice();
            }

            public void Refresh()
            {
                if (game.Player == null) return;
                RefreshLaunchHint(); // The 0.35s timing cue must not use the normal 10Hz HUD throttle.
                if (Time.unscaledTime < nextRefreshTime) return;
                nextRefreshTime = Time.unscaledTime + 0.10f;
                int nitroPercent = Mathf.RoundToInt(game.Player.Nitro * 100f);
                int skillPercent = Mathf.RoundToInt(game.Player.SkillEnergy * 100f);
                SetBarValue(nitroFill.rectTransform, game.Player.Nitro);
                SetBarValue(skillFill.rectTransform, game.Player.SkillEnergy);
                if (nitroPercent != lastNitroPercent)
                {
                    lastNitroPercent = nitroPercent;
                    nitroValueText.text = $"氮气 {nitroPercent}%";
                }
                string skillState = game.Player.SkillReady ? "就绪 · 点击释放" : game.Player.SkillRemaining > 0f ? $"{skillPercent}% / 生效 {game.Player.SkillRemaining:0.0}s" : $"{skillPercent}%";
                if (skillPercent != lastSkillPercent || skillState != lastSkillState)
                {
                    lastSkillPercent = skillPercent;
                    lastSkillState = skillState;
                    skillValueText.text = $"{CharacterNames[game.Player.CharacterId]} {skillState}";
                }
                nitroButtonImage.color = game.LaunchWindowOpen&&!game.LaunchTriggered ? new Color(.65f,1f,.80f) : game.Player.Nitro >= 0.34f ? Color.white : new Color(0.35f, 0.42f, 0.48f, 0.72f);
                skillButtonImage.color = game.Player.SkillReady ? Color.white : new Color(0.55f, 0.58f, 0.66f, 0.82f);
                if (feedbackText.gameObject.activeSelf && Time.unscaledTime >= feedbackUntil)
                    feedbackText.gameObject.SetActive(false);
                rankText.text = game.Player.Rank.ToString();
                int lap = Mathf.Clamp(Mathf.FloorToInt(game.Player.TotalProgress) + 1, 1, game.Laps);
                lapText.text = game.IsPracticeMode ? "练习" : $"{lap}/{game.Laps}";
                timerText.text = game.IsPracticeMode ? "FREE RUN" : FormatTime(game.RaceTime);
                bestText.text = game.BestTime > 0f ? "BEST  " + FormatTime(game.BestTime) : "BEST  --:--.--";
                minimap.SetVerticesDirty();
            }

            public void SetPauseVisible(bool value) => pausePanel.SetActive(value);

            public void ShowFinish()
            {
                finishText.text = $"FINISH\n{FormatTime(game.RaceTime)}\n第 {game.Player.Rank} 名";
                finishPanel.SetActive(true);
            }

            public void HideFinish() => finishPanel.SetActive(false);

            public void HideCharacterChoice() => characterPanel.SetActive(false);

            public void RefreshCharacterChoice()
            {
                if (characterPanel == null) return;
                int selected = game.SelectedCharacterId;
                selectedCharacterText.text = CharacterNames[selected];
                characterDescriptionText.text = CharacterDescriptions[selected];
                if (skillValueText != null)
                    skillValueText.text = $"{CharacterNames[selected]} {Mathf.RoundToInt(game.Player.SkillEnergy * 100f)}%";
                if (skillButtonImage != null)
                {
                    Sprite selectedSkillSprite = UiArt.Skill(selected);
                    if (selectedSkillSprite != null)
                    {
                        skillButtonImage.sprite = selectedSkillSprite;
                        skillButtonImage.preserveAspect = true;
                        if (skillButtonGlyph != null) skillButtonGlyph.gameObject.SetActive(false);
                    }
                    else if (skillButtonGlyph != null)
                    {
                        skillButtonGlyph.gameObject.SetActive(true);
                    }
                }
                for (int i = 0; i < characterButtons.Count; i++)
                {
                    characterButtons[i].GetComponent<Image>().color = i == selected ? Color.white : new Color(0.52f, 0.56f, 0.66f, 0.86f);
                    if (i >= characterIcons.Count) continue;
                    Sprite iconSprite = UiArt.Skill(i);
                    if (iconSprite == null) continue;
                    characterIcons[i].sprite = iconSprite;
                    characterIcons[i].color = Color.white;
                }
            }

            public void ShowFeedback(string message, Color color)
            {
                feedbackText.text = message;
                feedbackText.color = color;
                feedbackUntil = Time.unscaledTime + 1.15f;
                feedbackText.gameObject.SetActive(true);
            }

            private static void SetBarValue(RectTransform fill, float value)
            {
                Image image = fill.GetComponent<Image>();
                if (image != null && image.sprite != null && image.type == Image.Type.Filled)
                {
                    image.fillAmount = Mathf.Clamp01(value);
                    return;
                }
                fill.anchorMin = Vector2.zero;
                fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
                fill.offsetMin = Vector2.zero;
                fill.offsetMax = Vector2.zero;
            }

            private static string FormatTime(float seconds)
            {
                int minutes = Mathf.FloorToInt(seconds / 60f);
                float remain = seconds - minutes * 60f;
                return $"{minutes:00}:{remain:00.00}";
            }

            private GameObject Modal(Transform parent, string title, string actionA, UnityEngine.Events.UnityAction callbackA, string actionB, UnityEngine.Events.UnityAction callbackB)
            {
                var shade = Panel(title + "Panel", parent, new Color(0.005f, 0.005f, 0.025f, 0.86f), Vector2.zero, Vector2.one).gameObject;
                var card = Panel("Card", shade.transform, Color.white, new Vector2(0.31f, 0.25f), new Vector2(0.69f, 0.75f));
                UsePanelArt(card);
                Text label = Label("Title", card.transform, title, 62, TextAnchor.MiddleCenter, Color.white);
                SetAnchors(label.rectTransform, new Vector2(0.05f, 0.40f), new Vector2(0.95f, 0.92f));
                Button a = ButtonRect("ActionA", card.transform, actionA, new Color(0.95f, 0.47f, 0.12f), callbackA);
                UsePillArt(a.GetComponent<Image>());
                SetAnchors(a.GetComponent<RectTransform>(), new Vector2(0.10f, 0.10f), new Vector2(0.48f, 0.31f));
                Button b = ButtonRect("ActionB", card.transform, actionB, new Color(0.12f, 0.58f, 0.88f), callbackB);
                UsePillArt(b.GetComponent<Image>());
                SetAnchors(b.GetComponent<RectTransform>(), new Vector2(0.52f, 0.10f), new Vector2(0.90f, 0.31f));
                return shade;
            }

            private void BuildCharacterChoice(Transform parent)
            {
                characterPanel = Panel("CharacterSelect", parent, new Color(0.005f, 0.005f, 0.025f, 1f), Vector2.zero, Vector2.one).gameObject;
                var card = Panel("Card", characterPanel.transform, Color.white, new Vector2(0.14f, 0.07f), new Vector2(0.86f, 0.93f));
                UsePanelArt(card);
                Text title = Label("Title", card.transform, "选择出战球", 48, TextAnchor.MiddleCenter, Color.white);
                SetAnchors(title.rectTransform, new Vector2(0.08f, 0.85f), new Vector2(0.92f, 0.96f));

                for (int i = 0; i < CharacterNames.Length; i++)
                {
                    int id = i;
                    int column = i % 3;
                    int row = i / 3;
                    Button button = ButtonRect($"Character_{i}", card.transform, CharacterNames[i], new Color(0.12f, 0.20f, 0.42f), () => game.SelectCharacter(id));
                    UsePillArt(button.GetComponent<Image>());
                    Text name = button.transform.Find("Glyph").GetComponent<Text>();
                    name.fontSize = 28;
                    name.alignment = TextAnchor.MiddleCenter;
                    SetAnchors(name.rectTransform, new Vector2(0.35f, 0.05f), new Vector2(0.95f, 0.95f));
                    Image icon = Panel("Icon", button.transform, Color.clear, new Vector2(0.055f, 0.10f), new Vector2(0.35f, 0.90f));
                    Sprite iconSprite = UiArt.Skill(i);
                    if (iconSprite != null)
                    {
                        icon.sprite = iconSprite;
                        icon.color = Color.white;
                    }
                    icon.preserveAspect = true;
                    icon.raycastTarget = false;
                    characterIcons.Add(icon);
                    float x0 = 0.075f + column * 0.30f;
                    float y1 = 0.81f - row * 0.17f;
                    SetAnchors(button.GetComponent<RectTransform>(), new Vector2(x0, y1 - 0.13f), new Vector2(x0 + 0.25f, y1));
                    characterButtons.Add(button);
                }

                selectedCharacterText = Label("SelectedName", card.transform, string.Empty, 36, TextAnchor.MiddleCenter, new Color(0.28f, 0.96f, 1f));
                SetAnchors(selectedCharacterText.rectTransform, new Vector2(0.12f, 0.30f), new Vector2(0.88f, 0.39f));
                characterDescriptionText = Label("Description", card.transform, string.Empty, 23, TextAnchor.MiddleCenter, new Color(0.86f, 0.92f, 1f));
                characterDescriptionText.horizontalOverflow = HorizontalWrapMode.Wrap;
                SetAnchors(characterDescriptionText.rectTransform, new Vector2(0.10f, 0.17f), new Vector2(0.90f, 0.29f));
                Button confirm = ButtonRect("Confirm", card.transform, "下一步 · 选地图", new Color(0.95f, 0.47f, 0.12f), game.ConfirmCharacter);
                UsePillArt(confirm.GetComponent<Image>());
                confirm.transform.Find("Glyph").GetComponent<Text>().fontSize = 32;
                SetAnchors(confirm.GetComponent<RectTransform>(), new Vector2(0.46f, 0.035f), new Vector2(0.88f, 0.135f));
                Button guide = ButtonRect("OpenGuide", card.transform, "玩法说明", Color.white, ShowGuide);
                UsePillArt(guide.GetComponent<Image>());
                guide.transform.Find("Glyph").GetComponent<Text>().fontSize = 32;
                SetAnchors(guide.GetComponent<RectTransform>(), new Vector2(0.12f, 0.035f), new Vector2(0.39f, 0.135f));
                BuildGuide(characterPanel.transform);
            }

            private Image Panel(string name, Transform parent, Color color, Vector2 min, Vector2 max)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                go.transform.SetParent(parent, false);
                var img = go.GetComponent<Image>(); img.color = color;
                SetAnchors(img.rectTransform, min, max);
                return img;
            }

            private Text Label(string name, Transform parent, string text, int size, TextAnchor align, Color color)
            {
                var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                go.transform.SetParent(parent, false);
                Text label = go.GetComponent<Text>();
                label.font = font; label.text = text; label.fontSize = size; label.alignment = align; label.color = color;
                label.horizontalOverflow = HorizontalWrapMode.Overflow; label.verticalOverflow = VerticalWrapMode.Overflow;
                label.raycastTarget = false;
                SetAnchors(label.rectTransform, Vector2.zero, Vector2.one);
                return label;
            }

            private Button ButtonRect(string name, Transform parent, string glyph, Color color, UnityEngine.Events.UnityAction callback)
            {
                Image img = Panel(name, parent, color, Vector2.zero, Vector2.one);
                Button button = img.gameObject.AddComponent<Button>();
                if (callback != null) button.onClick.AddListener(callback);
                Text text = Label("Glyph", img.transform, glyph, 38, TextAnchor.MiddleCenter, Color.white);
                text.raycastTarget = false;
                return button;
            }

            private Button CircleButton(string name, Transform parent, string glyph, Color color, UnityEngine.Events.UnityAction callback)
            {
                Button button = ButtonRect(name, parent, glyph, color, callback);
                if (button.GetComponent<Image>().sprite == null) button.GetComponent<Image>().sprite = circleSprite;
                return button;
            }

            private static void UsePanelArt(Image image)
            {
                Sprite sprite = UiArt.PanelFrame;
                if (sprite == null) return;
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = false;
                image.type = Image.Type.Sliced;
            }

            private static void UsePillArt(Image image)
            {
                Sprite sprite = UiArt.PillButton;
                if (sprite == null) return;
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = false;
                image.type = Image.Type.Sliced;
            }

            private static bool UseControlArt(Image image, int index)
            {
                Sprite sprite = UiArt.Control(index);
                if (sprite == null) return false;
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = true;
                image.type = Image.Type.Simple;
                return true;
            }

            private static bool UseEnergyArt(Image image, int index)
            {
                Sprite sprite = UiArt.Energy(index);
                if (sprite == null) return false;
                image.sprite = sprite;
                image.color = Color.white;
                image.preserveAspect = false;
                image.type = Image.Type.Simple;
                return true;
            }

            private static Sprite CreateCircleSprite(int size)
            {
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "RuntimeCircle",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color32[size * size];
                float center = (size - 1) * 0.5f;
                float radius = center - 1f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(radius - distance + 1f) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            }

            private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max)
            {
                rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            }

            private static void Stretch(RectTransform rt, float padding)
            {
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.one * padding; rt.offsetMax = Vector2.one * -padding;
            }

            private static void EnsureEventSystem()
            {
                if (FindAnyObjectByType<EventSystem>() != null) return;
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                DontDestroyOnLoad(es);
            }
        }

        public sealed class HoldSteer : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
        {
            private SpeedTournamentPrototype game;
            private float direction;
            private bool held;
            private float nextRepeatTime;
            public void Setup(SpeedTournamentPrototype owner, float value) { game = owner; direction = value; }
            public void OnPointerDown(PointerEventData eventData)
            {
                held = true;
                game.RequestLaneChange(Mathf.RoundToInt(direction));
                nextRepeatTime = Time.unscaledTime + RaceBalance.HoldRepeatSeconds;
            }
            public void OnPointerUp(PointerEventData eventData) => held = false;
            public void OnPointerExit(PointerEventData eventData) => held = false;
            private void Update()
            {
                if (!held || Time.unscaledTime < nextRepeatTime) return;
                game.RequestLaneChange(Mathf.RoundToInt(direction));
                nextRepeatTime = Time.unscaledTime + RaceBalance.HoldRepeatSeconds;
            }
        }

        public sealed class SafeAreaFitter : MonoBehaviour
        {
            private Rect lastSafe;
            private Vector2Int lastScreen;
            private void OnEnable() => Apply();
            private void Update()
            {
                if (lastSafe != Screen.safeArea || lastScreen.x != Screen.width || lastScreen.y != Screen.height) Apply();
            }
            private void Apply()
            {
                Rect safe = Screen.safeArea;
                lastSafe = safe; lastScreen = new Vector2Int(Screen.width, Screen.height);
                RectTransform rt = (RectTransform)transform;
                rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
                rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
        }

        public sealed class MiniMapGraphic : MaskableGraphic
        {
            public SpeedTournamentPrototype Game;
            public bool MarkersOnly;
            private const int Samples = 90;

            protected override void OnPopulateMesh(VertexHelper vh)
            {
                vh.Clear();
                if (Game?.Track == null) return;
                Rect r = rectTransform.rect;
                Bounds bounds=Game.Track.MapBounds;
                Vector2 scale = Vector2.one * Mathf.Min(r.width / (bounds.size.x+55f), r.height / (bounds.size.z+55f));
                Vector2 center = r.center-new Vector2(bounds.center.x,bounds.center.z)*scale;
                if (!MarkersOnly)
                {
                for (int route = 0; route < TrackModel.RouteCount; route++)
                {
                    Color32 routeColor = route == 0 ? new Color32(225, 236, 255, 230) : new Color32(194, 118, 255, 220);
                    for (int i = 0; i < Samples; i++)
                    {
                        float t0 = i / (float)Samples;
                        float t1 = (i + 1) / (float)Samples;
                        if (route == 1 && !Game.Track.InnerIsOpen((t0+t1)*0.5f)) continue;
                        Vector3 w0 = Game.Track.EvaluateRoute(route, t0);
                        Vector3 w1 = Game.Track.EvaluateRoute(route, t1);
                        AddLine(vh, center + new Vector2(w0.x, w0.z) * scale, center + new Vector2(w1.x, w1.z) * scale, 2.6f, routeColor);
                    }
                }
                for (int branch = 0; branch < Game.Track.BranchCount; branch++)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        Vector3 w0 = Game.Track.EvaluateBranch(branch, i / 12f);
                        Vector3 w1 = Game.Track.EvaluateBranch(branch, (i + 1) / 12f);
                        AddLine(vh, center + new Vector2(w0.x, w0.z) * scale, center + new Vector2(w1.x, w1.z) * scale, 2.1f, new Color32(255, 184, 48, 230));
                    }
                }
                }
                if (!MarkersOnly) return;
                for (int i = 0; i < Game.Racers.Count; i++)
                {
                    RacerAgent racer = Game.Racers[i];
                    Vector3 p = racer.transform.position;
                    AddDot(vh, center + new Vector2(p.x, p.z) * scale, racer == Game.Player ? 7f : 4.5f, racer == Game.Player ? new Color32(255, 175, 30, 255) : new Color32(65, 225, 255, 230));
                }
            }

            private static void AddLine(VertexHelper vh, Vector2 a, Vector2 b, float width, Color32 color)
            {
                Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
                int start = vh.currentVertCount;
                vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
                vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
            }

            private static void AddDot(VertexHelper vh, Vector2 p, float radius, Color32 color)
            {
                int start = vh.currentVertCount;
                vh.AddVert(p + new Vector2(-radius, -radius), color, Vector2.zero);
                vh.AddVert(p + new Vector2(-radius, radius), color, Vector2.zero);
                vh.AddVert(p + new Vector2(radius, radius), color, Vector2.zero);
                vh.AddVert(p + new Vector2(radius, -radius), color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
