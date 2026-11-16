using UnityEngine;
using OutrunAftermath.Client.Runtime.Audio;

namespace OutrunAftermath.Client.Runtime.Environment
{
    public enum WeatherCondition
    {
        Clear,
        Sandstorm,
        ToxicRain,
        Overcast,
        Night
    }

    /// <summary>
    /// Master atmospheric and environmental lighting director for OUTRUN: AFTERMATH.
    /// Controls dynamic weather states, directional sunlight, ambient sky harmonics,
    /// distance fog density, and atmospheric audio ambiance.
    /// </summary>
    public sealed class AtmosphereDirector : MonoBehaviour
    {
        public static AtmosphereDirector Instance { get; private set; }

        [Header("Active Atmospheric State")]
        [SerializeField] private WeatherCondition currentWeather = WeatherCondition.Clear;

        private Light _sunLight;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void ApplyWeather(WeatherCondition weather)
        {
            currentWeather = weather;
            EnsureSunLight();

            switch (weather)
            {
                case WeatherCondition.Sandstorm:
                    ConfigureAtmosphere(
                        sunColor: new Color(1.0f, 0.65f, 0.35f),
                        sunIntensity: 0.65f,
                        sunRot: Quaternion.Euler(35f, -25f, 0f),
                        skyColor: new Color(0.72f, 0.52f, 0.28f),
                        eqColor: new Color(0.55f, 0.40f, 0.20f),
                        groundColor: new Color(0.35f, 0.22f, 0.12f),
                        fogColor: new Color(0.68f, 0.48f, 0.26f),
                        fogDensity: 0.024f,
                        ambianceType: "sandstorm");
                    break;

                case WeatherCondition.ToxicRain:
                    ConfigureAtmosphere(
                        sunColor: new Color(0.60f, 0.85f, 0.55f),
                        sunIntensity: 0.50f,
                        sunRot: Quaternion.Euler(60f, -40f, 0f),
                        skyColor: new Color(0.32f, 0.48f, 0.35f),
                        eqColor: new Color(0.25f, 0.38f, 0.28f),
                        groundColor: new Color(0.14f, 0.20f, 0.15f),
                        fogColor: new Color(0.28f, 0.42f, 0.30f),
                        fogDensity: 0.018f,
                        ambianceType: "toxicrain");
                    break;

                case WeatherCondition.Overcast:
                    ConfigureAtmosphere(
                        sunColor: new Color(0.92f, 0.94f, 1.0f),
                        sunIntensity: 0.85f,
                        sunRot: Quaternion.Euler(55f, -30f, 0f),
                        skyColor: new Color(0.50f, 0.55f, 0.62f),
                        eqColor: new Color(0.40f, 0.44f, 0.48f),
                        groundColor: new Color(0.22f, 0.24f, 0.26f),
                        fogColor: new Color(0.52f, 0.56f, 0.62f),
                        fogDensity: 0.009f,
                        ambianceType: "clear");
                    break;

                case WeatherCondition.Night:
                    ConfigureAtmosphere(
                        sunColor: new Color(0.35f, 0.45f, 0.70f),
                        sunIntensity: 0.22f,
                        sunRot: Quaternion.Euler(25f, 160f, 0f),
                        skyColor: new Color(0.08f, 0.11f, 0.18f),
                        eqColor: new Color(0.06f, 0.08f, 0.12f),
                        groundColor: new Color(0.03f, 0.04f, 0.06f),
                        fogColor: new Color(0.08f, 0.10f, 0.16f),
                        fogDensity: 0.015f,
                        ambianceType: "clear");
                    break;

                case WeatherCondition.Clear:
                default:
                    ConfigureAtmosphere(
                        sunColor: new Color(1.0f, 0.96f, 0.88f),
                        sunIntensity: 1.25f,
                        sunRot: Quaternion.Euler(46f, -38f, 0f),
                        skyColor: new Color(0.55f, 0.64f, 0.76f),
                        eqColor: new Color(0.38f, 0.37f, 0.35f),
                        groundColor: new Color(0.18f, 0.17f, 0.16f),
                        fogColor: new Color(0.65f, 0.70f, 0.76f),
                        fogDensity: 0.0045f,
                        ambianceType: "clear");
                    break;
            }

            Debug.Log($"[AtmosphereDirector] Applied Weather Condition: {weather.ToString().ToUpper()}");
        }

        private void EnsureSunLight()
        {
            if (_sunLight == null)
            {
                var sunGo = GameObject.Find("Sun_DirectionalLight");
                if (sunGo != null)
                {
                    _sunLight = sunGo.GetComponent<Light>();
                }
                else
                {
                    sunGo = new GameObject("Sun_DirectionalLight");
                    _sunLight = sunGo.AddComponent<Light>();
                    _sunLight.type = LightType.Directional;
                    _sunLight.shadows = LightShadows.Soft;
                }
            }
        }

        private void ConfigureAtmosphere(
            Color sunColor, float sunIntensity, Quaternion sunRot,
            Color skyColor, Color eqColor, Color groundColor,
            Color fogColor, float fogDensity, string ambianceType)
        {
            if (_sunLight != null)
            {
                _sunLight.color = sunColor;
                _sunLight.intensity = sunIntensity;
                _sunLight.transform.rotation = sunRot;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = skyColor;
            RenderSettings.ambientEquatorColor = eqColor;
            RenderSettings.ambientGroundColor = groundColor;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;

            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.StartAmbientAmbiance(ambianceType);
            }
        }
    }
}
