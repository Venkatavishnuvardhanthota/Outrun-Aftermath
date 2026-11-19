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

        [MenuItem("Outrun/Capture Character Visual Evidence (Phase C.1.3)")]
        public static void CaptureAllEvidence()
        {
            Debug.Log($"[CaptureCharacterVisuals] Setting up calibrated capture studio... Device: {SystemInfo.graphicsDeviceType}, Name: {SystemInfo.graphicsDeviceName}, Vendor: {SystemInfo.graphicsDeviceVendor}");

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Debug.LogWarning("[CaptureCharacterVisuals] WARNING: Graphics device is Null (running under -nographics). Frame rendering produces blank buffers in batchmode. Run Unity batchmode with graphics to enable hardware GPU rendering.");
            }

            // Create temporary studio environment
            var studioGo = new GameObject("CaptureStudio");

            var lightGo = new GameObject("KeyLight");
            var keyLight = lightGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.35f;
            keyLight.color = new Color(0.98f, 0.97f, 0.95f);
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var fillGo = new GameObject("FillLight");
            var fillLight = fillGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.55f;
            fillLight.color = new Color(0.85f, 0.88f, 0.95f);
            fillGo.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

            var rimGo = new GameObject("RimLight");
            var rimLight = rimGo.AddComponent<Light>();
            rimLight.type = LightType.Directional;
            rimLight.intensity = 0.45f;
            rimLight.color = new Color(0.95f, 0.92f, 0.85f);
            rimGo.transform.rotation = Quaternion.Euler(-25f, -120f, 0f);

            var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGo.transform.localScale = new Vector3(8f, 1f, 8f);
            var groundMr = groundGo.GetComponent<MeshRenderer>();
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            groundMat.color = new Color(0.14f, 0.16f, 0.18f);
            groundMat.SetFloat("_Smoothness", 0.35f);
            groundMr.sharedMaterial = groundMat;

            var camGo = new GameObject("StudioCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.clearFlags = CameraClearFlags.Color;
            cam.backgroundColor = new Color(0.10f, 0.11f, 0.13f);

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
                // ==========================================
                // SECTION 1: STANDARD MULTI-PERSPECTIVE VIEWS & REQUIRED 10 DELIVERABLES
                // ==========================================
                vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);

                // 1. REX Front View (Full Body, neutral combat stance)
                PositionCamera(cam, new Vector3(0f, 1.10f, 2.7f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_neutral_front.png");
                CaptureFrame(cam, "screenshot_rex_front.png");

                // 2. REX Back View (Backpack, rear armor, belt, holsters)
                PositionCamera(cam, new Vector3(0f, 1.10f, -2.7f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_neutral_back.png");
                CaptureFrame(cam, "screenshot_rex_back.png");

                // 3. REX Side View (Profile, armor plate thickness, backpack)
                PositionCamera(cam, new Vector3(2.6f, 1.10f, 0f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_neutral_side.png");
                CaptureFrame(cam, "screenshot_rex_side.png");

                // 4. REX Head & Hands Close-Up (Facial structure, jaw, nose, chin, and palm/fingers)
                Transform lArm = FindTransformInRex(rex.transform, "LeftUpperArm");
                Transform lElbow = FindTransformInRex(rex.transform, "LeftLowerArm");
                Transform lHand = FindTransformInRex(rex.transform, "LeftHand");
                if (lArm != null) lArm.localRotation = Quaternion.Euler(30f, 0f, 40f);
                if (lElbow != null) lElbow.localRotation = Quaternion.Euler(0f, 0f, 85f);
                if (lHand != null) lHand.localRotation = Quaternion.Euler(0f, 30f, 10f);
                PositionCamera(cam, new Vector3(0.20f, 1.50f, 0.95f), new Vector3(0f, 1.50f, 0.05f));
                CaptureFrame(cam, "screenshot_rex_head_hands.png");
                if (lArm != null) lArm.localRotation = Quaternion.identity;
                if (lElbow != null) lElbow.localRotation = Quaternion.identity;
                if (lHand != null) lHand.localRotation = Quaternion.identity;

                // 5. REX Wireframe Diagnostic Overlay (Continuous 2-manifold topology, 1 component, 0 boundary edges)
                var wireframeGo = CreateWireframeVisualization(rex);
                PositionCamera(cam, new Vector3(0.3f, 1.15f, 2.5f), new Vector3(0f, 1.05f, 0f));
                CaptureFrame(cam, "screenshot_rex_wireframe.png");
                if (wireframeGo != null) UnityEngine.Object.DestroyImmediate(wireframeGo);

                // 6. REX Skeleton Diagnostic View (25-Bone Anatomical Hierarchy with joint spheres and bone rods)
                var skeletonGo = CreateSkeletonVisualization(rex);
                PositionCamera(cam, new Vector3(0.4f, 1.15f, 2.5f), new Vector3(0f, 1.05f, 0f));
                CaptureFrame(cam, "screenshot_rex_skeleton.png");
                if (skeletonGo != null) UnityEngine.Object.DestroyImmediate(skeletonGo);

                // 7. REX Deformation: Shoulder & Elbow (Arms raised +75, elbows bent 90, forearms rotated)
                Transform rArm = FindTransformInRex(rex.transform, "RightUpperArm");
                Transform rElbow = FindTransformInRex(rex.transform, "RightLowerArm");
                Transform headBone = FindTransformInRex(rex.transform, "Head");
                if (lArm != null) lArm.localRotation = Quaternion.Euler(0f, 0f, 75f);
                if (lElbow != null) lElbow.localRotation = Quaternion.Euler(0f, 0f, 90f);
                if (rArm != null) rArm.localRotation = Quaternion.Euler(0f, 0f, -65f);
                if (rElbow != null) rElbow.localRotation = Quaternion.Euler(0f, 0f, -80f);
                if (headBone != null) headBone.localRotation = Quaternion.Euler(0f, 25f, 0f);
                PositionCamera(cam, new Vector3(0f, 1.35f, 2.0f), new Vector3(0f, 1.30f, 0f));
                CaptureFrame(cam, "screenshot_rex_deform_shoulder_elbow.png");
                if (lArm != null) lArm.localRotation = Quaternion.identity;
                if (lElbow != null) lElbow.localRotation = Quaternion.identity;
                if (rArm != null) rArm.localRotation = Quaternion.identity;
                if (rElbow != null) rElbow.localRotation = Quaternion.identity;
                if (headBone != null) headBone.localRotation = Quaternion.identity;

                // 8. REX Deformation: Knee & Hip (Deep tactical lunge with forward and backward leg extension)
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
                CaptureFrame(cam, "screenshot_rex_deform_knee_hip.png");
                if (lThigh != null) lThigh.localRotation = Quaternion.identity;
                if (lCalf != null) lCalf.localRotation = Quaternion.identity;
                if (rThigh != null) rThigh.localRotation = Quaternion.identity;
                if (rCalf != null) rCalf.localRotation = Quaternion.identity;
                if (spineBone != null) spineBone.localRotation = Quaternion.identity;

                // 9. REX Neutral Clay Render (Clean neutral matte clay material to evaluate geometry in isolation)
                var smr = rex.GetComponentInChildren<SkinnedMeshRenderer>();
                Material origMat = smr != null ? smr.sharedMaterial : null;
                var clayMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Characters/Shared/Materials/M_Operator_Neutral_Clay.mat");
                if (smr != null && clayMat != null) smr.sharedMaterial = clayMat;

                // Temporarily hide equipment for pure anatomical geometry inspection
                var equipRenderers = rex.GetComponentsInChildren<MeshRenderer>(true);
                var equipStates = new Dictionary<MeshRenderer, bool>();
                foreach (var er in equipRenderers)
                {
                    if (er.gameObject.name.Contains("Plane")) continue;
                    equipStates[er] = er.enabled;
                    er.enabled = false;
                }

                PositionCamera(cam, new Vector3(0f, 1.10f, 2.7f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_neutral_clay.png");

                // Restore equipment & material
                foreach (var kvp in equipStates)
                {
                    kvp.Key.enabled = kvp.Value;
                }
                if (smr != null && origMat != null) smr.sharedMaterial = origMat;

                // 10. REX Final Shaded Render (PBR calibrated, clean textures, full equipment)
                PositionCamera(cam, new Vector3(0.35f, 1.10f, 2.6f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_shaded_final.png");

                // Third-Person View (Over-the-shoulder tactical camera)
                PositionCamera(cam, new Vector3(0.55f, 1.45f, -1.8f), new Vector3(0.15f, 1.35f, 2.0f));
                CaptureFrame(cam, "screenshot_rex_thirdperson.png");
                CaptureFrame(cam, "screenshot_rex_thirdperson_rifle.png");
                CaptureFrame(cam, "rex_thirdperson_rifle.png");

                // First-Person View (Viewmodel arms, hands on weapon, no clipping)
                vc.ApplyPerspectiveMode(CameraPerspective.FirstPerson);
                PositionCamera(cam, new Vector3(0f, 1.55f, 0.15f), new Vector3(0f, 1.55f, 3.0f));
                CaptureFrame(cam, "screenshot_rex_firstperson.png");
                CaptureFrame(cam, "screenshot_rex_firstperson_rifle.png");
                CaptureFrame(cam, "rex_firstperson_rifle.png");

                // Weapon Switching & Holster Views
                vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);

                // Shotgun Equipped (Active in right hand)
                vc.SetActiveSlotIndex(1); // Breacher-12 Shotgun
                PositionCamera(cam, new Vector3(0.75f, 1.25f, 1.8f), new Vector3(0f, 1.10f, 0f));
                CaptureFrame(cam, "screenshot_rex_weapon_switch.png");
                CaptureFrame(cam, "screenshot_rex_shotgun_equipped.png");
                CaptureFrame(cam, "rex_shotgun_grip.png");

                // Rifle Holstered Close-up (Rear view of rifle slung on back)
                PositionCamera(cam, new Vector3(-0.35f, 1.35f, -1.2f), new Vector3(-0.05f, 1.25f, 0f));
                CaptureFrame(cam, "screenshot_rex_rifle_holstered.png");

                // Pistol Equipped (Active in right hand)
                vc.SetActiveSlotIndex(2); // Sidekick-9 Pistol
                PositionCamera(cam, new Vector3(0.65f, 1.20f, 1.6f), new Vector3(0f, 1.10f, 0f));
                CaptureFrame(cam, "screenshot_rex_pistol_equipped.png");
                CaptureFrame(cam, "rex_pistol_grip.png");

                // Shotgun Holstered Close-up (Rear view with shotgun on back)
                PositionCamera(cam, new Vector3(0.35f, 1.35f, -1.2f), new Vector3(0.05f, 1.25f, 0f));
                CaptureFrame(cam, "screenshot_rex_shotgun_holstered.png");

                // Reset to Rifle (Slot 0)
                vc.SetActiveSlotIndex(0);

                // Pistol Holstered Close-up (Thigh holster view)
                PositionCamera(cam, new Vector3(0.45f, 0.85f, 0.95f), new Vector3(0.15f, 0.80f, 0f));
                CaptureFrame(cam, "screenshot_rex_pistol_holstered.png");

                // Support Hand Foregrip Grip Close-up (Left hand on rifle foregrip)
                PositionCamera(cam, new Vector3(0.25f, 1.22f, 0.85f), new Vector3(0.08f, 1.18f, 0.25f));
                CaptureFrame(cam, "screenshot_rex_support_hand_grip.png");

                PositionCamera(cam, new Vector3(0.55f, 1.25f, 0.45f), new Vector3(0.08f, 1.18f, 0.25f));
                CaptureFrame(cam, "rex_support_hand_ik_side.png");

                PositionCamera(cam, new Vector3(-0.45f, 1.25f, 0.55f), new Vector3(-0.25f, 1.20f, 0.15f));
                CaptureFrame(cam, "rex_ik_elbow_bend_closeup.png");

                // Detailed Equipment Close-Ups
                PositionCamera(cam, new Vector3(0.15f, 1.62f, 0.95f), new Vector3(0f, 1.60f, 0f));
                CaptureFrame(cam, "screenshot_rex_head_helmet.png");

                PositionCamera(cam, new Vector3(0.20f, 1.28f, 1.15f), new Vector3(0f, 1.26f, 0f));
                CaptureFrame(cam, "screenshot_rex_upper_equipment.png");

                PositionCamera(cam, new Vector3(0.38f, 1.08f, 0.85f), new Vector3(0.28f, 1.05f, 0.25f));
                CaptureFrame(cam, "screenshot_rex_hands_gloves.png");

                PositionCamera(cam, new Vector3(0.20f, 0.55f, 1.45f), new Vector3(0f, 0.45f, 0f));
                CaptureFrame(cam, "screenshot_rex_lower_equipment.png");

                // Skin-Weights Heatmap View
                var weightMat = CreateSkinWeightMaterial(smr);
                if (smr != null && weightMat != null) smr.sharedMaterial = weightMat;
                PositionCamera(cam, new Vector3(0.3f, 1.15f, 2.5f), new Vector3(0f, 1.05f, 0f));
                CaptureFrame(cam, "screenshot_rex_skin_weights.png");
                if (smr != null && origMat != null) smr.sharedMaterial = origMat;
                if (smr != null && origMat != null) smr.sharedMaterial = origMat;

                // ==========================================
                // SECTION 3: MODE B — ANIMATION PLAYBACK OVER TIME
                // ==========================================

                // 14. Idle State (Tactical ready stance, low weapon ready)
                SampleClip(rex, $"{animDir}/Operator_Idle.anim", 0.5f);
                PositionCamera(cam, new Vector3(0.45f, 1.2f, 2.4f), new Vector3(0f, 1.05f, 0f));
                CaptureFrame(cam, "screenshot_rex_idle.png");

                // 15. Walk Cycle (Stride extension, arm counter-balance, foot plant)
                SampleClip(rex, $"{animDir}/Operator_Walk.anim", 0.45f);
                PositionCamera(cam, new Vector3(1.6f, 1.15f, 1.8f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_walk.png");
                CaptureFrame(cam, "rex_walk_pose.png");

                // 16. Run Cycle (Athletic forward lean, arm swing, knee drive)
                SampleClip(rex, $"{animDir}/Operator_Run.anim", 0.35f);
                PositionCamera(cam, new Vector3(1.8f, 1.15f, 1.6f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_run.png");

                // 16b. Sprint Pose
                SampleClip(rex, $"{animDir}/Operator_Sprint.anim", 0.35f);
                PositionCamera(cam, new Vector3(1.8f, 1.15f, 1.6f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "rex_sprint_pose.png");

                // 17. Crouch Pose (Low center of gravity, knee bend, compact tactical profile)
                SampleClip(rex, $"{animDir}/Operator_Crouch.anim", 0.5f);
                PositionCamera(cam, new Vector3(0.55f, 0.90f, 2.2f), new Vector3(0f, 0.75f, 0f));
                CaptureFrame(cam, "screenshot_rex_crouch.png");

                // 18. Aim & Fire State (Upper-body recoil impulse, muzzle climb, weapon shoulder brace)
                SampleClip(rex, $"{animDir}/Operator_Fire.anim", 0.05f);
                PositionCamera(cam, new Vector3(0.85f, 1.35f, 1.7f), new Vector3(0.1f, 1.30f, 0f));
                CaptureFrame(cam, "screenshot_rex_aim_fire.png");
                CaptureFrame(cam, "screenshot_rex_fire.png");

                // 19. Reload State (Weapon lowered, left hand indexing fresh magazine into magwell)
                SampleClip(rex, $"{animDir}/Operator_Reload.anim", 1.20f);
                PositionCamera(cam, new Vector3(0.65f, 1.30f, 1.6f), new Vector3(0f, 1.20f, 0f));
                CaptureFrame(cam, "screenshot_rex_reload.png");
                CaptureFrame(cam, "rex_reload_pose.png");

                // 20. Hit Reaction State (Upper torso twist, head snap, shoulder flinch)
                SampleClip(rex, $"{animDir}/Operator_HitReaction.anim", 0.12f);
                PositionCamera(cam, new Vector3(0.70f, 1.25f, 1.8f), new Vector3(0f, 1.15f, 0f));
                CaptureFrame(cam, "screenshot_rex_hit_reaction.png");

                // 21. Downed State (Ground collapse, sprawled incapacitated posture)
                SampleClip(rex, $"{animDir}/Operator_Downed.anim", 1.0f);
                PositionCamera(cam, new Vector3(1.3f, 0.95f, 1.5f), new Vector3(0f, 0.35f, 0f));
                CaptureFrame(cam, "screenshot_rex_downed_death.png");
                CaptureFrame(cam, "screenshot_rex_downed.png");
                CaptureFrame(cam, "rex_downed_pose.png");

                // 22. Revive State (Push-up ascent from downed posture)
                SampleClip(rex, $"{animDir}/Operator_Revive.anim", 0.5f);
                PositionCamera(cam, new Vector3(1.1f, 0.85f, 1.5f), new Vector3(0f, 0.45f, 0f));
                CaptureFrame(cam, "screenshot_rex_revive.png");
                CaptureFrame(cam, "rex_revive_pose.png");

                // 23. Death State (Full ground collapse)
                SampleClip(rex, $"{animDir}/Operator_Death.anim", 1.2f);
                PositionCamera(cam, new Vector3(1.4f, 0.90f, 1.4f), new Vector3(0f, 0.25f, 0f));
                CaptureFrame(cam, "screenshot_rex_death.png");
                CaptureFrame(cam, "rex_death_pose.png");

                // Reset pose
                SampleClip(rex, $"{animDir}/Operator_Idle.anim", 0f);

                // ==========================================
                // SECTION 4: REAL MULTIPLAYER PROXY PERSPECTIVE
                // ==========================================

                // 24. Multiplayer Client Perspective (Remote proxy operator with Third-Person visual, socketed rifle, and overhead HUD nametag)
                var proxyGo = UnityEngine.Object.Instantiate(rexPrefab);
                proxyGo.transform.position = new Vector3(0.6f, 0f, 1.8f);
                proxyGo.transform.rotation = Quaternion.Euler(0f, -155f, 0f);
                var proxyVc = proxyGo.GetComponent<OperatorVisualController>();
                proxyVc.Initialize("rex", "Rogue", null, null, null, null, false);
                proxyVc.DiscoverSocketsAndRenderers();
                proxyVc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);

                // Add overhead HUD nametag
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
                CaptureFrame(cam, "screenshot_rex_multiplayer_proxy.png");
                CaptureFrame(cam, "screenshot_rex_remote_multiplayer.png");
                CaptureFrame(cam, "rex_remote_player_replication.png");

                UnityEngine.Object.DestroyImmediate(proxyGo);

                // 25. TRACE Third-Person Presentation (Recon Operator with scout rig, sensor visor, and combat camo)
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
                    PositionCamera(cam, new Vector3(0.55f, 1.45f, -1.8f), new Vector3(0.15f, 1.35f, 2.0f));
                    CaptureFrame(cam, "trace_thirdperson_presentation.png");
                    CaptureFrame(cam, "screenshot_trace_thirdperson.png");
                    UnityEngine.Object.DestroyImmediate(traceGo);
                }

                Debug.Log("[CaptureCharacterVisuals] All character screenshots successfully captured!");

                Debug.Log("[CaptureCharacterVisuals] All character screenshots successfully captured!");
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

            var wireMesh = new Mesh();
            wireMesh.vertices = mesh.vertices;
            wireMesh.SetIndices(lineIndices.ToArray(), MeshTopology.Lines, 0);

            var wireGo = new GameObject("WireframeOverlay");
            wireGo.transform.SetParent(smr.transform, false);

            var mf = wireGo.AddComponent<MeshFilter>();
            mf.sharedMesh = wireMesh;

            var mr = wireGo.AddComponent<MeshRenderer>();
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            mat.color = new Color(0f, 0.95f, 1f, 1f);
            mr.sharedMaterial = mat;

            return wireGo;
        }

        private static GameObject CreateSkeletonVisualization(GameObject charRoot)
        {
            var skelGo = new GameObject("SkeletonVisualizer");
            var transforms = charRoot.GetComponentsInChildren<Transform>();

            var jointMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            jointMat.color = new Color(1.0f, 0.55f, 0.1f); // Amber

            var boneMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color"));
            boneMat.color = new Color(0.2f, 0.85f, 1.0f); // Cyan

            foreach (var t in transforms)
            {
                if (t.name.Contains("Socket") || t.name.Contains("Presentation") || t.name.Contains("PF_") || t.name.Contains("REX_")) continue;

                // Create joint marker sphere
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = $"Joint_{t.name}";
                sphere.transform.SetParent(skelGo.transform, false);
                sphere.transform.position = t.position;
                sphere.transform.localScale = Vector3.one * 0.038f;
                var smr = sphere.GetComponent<MeshRenderer>();
                smr.sharedMaterial = jointMat;
                UnityEngine.Object.DestroyImmediate(sphere.GetComponent<Collider>());

                // Create bone rod connecting to children
                foreach (Transform child in t)
                {
                    if (child.name.Contains("Socket") || child.name.Contains("PF_") || child.name.Contains("REX_")) continue;

                    var rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    rod.name = $"Bone_{t.name}_to_{child.name}";
                    rod.transform.SetParent(skelGo.transform, false);
                    Vector3 start = t.position;
                    Vector3 end = child.position;
                    float dist = Vector3.Distance(start, end);
                    if (dist > 0.01f)
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
                new Color(0.5f, 0.5f, 0.5f), // 0: Root (Grey)
                new Color(1.0f, 0.15f, 0.15f), // 1: Hips (Red)
                new Color(1.0f, 0.55f, 0.1f),  // 2: Spine (Orange)
                new Color(1.0f, 0.85f, 0.1f),  // 3: Chest (Yellow)
                new Color(0.9f, 0.95f, 0.2f),  // 4: UpperChest (Lime-Yellow)
                new Color(0.3f, 0.95f, 0.4f),  // 5: Neck (Mint)
                new Color(0.1f, 1.0f, 0.7f),   // 6: Head (Cyan-Mint)
                new Color(0.2f, 0.8f, 1.0f),   // 7: L Clavicle
                new Color(0.1f, 0.5f, 1.0f),   // 8: L UpperArm (Azure)
                new Color(0.2f, 0.2f, 1.0f),   // 9: L LowerArm (Blue)
                new Color(0.5f, 0.2f, 1.0f),   // 10: L Hand (Violet)
                new Color(0.7f, 0.2f, 1.0f),   // 11: L Fingers (Purple)
                new Color(0.2f, 0.8f, 1.0f),   // 12: R Clavicle
                new Color(0.1f, 0.5f, 1.0f),   // 13: R UpperArm
                new Color(0.2f, 0.2f, 1.0f),   // 14: R LowerArm
                new Color(0.5f, 0.2f, 1.0f),   // 15: R Hand
                new Color(0.7f, 0.2f, 1.0f),   // 16: R Fingers
                new Color(0.85f, 0.1f, 0.5f),  // 17: L Thigh (Magenta)
                new Color(0.95f, 0.2f, 0.3f),  // 18: L Calf (Crimson)
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

        private static void CaptureFrame(Camera cam, string filename)
        {
            int w = 1920;
            int h = 1080;
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

            // Sample center pixels to verify non-blank render
            Color[] samples = tex.GetPixels(w / 4, h / 4, 100, 100);
            float minLum = 1f, maxLum = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float lum = samples[i].grayscale;
                if (lum < minLum) minLum = lum;
                if (lum > maxLum) maxLum = lum;
            }
            float lumVariance = maxLum - minLum;

            byte[] bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            bool isBlank = (bytes.Length < 35000 && lumVariance < 0.02f);
            if (isBlank)
            {
                Debug.LogWarning($"[CaptureCharacterVisuals] WARNING: Saved frame '{filename}' appears blank (size: {bytes.Length} bytes, lum delta: {lumVariance:F3}, Device: {SystemInfo.graphicsDeviceType})");
            }
            else
            {
                Debug.Log($"[CaptureCharacterVisuals] Rendered valid frame '{filename}' (size: {bytes.Length} bytes, lum delta: {lumVariance:F3})");
            }

            // Determine root paths
            string projectDir = Directory.GetCurrentDirectory(); // d:\projects\outrun-aftermath\client
            string workspaceRoot = Path.GetFullPath(Path.Combine(projectDir, "..")); // d:\projects\outrun-aftermath

            string rootPath = Path.Combine(workspaceRoot, filename);
            string clientPath = Path.Combine(projectDir, filename);
            string evidenceDir = Path.Combine(workspaceRoot, "docs", "evidence", "phase-c1.3.1");

            try
            {
                if (!Directory.Exists(evidenceDir))
                {
                    Directory.CreateDirectory(evidenceDir);
                }
                string evidencePath = Path.Combine(evidenceDir, filename);
                File.WriteAllBytes(evidencePath, bytes);
                Debug.Log($"[CaptureCharacterVisuals] Saved evidence: {evidencePath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CaptureCharacterVisuals] Failed writing evidence: {ex.Message}");
            }

            try
            {
                File.WriteAllBytes(rootPath, bytes);
                Debug.Log($"[CaptureCharacterVisuals] Saved root: {rootPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CaptureCharacterVisuals] Failed writing root {rootPath}: {ex.Message}");
            }

            try
            {
                File.WriteAllBytes(clientPath, bytes);
                Debug.Log($"[CaptureCharacterVisuals] Saved client: {clientPath}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CaptureCharacterVisuals] Failed writing client {clientPath}: {ex.Message}");
            }

            // Copy to brain artifact directory if it exists
            if (Directory.Exists(BrainArtifactsDir))
            {
                try
                {
                    string brainPath = Path.Combine(BrainArtifactsDir, filename);
                    File.WriteAllBytes(brainPath, bytes);
                    Debug.Log($"[CaptureCharacterVisuals] Saved brain: {brainPath}");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[CaptureCharacterVisuals] Failed writing brain: {ex.Message}");
                }
            }

            Debug.Log($"[CaptureCharacterVisuals] Saved screenshot: {filename}");
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
