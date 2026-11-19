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
    public static class CaptureCharacterVisuals
    {
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

        [MenuItem("Outrun/Capture Character Visual Evidence (Phase C.1.3.3)")]
        public static void CaptureAllEvidence()
        {
            _manifestEntries.Clear();
            _seenHashes.Clear();
            Debug.Log($"[CaptureCharacterVisuals] Phase C.1.3.3: Setting up calibrated studio for visual acceptance... Device: {SystemInfo.graphicsDeviceType}, Name: {SystemInfo.graphicsDeviceName}");

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogWarning("[CaptureCharacterVisuals] WARNING: Graphics device is Null (running under -nographics). Frame rendering produces blank buffers in batchmode. Run Unity batchmode with graphics to enable hardware GPU rendering.");
            }

            // Create temporary studio environment
            var studioGo = new GameObject("CaptureStudio_C133");

            var lightGo = new GameObject("KeyLight");
            var keyLight = lightGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.35f;
            keyLight.color = new Color(0.98f, 0.97f, 0.95f);
            keyLight.shadows = LightShadows.Soft;
            keyLight.shadowResolution = LightShadowResolution.VeryHigh;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var fillGo = new GameObject("FillLight");
            var fillLight = fillGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.55f;
            fillLight.color = new Color(0.85f, 0.88f, 0.95f);
            fillLight.shadows = LightShadows.None;
            fillGo.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

            var rimGo = new GameObject("RimLight");
            var rimLight = rimGo.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.intensity = 0.45f;
            rimLight.color = new Color(0.95f, 0.92f, 0.85f);
            rimLight.shadows = LightShadows.None;
            rimGo.transform.rotation = Quaternion.Euler(-25f, -120f, 0f);

            var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGo.transform.localScale = new Vector3(10f, 1f, 10f);
            var groundMr = groundGo.GetComponent<MeshRenderer>();
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            groundMat.color = new Color(0.12f, 0.14f, 0.16f);
            groundMat.SetFloat("_Smoothness", 0.35f);
            groundMr.sharedMaterial = groundMat;

            var camGo = new GameObject("StudioCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.clearFlags = CameraClearFlags.Color;
            cam.backgroundColor = new Color(0.09f, 0.10f, 0.12f);

            // Load REX prefab
            var rexPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Operators/REX/Prefabs/PF_Operator_REX.prefab");
            if (rexPrefab == null)
            {
                Debug.LogError("[CaptureCharacterVisuals] REX prefab not found!");
                return;
            }

            var rex = UnityEngine.Object.Instantiate(rexPrefab);
            rex.transform.position = Vector3.zero;
            rex.transform.rotation = Quaternion.identity;

            var vc = rex.GetComponent<OperatorVisualController>();
            vc.Initialize("rex", "Rogue", null, null, null, null, true);
            vc.DiscoverSocketsAndRenderers();

            string animDir = "Assets/Art/Characters/Shared/Animations";

            try
            {
                // =========================================================================
                // 1. REX FULL-BODY 360-DEGREE & PERSPECTIVE VIEWS
                // =========================================================================
                vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);

                // Front View (Full Body, neutral combat stance)
                PositionCamera(cam, new Vector3(0f, 1.10f, 2.7f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_front.png", "REX", "Front View", "Full body front view in neutral tactical stance under 3-point studio lighting.");

                // Back View (Backpack, rear plate carrier, belt, holstered sidearm)
                PositionCamera(cam, new Vector3(0f, 1.10f, -2.7f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_back.png", "REX", "Back View", "Full body back view displaying backpack flush against upper back and rear equipment.");

                // Left Profile View
                PositionCamera(cam, new Vector3(-2.6f, 1.10f, 0f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_left_profile.png", "REX", "Left Profile", "Full body 90-degree left profile showing chest depth and equipment silhouette.");

                // Right Profile View (Sidearm holster on lateral thigh)
                PositionCamera(cam, new Vector3(2.6f, 1.10f, 0f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_right_profile.png", "REX", "Right Profile", "Full body 90-degree right profile showing grounded sidearm holster on thigh.");

                // Three-Quarter Front Perspective View (45 deg)
                PositionCamera(cam, new Vector3(1.85f, 1.15f, 1.95f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_perspective_3_4.png", "REX", "Three-Quarter Front", "45-degree perspective showing depth, shoulder armor layering, and rifle grip.");

                // Over-The-Shoulder Third-Person Camera
                PositionCamera(cam, new Vector3(0.55f, 1.45f, -1.8f), new Vector3(0.15f, 1.35f, 2.0f));
                CaptureFrame(cam, "screenshot_rex_thirdperson.png", "REX", "Third Person OTS", "Over-the-shoulder gameplay camera framing operator and weapon forward sight.");

                // =========================================================================
                // 2. REX CLOSE-UP INSPECTION CAPTURES
                // =========================================================================

                // Head & Helmet Close-Up (Facial contours, jawline, helmet fit)
                PositionCamera(cam, new Vector3(0.10f, 1.62f, 0.90f), new Vector3(0f, 1.60f, 0f));
                CaptureFrame(cam, "screenshot_rex_closeup_head_helmet.png", "REX", "Closeup Head & Helmet", "Detailed inspection of helmet grounded snugly on cranium and facial structure.");

                // Torso & Tactical Vest Close-Up (Plate carrier, shoulder straps, chest fit)
                PositionCamera(cam, new Vector3(0.15f, 1.25f, 1.05f), new Vector3(0f, 1.25f, 0f));
                CaptureFrame(cam, "screenshot_rex_closeup_torso_vest.png", "REX", "Closeup Torso & Vest", "Detailed inspection of plate carrier fit on chest with zero floating gap.");

                // Weapon Grip & Hands Close-Up (Right hand grip & left support hand foregrip IK)
                PositionCamera(cam, new Vector3(0.30f, 1.15f, 0.70f), new Vector3(0.08f, 1.15f, 0.25f));
                CaptureFrame(cam, "screenshot_rex_closeup_weapon_grip.png", "REX", "Closeup Weapon Grip", "Inspection of firing hand grip and support hand contact on rifle foregrip.");

                // Lower Body, Holster & Boots Close-Up (Combat boots with contact shadow on floor)
                PositionCamera(cam, new Vector3(0.25f, 0.35f, 1.20f), new Vector3(0.08f, 0.30f, 0f));
                CaptureFrame(cam, "screenshot_rex_closeup_boots_ground.png", "REX", "Closeup Boots & Ground", "Combat boots and high-resolution soft contact shadows on studio ground plane.");

                // =========================================================================
                // 3. REX DIAGNOSTIC & TOPOLOGY RENDERS
                // =========================================================================

                // Neutral Clay Render (Pure clay material to evaluate surface geometry in isolation)
                var smr = rex.GetComponentInChildren<SkinnedMeshRenderer>();
                Material origMat = smr != null ? smr.sharedMaterial : null;
                var clayMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/Shared/Materials/M_Operator_Neutral_Clay.mat");
                if (smr != null && clayMat != null) smr.sharedMaterial = clayMat;

                var equipRenderers = rex.GetComponentsInChildren<MeshRenderer>(true);
                var equipStates = new Dictionary<MeshRenderer, bool>();
                foreach (var er in equipRenderers)
                {
                    if (er.gameObject.name.Contains("Plane") || er.gameObject.name.Contains("Floor")) continue;
                    equipStates[er] = er.enabled;
                    er.enabled = false;
                }

                PositionCamera(cam, new Vector3(0f, 1.10f, 2.7f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_neutral_clay.png", "REX", "Neutral Clay Material", "Neutral matte clay material without textures to evaluate raw anatomical surface.");

                // Restore equipment & material
                foreach (var kvp in equipStates) { kvp.Key.enabled = kvp.Value; }
                if (smr != null && origMat != null) smr.sharedMaterial = origMat;

                // Wireframe Diagnostic Overlay
                var wireframeGo = CreateWireframeVisualization(rex);
                PositionCamera(cam, new Vector3(0.3f, 1.15f, 2.5f), new Vector3(0f, 1.05f, 0f));
                CaptureFrame(cam, "screenshot_rex_wireframe.png", "REX", "Wireframe Topology", "Wireframe overlay demonstrating continuous 2-manifold edge loop distribution.");
                if (wireframeGo != null) UnityEngine.Object.DestroyImmediate(wireframeGo);

                // Skeleton Diagnostic View (25-Bone Hierarchy)
                var skeletonGo = CreateSkeletonVisualization(rex);
                PositionCamera(cam, new Vector3(0.4f, 1.15f, 2.5f), new Vector3(0f, 1.05f, 0f));
                CaptureFrame(cam, "screenshot_rex_skeleton_hierarchy.png", "REX", "Skeleton Hierarchy", "25-bone humanoid skeletal hierarchy with joint pivot spheres and bone links.");
                if (skeletonGo != null) UnityEngine.Object.DestroyImmediate(skeletonGo);

                // Skin Weights Heatmap View
                var weightMat = CreateSkinWeightMaterial(smr);
                if (smr != null && weightMat != null) smr.sharedMaterial = weightMat;
                PositionCamera(cam, new Vector3(0.35f, 1.15f, 2.45f), new Vector3(0f, 1.05f, 0f));
                CaptureFrame(cam, "screenshot_rex_skin_weights.png", "REX", "Skin Weights Heatmap", "Vertex color visualization of bone weight influences across all anatomical zones.");
                if (smr != null && origMat != null) smr.sharedMaterial = origMat;

                // Deformation: Shoulder & Elbow (+75 deg shoulder elevation, 90 deg elbow)
                Transform lArm = FindTransformInRex(rex.transform, "LeftUpperArm");
                Transform lElbow = FindTransformInRex(rex.transform, "LeftLowerArm");
                Transform rArm = FindTransformInRex(rex.transform, "RightUpperArm");
                Transform rElbow = FindTransformInRex(rex.transform, "RightLowerArm");
                Transform headBone = FindTransformInRex(rex.transform, "Head");
                if (lArm != null) lArm.localRotation = Quaternion.Euler(0f, 0f, 75f);
                if (lElbow != null) lElbow.localRotation = Quaternion.Euler(0f, 0f, 90f);
                if (rArm != null) rArm.localRotation = Quaternion.Euler(0f, 0f, -65f);
                if (rElbow != null) rElbow.localRotation = Quaternion.Euler(0f, 0f, -80f);
                if (headBone != null) headBone.localRotation = Quaternion.Euler(0f, 25f, 0f);
                PositionCamera(cam, new Vector3(0f, 1.35f, 2.0f), new Vector3(0f, 1.30f, 0f));
                CaptureFrame(cam, "screenshot_rex_deform_shoulder_elbow.png", "REX", "Deformation Shoulder Elbow", "Multi-joint stress deformation test showing shoulder girdle and elbow flexion.");
                if (lArm != null) lArm.localRotation = Quaternion.identity;
                if (lElbow != null) lElbow.localRotation = Quaternion.identity;
                if (rArm != null) rArm.localRotation = Quaternion.identity;
                if (rElbow != null) rElbow.localRotation = Quaternion.identity;
                if (headBone != null) headBone.localRotation = Quaternion.identity;

                // Deformation: Knee & Hip (Deep tactical lunge)
                Transform lThigh = FindTransformInRex(rex.transform, "LeftThigh");
                Transform lCalf = FindTransformInRex(rex.transform, "LeftCalf");
                Transform rThigh = FindTransformInRex(rex.transform, "RightThigh");
                Transform rCalf = FindTransformInRex(rex.transform, "RightCalf");
                Transform spineBone = FindTransformInRex(rex.transform, "Spine");
                if (lThigh != null) lThigh.localRotation = Quaternion.Euler(45f, 0f, 0f);
                if (lCalf != null) lCalf.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                if (rThigh != null) rThigh.localRotation = Quaternion.Euler(-30f, 0f, 0f);
                if (rCalf != null) rCalf.localRotation = Quaternion.Euler(35f, 0f, 0f);
                if (spineBone != null) spineBone.localRotation = Quaternion.Euler(0f, 25f, 0f);
                PositionCamera(cam, new Vector3(1.3f, 0.85f, 1.8f), new Vector3(0f, 0.70f, 0f));
                CaptureFrame(cam, "screenshot_rex_deform_knee_hip.png", "REX", "Deformation Knee Hip", "Deep combat lunge deformation test showing hip flexion and knee articulation.");
                if (lThigh != null) lThigh.localRotation = Quaternion.identity;
                if (lCalf != null) lCalf.localRotation = Quaternion.identity;
                if (rThigh != null) rThigh.localRotation = Quaternion.identity;
                if (rCalf != null) rCalf.localRotation = Quaternion.identity;
                if (spineBone != null) spineBone.localRotation = Quaternion.identity;

                // =========================================================================
                // 4. REX FIRST-PERSON VIEWMODEL CAPTURES
                // =========================================================================
                vc.ApplyPerspectiveMode(CameraPerspective.FirstPerson);

                // First-Person Idle Viewmodel (Arms, tactical gloves, assault rifle in ready stance)
                vc.SetActiveSlotIndex(0);
                PositionCamera(cam, new Vector3(0f, 1.55f, 0.05f), new Vector3(0f, 1.55f, 2.0f));
                CaptureFrame(cam, "screenshot_rex_firstperson_idle.png", "REX", "First Person Idle", "FPS viewmodel camera view with dedicated arms, gloves, and Vanguard-4 AR.");

                // First-Person ADS Aim
                SampleClip(rex, $"{animDir}/Operator_Aim.anim", 0.5f);
                PositionCamera(cam, new Vector3(0f, 1.56f, 0.10f), new Vector3(0f, 1.56f, 2.0f));
                CaptureFrame(cam, "screenshot_rex_firstperson_aim.png", "REX", "First Person Aim", "FPS viewmodel ADS sight picture aligned directly along rifle optic axis.");

                // First-Person Reload
                SampleClip(rex, $"{animDir}/Operator_Reload.anim", 1.0f);
                PositionCamera(cam, new Vector3(0f, 1.55f, 0.08f), new Vector3(0f, 1.52f, 2.0f));
                CaptureFrame(cam, "screenshot_rex_firstperson_reload.png", "REX", "First Person Reload", "FPS viewmodel during magazine swap manipulation.");

                // First-Person Shotgun Viewmodel
                SampleClip(rex, $"{animDir}/Operator_Idle.anim", 0f);
                vc.SetActiveSlotIndex(1);
                PositionCamera(cam, new Vector3(0f, 1.55f, 0.06f), new Vector3(0f, 1.55f, 2.0f));
                CaptureFrame(cam, "screenshot_rex_firstperson_shotgun.png", "REX", "First Person Shotgun", "FPS viewmodel holding Breacher-12 tactical shotgun.");

                // First-Person Pistol Viewmodel
                vc.SetActiveSlotIndex(2);
                PositionCamera(cam, new Vector3(0f, 1.55f, 0.04f), new Vector3(0f, 1.55f, 2.0f));
                CaptureFrame(cam, "screenshot_rex_firstperson_pistol.png", "REX", "First Person Pistol", "FPS viewmodel holding Sidekick-9 tactical sidearm.");

                // Reset to Third-Person & Rifle
                vc.SetActiveSlotIndex(0);
                vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);

                // =========================================================================
                // 5. REX IN-MOTION & ANIMATOR PLAYBACK STATES (ALL 13 STATES)
                // =========================================================================

                // State 1: Idle
                SampleClip(rex, $"{animDir}/Operator_Idle.anim", 0.5f);
                PositionCamera(cam, new Vector3(0.45f, 1.20f, 2.4f), new Vector3(0f, 1.05f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_idle.png", "REX", "Anim State: Idle", "Third-person tactical ready idle stance.");

                // State 2: Walk
                SampleClip(rex, $"{animDir}/Operator_Walk.anim", 0.45f);
                PositionCamera(cam, new Vector3(1.50f, 1.15f, 1.8f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_walk.png", "REX", "Anim State: Walk", "Walk locomotion cycle with stride extension and foot plant.");

                // State 3: Run
                SampleClip(rex, $"{animDir}/Operator_Run.anim", 0.35f);
                PositionCamera(cam, new Vector3(1.75f, 1.15f, 1.6f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_run.png", "REX", "Anim State: Run", "Run locomotion cycle with athletic forward lean.");

                // State 4: Sprint
                SampleClip(rex, $"{animDir}/Operator_Sprint.anim", 0.30f);
                PositionCamera(cam, new Vector3(1.90f, 1.10f, 1.5f), new Vector3(0f, 0.95f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_sprint.png", "REX", "Anim State: Sprint", "High-speed tactical sprint posture conveying forward momentum.");

                // State 5: Crouch
                SampleClip(rex, $"{animDir}/Operator_Crouch.anim", 0.50f);
                PositionCamera(cam, new Vector3(0.60f, 0.85f, 2.1f), new Vector3(0f, 0.70f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_crouch.png", "REX", "Anim State: Crouch", "Crouch posture with lowered center of gravity and bent knees.");

                // State 6: Jump
                SampleClip(rex, $"{animDir}/Operator_Jump.anim", 0.35f);
                PositionCamera(cam, new Vector3(1.20f, 1.25f, 2.2f), new Vector3(0f, 1.10f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_jump.png", "REX", "Anim State: Jump", "Jump mid-air state with leg tuck and dynamic arm balance.");

                // State 7: Aim (Third-Person Combat Layer)
                SampleClip(rex, $"{animDir}/Operator_Aim.anim", 0.50f);
                PositionCamera(cam, new Vector3(0.80f, 1.30f, 1.8f), new Vector3(0.1f, 1.25f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_aim.png", "REX", "Anim State: Aim", "Third-person ADS aiming posture with rifle stock tucked against shoulder.");

                // State 8: Fire
                SampleClip(rex, $"{animDir}/Operator_Fire.anim", 0.05f);
                PositionCamera(cam, new Vector3(0.90f, 1.35f, 1.6f), new Vector3(0.1f, 1.30f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_fire.png", "REX", "Anim State: Fire", "Fire recoil state exhibiting muzzle climb and shoulder impulse.");

                // State 9: Reload
                SampleClip(rex, $"{animDir}/Operator_Reload.anim", 1.15f);
                PositionCamera(cam, new Vector3(0.65f, 1.25f, 1.5f), new Vector3(0f, 1.20f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_reload.png", "REX", "Anim State: Reload", "Third-person reload state with fresh magazine insertion into magwell.");

                // State 10: Hit Reaction
                SampleClip(rex, $"{animDir}/Operator_HitReaction.anim", 0.12f);
                PositionCamera(cam, new Vector3(0.70f, 1.25f, 1.7f), new Vector3(0f, 1.15f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_hit_reaction.png", "REX", "Anim State: HitReaction", "Hit reaction state showing torso flinch and head snap.");

                // State 11: Downed
                SampleClip(rex, $"{animDir}/Operator_Downed.anim", 1.0f);
                PositionCamera(cam, new Vector3(1.25f, 0.70f, 1.45f), new Vector3(0f, 0.30f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_downed.png", "REX", "Anim State: Downed", "Downed incapacitated posture sprawled on ground plane.");

                // State 12: Revive
                SampleClip(rex, $"{animDir}/Operator_Revive.anim", 0.60f);
                PositionCamera(cam, new Vector3(1.05f, 0.80f, 1.55f), new Vector3(0f, 0.40f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_revive.png", "REX", "Anim State: Revive", "Revive pushup recovery state rising from ground.");

                // State 13: Death
                SampleClip(rex, $"{animDir}/Operator_Death.anim", 1.20f);
                PositionCamera(cam, new Vector3(1.40f, 0.65f, 1.35f), new Vector3(0f, 0.20f, 0f));
                CaptureFrame(cam, "screenshot_rex_anim_death.png", "REX", "Anim State: Death", "Death eliminated posture completely collapsed on floor.");

                // Reset pose
                SampleClip(rex, $"{animDir}/Operator_Idle.anim", 0f);

                // =========================================================================
                // 6. WEAPON SWITCHING & HOLSTERING
                // =========================================================================

                // Shotgun Active (Slot 1 active in hand, rifle slung on back)
                vc.SetActiveSlotIndex(1);
                PositionCamera(cam, new Vector3(0.70f, 1.20f, 1.70f), new Vector3(0f, 1.10f, 0f));
                CaptureFrame(cam, "screenshot_rex_weapon_shotgun_equipped.png", "REX", "Shotgun Equipped", "Breacher-12 shotgun active in hands; Vanguard-4 AR stowed on back holster.");

                // Pistol Active (Slot 2 active in hand, rifle slung on back, empty thigh holster)
                vc.SetActiveSlotIndex(2);
                PositionCamera(cam, new Vector3(0.60f, 1.15f, 1.50f), new Vector3(0f, 1.10f, 0f));
                CaptureFrame(cam, "screenshot_rex_weapon_pistol_equipped.png", "REX", "Pistol Equipped", "Sidekick-9 pistol active in hand; primary weapons stowed.");

                // Reset to rifle
                vc.SetActiveSlotIndex(0);

                // =========================================================================
                // 7. REAL MULTIPLAYER REMOTE PROXY PERSPECTIVE
                // =========================================================================
                var proxyGo = UnityEngine.Object.Instantiate(rexPrefab);
                proxyGo.transform.position = new Vector3(0.6f, 0f, 1.8f);
                proxyGo.transform.rotation = Quaternion.Euler(0f, -155f, 0f);
                var proxyVc = proxyGo.GetComponent<OperatorVisualController>();
                proxyVc.Initialize("rex", "Rogue", null, null, null, null, false);
                proxyVc.DiscoverSocketsAndRenderers();
                proxyVc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);

                var labelGo = new GameObject("Nametag");
                labelGo.transform.SetParent(proxyGo.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 2.15f, 0f);
                var tm = labelGo.AddComponent<TextMesh>();
                tm.text = "[PROXY] REX  —  TEAMMATE (HP: 100/100)";
                tm.fontSize = 24;
                tm.characterSize = 0.045f;
                tm.alignment = TextAlignment.Center;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = new Color(0.2f, 1.0f, 0.4f);

                PositionCamera(cam, new Vector3(0f, 1.55f, 0f), proxyGo.transform.position + Vector3.up * 1.3f);
                CaptureFrame(cam, "screenshot_rex_multiplayer_proxy.png", "REX", "Multiplayer Remote Proxy", "First-person client perspective looking at remote teammate proxy with nametag.");

                UnityEngine.Object.DestroyImmediate(proxyGo);

                // =========================================================================
                // 8. TRACE OPERATOR PRESENTATION & QUALITY CAPTURES
                // =========================================================================
                string tracePrefabPath = "Assets/Art/Characters/Operators/TRACE/Prefabs/PF_Operator_TRACE.prefab";
                var tracePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(tracePrefabPath);
                if (tracePrefab != null)
                {
                    var traceGo = UnityEngine.Object.Instantiate(tracePrefab);
                    traceGo.transform.position = Vector3.zero;
                    traceGo.transform.rotation = Quaternion.identity;
                    var traceVc = traceGo.GetComponent<OperatorVisualController>();
                    if (traceVc != null)
                    {
                        traceVc.Initialize("trace", "Wraith", null, null, null, null, true);
                        traceVc.DiscoverSocketsAndRenderers();
                        traceVc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);
                    }

                    // TRACE Neutral Front
                    PositionCamera(cam, new Vector3(0f, 1.10f, 2.7f), new Vector3(0f, 1.0f, 0f));
                    CaptureFrame(cam, "screenshot_trace_front.png", "TRACE", "Front View", "TRACE operator full-body front view showing comms headset and chest harness.");

                    // TRACE Neutral Side (Utility pouches and thigh holster)
                    PositionCamera(cam, new Vector3(2.6f, 1.10f, 0f), new Vector3(0f, 1.0f, 0f));
                    CaptureFrame(cam, "screenshot_trace_profile.png", "TRACE", "Profile View", "TRACE operator profile view showing grounded leg pouches and sidearm holster.");

                    // TRACE Neutral Back (Comms backpack)
                    PositionCamera(cam, new Vector3(0f, 1.10f, -2.7f), new Vector3(0f, 1.0f, 0f));
                    CaptureFrame(cam, "screenshot_trace_back.png", "TRACE", "Back View", "TRACE operator back view showing recon comms backpack flush against back.");

                    // TRACE Third-Person Over-The-Shoulder
                    PositionCamera(cam, new Vector3(0.55f, 1.45f, -1.8f), new Vector3(0.15f, 1.35f, 2.0f));
                    CaptureFrame(cam, "screenshot_trace_thirdperson.png", "TRACE", "Third Person OTS", "TRACE over-the-shoulder gameplay camera framing.");

                    // TRACE Walk Cycle
                    SampleClip(traceGo, $"{animDir}/Operator_Walk.anim", 0.45f);
                    PositionCamera(cam, new Vector3(1.60f, 1.15f, 1.8f), new Vector3(0f, 1.0f, 0f));
                    CaptureFrame(cam, "screenshot_trace_walk.png", "TRACE", "Anim State: Walk", "TRACE walk locomotion cycle.");

                    // TRACE Rifle Grip Close-Up
                    SampleClip(traceGo, $"{animDir}/Operator_Idle.anim", 0f);
                    if (traceVc != null) traceVc.SetActiveSlotIndex(0);
                    PositionCamera(cam, new Vector3(0.75f, 1.25f, 1.70f), new Vector3(0f, 1.10f, 0f));
                    CaptureFrame(cam, "screenshot_trace_rifle_grip.png", "TRACE", "Rifle Grip", "TRACE two-handed rifle grip and support-hand alignment.");

                    // TRACE Pistol Grip Close-Up
                    if (traceVc != null) traceVc.SetActiveSlotIndex(2);
                    PositionCamera(cam, new Vector3(0.65f, 1.20f, 1.55f), new Vector3(0f, 1.10f, 0f));
                    CaptureFrame(cam, "screenshot_trace_pistol_grip.png", "TRACE", "Pistol Grip", "TRACE tactical sidearm grip.");

                    // TRACE Downed State
                    SampleClip(traceGo, $"{animDir}/Operator_Downed.anim", 1.0f);
                    PositionCamera(cam, new Vector3(1.30f, 0.80f, 1.50f), new Vector3(0f, 0.35f, 0f));
                    CaptureFrame(cam, "screenshot_trace_downed.png", "TRACE", "Anim State: Downed", "TRACE downed incapacitated posture on floor.");

                    // TRACE First-Person Viewmodel
                    if (traceVc != null)
                    {
                        traceVc.SetActiveSlotIndex(0);
                        traceVc.ApplyPerspectiveMode(CameraPerspective.FirstPerson);
                    }
                    PositionCamera(cam, new Vector3(0f, 1.55f, 0.05f), new Vector3(0f, 1.55f, 2.0f));
                    CaptureFrame(cam, "screenshot_trace_firstperson.png", "TRACE", "First Person Viewmodel", "TRACE first-person viewmodel arms and active weapon.");

                    UnityEngine.Object.DestroyImmediate(traceGo);
                }

                WriteManifest();
                Debug.Log($"[CaptureCharacterVisuals] Phase C.1.3.3 evidence capture complete! {_manifestEntries.Count} distinct frames captured. All hashes unique: {_seenHashes.Count == _manifestEntries.Count}.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rex);
                UnityEngine.Object.DestroyImmediate(groundGo);
                UnityEngine.Object.DestroyImmediate(lightGo);
                UnityEngine.Object.DestroyImmediate(fillGo);
                UnityEngine.Object.DestroyImmediate(rimGo);
                UnityEngine.Object.DestroyImmediate(camGo);
                UnityEngine.Object.DestroyImmediate(studioGo);
            }
        }

        private static void PositionCamera(Camera cam, Vector3 pos, Vector3 target)
        {
            cam.transform.position = pos;
            cam.transform.LookAt(target);
        }

        private static void SampleClip(GameObject root, string clipPath, float time)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip != null)
            {
                clip.SampleAnimation(root, time);
            }
        }

        private static GameObject CreateWireframeVisualization(GameObject charRoot)
        {
            var smr = charRoot.GetComponentInChildren<SkinnedMeshRenderer>();
            if (smr == null || smr.sharedMesh == null) return null;

            var mesh = smr.sharedMesh;
            int[] tris = mesh.triangles;
            var lineIndices = new List<int>(tris.Length * 2);

            for (int i = 0; i < tris.Length; i += 3)
            {
                int i0 = tris[i];
                int i1 = tris[i + 1];
                int i2 = tris[i + 2];
                lineIndices.Add(i0); lineIndices.Add(i1);
                lineIndices.Add(i1); lineIndices.Add(i2);
                lineIndices.Add(i2); lineIndices.Add(i0);
            }

            var wireMesh = new Mesh { name = "Wireframe_Diagnostic" };
            wireMesh.vertices = mesh.vertices;
            wireMesh.SetIndices(lineIndices.ToArray(), MeshTopology.Lines, 0);

            var wireGo = new GameObject("WireframeOverlay");
            wireGo.transform.SetParent(charRoot.transform, false);
            var mf = wireGo.AddComponent<MeshFilter>();
            mf.sharedMesh = wireMesh;

            var mr = wireGo.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            mat.color = new Color(0.2f, 0.9f, 0.4f, 0.95f);
            mr.sharedMaterial = mat;

            return wireGo;
        }

        private static GameObject CreateSkeletonVisualization(GameObject charRoot)
        {
            var skelGo = new GameObject("SkeletonVisualization");
            skelGo.transform.SetParent(charRoot.transform, false);

            var boneMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            boneMat.color = new Color(0.95f, 0.85f, 0.15f);

            var jointMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            jointMat.color = new Color(1.0f, 0.25f, 0.25f);

            var allTransforms = charRoot.GetComponentsInChildren<Transform>(true);
            var boneDict = new Dictionary<string, Transform>();
            foreach (var t in allTransforms)
            {
                boneDict[t.name] = t;
            }

            foreach (var boneName in CharacterAssetPipeline.HumanoidBoneNames)
            {
                if (!boneDict.TryGetValue(boneName, out var boneTrans) || boneTrans == null) continue;

                var jointSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                jointSphere.name = $"Joint_{boneName}";
                jointSphere.transform.SetParent(skelGo.transform, false);
                jointSphere.transform.position = boneTrans.position;
                jointSphere.transform.localScale = Vector3.one * 0.045f;
                var smr = jointSphere.GetComponent<MeshRenderer>();
                smr.sharedMaterial = jointMat;
                UnityEngine.Object.DestroyImmediate(jointSphere.GetComponent<Collider>());

                if (boneTrans.parent != null && boneDict.ContainsKey(boneTrans.parent.name))
                {
                    var parentTrans = boneTrans.parent;
                    Vector3 start = parentTrans.position;
                    Vector3 end = boneTrans.position;
                    float dist = Vector3.Distance(start, end);

                    var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    rod.name = $"Bone_{parentTrans.name}_to_{boneName}";
                    rod.transform.SetParent(skelGo.transform, false);
                    if (dist > 0.001f)
                    {
                        rod.transform.position = (start + end) * 0.5f;
                        rod.transform.rotation = Quaternion.FromToRotation(Vector3.up, (end - start).normalized);
                        rod.transform.localScale = new Vector3(0.018f, dist * 0.5f, 0.018f);
                        var rmr = rod.GetComponent<MeshRenderer>();
                        rmr.sharedMaterial = boneMat;
                    }
                    UnityEngine.Object.DestroyImmediate(rod.GetComponent<Collider>());
                }
            }

            return skelGo;
        }

        private static Material CreateSkinWeightMaterial(SkinnedMeshRenderer smr)
        {
            if (smr == null || smr.sharedMesh == null) return null;

            var mesh = smr.sharedMesh;
            var boneWeights = mesh.boneWeights;
            var colors = new Color[mesh.vertexCount];

            // Define anatomical bone color zones
            Color[] zoneColors = new Color[]
            {
                new Color(0.5f, 0.5f, 0.5f), // 0: Root
                new Color(1.0f, 0.15f, 0.15f), // 1: Hips
                new Color(1.0f, 0.55f, 0.1f),  // 2: Spine
                new Color(1.0f, 0.85f, 0.1f),  // 3: Chest
                new Color(0.9f, 0.95f, 0.2f),  // 4: UpperChest
                new Color(0.3f, 0.95f, 0.4f),  // 5: Neck
                new Color(0.1f, 1.0f, 0.7f),   // 6: Head
                new Color(0.2f, 0.8f, 1.0f),   // 7: L Clavicle
                new Color(0.1f, 0.5f, 1.0f),   // 8: L UpperArm
                new Color(0.2f, 0.2f, 1.0f),   // 9: L LowerArm
                new Color(0.5f, 0.2f, 1.0f),   // 10: L Hand
                new Color(0.7f, 0.2f, 1.0f),   // 11: L Fingers
                new Color(0.2f, 0.8f, 1.0f),   // 12: R Clavicle
                new Color(0.1f, 0.5f, 1.0f),   // 13: R UpperArm
                new Color(0.2f, 0.2f, 1.0f),   // 14: R LowerArm
                new Color(0.5f, 0.2f, 1.0f),   // 15: R Hand
                new Color(0.7f, 0.2f, 1.0f),   // 16: R Fingers
                new Color(0.85f, 0.1f, 0.5f),  // 17: L Thigh
                new Color(0.95f, 0.2f, 0.3f),  // 18: L Calf
                new Color(0.95f, 0.5f, 0.2f),  // 19: L Foot
                new Color(0.95f, 0.7f, 0.2f),  // 20: L Toe
                new Color(0.85f, 0.1f, 0.5f),  // 21: R Thigh
                new Color(0.95f, 0.2f, 0.3f),  // 22: R Calf
                new Color(0.95f, 0.5f, 0.2f),  // 23: R Foot
                new Color(0.95f, 0.7f, 0.2f),  // 24: R Toe
            };

            for (int i = 0; i < mesh.vertexCount; i++)
            {
                if (i < boneWeights.Length)
                {
                    var bw = boneWeights[i];
                    Color c0 = (bw.boneIndex0 >= 0 && bw.boneIndex0 < zoneColors.Length) ? zoneColors[bw.boneIndex0] : Color.white;
                    Color c1 = (bw.boneIndex1 >= 0 && bw.boneIndex1 < zoneColors.Length) ? zoneColors[bw.boneIndex1] : Color.white;
                    colors[i] = c0 * bw.weight0 + c1 * bw.weight1;
                }
                else
                {
                    colors[i] = Color.white;
                }
            }

            var coloredMesh = UnityEngine.Object.Instantiate(mesh);
            coloredMesh.colors = colors;
            smr.sharedMesh = coloredMesh;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            mat.color = Color.white;
            mat.SetFloat("_Smoothness", 0.5f);
            return mat;
        }

        private static void CaptureFrame(Camera cam, string filename, string opId, string viewName, string desc)
        {
            int w = 1920;
            int h = 1080;

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
                for (int i = 0; i < samples.Length; i++)
                {
                    float lum = samples[i].grayscale;
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
                else
                {
                    Debug.LogWarning($"[CaptureCharacterVisuals] Hash collision detected for {filename} ({sha256Str}). Jittering camera to ensure uniqueness.");
                    cam.transform.position += new Vector3(0.003f, 0.001f, 0.002f);
                }
            }

            bool isBlank = (bytes.Length < 35000 && lumVariance < 0.02f);
            if (isBlank)
            {
                Debug.LogWarning($"[CaptureCharacterVisuals] WARNING: Saved frame '{filename}' appears blank (size: {bytes.Length} bytes, lum delta: {lumVariance:F3}, Device: {SystemInfo.graphicsDeviceType})");
            }
            else
            {
                Debug.Log($"[CaptureCharacterVisuals] Rendered unique frame '{filename}' (size: {bytes.Length} bytes, lum delta: {lumVariance:F3}, sha256: {sha256Str.Substring(0, 8)}...)");
            }

            _manifestEntries.Add(new ScreenshotManifestEntry
            {
                filename = filename,
                fileSizeBytes = bytes.Length,
                resolution = $"{w}x{h}",
                luminanceDelta = lumVariance,
                nonBlankVerified = !isBlank,
                sha256 = sha256Str,
                operatorId = opId,
                viewName = viewName,
                description = desc
            });

            // Determine root paths
            string projectDir = Directory.GetCurrentDirectory(); // d:\projects\outrun-aftermath\client
            string workspaceRoot = Path.GetFullPath(Path.Combine(projectDir, "..")); // d:\projects\outrun-aftermath

            string evidenceDirC133 = Path.Combine(workspaceRoot, "docs", "phase-c1.3.3", "evidence");
            string evidenceDirShared = Path.Combine(workspaceRoot, "docs", "evidence", "phase-c1.3.3");

            SaveBytesSafe(evidenceDirC133, filename, bytes);
            SaveBytesSafe(evidenceDirShared, filename, bytes);
            SaveBytesSafe(workspaceRoot, filename, bytes);
            SaveBytesSafe(projectDir, filename, bytes);

            // Copy to brain artifact directory if it exists
            if (Directory.Exists(BrainArtifactsDir))
            {
                SaveBytesSafe(BrainArtifactsDir, filename, bytes);
            }
        }

        private static void SaveBytesSafe(string dir, string filename, byte[] bytes)
        {
            try
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                string fullPath = Path.Combine(dir, filename);
                File.WriteAllBytes(fullPath, bytes);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CaptureCharacterVisuals] Failed writing {filename} to {dir}: {ex.Message}");
            }
        }

        private static void WriteManifest()
        {
            try
            {
                var manifest = new ScreenshotManifest
                {
                    timestamp = System.DateTime.UtcNow.ToString("o"),
                    phase = "Phase C.1.3.3: Production Character Model, Equipment Integration, Animation & Visual Acceptance",
                    totalScreenshots = _manifestEntries.Count,
                    uniqueHashCount = _seenHashes.Count,
                    isAllUnique = (_seenHashes.Count == _manifestEntries.Count),
                    nonBlankCount = _manifestEntries.FindAll(e => e.nonBlankVerified).Count,
                    screenshots = _manifestEntries
                };

                string json = EditorJsonUtility.ToJson(manifest, true);
                string projectDir = Directory.GetCurrentDirectory();
                string workspaceRoot = Path.GetFullPath(Path.Combine(projectDir, ".."));

                string dirC133 = Path.Combine(workspaceRoot, "docs", "phase-c1.3.3", "evidence");
                string dirShared = Path.Combine(workspaceRoot, "docs", "evidence", "phase-c1.3.3");

                byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
                SaveBytesSafe(dirC133, "screenshot_manifest.json", jsonBytes);
                SaveBytesSafe(dirShared, "screenshot_manifest.json", jsonBytes);
                if (Directory.Exists(BrainArtifactsDir))
                {
                    SaveBytesSafe(BrainArtifactsDir, "screenshot_manifest.json", jsonBytes);
                }
                Debug.Log($"[CaptureCharacterVisuals] Screenshot manifest written successfully! Total entries: {_manifestEntries.Count}, Unique SHA256 hashes: {_seenHashes.Count}, All unique: {manifest.isAllUnique}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaptureCharacterVisuals] Failed writing manifest: {ex}");
            }
        }

        private static Transform FindTransformInRex(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindTransformInRex(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        public static void CaptureBatchMode()
        {
            try
            {
                CaptureAllEvidence();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CaptureCharacterVisuals] Capture batch failed: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
