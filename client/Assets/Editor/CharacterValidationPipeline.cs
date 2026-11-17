using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;
using OutrunAftermath.Client.Runtime.Character;

namespace OutrunAftermath.Client.Editor
{
    [Serializable]
    public class CharacterValidationReport
    {
        public string timestamp;
        public string phase = "Phase C: Character Foundation";
        public string status; // PASS or FAIL
        public SkeletonValidation skeleton = new SkeletonValidation();
        public MeshValidation mesh = new MeshValidation();
        public AvatarValidation avatar = new AvatarValidation();
        public AnimatorValidation animator = new AnimatorValidation();
        public MaterialValidation materials = new MaterialValidation();
        public OperatorPrefabValidation rex = new OperatorPrefabValidation();
        public OperatorPrefabValidation trace = new OperatorPrefabValidation();
        public List<string> errors = new List<string>();
        public List<string> warnings = new List<string>();
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
    public class MeshValidation
    {
        public bool valid;
        public string meshName;
        public int vertexCount;
        public int triangleCount;
        public int boneWeightCount;
        public int bindPoseCount;
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
    }

    [Serializable]
    public class MaterialValidation
    {
        public bool valid;
        public List<string> validatedMaterials = new List<string>();
        public List<string> missingMaterials = new List<string>();
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

        [MenuItem("Outrun/Validate Character Foundation (Phase C)")]
        public static bool ValidateCharacterFoundation()
        {
            Debug.Log("[CharacterValidationPipeline] Starting automated character audit...");
            var report = new CharacterValidationReport
            {
                timestamp = DateTime.UtcNow.ToString("o")
            };

            bool allPassed = true;

            // 1. Audit Skinned Mesh
            string meshPath = "Assets/Art/Characters/Shared/Meshes/SK_Humanoid_Operator.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (mesh != null)
            {
                report.mesh.valid = mesh.vertexCount > 0 && mesh.boneWeights.Length > 0 && mesh.bindposes.Length == 25;
                report.mesh.meshName = mesh.name;
                report.mesh.vertexCount = mesh.vertexCount;
                report.mesh.triangleCount = mesh.triangles.Length / 3;
                report.mesh.boneWeightCount = mesh.boneWeights.Length;
                report.mesh.bindPoseCount = mesh.bindposes.Length;

                if (!report.mesh.valid)
                {
                    allPassed = false;
                    report.errors.Add($"Skinned mesh invalid: verts={mesh.vertexCount}, weights={mesh.boneWeights.Length}, bindposes={mesh.bindposes.Length}");
                }
            }
            else
            {
                report.mesh.valid = false;
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

            // 4. Audit Animator Controller
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

                // Check 13 required states
                foreach (var reqState in CharacterAssetPipeline.RequiredAnimationStates)
                {
                    if (!report.animator.states.Contains(reqState))
                    {
                        report.animator.missingStates.Add(reqState);
                    }
                }

                report.animator.hasUpperBodyMask = animController.layers.Length > 1 && animController.layers[1].avatarMask != null;
                report.animator.valid = report.animator.missingStates.Count == 0 && report.animator.hasUpperBodyMask;

                if (!report.animator.valid)
                {
                    allPassed = false;
                    report.errors.Add($"Animator validation failed: missing states count={report.animator.missingStates.Count}, hasUpperBodyMask={report.animator.hasUpperBodyMask}");
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

            // 6. Audit Operator Prefabs: REX and TRACE
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

            Debug.Log($"[CharacterValidationPipeline] Audit Complete: Status={report.status}. Report written to {ReportPath}");
            return allPassed;
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
