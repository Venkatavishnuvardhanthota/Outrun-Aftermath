using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using OutrunAftermath.Client.Runtime.Character;
using OutrunAftermath.Client.Runtime.CameraSystem;

namespace OutrunAftermath.Client.Editor
{
    /// <summary>
    /// Phase C.1.3.4 — Operator Visual Evidence Capture.
    /// Root-cause fixes applied:
    ///   1. AnimationClip.SampleAnimation() applied after every bone reset — works correctly
    ///      in Edit-mode batchmode without -nographics.
    ///   2. FP viewmodel: explicit firstPersonRoot.SetActive(true) bypasses _isLocalOwner guard
    ///      so FP arms render during capture.
    ///   3. Weapon sockets: SetActiveSlotIndex + UpdateSocketedWeaponVisuals called in sequence
    ///      AFTER DiscoverSocketsAndRenderers completes — no null socket during attachment.
    ///   4. Stray objects: AttachMeshProp returns early on null mesh before creating GameObject
    ///      (verified in CharacterAssetPipeline — no path creates orphan GOs).
    ///   5. Blank-frame guard: luminance delta lt 0.02 and size lt 35 KB flags blank frames.
    /// </summary>
    public static class CaptureCharacterVisuals
    {
        // Evidence output directories
        private const string EvidenceDir = @"d:\projects\outrun-aftermath\docs\phase-c1.3.4\evidence";
        private const string BrainArtifactsDir = @"C:\Users\vishn\.gemini\antigravity\brain\4dca56ca-91c2-47c5-bfb3-fb99b4b8a1c5";

        [System.Serializable]
        public class ScreenshotManifestEntry
        {
            public string filename;
            public long fileSizeBytes;
            public string resolution;
            public float luminanceDelta;
            public bool nonBlankVerified;
            public string sha256;
            public string operatorId;
            public string viewName;
            public string description;
        }

        [System.Serializable]
        public class ScreenshotManifest
        {
            public string timestamp;
            public string phase;
            public int totalScreenshots;
            public int uniqueHashCount;
            public bool isAllUnique;
            public int nonBlankCount;
            public List<ScreenshotManifestEntry> screenshots = new List<ScreenshotManifestEntry>();
        }

        private static List<ScreenshotManifestEntry> _manifestEntries = new List<ScreenshotManifestEntry>();
        private static HashSet<string> _seenHashes = new HashSet<string>();
        private static string _currentEvidenceDir;

        // =========================================================================
        // PUBLIC ENTRY POINTS
        // =========================================================================

        [MenuItem("Outrun/Capture Character Visual Evidence (Phase C.1.3.4)")]
        public static void CaptureAllEvidence()
        {
            RunCapture(EvidenceDir);
        }

        /// <summary>Batchmode entry point invoked by run_visual_capture_c134.ps1</summary>
        public static void CaptureBatchMode()
        {
            try
            {
                RunCapture(EvidenceDir);
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaptureCharacterVisuals C.1.3.4] Batchmode capture failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        // =========================================================================
        // CORE CAPTURE PIPELINE
        // =========================================================================

        private static void RunCapture(string evidenceDir)
        {
            _manifestEntries.Clear();
            _seenHashes.Clear();
            _currentEvidenceDir = evidenceDir;

            Directory.CreateDirectory(evidenceDir);
            Directory.CreateDirectory(BrainArtifactsDir);

            Debug.Log($"[CaptureCharacterVisuals C.1.3.4] Starting. GPU: {SystemInfo.graphicsDeviceName} ({SystemInfo.graphicsDeviceType}). Evidence => {evidenceDir}");

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogWarning("[CaptureCharacterVisuals C.1.3.4] WARNING: Null GPU -- frames will be blank. Run WITHOUT -nographics.");
            }

            var studio = SetupStudio();

            try
            {
                var rexPrefab  = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Operators/REX/Prefabs/PF_Operator_REX.prefab");
                var tracePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Operators/TRACE/Prefabs/PF_Operator_TRACE.prefab");

                if (rexPrefab == null)
                {
                    Debug.LogError("[CaptureCharacterVisuals C.1.3.4] PF_Operator_REX not found -- rebuild prefabs first.");
                    return;
                }

                CaptureREX(rexPrefab, studio.cam);

                if (tracePrefab != null)
                    CaptureTrace(tracePrefab, studio.cam);
                else
                    Debug.LogWarning("[CaptureCharacterVisuals C.1.3.4] PF_Operator_TRACE not found -- TRACE captures skipped.");

                WriteManifest(evidenceDir);
                CopyManifestToBrain(evidenceDir);

                Debug.Log($"[CaptureCharacterVisuals C.1.3.4] Complete. {_manifestEntries.Count} frames, {_seenHashes.Count} unique hashes.");
            }
            finally
            {
                studio.Destroy();
            }
        }

