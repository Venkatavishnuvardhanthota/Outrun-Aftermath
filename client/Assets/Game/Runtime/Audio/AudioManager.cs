using System.Collections.Generic;
using UnityEngine;

namespace OutrunAftermath.Client.Runtime.Audio
{
    /// <summary>
    /// Master Audio Director for OUTRUN: AFTERMATH.
    /// Manages 2D HUD feedback, 3D spatialized combat audio pools, vehicle telemetry soundscapes,
    /// dynamic weather ambiance, and weapon audio using procedural PCM synthesis.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const int PoolSize = 16;
        private readonly List<AudioSource> _spatialSources = new List<AudioSource>();
        private int _poolIndex = 0;

        private AudioSource _hudSource2D;
        private AudioSource _ambientSource;
        private AudioSource _heartbeatSource;

        // Cached synthesized clips
        private readonly Dictionary<string, AudioClip> _clipCache = new Dictionary<string, AudioClip>();

        [Header("Volume Balances")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1.0f;
        [Range(0f, 1f)] [SerializeField] private float hudVolume = 0.9f;
        [Range(0f, 1f)] [SerializeField] private float ambientVolume = 0.45f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitializeAudioSystem();
        }

        private void InitializeAudioSystem()
        {
            // 2D HUD AudioSource
            _hudSource2D = gameObject.AddComponent<AudioSource>();
            _hudSource2D.spatialBlend = 0f; // 2D stereo
            _hudSource2D.playOnAwake = false;

            // Ambient Loop AudioSource
            _ambientSource = gameObject.AddComponent<AudioSource>();
            _ambientSource.spatialBlend = 0f;
            _ambientSource.loop = true;
            _ambientSource.playOnAwake = false;
            _ambientSource.volume = ambientVolume * masterVolume;

            // Heartbeat AudioSource
            _heartbeatSource = gameObject.AddComponent<AudioSource>();
            _heartbeatSource.spatialBlend = 0f;
            _heartbeatSource.loop = true;
            _heartbeatSource.playOnAwake = false;
            _heartbeatSource.volume = 0.8f * masterVolume;

            // 3D Spatial AudioSource Pool
            var poolRoot = new GameObject("SpatialAudioPool");
            poolRoot.transform.SetParent(transform);

            for (int i = 0; i < PoolSize; i++)
            {
                var srcGo = new GameObject($"SpatialSource_{i}");
                srcGo.transform.SetParent(poolRoot.transform);
                var src = srcGo.AddComponent<AudioSource>();
                src.spatialBlend = 1.0f; // full 3D spatial
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 2.0f;
                src.maxDistance = 65.0f;
                src.dopplerLevel = 0.4f;
                src.playOnAwake = false;
                _spatialSources.Add(src);
            }

            PrewarmClips();
            StartAmbientAmbiance("clear");
            Debug.Log($"[AudioManager] Master Audio Director initialized with {PoolSize} 3D spatial sources. Procedural audio active.");
        }

        private void PrewarmClips()
        {
            GetOrGenerateClip("gun_rifle", () => ProceduralAudioSynthesizer.CreateGunshotClip("rifle"));
            GetOrGenerateClip("gun_smg", () => ProceduralAudioSynthesizer.CreateGunshotClip("smg"));
            GetOrGenerateClip("gun_sniper", () => ProceduralAudioSynthesizer.CreateGunshotClip("sniper"));
            GetOrGenerateClip("gun_shotgun", () => ProceduralAudioSynthesizer.CreateGunshotClip("shotgun"));
            GetOrGenerateClip("gun_pistol", () => ProceduralAudioSynthesizer.CreateGunshotClip("pistol"));
            GetOrGenerateClip("gun_turret", () => ProceduralAudioSynthesizer.CreateGunshotClip("turret"));

            GetOrGenerateClip("hit_body", () => ProceduralAudioSynthesizer.CreateHitMarkerClip(false, false));
            GetOrGenerateClip("hit_armor", () => ProceduralAudioSynthesizer.CreateHitMarkerClip(false, true));
            GetOrGenerateClip("hit_headshot", () => ProceduralAudioSynthesizer.CreateHitMarkerClip(true, false));

            GetOrGenerateClip("explosion", () => ProceduralAudioSynthesizer.CreateExplosionClip());
            GetOrGenerateClip("heartbeat", () => ProceduralAudioSynthesizer.CreateHeartbeatClip());
            GetOrGenerateClip("stinger_win", () => ProceduralAudioSynthesizer.CreateStingerClip(true));
            GetOrGenerateClip("stinger_lose", () => ProceduralAudioSynthesizer.CreateStingerClip(false));
        }

