using UnityEngine;

namespace SpeedTournament
{
    // Single source for gameplay tuning and on-screen skill descriptions.
    public static class RaceBalance
    {
        public const float BaseSpeed=47f, LaneChangeSeconds=0.13f, HoldRepeatSeconds=0.16f;
        public const float NitroCost=0.34f, NitroDuration=1.7f, NitroBonus=0.28f;
        public const float LaunchEarlyWindow=.35f, LaunchLateWindow=.20f, LaunchDuration=1.2f, LaunchBonus=.35f;
        public const float PassiveSkill=0.035f, PassiveNitro=0.015f, MaximumSpeedBonus=0.68f;
        public const float PickupSkill=0.34f, PickupNitro=0.24f, PulseRange=80f;
        public static readonly float[] Duration={3.5f,4f,3.5f,4f,3.3f,4f};
        public static readonly float[] SpeedBonus={0.42f,0.14f,0.28f,0.18f,0.28f,0.30f};
        public static readonly string[] Descriptions={
            "疾速推进 3.5秒 · 速度 +42%\n氮气可叠加；适合长直道与外线超车。",
            "坚盾 4秒 · 速度 +14% · 抵挡所有负面效果\n挡撞回收12%技能能量和氮气，每次开盾最多3次。",
            "脉冲清场 · 前方80米、相邻车道内击退对手并清障\n提速28%，持续3.5秒；每命中一个目标回收8%技能能量，最多24%。",
            "引力牵引 4秒 · 自动吸取相邻两条车道的补给\n速度 +18%，立即恢复24%氮气。",
            "相位超车 3.3秒 · 速度 +28% · 穿透所有障碍\n立即清除减速；在内线障碍区连续超车。",
            "能源超载 4秒 · 速度 +30%\n立即恢复34%氮气；能量满也会获得超载加速。"
        };
        public static float SpeedMultiplier(int role,bool skill,bool nitro,bool pad,bool launch=false)
        {
            float bonus=(skill&&role>=0?SpeedBonus[Mathf.Clamp(role,0,5)]:0f)+(nitro?NitroBonus:0f)+(pad?0.20f:0f)+(launch?LaunchBonus:0f);
            return 1f+Mathf.Min(MaximumSpeedBonus,bonus);
        }
    }
}