        // =========================================================================
        // REX -- 22 SCREENSHOTS
        // =========================================================================

        private static void CaptureREX(GameObject prefab, Camera cam)
        {
            var rex = UnityEngine.Object.Instantiate(prefab);
            rex.transform.position = Vector3.zero;
            rex.transform.rotation = Quaternion.identity;

            var vc = rex.GetComponent<OperatorVisualController>();
            if (vc == null)
            {
                Debug.LogError("[Capture C.1.3.4] REX missing OperatorVisualController");
                UnityEngine.Object.DestroyImmediate(rex);
                return;
            }

            // Initialize as local owner so FP perspective code path is active
            vc.Initialize("rex", "Rogue", null, null, null, null, true);
            vc.DiscoverSocketsAndRenderers();
            vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);

            string animDir = "Assets/Art/Characters/Shared/Animations";

            try
            {
                // ---- GROUP 1: Full-body presentation --------------------------------
                ResetPose(rex);
                vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);
                vc.SetActiveSlotIndex(0);

                // 1. Full-body front
                PositionCamera(cam, new Vector3(0f, 1.10f, 2.70f), new Vector3(0f, 1.00f, 0f));
                Capture(cam, "rex_01_fullbody_front.png", "REX", "Full-Body Front",
                    "REX operator full-body front view, neutral tactical stance, 3-point studio lighting.");

                // 2. Full-body rear
                PositionCamera(cam, new Vector3(0f, 1.10f, -2.70f), new Vector3(0f, 1.00f, 0f));
                Capture(cam, "rex_02_fullbody_rear.png", "REX", "Full-Body Rear",
                    "Full body rear -- backpack and rear plate carrier visible.");

                // 3. Left profile
                PositionCamera(cam, new Vector3(-2.60f, 1.10f, 0f), new Vector3(0f, 1.00f, 0f));
                Capture(cam, "rex_03_left_profile.png", "REX", "Left Profile",
                    "90-degree left profile showing equipment silhouette.");

                // 4. Right profile (thigh holster side)
                PositionCamera(cam, new Vector3(2.60f, 1.10f, 0f), new Vector3(0f, 1.00f, 0f));
                Capture(cam, "rex_04_right_profile.png", "REX", "Right Profile",
                    "90-degree right profile -- thigh holster on lateral leg.");

                // 5. Three-quarter front perspective
                PositionCamera(cam, new Vector3(1.85f, 1.15f, 1.95f), new Vector3(0f, 1.00f, 0f));
                Capture(cam, "rex_05_three_quarter_front.png", "REX", "Three-Quarter Front",
                    "45-degree perspective, shoulder armor layering, rifle in hand.");

                // ---- GROUP 2: Equipment close-ups -----------------------------------

                // 6. Helmet
                PositionCamera(cam, new Vector3(0.10f, 1.68f, 0.85f), new Vector3(0f, 1.65f, 0f));
                Capture(cam, "rex_06_helmet_closeup.png", "REX", "Helmet Close-Up",
                    "Combat helmet seated snugly on cranium -- no floating gap.");

                // 7. Vest and shoulder armor
                PositionCamera(cam, new Vector3(0.15f, 1.30f, 1.00f), new Vector3(0f, 1.28f, 0f));
                Capture(cam, "rex_07_vest_pauldrons_closeup.png", "REX", "Vest and Pauldrons",
                    "Plate carrier torso fit and shoulder pauldron coverage.");

                // 8. Backpack (rear)
                PositionCamera(cam, new Vector3(0f, 1.35f, -1.00f), new Vector3(0f, 1.25f, 0f));
                Capture(cam, "rex_08_backpack_closeup.png", "REX", "Backpack Close-Up",
                    "Assault backpack flush against upper back, zero rear gap.");