        private AudioClip GetOrGenerateClip(string key, System.Func<AudioClip> factory)
        {
            if (!_clipCache.TryGetValue(key, out var clip) || clip == null)
            {
                clip = factory();
                _clipCache[key] = clip;
                Debug.Log($"[ProceduralAudioSynthesizer] Synthesized PCM audio clip: [{key}]");
            }
            return clip;
        }

        // ==========================================
        // 2D HUD & FEEDBACK AUDIO
        // ==========================================

        public void PlayHitMarker(bool headshot, bool armor)
        {
            if (_hudSource2D == null) return;
            string key = headshot ? "hit_headshot" : (armor ? "hit_armor" : "hit_body");
            var clip = GetOrGenerateClip(key, () => ProceduralAudioSynthesizer.CreateHitMarkerClip(headshot, armor));
            _hudSource2D.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
            _hudSource2D.PlayOneShot(clip, hudVolume * masterVolume);
        }

        public void PlayMatchStinger(bool victory)
        {
            if (_hudSource2D == null) return;
            string key = victory ? "stinger_win" : "stinger_lose";
            var clip = GetOrGenerateClip(key, () => ProceduralAudioSynthesizer.CreateStingerClip(victory));
            _hudSource2D.pitch = 1f;
            _hudSource2D.PlayOneShot(clip, masterVolume);
        }

        public void SetHeartbeatActive(bool active)
        {
            if (_heartbeatSource == null) return;
            if (active && !_heartbeatSource.isPlaying)
            {
                var clip = GetOrGenerateClip("heartbeat", () => ProceduralAudioSynthesizer.CreateHeartbeatClip());
                _heartbeatSource.clip = clip;
                _heartbeatSource.Play();
            }
            else if (!active && _heartbeatSource.isPlaying)
            {
                _heartbeatSource.Stop();
            }
        }

        // ==========================================
        // 3D SPATIAL COMBAT AUDIO
        // ==========================================

        public void PlayGunshot(Vector3 position, string weaponType, float volumeScale = 1.0f)
        {
            string key = $"gun_{(weaponType ?? "rifle").ToLowerInvariant()}";
            var clip = GetOrGenerateClip(key, () => ProceduralAudioSynthesizer.CreateGunshotClip(weaponType));
            PlaySpatialSound(clip, position, volumeScale, pitchVariance: 0.08f);
        }

        public void PlayExplosion(Vector3 position, float volumeScale = 1.0f)
        {
            var clip = GetOrGenerateClip("explosion", () => ProceduralAudioSynthesizer.CreateExplosionClip());
            PlaySpatialSound(clip, position, volumeScale, pitchVariance: 0.04f);
        }

        public void PlayFootstep(Vector3 position, string surface = "pavement", float volumeScale = 0.5f)
        {
            string key = $"footstep_{surface}";
            var clip = GetOrGenerateClip(key, () => ProceduralAudioSynthesizer.CreateFootstepClip(surface));
            PlaySpatialSound(clip, position, volumeScale, pitchVariance: 0.12f);
        }

        public void PlayReloadClick(Vector3 position, int part, float volumeScale = 0.6f)
        {
            string key = $"reload_{part}";
            var clip = GetOrGenerateClip(key, () => ProceduralAudioSynthesizer.CreateReloadClip(part));
            PlaySpatialSound(clip, position, volumeScale, pitchVariance: 0.05f);
        }

        private void PlaySpatialSound(AudioClip clip, Vector3 position, float volumeScale, float pitchVariance = 0.05f)
        {
            if (clip == null || _spatialSources.Count == 0) return;

            var src = _spatialSources[_poolIndex];
            _poolIndex = (_poolIndex + 1) % _spatialSources.Count;

            src.transform.position = position;
            src.pitch = 1.0f + UnityEngine.Random.Range(-pitchVariance, pitchVariance);
            src.volume = volumeScale * sfxVolume * masterVolume;
            src.PlayOneShot(clip);
        }

