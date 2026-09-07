using UnityEngine;
namespace SpeedTournament
{
    [CreateAssetMenu(menuName="Speed Tournament/Audio settings")]
    public sealed class RaceAudioConfig : ScriptableObject
    {
        public AudioClip menuMusic, raceMusic;
        [Range(0,1)] public float menuVolume=0.16f;
        [Range(0,1)] public float raceVolume=0.42f;
        [Range(0,3)] public float raceStartDelay=0.5f;
        [Range(0,1)] public float effectsVolume=0.78f;
        [Range(0.05f,1f)] public float criticalMusicFactor=0.28f;
        [Range(0.05f,1f)] public float musicRecoverySeconds=0.28f;
    }
}