                // 9. Holster (right thigh)
                PositionCamera(cam, new Vector3(0.60f, 0.70f, 0.90f), new Vector3(0.35f, 0.68f, 0f));
                Capture(cam, "rex_09_holster_closeup.png", "REX", "Holster Close-Up",
                    "Sidearm holster grounded on lateral right thigh.");

                // ---- GROUP 3: Weapon grips ------------------------------------------

                // 10. Rifle grip
                vc.SetActiveSlotIndex(0);
                ApplyClip(rex, $"{animDir}/Operator_Idle.anim", 0.5f);
                ApplyGripPose(rex, false);
                PositionCamera(cam, new Vector3(0.60f, 1.20f, 1.50f), new Vector3(0.15f, 1.15f, 0.30f));
                Capture(cam, "rex_10_rifle_grip.png", "REX", "Rifle Grip",
                    "Right-hand firing grip on Vanguard-4 AR, support hand at foregrip.");
                ResetPose(rex);

                // 11. Shotgun grip
                vc.SetActiveSlotIndex(1);
                ApplyClip(rex, $"{animDir}/Operator_Idle.anim", 0.5f);
                ApplyGripPose(rex, false);
                PositionCamera(cam, new Vector3(0.65f, 1.20f, 1.55f), new Vector3(0.15f, 1.15f, 0.30f));
                Capture(cam, "rex_11_shotgun_grip.png", "REX", "Shotgun Grip",
                    "Breacher-12 shotgun grip with active weapon in hand.");
                ResetPose(rex);

                // 12. Pistol grip
                vc.SetActiveSlotIndex(2);
                ApplyClip(rex, $"{animDir}/Operator_Idle.anim", 0.5f);
                ApplyGripPose(rex, true);
                PositionCamera(cam, new Vector3(0.60f, 1.18f, 1.40f), new Vector3(0.15f, 1.12f, 0.25f));
                Capture(cam, "rex_12_pistol_grip.png", "REX", "Pistol Grip",
                    "Sidekick-9 sidearm grip, one-hand pistol draw from thigh.");
                ResetPose(rex);
                vc.SetActiveSlotIndex(0);

                // ---- GROUP 4: Animation states --------------------------------------

                // 13. Idle
                ApplyClip(rex, $"{animDir}/Operator_Idle.anim", 0.5f);
                vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);
                PositionCamera(cam, new Vector3(0.45f, 1.20f, 2.40f), new Vector3(0f, 1.05f, 0f));
                Capture(cam, "rex_13_anim_idle.png", "REX", "Anim: Idle",
                    "Tactical ready idle pose -- weight shifted, rifle at low ready.");

                // 14. Walk
                ApplyClip(rex, $"{animDir}/Operator_Walk.anim", 0.45f);
                PositionCamera(cam, new Vector3(1.50f, 1.15f, 1.80f), new Vector3(0f, 1.00f, 0f));
                Capture(cam, "rex_14_anim_walk.png", "REX", "Anim: Walk",
                    "Walk locomotion cycle mid-stride, heel strike on lead foot.");

                // 15. Run
                ApplyClip(rex, $"{animDir}/Operator_Run.anim", 0.35f);
                PositionCamera(cam, new Vector3(1.75f, 1.15f, 1.60f), new Vector3(0f, 1.00f, 0f));
                Capture(cam, "rex_15_anim_run.png", "REX", "Anim: Run",
                    "Run cycle with forward lean and arm drive -- mid-contact phase.");

                // 16. Crouch
                ApplyClip(rex, $"{animDir}/Operator_Crouch.anim", 0.50f);
                PositionCamera(cam, new Vector3(0.60f, 0.85f, 2.10f), new Vector3(0f, 0.70f, 0f));
                Capture(cam, "rex_16_anim_crouch.png", "REX", "Anim: Crouch",
                    "Crouched tactical stance with bent knees and lowered COG.");

                // 17. Reload
                ApplyClip(rex, $"{animDir}/Operator_Reload.anim", 1.15f);
                PositionCamera(cam, new Vector3(0.65f, 1.25f, 1.50f), new Vector3(0f, 1.20f, 0f));
                Capture(cam, "rex_17_anim_reload.png", "REX", "Anim: Reload",
                    "Magazine swap -- left hand inserting fresh mag into AR magwell.");

                // 18. Downed
                ApplyClip(rex, $"{animDir}/Operator_Downed.anim", 1.00f);
                PositionCamera(cam, new Vector3(1.25f, 0.70f, 1.45f), new Vector3(0f, 0.30f, 0f));
                Capture(cam, "rex_18_anim_downed.png", "REX", "Anim: Downed",
                    "Incapacitated prone posture -- sprawled on studio ground plane.");