        // ==========================================
        // ENVIRONMENTAL & WEATHER AMBIANCE
        // ==========================================

        public void StartAmbientAmbiance(string weatherType)
        {
            if (_ambientSource == null) return;
            string key = $"ambiance_{weatherType.ToLowerInvariant()}";
            var clip = GetOrGenerateClip(key, () => ProceduralAudioSynthesizer.CreateAmbianceLoopClip(weatherType));

            _ambientSource.clip = clip;
            _ambientSource.volume = ambientVolume * masterVolume;
            _ambientSource.Play();
        }

        // ==========================================
        // VEHICLE SPATIAL AUDIO ATTACHMENT
        // ==========================================

        public VehicleAudioEmitter AttachVehicleAudio(GameObject vehicleGo)
        {
            if (vehicleGo == null) return null;
            var emitter = vehicleGo.GetComponent<VehicleAudioEmitter>();
            if (emitter == null)
            {
                emitter = vehicleGo.AddComponent<VehicleAudioEmitter>();
            }
            return emitter;
        }
    }

    /// <summary>
    /// Attaches to moving vehicles to manage continuous 3D engine rev harmonics,
    /// nitro boost whistling, and tire skidding.
    /// </summary>
    public sealed class VehicleAudioEmitter : MonoBehaviour
    {
        private AudioSource _engineSource;
        private AudioSource _nitroSource;
        private AudioSource _screechSource;

        private void Awake()
        {
            _engineSource = gameObject.AddComponent<AudioSource>();
            _engineSource.spatialBlend = 1.0f;
            _engineSource.rolloffMode = AudioRolloffMode.Linear;
            _engineSource.minDistance = 3.0f;
            _engineSource.maxDistance = 55.0f;
            _engineSource.loop = true;
            _engineSource.playOnAwake = false;
            _engineSource.clip = ProceduralAudioSynthesizer.CreateVehicleEngineLoopClip();
            _engineSource.volume = 0.6f;
            _engineSource.Play();

            _nitroSource = gameObject.AddComponent<AudioSource>();
            _nitroSource.spatialBlend = 1.0f;
            _nitroSource.minDistance = 3.0f;
            _nitroSource.maxDistance = 50.0f;
            _nitroSource.loop = true;
            _nitroSource.playOnAwake = false;
            _nitroSource.clip = ProceduralAudioSynthesizer.CreateNitroBoostClip();
            _nitroSource.volume = 0.8f;

            _screechSource = gameObject.AddComponent<AudioSource>();
            _screechSource.spatialBlend = 1.0f;
            _screechSource.minDistance = 2.0f;
            _screechSource.maxDistance = 45.0f;
            _screechSource.loop = true;
            _screechSource.playOnAwake = false;
            _screechSource.clip = ProceduralAudioSynthesizer.CreateTireScreechClip();
            _screechSource.volume = 0.65f;
        }

        public void UpdateTelemetry(float speedMps, bool isNitro, bool isDrifting)
        {
            if (_engineSource != null)
            {
                // Idle pitch 0.85x up to 2.3x at 32 m/s (~115 km/h)
                float speedRatio = Mathf.Clamp01(Mathf.Abs(speedMps) / 32f);
                _engineSource.pitch = Mathf.Lerp(0.85f, 2.3f, speedRatio);
            }

            if (_nitroSource != null)
            {
                if (isNitro && !_nitroSource.isPlaying)
                {
                    _nitroSource.Play();
                }
                else if (!isNitro && _nitroSource.isPlaying)
                {
                    _nitroSource.Stop();
                }
            }

            if (_screechSource != null)
            {
                if (isDrifting && Mathf.Abs(speedMps) > 5f && !_screechSource.isPlaying)
                {
                    _screechSource.Play();
                }
                else if ((!isDrifting || Mathf.Abs(speedMps) <= 5f) && _screechSource.isPlaying)
                {
                    _screechSource.Stop();
                }
            }
        }
    }
}
