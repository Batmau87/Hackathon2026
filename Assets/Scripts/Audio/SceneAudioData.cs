using UnityEngine;

namespace HackathonJuego
{
    [CreateAssetMenu(fileName = "NewSceneAudio", menuName = "Hackathon/Scene Audio Data")]
    public class SceneAudioData : ScriptableObject
    {
        [Header("Música de Fondo")]
        public AudioClip[] backgroundMusic = new AudioClip[2];
        [Range(0f, 1f)] public float musicVolume = 0.5f;
        public bool loopMusic = true;
        public bool fadeInOnStart = true;
        [Range(0.1f, 5f)] public float fadeInDuration = 1.5f;

        [Header("Sonidos UI")]
        public AudioClip uiHover;
        public AudioClip uiClick;
        public AudioClip uiBack;
        [Range(0f, 1f)] public float uiVolume = 0.7f;

        [Header("Sonidos de Gameplay")]
        public AudioClip boxOpen;
        public AudioClip boxDrop;
        public AudioClip boxSlide;
        public AudioClip moneyReveal;
        public AudioClip bombReveal;
        public AudioClip roundStart;
        public AudioClip roundEnd;
        public AudioClip playerReady;
        public AudioClip timerTick;
        public AudioClip victory;
        public AudioClip defeat;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;

        [Header("Ambiente")]
        public AudioClip ambientLoop;
        [Range(0f, 1f)] public float ambientVolume = 0.3f;

        public AudioClip GetRandomMusic()
        {
            if (backgroundMusic == null || backgroundMusic.Length == 0) return null;
            // Filtra los slots vacíos
            var valid = System.Array.FindAll(backgroundMusic, c => c != null);
            if (valid.Length == 0) return null;
            return valid[Random.Range(0, valid.Length)];
        }
    }
}