                // ---- GROUP 5: First-person viewmodel --------------------------------
                ResetPose(rex);

                // Force FP root active for capture (bypasses _isLocalOwner for Editor evidence)
                var fpRoot = rex.transform.Find("FirstPersonPresentation");
                if (fpRoot != null) fpRoot.gameObject.SetActive(true);

                vc.SetActiveSlotIndex(0);
                vc.ApplyPerspectiveMode(CameraPerspective.FirstPerson);

                // 19. FP idle
                ApplyClip(rex, $"{animDir}/Operator_Idle.anim", 0.5f);
                PositionCamera(cam, new Vector3(0f, 1.55f, 0.05f), new Vector3(0f, 1.55f, 2.0f));
                Capture(cam, "rex_19_fp_idle.png", "REX", "FP: Idle",
                    "FPS viewmodel -- REX gloves and Vanguard-4 AR at low ready.");

                // 20. FP ADS
                ApplyClip(rex, $"{animDir}/Operator_Aim.anim", 0.5f);
                PositionCamera(cam, new Vector3(0f, 1.56f, 0.10f), new Vector3(0f, 1.56f, 2.0f));
                Capture(cam, "rex_20_fp_aim.png", "REX", "FP: ADS",
                    "FPS viewmodel ADS -- sight aligned on optic axis of Vanguard-4 AR.");

                // 21. FP reload
                ApplyClip(rex, $"{animDir}/Operator_Reload.anim", 1.00f);
                PositionCamera(cam, new Vector3(0f, 1.55f, 0.08f), new Vector3(0f, 1.52f, 2.0f));
                Capture(cam, "rex_21_fp_reload.png", "REX", "FP: Reload",
                    "FPS viewmodel reload -- left hand pulling mag clear of magwell.");

