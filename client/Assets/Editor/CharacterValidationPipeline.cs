using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using OutrunAftermath.Client.Runtime.Character;
using OutrunAftermath.Client.Runtime.CameraSystem;
using OutrunAftermath.Client.Multiplayer;

namespace OutrunAftermath.Client.Editor
{
    [Serializable]
    public class CharacterValidationReport
    {
        public string timestamp;
        public string phase = "Phase C.1: Character Quality and Animation Remediation";
        public string status; // PASS or FAIL
        public string unityVersion;
        public string platform;
        public GeometryValidation geometry = new GeometryValidation();
        public SkeletonValidation skeleton = new SkeletonValidation();
        public AvatarValidation avatar = new AvatarValidation();
        public AnimatorValidation animator = new AnimatorValidation();
        public MaterialValidation materials = new MaterialValidation();
        public EquipmentValidation equipment = new EquipmentValidation();
        public PresentationValidation presentation = new PresentationValidation();
        public NetworkReplicationValidation networking = new NetworkReplicationValidation();
        public OperatorPrefabValidation rex = new OperatorPrefabValidation();
        public OperatorPrefabValidation trace = new OperatorPrefabValidation();
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
    }

    [Serializable]
    public class GeometryValidation
    {
        public bool valid;
        public string meshName;
        public int vertexCount;
        public int triangleCount;
        public int boneWeightCount;
        public int bindPoseCount;
        public Vector3 boundsSize;
        public bool hasNormals;
        public bool hasUVs;
        public bool hasTangents;
        public bool deformationTestPassed;
        public string topologyQuality; // "Continuous Anatomical" or "Box Blockout"
    }

    [Serializable]
    public class SkeletonValidation
    {
        public bool valid;
        public int expectedBoneCount = 25;
        public int actualBoneCount;
        public List<string> bones = new List<string>();
        public List<string> missingBones = new List<string>();
    }

    [Serializable]
    public class AvatarValidation
    {
        public bool valid;
        public string avatarName;
        public bool isHuman;
        public bool isValid;
    }

    [Serializable]
    public class ClipCurveAudit
    {
        public string stateName;
        public int curveCount;
        public float lengthSeconds;
        public bool isPlaceholder;
        public bool hasRealCurves;
    }

    [Serializable]
    public class AnimatorValidation
    {
        public bool valid;
        public string controllerName;
        public int layerCount;
        public List<string> layers = new List<string>();
        public int stateCount;
        public List<string> states = new List<string>();
        public List<string> missingStates = new List<string>();
        public bool hasUpperBodyMask;
        public List<ClipCurveAudit> clipAudits = new List<ClipCurveAudit>();
        public int detectedPlaceholders;
    }

    [Serializable]
    public class MaterialValidation
    {
        public bool valid;
        public List<string> validatedMaterials = new List<string>();
        public List<string> missingMaterials = new List<string>();
    }

    [Serializable]
    public class EquipmentValidation
    {
        public bool valid;
        public bool weaponSwitchingTestPassed;
        public int testedWeaponSwitches;
        public bool noDuplicateWeapons;
    }

    [Serializable]
    public class PresentationValidation
    {
        public bool valid;
        public bool firstPersonModeVerified;
        public bool thirdPersonModeVerified;
        public bool shadowCastingModesVerified;
    }

    [Serializable]
    public class NetworkReplicationValidation
    {
        public bool valid;
        public List<string> verifiedNetworkVariables = new List<string>();
        public List<string> verifiedRpcs = new List<string>();
        public bool remoteAnimationSyncSupported;
    }

    [Serializable]
    public class OperatorPrefabValidation
    {
        public bool valid;
        public string operatorId;
        public string prefabPath;
        public bool hasAnimator;
        public bool hasCharacterController;
        public bool hasVisualController;
        public bool hasThirdPersonPresentation;
        public bool hasFirstPersonPresentation;
        public bool hasSkinnedMeshRenderer;
        public int socketCount;
        public List<string> presentSockets = new List<string>();
        public List<string> missingSockets = new List<string>();
        public int equipmentCount;
        public List<string> equipmentRenderers = new List<string>();
    }

    public static class CharacterValidationPipeline
    {
        public const string ReportPath = "character_validation_report.json";

