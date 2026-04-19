using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HackathonJuego
{
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Configuración por Escena")]
        [Tooltip("Audio para la escena de menú/startup")]
        public SceneAudioData menuAudio;
        [Tooltip("Audio para la escena de gameplay (Hackathon2026)")]
        public SceneAudioData gameplayAudio;

        [Header("Volumen Global")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float musicMasterVolume = 1f;
        [Range(0f, 1f)] public float sfxMasterVolume = 1f;

        private AudioSource _musicSource;
        private AudioSource _sfxSource;
        private AudioSource _uiSource;
        private AudioSource _ambientSource;

        private SceneAudioData _currentSceneAudio;
        private Coroutine _fadeCoroutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _musicSource = CreateAudioSource("Music");
            _musicSource.loop = true;

            _sfxSource = CreateAudioSource("SFX");

            _uiSource = CreateAudioSource("UI");

            _ambientSource = CreateAudioSource("Ambient");
            _ambientSource.loop = true;

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        private AudioSource CreateAudioSource(string name)
        {
            var go = new GameObject($"AudioSource_{name}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            return src;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneAudioData data = GetAudioDataForScene(scene.name);
            if (data != null)
            {
                ApplySceneAudio(data);
            }
        }

        private SceneAudioData GetAudioDataForScene(string sceneName)
        {
            switch (sceneName)
            {
                case "Startup":
                case "SampleScene":
                    return menuAudio;
                case "Hackathon2026":
                    return gameplayAudio;
                default:
                    return null;
            }
        }

        public void ApplySceneAudio(SceneAudioData data)
        {
            if (data == null) return;
            _currentSceneAudio = data;

            // Música de fondo
            AudioClip selectedMusic = data.GetRandomMusic();
            if (selectedMusic != null)
            {
                if (_musicSource.clip != selectedMusic)
                {
                    if (data.fadeInOnStart)
                    {
                        if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
                        _fadeCoroutine = StartCoroutine(CrossfadeMusic(selectedMusic, data.musicVolume * musicMasterVolume * masterVolume, data.fadeInDuration));
                    }
                    else
                    {
                        _musicSource.clip = selectedMusic;
                        _musicSource.volume = data.musicVolume * musicMasterVolume * masterVolume;
                        _musicSource.loop = data.loopMusic;
                        _musicSource.Play();
                    }
                }
            }
            else
            {
                _musicSource.Stop();
            }

            // Ambiente
            if (data.ambientLoop != null)
            {
                _ambientSource.clip = data.ambientLoop;
                _ambientSource.volume = data.ambientVolume * masterVolume;
                _ambientSource.Play();
            }
            else
            {
                _ambientSource.Stop();
            }
        }

        private IEnumerator CrossfadeMusic(AudioClip newClip, float targetVolume, float duration)
        {
            float startVolume = _musicSource.volume;

            // Fade out
            float t = 0f;
            while (t < duration * 0.5f)
            {
                t += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(startVolume, 0f, t / (duration * 0.5f));
                yield return null;
            }

            // Switch clip
            _musicSource.clip = newClip;
            _musicSource.loop = _currentSceneAudio != null ? _currentSceneAudio.loopMusic : true;
            _musicSource.Play();

            // Fade in
            t = 0f;
            while (t < duration * 0.5f)
            {
                t += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(0f, targetVolume, t / (duration * 0.5f));
                yield return null;
            }

            _musicSource.volume = targetVolume;
            _fadeCoroutine = null;
        }

        // ======== MÉTODOS PÚBLICOS PARA SONIDOS ========

        public void PlayUIHover()
        {
            if (_currentSceneAudio != null && _currentSceneAudio.uiHover != null)
                _uiSource.PlayOneShot(_currentSceneAudio.uiHover, _currentSceneAudio.uiVolume * sfxMasterVolume * masterVolume);
        }

        public void PlayUIClick()
        {
            if (_currentSceneAudio != null && _currentSceneAudio.uiClick != null)
                _uiSource.PlayOneShot(_currentSceneAudio.uiClick, _currentSceneAudio.uiVolume * sfxMasterVolume * masterVolume);
        }

        public void PlayUIBack()
        {
            if (_currentSceneAudio != null && _currentSceneAudio.uiBack != null)
                _uiSource.PlayOneShot(_currentSceneAudio.uiBack, _currentSceneAudio.uiVolume * sfxMasterVolume * masterVolume);
        }

        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip != null)
                _sfxSource.PlayOneShot(clip, volumeScale * sfxMasterVolume * masterVolume);
        }

        public void PlayBoxOpen()
        {
            if (_currentSceneAudio != null)
                PlaySFX(_currentSceneAudio.boxOpen, _currentSceneAudio.sfxVolume);
        }

        public void PlayBoxDrop()
        {
            if (_currentSceneAudio != null)
                PlaySFX(_currentSceneAudio.boxDrop, _currentSceneAudio.sfxVolume);
        }

        public void PlayBoxSlide()
        {
            if (_currentSceneAudio != null)
                PlaySFX(_currentSceneAudio.boxSlide, _currentSceneAudio.sfxVolume);
        }

        public void PlayMoneyReveal()
        {
            if (_currentSceneAudio != null)
                PlaySFX(_currentSceneAudio.moneyReveal, _currentSceneAudio.sfxVolume);
        }

        public void PlayBombReveal()
        {
            if (_currentSceneAudio != null)
                PlaySFX(_currentSceneAudio.bombReveal, _currentSceneAudio.sfxVolume);
        }

        public void PlayRoundStart()
        {
            if (_currentSceneAudio != null)
                PlaySFX(_currentSceneAudio.roundStart, _currentSceneAudio.sfxVolume);
        }

        public void PlayRoundEnd()
        {
            if (_currentSceneAudio != null)
                PlaySFX(_currentSceneAudio.roundEnd, _currentSceneAudio.sfxVolume);
        }

        public void PlayPlayerReady()
        {
            if (_currentSceneAudio != null)
                PlaySFX(_currentSceneAudio.playerReady, _currentSceneAudio.sfxVolume);
        }

        public void PlayVictory()
        {
            if (_currentSceneAudio != null)
                PlaySFX(_currentSceneAudio.victory, _currentSceneAudio.sfxVolume);
        }

        public void PlayDefeat()
        {
            if (_currentSceneAudio != null)
                PlaySFX(_currentSceneAudio.defeat, _currentSceneAudio.sfxVolume);
        }

        public void StopMusic()
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _musicSource.Stop();
        }

        public void SetMasterVolume(float vol)
        {
            masterVolume = Mathf.Clamp01(vol);
            RefreshVolumes();
        }

        public void SetMusicVolume(float vol)
        {
            musicMasterVolume = Mathf.Clamp01(vol);
            RefreshVolumes();
        }

        public void SetSFXVolume(float vol)
        {
            sfxMasterVolume = Mathf.Clamp01(vol);
        }

        private void RefreshVolumes()
        {
            if (_currentSceneAudio != null)
            {
                _musicSource.volume = _currentSceneAudio.musicVolume * musicMasterVolume * masterVolume;
                _ambientSource.volume = _currentSceneAudio.ambientVolume * masterVolume;
            }
        }
    }
}