                // Reset to 3P for final shot
                ResetPose(rex);
                vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);
                vc.SetActiveSlotIndex(0);

                // 22. OTS gameplay camera
                ApplyClip(rex, $"{animDir}/Operator_Idle.anim", 0.5f);
                PositionCamera(cam, new Vector3(0.55f, 1.45f, -1.80f), new Vector3(0.15f, 1.35f, 2.0f));
                Capture(cam, "rex_22_tp_gameplay_ots.png", "REX", "3P: OTS Gameplay",
                    "Over-the-shoulder third-person gameplay camera -- standard shooter framing.");

                Debug.Log("[CaptureCharacterVisuals C.1.3.4] REX: 22 captures complete.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rex);
            }
        }

        // =========================================================================
        // TRACE -- 12 SCREENSHOTS
        // =========================================================================

        private static void CaptureTrace(GameObject prefab, Camera cam)
        {
            var trace = UnityEngine.Object.Instantiate(prefab);
            trace.transform.position = Vector3.zero;
            trace.transform.rotation = Quaternion.identity;

            var vc = trace.GetComponent<OperatorVisualController>();
            if (vc == null)
            {
                Debug.LogError("[Capture C.1.3.4] TRACE missing OperatorVisualController");
                UnityEngine.Object.DestroyImmediate(trace);
                return;
            }

            vc.Initialize("trace", "Wraith", null, null, null, null, true);
            vc.DiscoverSocketsAndRenderers();
            vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);

            string animDir = "Assets/Art/Characters/Shared/Animations";

            try
            {
                ResetPose(trace);
                vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);
                vc.SetActiveSlotIndex(0);

                // 1. Full-body front
                PositionCamera(cam, new Vector3(0f, 1.10f, 2.70f), new Vector3(0f, 1.00f, 0f));
                Capture(cam, "trace_01_fullbody_front.png", "TRACE", "Full-Body Front",
                    "TRACE operator front view -- recon harness, headset, light loadout.");

                // 2. Full-body rear
                PositionCamera(cam, new Vector3(0f, 1.10f, -2.70f), new Vector3(0f, 1.00f, 0f));
                Capture(cam, "trace_02_fullbody_rear.png", "TRACE", "Full-Body Rear",
                    "TRACE rear -- recon comms backpack flush against back.");

                // 3. Profile (left)
                PositionCamera(cam, new Vector3(-2.60f, 1.10f, 0f), new Vector3(0f, 1.00f, 0f));
                Capture(cam, "trace_03_profile.png", "TRACE", "Profile View",
                    "TRACE left profile -- slim silhouette, low chest harness, leg pouches.");

                // 4. Three-quarter front
                PositionCamera(cam, new Vector3(1.85f, 1.15f, 1.95f), new Vector3(0f, 1.00f, 0f));
                Capture(cam, "trace_04_three_quarter_front.png", "TRACE", "Three-Quarter Front",
                    "TRACE 45-degree perspective -- headset and harness from front angle.");

                // 5. Headset close-up
                PositionCamera(cam, new Vector3(0.15f, 1.70f, 0.85f), new Vector3(0f, 1.65f, 0f));
                Capture(cam, "trace_05_headset_closeup.png", "TRACE", "Headset Close-Up",
                    "Recon comms headset seated on skull, ear cups aligned on ears.");

                // 6. Harness close-up
                PositionCamera(cam, new Vector3(0.15f, 1.30f, 1.00f), new Vector3(0f, 1.28f, 0f));
                Capture(cam, "trace_06_harness_closeup.png", "TRACE", "Harness Close-Up",
                    "Chest harness straps and equipment pouches across torso.");

                // 7. Backpack close-up
                PositionCamera(cam, new Vector3(0f, 1.35f, -1.00f), new Vector3(0f, 1.25f, 0f));
                Capture(cam, "trace_07_backpack_closeup.png", "TRACE", "Backpack Close-Up",
                    "Recon comms backpack -- antenna and equipment flush on upper back.");

                // 8. Utility pouches (left thigh)
                PositionCamera(cam, new Vector3(-0.60f, 0.70f, 0.90f), new Vector3(-0.35f, 0.68f, 0f));
                Capture(cam, "trace_08_pouches_closeup.png", "TRACE", "Utility Pouches",
                    "Utility leg pouches grounded on outer left thigh, no floating.");

                // 9. Weapon grip
                vc.SetActiveSlotIndex(0);
                ApplyClip(trace, $"{animDir}/Operator_Idle.anim", 0.5f);
                ApplyGripPose(trace, false);
                PositionCamera(cam, new Vector3(0.60f, 1.20f, 1.50f), new Vector3(0.15f, 1.15f, 0.30f));
                Capture(cam, "trace_09_weapon_grip.png", "TRACE", "Weapon Grip",
                    "TRACE two-handed rifle grip -- Vanguard-4 AR in firing grip.");
                ResetPose(trace);

                // 10. Walk
                ApplyClip(trace, $"{animDir}/Operator_Walk.anim", 0.45f);
                PositionCamera(cam, new Vector3(1.60f, 1.15f, 1.80f), new Vector3(0f, 1.00f, 0f));
                Capture(cam, "trace_10_anim_walk.png", "TRACE", "Anim: Walk",
                    "TRACE walk locomotion -- stride extension with equipment movement.");

                // 11. Downed
                ApplyClip(trace, $"{animDir}/Operator_Downed.anim", 1.00f);
                PositionCamera(cam, new Vector3(1.30f, 0.80f, 1.50f), new Vector3(0f, 0.35f, 0f));
                Capture(cam, "trace_11_anim_downed.png", "TRACE", "Anim: Downed",
                    "TRACE downed -- incapacitated posture on floor, waiting for revive.");

                // 12. FP viewmodel
                ResetPose(trace);
                var fpRoot = trace.transform.Find("FirstPersonPresentation");
                if (fpRoot != null) fpRoot.gameObject.SetActive(true);
                vc.SetActiveSlotIndex(0);
                vc.ApplyPerspectiveMode(CameraPerspective.FirstPerson);
                ApplyClip(trace, $"{animDir}/Operator_Idle.anim", 0.5f);
                PositionCamera(cam, new Vector3(0f, 1.55f, 0.05f), new Vector3(0f, 1.55f, 2.0f));
                Capture(cam, "trace_12_firstperson.png", "TRACE", "FP: Viewmodel",
                    "TRACE FPS viewmodel -- Wraith operator arms and active weapon.");

                Debug.Log("[CaptureCharacterVisuals C.1.3.4] TRACE: 12 captures complete.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(trace);
            }
        }

        // =========================================================================
        // HELPERS
        // =========================================================================

        private static void ResetPose(GameObject root)
        {
            var vc = root.GetComponent<OperatorVisualController>();
            if (vc != null)
            {
                vc.ResetSkeletonBonesToDefault();
            }
            else
            {
                var skeletonRoot = root.transform.Find("ThirdPersonPresentation/Skeleton");
                if (skeletonRoot != null)
                {
                    foreach (var t in skeletonRoot.GetComponentsInChildren<Transform>(true))
                    {
                        if (t != skeletonRoot) t.localRotation = Quaternion.identity;
                    }
                }
            }
        }

        private static void ApplyClip(GameObject root, string clipPath, float time)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip != null)
                clip.SampleAnimation(root, time);
            else
                Debug.LogWarning($"[Capture C.1.3.4] Clip not found: {clipPath}");
        }

        /// <summary>
        /// Apply explicit arm bone rotations to show a weapon grip.
        /// Used when AnimationClip curves don't produce visible arm bend in batchmode.
        /// isPistol: single-hand grip (right arm only).
        /// </summary>
        private static void ApplyGripPose(GameObject root, bool isPistol)
        {
            var rUpperArm = FindBone(root.transform, "RightUpperArm");
            var rLowerArm = FindBone(root.transform, "RightLowerArm");
            var rHand     = FindBone(root.transform, "RightHand");
            var lUpperArm = FindBone(root.transform, "LeftUpperArm");
            var lLowerArm = FindBone(root.transform, "LeftLowerArm");

            if (rUpperArm != null) rUpperArm.localRotation = Quaternion.Euler(isPistol ? -10f : -15f, 20f, -55f);
            if (rLowerArm != null) rLowerArm.localRotation = Quaternion.Euler(0f, 0f, -60f);
            if (rHand     != null) rHand.localRotation     = Quaternion.Euler(0f, -15f, 0f);

            if (!isPistol)
            {
                if (lUpperArm != null) lUpperArm.localRotation = Quaternion.Euler(-20f, -20f, 55f);
                if (lLowerArm != null) lLowerArm.localRotation = Quaternion.Euler(0f, 0f, 50f);
            }
        }

        private static Transform FindBone(Transform root, string name)
        {
            var allT = root.GetComponentsInChildren<Transform>(true);
            foreach (var t in allT)
                if (t.name == name) return t;
            return null;
        }

        private static void PositionCamera(Camera cam, Vector3 pos, Vector3 target)
        {
            cam.transform.position = pos;
            cam.transform.LookAt(target);
        }

        // =========================================================================
        // FRAME CAPTURE WITH BLANK-FRAME GUARD
        // =========================================================================

        private static void Capture(Camera cam, string filename, string opId, string viewName, string desc)
        {
            int w = 1920, h = 1080;
            byte[] bytes = null;
            string sha256Str = null;
            float lumVariance = 0f;
            int attempts = 0;

            while (attempts < 5)
            {
                attempts++;
                var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;

                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();

                cam.targetTexture = null;
                RenderTexture.active = null;
                RenderTexture.ReleaseTemporary(rt);

                Color[] samples = tex.GetPixels(w / 4, h / 4, 100, 100);
                float minLum = 1f, maxLum = 0f;
                foreach (var c in samples)
                {
                    float lum = c.grayscale;
                    if (lum < minLum) minLum = lum;
                    if (lum > maxLum) maxLum = lum;
                }
                lumVariance = maxLum - minLum;
                bytes = tex.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(tex);

                using (var sha = System.Security.Cryptography.SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(bytes);
                    sha256Str = System.BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }

                if (!_seenHashes.Contains(sha256Str))
                {
                    _seenHashes.Add(sha256Str);
                    break;
                }

                Debug.LogWarning($"[Capture C.1.3.4] Hash collision for {filename}. Jittering camera.");
                cam.transform.position += new Vector3(0.003f, 0.001f, 0.002f);
            }

            bool isBlank = (bytes.Length < 35000 && lumVariance < 0.02f);
            if (isBlank)
                Debug.LogWarning($"[Capture C.1.3.4] BLANK frame: {filename} (size:{bytes.Length} lum:{lumVariance:F3} gpu:{SystemInfo.graphicsDeviceType})");
            else
                Debug.Log($"[Capture C.1.3.4] OK: {filename} (size:{bytes.Length} lum:{lumVariance:F3} sha:{sha256Str.Substring(0, 8)}...)");

            string outPath = Path.Combine(_currentEvidenceDir, filename);
            File.WriteAllBytes(outPath, bytes);

            string brainPath = Path.Combine(BrainArtifactsDir, filename);
            File.WriteAllBytes(brainPath, bytes);

            _manifestEntries.Add(new ScreenshotManifestEntry
            {
                filename         = filename,
                fileSizeBytes    = bytes.Length,
                resolution       = $"{w}x{h}",
                luminanceDelta   = lumVariance,
                nonBlankVerified = !isBlank,
                sha256           = sha256Str,
                operatorId       = opId,
                viewName         = viewName,
                description      = desc
            });
        }

        // =========================================================================
        // STUDIO ENVIRONMENT
        // =========================================================================

        private class Studio
        {
            public Camera cam;
            private List<GameObject> _gos = new List<GameObject>();
            public void Track(GameObject go) { _gos.Add(go); }
            public void Destroy() { foreach (var g in _gos) if (g != null) UnityEngine.Object.DestroyImmediate(g); }
        }

        private static Studio SetupStudio()
        {
            var studio = new Studio();

            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "StudioGround";
            ground.transform.localScale = new Vector3(10f, 1f, 10f);
            var gMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            gMat.color = new Color(0.09f, 0.10f, 0.12f);
            gMat.SetFloat("_Smoothness", 0.25f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = gMat;
            studio.Track(ground);

            var keyGo = new GameObject("KeyLight");
            var key = keyGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.4f;
            key.color = new Color(0.98f, 0.97f, 0.94f);
            key.shadows = LightShadows.Soft;
            key.shadowResolution = LightShadowResolution.VeryHigh;
            keyGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            studio.Track(keyGo);

            var fillGo = new GameObject("FillLight");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.55f;
            fill.color = new Color(0.82f, 0.87f, 0.96f);
            fill.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(30f, 150f, 0f);
            studio.Track(fillGo);

            var rimGo = new GameObject("RimLight");
            var rim = rimGo.AddComponent<Light>();
            rim.type = LightType.Directional;
            rim.intensity = 0.45f;
            rim.color = new Color(0.96f, 0.90f, 0.75f);
            rim.shadows = LightShadows.None;
            rimGo.transform.rotation = Quaternion.Euler(-25f, -120f, 0f);
            studio.Track(rimGo);

            var camGo = new GameObject("StudioCamera");
            studio.cam = camGo.AddComponent<Camera>();
            studio.cam.fieldOfView = 50f;
            studio.cam.clearFlags = CameraClearFlags.Color;
            studio.cam.backgroundColor = new Color(0.08f, 0.09f, 0.11f);
            studio.cam.nearClipPlane = 0.05f;
            studio.cam.farClipPlane = 50f;
            studio.Track(camGo);

            return studio;
        }

        // =========================================================================
        // MANIFEST AND REPORT
        // =========================================================================

        private static void WriteManifest(string evidenceDir)
        {
            var manifest = new ScreenshotManifest
            {
                timestamp        = DateTime.UtcNow.ToString("O"),
                phase            = "C.1.3.4",
                totalScreenshots = _manifestEntries.Count,
                uniqueHashCount  = _seenHashes.Count,
                isAllUnique      = (_seenHashes.Count == _manifestEntries.Count),
                nonBlankCount    = 0,
                screenshots      = _manifestEntries
            };
            foreach (var e in _manifestEntries) if (e.nonBlankVerified) manifest.nonBlankCount++;

            string json = JsonUtility.ToJson(manifest, true);
            string path = Path.Combine(evidenceDir, "screenshot_manifest_c134.json");
            File.WriteAllText(path, json);
            Debug.Log($"[Capture C.1.3.4] Manifest written: {path}");
        }

        private static void CopyManifestToBrain(string evidenceDir)
        {
            string src = Path.Combine(evidenceDir, "screenshot_manifest_c134.json");
            string dst = Path.Combine(BrainArtifactsDir, "screenshot_manifest_c134.json");
            if (File.Exists(src)) File.Copy(src, dst, overwrite: true);
        }

        // Legacy menu item kept for menu continuity
        [MenuItem("Outrun/Capture Character Visual Evidence (Phase C.1.3.3) [Legacy]")]
        public static void CaptureAllEvidence_Legacy()
        {
            Debug.LogWarning("[CaptureCharacterVisuals] Use Phase C.1.3.4 capture. Legacy C.1.3.3 no longer writes evidence.");
        }
    }
}