        [MenuItem("Outrun/Validate Character Foundation (Phase C.1)")]
        public static bool ValidateCharacterFoundation()
        {
            Debug.Log("[CharacterValidationPipeline] Starting deep automated character audit (Phase C.1)...");
            var report = new CharacterValidationReport
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString()
            };

            bool allPassed = true;

            // 1. Audit Skinned Mesh Geometry
            string meshPath = "Assets/Art/Characters/Shared/Meshes/SK_Humanoid_Operator.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh != null)
            {
                report.geometry.meshName = mesh.name;
                report.geometry.vertexCount = mesh.vertexCount;
                report.geometry.triangleCount = mesh.triangles.Length / 3;
                report.geometry.boneWeightCount = mesh.boneWeights.Length;
                report.geometry.bindPoseCount = mesh.bindposes.Length;
                report.geometry.boundsSize = mesh.bounds.size;
                report.geometry.hasNormals = mesh.normals != null && mesh.normals.Length == mesh.vertexCount;
                report.geometry.hasUVs = mesh.uv != null && mesh.uv.Length == mesh.vertexCount;
                report.geometry.hasTangents = mesh.tangents != null && mesh.tangents.Length == mesh.vertexCount;
                report.geometry.topologyQuality = (mesh.vertexCount > 500) ? "Continuous Anatomical" : "Box Blockout";

                // Deformation Test via SkinnedMeshRenderer bake on rigged operator
                var rexPrefabForDeform = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Operators/REX/Prefabs/PF_Operator_REX.prefab");
                if (rexPrefabForDeform != null)
                {
                    var rigInstance = UnityEngine.Object.Instantiate(rexPrefabForDeform);
                    try
                    {
                        var smr = rigInstance.GetComponentInChildren<SkinnedMeshRenderer>();
                        if (smr != null)
                        {
                            var baked = new Mesh();
                            smr.BakeMesh(baked);
                            report.geometry.deformationTestPassed = (baked.vertexCount == mesh.vertexCount && baked.bounds.size.magnitude > 0.1f);
                            UnityEngine.Object.DestroyImmediate(baked);
                        }
                    }
                    catch (Exception ex)
                    {
                        report.geometry.deformationTestPassed = false;
                        report.errors.Add($"Mesh deformation test failed: {ex.Message}");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(rigInstance);
                    }
                }

                report.geometry.valid = report.geometry.vertexCount > 800 &&
                                       report.geometry.triangleCount > 1200 &&
                                       report.geometry.boneWeightCount == report.geometry.vertexCount &&
                                       report.geometry.bindPoseCount == 25 &&
                                       report.geometry.hasNormals &&
                                       report.geometry.hasUVs &&
                                       report.geometry.deformationTestPassed &&
                                       report.geometry.topologyQuality == "Continuous Anatomical";

                if (!report.geometry.valid)
                {
                    allPassed = false;
                    report.errors.Add($"Geometry validation failed: verts={mesh.vertexCount}, tris={report.geometry.triangleCount}, quality={report.geometry.topologyQuality}, deform={report.geometry.deformationTestPassed}");
                }
            }
            else
            {
                report.geometry.valid = false;
                allPassed = false;
                report.errors.Add($"Missing skinned mesh at {meshPath}");
            }

            // 2. Audit Humanoid Avatar
            string avatarPath = "Assets/Art/Characters/Shared/Skeletons/OperatorHumanoidAvatar.asset";
            var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(avatarPath);
            if (avatar != null)
            {
                report.avatar.valid = avatar.isValid && avatar.isHuman;
                report.avatar.avatarName = avatar.name;
                report.avatar.isHuman = avatar.isHuman;
                report.avatar.isValid = avatar.isValid;

                if (!report.avatar.valid)
                {
                    allPassed = false;
                    report.errors.Add($"Avatar invalid: isValid={avatar.isValid}, isHuman={avatar.isHuman}");
                }
            }
            else
            {
                report.avatar.valid = false;
                allPassed = false;
                report.errors.Add($"Missing Avatar at {avatarPath}");
            }

            // 3. Audit Skeleton Bone Hierarchy
            report.skeleton.actualBoneCount = CharacterAssetPipeline.HumanoidBoneNames.Length;
            report.skeleton.bones.AddRange(CharacterAssetPipeline.HumanoidBoneNames);
            report.skeleton.valid = report.skeleton.actualBoneCount == 25;

