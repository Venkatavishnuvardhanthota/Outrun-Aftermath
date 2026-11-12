using UnityEngine;

namespace OutrunAftermath.Client
{
    /// <summary>
    /// PLACEHOLDER bootstrap. Does not implement a match.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private bool firstPerson = true;

        private void Awake()
        {
            Application.runInBackground = true;
            Debug.Log($"OUTRUN: AFTERMATH client boot. Camera mode intent={(firstPerson ? "FPS" : "TPS")}. PLACEHOLDER.");
        }
    }
}