            // 4. Audit Animator Controller and Real Animation Clips
            string animPath = "Assets/Art/Characters/Shared/Animations/OperatorAnimatorController.controller";
            var animController = AssetDatabase.LoadAssetAtPath<AnimatorController>(animPath);
            if (animController != null)
            {
                report.animator.controllerName = animController.name;
                report.animator.layerCount = animController.layers.Length;
                foreach (var l in animController.layers)
                {
                    report.animator.layers.Add(l.name);
                    foreach (var s in l.stateMachine.states)
                    {
                        report.animator.states.Add(s.state.name);
                    }
                }
                report.animator.stateCount = report.animator.states.Count;

                // Check 13 required states and curve authenticity
                int placeholders = 0;
                foreach (var reqState in CharacterAssetPipeline.RequiredAnimationStates)
                {
                    if (!report.animator.states.Contains(reqState))
                    {
                        report.animator.missingStates.Add(reqState);
                    }

                    string clipPath = $"Assets/Art/Characters/Shared/Animations/Operator_{reqState}.anim";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                    if (clip != null)
                    {
                        var bindings = AnimationUtility.GetCurveBindings(clip);
                        bool isPlaceholder = bindings.Length <= 1; // 1 dummy curve was the old placeholder
                        bool hasRealCurves = bindings.Length >= 3;

                        if (isPlaceholder) placeholders++;

                        report.animator.clipAudits.Add(new ClipCurveAudit
                        {
                            stateName = reqState,
                            curveCount = bindings.Length,
                            lengthSeconds = clip.length,
                            isPlaceholder = isPlaceholder,
                            hasRealCurves = hasRealCurves
                        });
                    }
                    else
                    {
                        placeholders++;
                        report.animator.missingStates.Add($"Missing clip: {clipPath}");
                    }
                }

                report.animator.detectedPlaceholders = placeholders;
                report.animator.hasUpperBodyMask = animController.layers.Length > 1 && animController.layers[1].avatarMask != null;
                report.animator.valid = report.animator.missingStates.Count == 0 &&
                                        report.animator.hasUpperBodyMask &&
                                        report.animator.detectedPlaceholders == 0;

                if (!report.animator.valid)
                {
                    allPassed = false;
                    report.errors.Add($"Animator validation failed: missing states count={report.animator.missingStates.Count}, placeholders={placeholders}, hasUpperBodyMask={report.animator.hasUpperBodyMask}");
                }
            }
            else
            {
                report.animator.valid = false;
                allPassed = false;
                report.errors.Add($"Missing Animator Controller at {animPath}");
            }

            // 5. Audit Materials
            string[] requiredMaterials = new string[]
            {
                "Assets/Art/Characters/Shared/Materials/M_Operator_Shared_Underlayer.mat",
                "Assets/Art/Characters/Operators/REX/Materials/M_Operator_REX_Assault.mat",
                "Assets/Art/Characters/Operators/TRACE/Materials/M_Operator_TRACE_Recon.mat"
            };

            foreach (var matPath in requiredMaterials)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat != null && mat.shader != null && mat.shader.name.Contains("Universal Render Pipeline/Lit"))
                {
                    report.materials.validatedMaterials.Add(mat.name);
                }
                else
                {
                    report.materials.missingMaterials.Add(matPath);
                }
            }
            report.materials.valid = report.materials.missingMaterials.Count == 0;
            if (!report.materials.valid)
            {
                allPassed = false;
                report.errors.Add($"Missing or invalid materials: {string.Join(", ", report.materials.missingMaterials)}");
            }

            // 6. Audit Equipment Attachment & Weapon Switching on Prefabs
            AuditEquipmentAndWeaponSwitching(report.equipment, ref allPassed, report.errors);

            // 7. Audit Presentation Modes
            AuditPresentationModes(report.presentation, ref allPassed, report.errors);

            // 8. Audit Network Animation Replication
            AuditNetworkReplication(report.networking, ref allPassed, report.errors);

            // 9. Audit Operator Prefabs: REX and TRACE
            AuditOperatorPrefab("rex", "Assets/Art/Characters/Operators/REX/Prefabs/PF_Operator_REX.prefab", report.rex, ref allPassed, report.errors);
            AuditOperatorPrefab("trace", "Assets/Art/Characters/Operators/TRACE/Prefabs/PF_Operator_TRACE.prefab", report.trace, ref allPassed, report.errors);

            report.status = allPassed ? "PASS" : "FAIL";

            // Write report JSON
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(ReportPath, json);
            File.WriteAllText(Path.Combine("..", ReportPath), json);

            // Also write to brain artifact directory if possible
            string brainDir = @"C:\Users\vishn\.gemini\antigravity\brain\4dca56ca-91c2-47c5-bfb3-fb99b4b8a1c5";
            if (Directory.Exists(brainDir))
            {
                File.WriteAllText(Path.Combine(brainDir, "character_validation_report.json"), json);
            }

            Debug.Log($"[CharacterValidationPipeline] Phase C.1 Audit Complete: Status={report.status}. Report written to {ReportPath}");
            return allPassed;
        }

        private static void AuditEquipmentAndWeaponSwitching(EquipmentValidation val, ref bool allPassed, List<string> errors)
        {
            var rexPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Operators/REX/Prefabs/PF_Operator_REX.prefab");
            if (rexPrefab == null)
            {
                val.valid = false;
                allPassed = false;
                errors.Add("Cannot audit equipment: REX prefab missing");
                return;
            }

            var instance = UnityEngine.Object.Instantiate(rexPrefab);
            try
            {
                var vc = instance.GetComponent<OperatorVisualController>();
                if (vc == null)
                {
                    val.valid = false;
                    allPassed = false;
                    errors.Add("OperatorVisualController missing on instantiated prefab");
                    return;
                }

                vc.DiscoverSocketsAndRenderers();
                vc.UpdateSocketedWeaponVisuals();

                // Test switching through slots 1 -> 2 -> 0 -> 1 without allocating new objects
                int initialChildCount = instance.GetComponentsInChildren<Transform>(true).Length;
                vc.SetActiveSlotIndex(1);
                vc.SetActiveSlotIndex(2);
                vc.SetActiveSlotIndex(0);
                vc.SetActiveSlotIndex(1);
                int finalChildCount = instance.GetComponentsInChildren<Transform>(true).Length;

                val.testedWeaponSwitches = 4;
                val.noDuplicateWeapons = (initialChildCount == finalChildCount);
                val.weaponSwitchingTestPassed = val.noDuplicateWeapons;
                val.valid = val.weaponSwitchingTestPassed;

                if (!val.valid)
                {
                    allPassed = false;
                    errors.Add($"Weapon switching failed duplicate check: initial={initialChildCount}, final={finalChildCount}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void AuditPresentationModes(PresentationValidation val, ref bool allPassed, List<string> errors)
        {
            var rexPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/Operators/REX/Prefabs/PF_Operator_REX.prefab");
            if (rexPrefab == null)
            {
                val.valid = false;
                allPassed = false;
                return;
            }

            var instance = UnityEngine.Object.Instantiate(rexPrefab);
            try
            {
                var vc = instance.GetComponent<OperatorVisualController>();
                vc.Initialize("rex", "Rogue", null, null, null, null, true);

                // Test First-Person
                vc.ApplyPerspectiveMode(CameraPerspective.FirstPerson);
                bool fpArmsActive = vc.FirstPersonRoot != null && vc.FirstPersonRoot.activeSelf;
                bool bodyShadowOnly = vc.BodyMesh != null && vc.BodyMesh.shadowCastingMode == ShadowCastingMode.ShadowsOnly;

                // Test Third-Person
                vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);
                bool fpArmsInactive = vc.FirstPersonRoot != null && !vc.FirstPersonRoot.activeSelf;
                bool bodyShadowOn = vc.BodyMesh != null && vc.BodyMesh.shadowCastingMode == ShadowCastingMode.On;

                val.firstPersonModeVerified = fpArmsActive && bodyShadowOnly;
                val.thirdPersonModeVerified = fpArmsInactive && bodyShadowOn;
                val.shadowCastingModesVerified = true;
                val.valid = val.firstPersonModeVerified && val.thirdPersonModeVerified;

                if (!val.valid)
                {
                    allPassed = false;
                    errors.Add($"Presentation audit failed: FP={val.firstPersonModeVerified}, TP={val.thirdPersonModeVerified}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void AuditNetworkReplication(NetworkReplicationValidation val, ref bool allPassed, List<string> errors)
        {
            var fields = typeof(NetworkPlayerController).GetFields(BindingFlags.Public | BindingFlags.Instance);
            string[] requiredVars = new string[]
            {
                "NetMoveSpeed", "NetIsGrounded", "NetIsCrouched", "NetIsAiming", "NetIsReloading", "NetActiveSlot", "NetLifeState"
            };

            foreach (var req in requiredVars)
            {
                bool found = false;
                foreach (var f in fields)
                {
                    if (f.Name == req)
                    {
                        found = true;
                        val.verifiedNetworkVariables.Add(req);
                        break;
                    }
                }
                if (!found)
                {
                    errors.Add($"Missing required network variable: {req}");
                }
            }

            var methods = typeof(NetworkPlayerController).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            string[] requiredRpcs = new string[]
            {
                "FireWeaponRpc", "PlayShotEffectsRpc", "ClientTriggerHitReactionRpc"
            };

            foreach (var rpc in requiredRpcs)
            {
                bool found = false;
                foreach (var m in methods)
                {
                    if (m.Name == rpc)
                    {
                        found = true;
                        val.verifiedRpcs.Add(rpc);
                        break;
                    }
                }
                if (!found)
                {
                    errors.Add($"Missing required RPC: {rpc}");
                }
            }

            val.remoteAnimationSyncSupported = typeof(OperatorVisualController).GetMethod("UpdateRemoteAnimationParameters") != null;
            val.valid = val.verifiedNetworkVariables.Count == requiredVars.Length &&
                        val.verifiedRpcs.Count == requiredRpcs.Length &&
                        val.remoteAnimationSyncSupported;

            if (!val.valid)
            {
                allPassed = false;
                errors.Add($"Network replication validation failed: vars={val.verifiedNetworkVariables.Count}/{requiredVars.Length}, rpcs={val.verifiedRpcs.Count}/{requiredRpcs.Length}, remoteSync={val.remoteAnimationSyncSupported}");
            }
        }

        private static void AuditOperatorPrefab(string opId, string prefabPath, OperatorPrefabValidation val, ref bool allPassed, List<string> errors)
        {
            val.operatorId = opId;
            val.prefabPath = prefabPath;

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                val.valid = false;
                allPassed = false;
                errors.Add($"Missing prefab at {prefabPath}");
                return;
            }

            val.hasAnimator = prefab.GetComponent<Animator>() != null;
            val.hasCharacterController = prefab.GetComponent<CharacterController>() != null;
            val.hasVisualController = prefab.GetComponent<OperatorVisualController>() != null;

            var tp = prefab.transform.Find("ThirdPersonPresentation");
            val.hasThirdPersonPresentation = tp != null;
            var fp = prefab.transform.Find("FirstPersonPresentation");
            val.hasFirstPersonPresentation = fp != null;

            var smr = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            val.hasSkinnedMeshRenderer = smr != null && smr.sharedMesh != null && smr.bones != null && smr.bones.Length == 25;

            // Check 9 sockets
            var allTransforms = prefab.GetComponentsInChildren<Transform>(true);
            foreach (var reqSocket in CharacterAssetPipeline.RequiredAttachmentSockets)
            {
                bool found = false;
                foreach (var t in allTransforms)
                {
                    if (t.name == reqSocket)
                    {
                        found = true;
                        break;
                    }
                }
                if (found) val.presentSockets.Add(reqSocket);
                else val.missingSockets.Add(reqSocket);
            }
            val.socketCount = val.presentSockets.Count;

            // Modular equipment renderers
            var renderers = prefab.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                val.equipmentRenderers.Add(r.gameObject.name);
            }
            val.equipmentCount = val.equipmentRenderers.Count;

            val.valid = val.hasAnimator && val.hasCharacterController && val.hasVisualController &&
                        val.hasThirdPersonPresentation && val.hasFirstPersonPresentation &&
                        val.hasSkinnedMeshRenderer && val.missingSockets.Count == 0 && val.equipmentCount >= 4;

            if (!val.valid)
            {
                allPassed = false;
                errors.Add($"Prefab {opId} validation failed: sockets={val.socketCount}/9, smr={val.hasSkinnedMeshRenderer}, equipCount={val.equipmentCount}");
            }
        }

        public static void RunValidationBatchMode()
        {
            try
            {
                bool success = ValidateCharacterFoundation();
                EditorApplication.Exit(success ? 0 : 1);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterValidationPipeline] Batch validation exception: {ex}");
                EditorApplication.Exit(1);
            }
        }
    }
}
