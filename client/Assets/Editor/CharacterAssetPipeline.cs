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
    public static class CharacterAssetPipeline
    {
        public const string CharactersRoot = "Assets/Art/Characters";
        public const string TexturesRoot = "Assets/Art/Textures/Characters";
        public const string SharedSkeletonsDir = "Assets/Art/Characters/Shared/Skeletons";
        public const string SharedMeshesDir = "Assets/Art/Characters/Shared/Meshes";
        public const string SharedMaterialsDir = "Assets/Art/Characters/Shared/Materials";
        public const string SharedAnimationsDir = "Assets/Art/Characters/Shared/Animations";
        public const string RexPrefabsDir = "Assets/Art/Characters/Operators/REX/Prefabs";
        public const string TracePrefabsDir = "Assets/Art/Characters/Operators/TRACE/Prefabs";
        public const string ResourcesCharactersDir = "Assets/Resources/Art/Characters";

        [MenuItem("Outrun/Configure Character Pipeline & Build Operators")]
        public static void ConfigureCharacterPipeline()
        {
            Debug.Log("[CharacterAssetPipeline] Starting Character Pipeline Configuration...");

            EnsureDirectories();
            ConfigureTextureImporters();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // 1. Create Materials
            var matUnderlayer = CreateOrUpdateMaterial("M_Operator_Shared_Underlayer", SharedMaterialsDir,
                $"{TexturesRoot}/Shared/T_Operator_Shared_Albedo.png",
                $"{TexturesRoot}/Shared/T_Operator_Shared_Normal.png",
                $"{TexturesRoot}/Shared/T_Operator_Shared_MaskMap.png",
                smoothness: 0.30f, metallic: 0.10f);

            var matRexAssault = CreateOrUpdateMaterial("M_Operator_REX_Assault", "Assets/Art/Characters/Operators/REX/Materials",
                $"{TexturesRoot}/REX/T_Operator_REX_Albedo.png",
                $"{TexturesRoot}/REX/T_Operator_REX_Normal.png",
                $"{TexturesRoot}/REX/T_Operator_REX_MaskMap.png",
                smoothness: 0.45f, metallic: 0.65f);

            var matTraceRecon = CreateOrUpdateMaterial("M_Operator_TRACE_Recon", "Assets/Art/Characters/Operators/TRACE/Materials",
                $"{TexturesRoot}/TRACE/T_Operator_TRACE_Albedo.png",
                $"{TexturesRoot}/TRACE/T_Operator_TRACE_Normal.png",
                $"{TexturesRoot}/TRACE/T_Operator_TRACE_MaskMap.png",
                smoothness: 0.55f, metallic: 0.70f);

            // 2. Build Shared Skeleton, Skinned Mesh & Humanoid Avatar
            var (avatar, sharedMesh) = BuildSharedSkeletonAndAvatar(matUnderlayer);

            // 3. Build 13-State Animator Controller
            var animController = BuildAnimatorController(avatar);

            // 4. Assemble Operator Prefabs: REX and TRACE
            BuildOperatorPrefab("rex", "Rogue", avatar, sharedMesh, matUnderlayer, matRexAssault, animController);
            BuildOperatorPrefab("trace", "Sentinel", avatar, sharedMesh, matUnderlayer, matTraceRecon, animController);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CharacterAssetPipeline] Phase C Character Foundation successfully configured!");
        }

        public static void BuildAllCharacterAssetsBatchMode()
        {
            try
            {
                ConfigureCharacterPipeline();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterAssetPipeline] Batch character build failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        private static void EnsureDirectories()
        {
            Directory.CreateDirectory(SharedSkeletonsDir);
            Directory.CreateDirectory(SharedMeshesDir);
            Directory.CreateDirectory(SharedMaterialsDir);
            Directory.CreateDirectory(SharedAnimationsDir);
            Directory.CreateDirectory("Assets/Art/Characters/Operators/REX/Materials");
            Directory.CreateDirectory(RexPrefabsDir);
            Directory.CreateDirectory("Assets/Art/Characters/Operators/TRACE/Materials");
            Directory.CreateDirectory(TracePrefabsDir);
            Directory.CreateDirectory(ResourcesCharactersDir);
        }

        private static void ConfigureTextureImporters()
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { TexturesRoot });
            foreach (var guid in textureGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool modified = false;
                if (path.Contains("Normal"))
                {
                    if (importer.textureType != TextureImporterType.NormalMap)
                    {
                        importer.textureType = TextureImporterType.NormalMap;
                        modified = true;
                    }
                }
                else if (path.Contains("MaskMap"))
                {
                    if (importer.sRGBTexture)
                    {
                        importer.sRGBTexture = false;
                        modified = true;
                    }
                }

                if (importer.maxTextureSize != 1024)
                {
                    importer.maxTextureSize = 1024;
                    modified = true;
                }

                if (modified)
                {
                    importer.SaveAndReimport();
                    Debug.Log($"[CharacterAssetPipeline] Configured character texture: {path}");
                }
            }
        }

        private static Material CreateOrUpdateMaterial(string matName, string folder, string albedoPath, string normalPath, string maskPath, float smoothness, float metallic)
        {
            string matPath = $"{folder}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = matName;
                AssetDatabase.CreateAsset(mat, matPath);
                Debug.Log($"[CharacterAssetPipeline] Created Material: {matPath}");
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            var albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            if (albedoTex != null)
            {
                mat.SetTexture("_BaseMap", albedoTex);
                mat.SetTexture("_MainTex", albedoTex);
            }

            var normalTex = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (normalTex != null)
            {
                mat.SetTexture("_BumpMap", normalTex);
                mat.EnableKeyword("_NORMALMAP");
            }

            var maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
            if (maskTex != null)
            {
                mat.SetTexture("_MetallicGlossMap", maskTex);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }

            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            mat.enableInstancing = true;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ==============================================================================
        // SHARED SKELETON, SKINNED MESH & AVATAR
        // ==============================================================================

        public static readonly string[] HumanoidBoneNames = new string[]
        {
            "Root",             // 0
            "Hips",             // 1
            "Spine",            // 2
            "Chest",            // 3
            "UpperChest",       // 4
            "Neck",             // 5
            "Head",             // 6
            "LeftClavicle",     // 7
            "LeftUpperArm",     // 8
            "LeftLowerArm",     // 9
            "LeftHand",         // 10
            "LeftHandFingers",  // 11
            "RightClavicle",    // 12
            "RightUpperArm",    // 13
            "RightLowerArm",    // 14
            "RightHand",        // 15
            "RightHandFingers", // 16
            "LeftThigh",        // 17
            "LeftCalf",         // 18
            "LeftFoot",         // 19
            "LeftToe",          // 20
            "RightThigh",       // 21
            "RightCalf",        // 22
            "RightFoot",        // 23
            "RightToe"          // 24
        };

        public static (Avatar, Mesh) BuildSharedSkeletonAndAvatar(Material material)
        {
            var rigGo = new GameObject("Operator_Skeleton_Rig");
            try
            {
                var boneDict = new Dictionary<string, Transform>();
                Transform root = rigGo.transform;

                // Build Bone Hierarchy Transforms in perfect T-Pose
                Transform bRoot = CreateBone("Root", root, Vector3.zero, Quaternion.identity);
                Transform bHips = CreateBone("Hips", bRoot, new Vector3(0f, 0.92f, 0f), Quaternion.identity);
                Transform bSpine = CreateBone("Spine", bHips, new Vector3(0f, 0.14f, 0f), Quaternion.identity);
                Transform bChest = CreateBone("Chest", bSpine, new Vector3(0f, 0.14f, 0f), Quaternion.identity);
                Transform bUpperChest = CreateBone("UpperChest", bChest, new Vector3(0f, 0.14f, 0f), Quaternion.identity);
                Transform bNeck = CreateBone("Neck", bUpperChest, new Vector3(0f, 0.12f, 0f), Quaternion.identity);
                Transform bHead = CreateBone("Head", bNeck, new Vector3(0f, 0.14f, 0f), Quaternion.identity);

                // Left Arm
                Transform bLClavicle = CreateBone("LeftClavicle", bUpperChest, new Vector3(-0.08f, 0.08f, 0.02f), Quaternion.identity);
                Transform bLUpperArm = CreateBone("LeftUpperArm", bLClavicle, new Vector3(-0.16f, -0.02f, -0.02f), Quaternion.identity);
                Transform bLLowerArm = CreateBone("LeftLowerArm", bLUpperArm, new Vector3(-0.24f, 0f, 0f), Quaternion.identity);
                Transform bLHand = CreateBone("LeftHand", bLLowerArm, new Vector3(-0.22f, 0f, 0f), Quaternion.identity);
                Transform bLFingers = CreateBone("LeftHandFingers", bLHand, new Vector3(-0.08f, 0f, 0f), Quaternion.identity);

                // Right Arm
                Transform bRClavicle = CreateBone("RightClavicle", bUpperChest, new Vector3(0.08f, 0.08f, 0.02f), Quaternion.identity);
                Transform bRUpperArm = CreateBone("RightUpperArm", bRClavicle, new Vector3(0.16f, -0.02f, -0.02f), Quaternion.identity);
                Transform bRLowerArm = CreateBone("RightLowerArm", bRUpperArm, new Vector3(0.24f, 0f, 0f), Quaternion.identity);
                Transform bRHand = CreateBone("RightHand", bRLowerArm, new Vector3(0.22f, 0f, 0f), Quaternion.identity);
                Transform bRFingers = CreateBone("RightHandFingers", bRHand, new Vector3(0.08f, 0f, 0f), Quaternion.identity);

                // Left Leg
                Transform bLThigh = CreateBone("LeftThigh", bHips, new Vector3(-0.12f, -0.06f, 0f), Quaternion.identity);
                Transform bLCalf = CreateBone("LeftCalf", bLThigh, new Vector3(0f, -0.40f, 0f), Quaternion.identity);
                Transform bLFoot = CreateBone("LeftFoot", bLCalf, new Vector3(0f, -0.36f, 0f), Quaternion.identity);
                Transform bLToe = CreateBone("LeftToe", bLFoot, new Vector3(0f, -0.08f, 0.12f), Quaternion.identity);

                // Right Leg
                Transform bRThigh = CreateBone("RightThigh", bHips, new Vector3(0.12f, -0.06f, 0f), Quaternion.identity);
                Transform bRCalf = CreateBone("RightCalf", bRThigh, new Vector3(0f, -0.40f, 0f), Quaternion.identity);
                Transform bRFoot = CreateBone("RightFoot", bRCalf, new Vector3(0f, -0.36f, 0f), Quaternion.identity);
                Transform bRToe = CreateBone("RightToe", bRFoot, new Vector3(0f, -0.08f, 0.12f), Quaternion.identity);

                Transform[] bones = new Transform[]
                {
                    bRoot, bHips, bSpine, bChest, bUpperChest, bNeck, bHead,
                    bLClavicle, bLUpperArm, bLLowerArm, bLHand, bLFingers,
                    bRClavicle, bRUpperArm, bRLowerArm, bRHand, bRFingers,
                    bLThigh, bLCalf, bLFoot, bLToe,
                    bRThigh, bRCalf, bRFoot, bRToe
                };

                for (int i = 0; i < bones.Length; i++)
                {
                    boneDict[bones[i].name] = bones[i];
                }

                // Generate Continuous Skinned Humanoid Mesh
                Mesh skinnedMesh = GenerateHumanoidSkinnedMesh(bones, root);
                string meshAssetPath = $"{SharedMeshesDir}/SK_Humanoid_Operator.asset";
                AssetDatabase.CreateAsset(skinnedMesh, meshAssetPath);
                Debug.Log($"[CharacterAssetPipeline] Saved Skinned Humanoid Mesh: {meshAssetPath}");

                // Build Unity Humanoid Avatar via AvatarBuilder
                Avatar avatar = BuildHumanAvatarAsset(rigGo, bones);
                string avatarPath = $"{SharedSkeletonsDir}/OperatorHumanoidAvatar.asset";
                AssetDatabase.CreateAsset(avatar, avatarPath);
                Debug.Log($"[CharacterAssetPipeline] Saved Humanoid Avatar: {avatarPath} (isValid={avatar.isValid}, isHuman={avatar.isHuman})");

                return (avatar, skinnedMesh);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rigGo);
            }
        }

        private static Transform CreateBone(string name, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static Mesh GenerateHumanoidSkinnedMesh(Transform[] bones, Transform root)
        {
            var mesh = new Mesh { name = "SK_Humanoid_Operator" };
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var triangles = new List<int>();

            // Helper to add segment with smooth skinning
            void AddSegmentBox(Vector3 center, Vector3 size, int boneA, int boneB, float weightA)
            {
                Vector3 half = size * 0.5f;
                int startIdx = verts.Count;

                Vector3[] boxVerts = new Vector3[]
                {
                    center + new Vector3(-half.x, -half.y, -half.z),
                    center + new Vector3( half.x, -half.y, -half.z),
                    center + new Vector3( half.x,  half.y, -half.z),
                    center + new Vector3(-half.x,  half.y, -half.z),
                    center + new Vector3(-half.x, -half.y,  half.z),
                    center + new Vector3( half.x, -half.y,  half.z),
                    center + new Vector3( half.x,  half.y,  half.z),
                    center + new Vector3(-half.x,  half.y,  half.z),
                };

                for (int i = 0; i < 8; i++)
                {
                    verts.Add(boxVerts[i]);
                    normals.Add((boxVerts[i] - center).normalized);
                    uvs.Add(new Vector2((i % 2), ((i / 2) % 2)));

                    weights.Add(new BoneWeight
                    {
                        boneIndex0 = boneA,
                        weight0 = weightA,
                        boneIndex1 = boneB,
                        weight1 = 1.0f - weightA
                    });
                }

                int[] boxTris = new int[]
                {
                    0, 2, 1, 0, 3, 2, // Back
                    4, 5, 6, 4, 6, 7, // Front
                    0, 1, 5, 0, 5, 4, // Bottom
                    3, 6, 2, 3, 7, 6, // Top
                    0, 4, 7, 0, 7, 3, // Left
                    1, 2, 6, 1, 6, 5  // Right
                };

                for (int i = 0; i < boxTris.Length; i++)
                {
                    triangles.Add(startIdx + boxTris[i]);
                }
            }

            // Head (Sphere/Box at Y=1.65, Bone=Head:6)
            AddSegmentBox(new Vector3(0f, 1.66f, 0f), new Vector3(0.22f, 0.24f, 0.24f), 6, 5, 1.0f);

            // Neck (Bone=Neck:5, UpperChest:4)
            AddSegmentBox(new Vector3(0f, 1.50f, 0f), new Vector3(0.14f, 0.12f, 0.14f), 5, 4, 0.7f);

            // Upper Chest / Shoulders (Bone=UpperChest:4, Chest:3)
            AddSegmentBox(new Vector3(0f, 1.38f, 0f), new Vector3(0.38f, 0.14f, 0.22f), 4, 3, 0.6f);

            // Chest / Mid-Torso (Bone=Chest:3, Spine:2)
            AddSegmentBox(new Vector3(0f, 1.22f, 0f), new Vector3(0.34f, 0.18f, 0.22f), 3, 2, 0.6f);

            // Abdomen / Pelvis (Bone=Spine:2, Hips:1)
            AddSegmentBox(new Vector3(0f, 1.02f, 0f), new Vector3(0.32f, 0.20f, 0.22f), 2, 1, 0.5f);

            // Pelvis / Hips (Bone=Hips:1)
            AddSegmentBox(new Vector3(0f, 0.88f, 0f), new Vector3(0.32f, 0.14f, 0.22f), 1, 2, 0.9f);

            // Left Arm:
            // Left Upper Arm (Bone=LeftUpperArm:8, LeftClavicle:7)
            AddSegmentBox(new Vector3(-0.36f, 1.38f, 0f), new Vector3(0.24f, 0.12f, 0.12f), 8, 9, 0.8f);
            // Left Lower Arm (Bone=LeftLowerArm:9, LeftHand:10)
            AddSegmentBox(new Vector3(-0.59f, 1.38f, 0f), new Vector3(0.22f, 0.10f, 0.10f), 9, 10, 0.8f);
            // Left Hand (Bone=LeftHand:10, Fingers:11)
            AddSegmentBox(new Vector3(-0.74f, 1.38f, 0f), new Vector3(0.10f, 0.08f, 0.10f), 10, 11, 0.9f);

            // Right Arm:
            // Right Upper Arm (Bone=RightUpperArm:13, RightClavicle:12)
            AddSegmentBox(new Vector3(0.36f, 1.38f, 0f), new Vector3(0.24f, 0.12f, 0.12f), 13, 14, 0.8f);
            // Right Lower Arm (Bone=RightLowerArm:14, RightHand:15)
            AddSegmentBox(new Vector3(0.59f, 1.38f, 0f), new Vector3(0.22f, 0.10f, 0.10f), 14, 15, 0.8f);
            // Right Hand (Bone=RightHand:15, Fingers:16)
            AddSegmentBox(new Vector3(0.74f, 1.38f, 0f), new Vector3(0.10f, 0.08f, 0.10f), 15, 16, 0.9f);

            // Left Leg:
            // Left Thigh (Bone=LeftThigh:17, LeftCalf:18)
            AddSegmentBox(new Vector3(-0.12f, 0.66f, 0f), new Vector3(0.16f, 0.38f, 0.18f), 17, 18, 0.7f);
            // Left Calf (Bone=LeftCalf:18, LeftFoot:19)
            AddSegmentBox(new Vector3(-0.12f, 0.28f, 0f), new Vector3(0.14f, 0.36f, 0.16f), 18, 19, 0.7f);
            // Left Foot (Bone=LeftFoot:19, LeftToe:20)
            AddSegmentBox(new Vector3(-0.12f, 0.05f, 0.05f), new Vector3(0.12f, 0.10f, 0.24f), 19, 20, 0.8f);

            // Right Leg:
            // Right Thigh (Bone=RightThigh:21, RightCalf:22)
            AddSegmentBox(new Vector3(0.12f, 0.66f, 0f), new Vector3(0.16f, 0.38f, 0.18f), 21, 22, 0.7f);
            // Right Calf (Bone=RightCalf:22, RightFoot:23)
            AddSegmentBox(new Vector3(0.12f, 0.28f, 0f), new Vector3(0.14f, 0.36f, 0.16f), 22, 23, 0.7f);
            // Right Foot (Bone=RightFoot:23, RightToe:24)
            AddSegmentBox(new Vector3(0.12f, 0.05f, 0.05f), new Vector3(0.12f, 0.10f, 0.24f), 23, 24, 0.8f);

            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.boneWeights = weights.ToArray();
            mesh.SetTriangles(triangles, 0);

            // Compute Bind Poses for all 25 bones
            Matrix4x4[] bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bindposes[i] = bones[i].worldToLocalMatrix * root.localToWorldMatrix;
            }
            mesh.bindposes = bindposes;
            mesh.RecalculateBounds();

            return mesh;
        }

        private static Avatar BuildHumanAvatarAsset(GameObject rootGo, Transform[] bones)
        {
            var humanBones = new List<HumanBone>
            {
                new HumanBone { humanName = "Hips", boneName = "Hips", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "Spine", boneName = "Spine", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "Chest", boneName = "Chest", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "UpperChest", boneName = "UpperChest", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "Neck", boneName = "Neck", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "Head", boneName = "Head", limit = new HumanLimit { useDefaultValues = true } },

                new HumanBone { humanName = "LeftShoulder", boneName = "LeftClavicle", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "LeftUpperArm", boneName = "LeftUpperArm", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "LeftLowerArm", boneName = "LeftLowerArm", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "LeftHand", boneName = "LeftHand", limit = new HumanLimit { useDefaultValues = true } },

                new HumanBone { humanName = "RightShoulder", boneName = "RightClavicle", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "RightUpperArm", boneName = "RightUpperArm", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "RightLowerArm", boneName = "RightLowerArm", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "RightHand", boneName = "RightHand", limit = new HumanLimit { useDefaultValues = true } },

                new HumanBone { humanName = "LeftUpperLeg", boneName = "LeftThigh", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "LeftLowerLeg", boneName = "LeftCalf", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "LeftFoot", boneName = "LeftFoot", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "LeftToes", boneName = "LeftToe", limit = new HumanLimit { useDefaultValues = true } },

                new HumanBone { humanName = "RightUpperLeg", boneName = "RightThigh", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "RightLowerLeg", boneName = "RightCalf", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "RightFoot", boneName = "RightFoot", limit = new HumanLimit { useDefaultValues = true } },
                new HumanBone { humanName = "RightToes", boneName = "RightToe", limit = new HumanLimit { useDefaultValues = true } }
            };

            var skeletonBones = new List<SkeletonBone>();
            // Root
            skeletonBones.Add(new SkeletonBone
            {
                name = rootGo.name,
                position = rootGo.transform.localPosition,
                rotation = rootGo.transform.localRotation,
                scale = rootGo.transform.localScale
            });

            foreach (var b in bones)
            {
                skeletonBones.Add(new SkeletonBone
                {
                    name = b.name,
                    position = b.localPosition,
                    rotation = b.localRotation,
                    scale = b.localScale
                });
            }

            var humanDesc = new HumanDescription
            {
                human = humanBones.ToArray(),
                skeleton = skeletonBones.ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0.0f,
                hasTranslationDoF = false
            };

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(rootGo, humanDesc);
            avatar.name = "OperatorHumanoidAvatar";
            return avatar;
        }

        // ==============================================================================
        // 13-STATE ANIMATOR CONTROLLER & CLIPS
        // ==============================================================================

        public static readonly string[] RequiredAnimationStates = new string[]
        {
            "Idle",
            "Walk",
            "Run",
            "Sprint",
            "Crouch",
            "Jump",
            "Aim",
            "Fire",
            "Reload",
            "HitReaction",
            "Downed",
            "Revive",
            "Death"
        };

        private static AnimatorController BuildAnimatorController(Avatar avatar)
        {
            string controllerPath = $"{SharedAnimationsDir}/OperatorAnimatorController.controller";
            var controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);

            // Add standard parameters
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("IsGrounded", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsCrouched", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsAiming", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsFiring", AnimatorControllerParameterType.Bool);
            controller.AddParameter("IsReloading", AnimatorControllerParameterType.Bool);
            controller.AddParameter("HitTrigger", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("LifeState", AnimatorControllerParameterType.Int);
            controller.AddParameter("IsReviving", AnimatorControllerParameterType.Bool);

            // 1. Create Animation Clips for each required state
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var state in RequiredAnimationStates)
            {
                string clipPath = $"{SharedAnimationsDir}/Operator_{state}.anim";
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if (clip == null)
                {
                    clip = new AnimationClip { name = $"Operator_{state}" };
                    // Add subtle curve to make it valid in engine
                    var curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
                    clip.SetCurve("", typeof(Transform), "m_LocalPosition.y", curve);
                    AssetDatabase.CreateAsset(clip, clipPath);
                }
                clips[state] = clip;
            }

            // 2. Base Locomotion Layer
            var rootSm = controller.layers[0].stateMachine;

            var sIdle = rootSm.AddState("Idle");
            sIdle.motion = clips["Idle"];
            rootSm.defaultState = sIdle;

            var sWalk = rootSm.AddState("Walk");
            sWalk.motion = clips["Walk"];

            var sRun = rootSm.AddState("Run");
            sRun.motion = clips["Run"];

            var sSprint = rootSm.AddState("Sprint");
            sSprint.motion = clips["Sprint"];

            var sCrouch = rootSm.AddState("Crouch");
            sCrouch.motion = clips["Crouch"];

            var sJump = rootSm.AddState("Jump");
            sJump.motion = clips["Jump"];

            var sDowned = rootSm.AddState("Downed");
            sDowned.motion = clips["Downed"];

            var sRevive = rootSm.AddState("Revive");
            sRevive.motion = clips["Revive"];

            var sDeath = rootSm.AddState("Death");
            sDeath.motion = clips["Death"];

            // Locomotion Transitions
            var tIdleWalk = sIdle.AddTransition(sWalk);
            tIdleWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tIdleWalk.hasExitTime = false;

            var tWalkIdle = sWalk.AddTransition(sIdle);
            tWalkIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tWalkIdle.hasExitTime = false;

            var tWalkRun = sWalk.AddTransition(sRun);
            tWalkRun.AddCondition(AnimatorConditionMode.Greater, 3.0f, "Speed");
            tWalkRun.hasExitTime = false;

            var tRunWalk = sRun.AddTransition(sWalk);
            tRunWalk.AddCondition(AnimatorConditionMode.Less, 3.0f, "Speed");
            tRunWalk.hasExitTime = false;

            var tRunSprint = sRun.AddTransition(sSprint);
            tRunSprint.AddCondition(AnimatorConditionMode.Greater, 6.0f, "Speed");
            tRunSprint.hasExitTime = false;

            var tSprintRun = sSprint.AddTransition(sRun);
            tSprintRun.AddCondition(AnimatorConditionMode.Less, 6.0f, "Speed");
            tSprintRun.hasExitTime = false;

            // Crouch Transitions
            var tToCrouch = rootSm.AddAnyStateTransition(sCrouch);
            tToCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouched");
            tToCrouch.hasExitTime = false;

            var tFromCrouch = sCrouch.AddTransition(sIdle);
            tFromCrouch.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouched");
            tFromCrouch.hasExitTime = false;

            // Jump Transition
            var tToJump = rootSm.AddAnyStateTransition(sJump);
            tToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
            tToJump.hasExitTime = false;

            var tFromJump = sJump.AddTransition(sIdle);
            tFromJump.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            tFromJump.hasExitTime = false;

            // LifeState Transitions (Downed, Revive, Death)
            var tToDowned = rootSm.AddAnyStateTransition(sDowned);
            tToDowned.AddCondition(AnimatorConditionMode.Equals, 1, "LifeState");
            tToDowned.hasExitTime = false;

            var tToRevive = sDowned.AddTransition(sRevive);
            tToRevive.AddCondition(AnimatorConditionMode.If, 0, "IsReviving");
            tToRevive.hasExitTime = false;

            var tReviveToIdle = sRevive.AddTransition(sIdle);
            tReviveToIdle.AddCondition(AnimatorConditionMode.Equals, 0, "LifeState");
            tReviveToIdle.hasExitTime = false;

            var tToDeath = rootSm.AddAnyStateTransition(sDeath);
            tToDeath.AddCondition(AnimatorConditionMode.Equals, 2, "LifeState");
            tToDeath.hasExitTime = false;

            // 3. Upper-Body Combat Layer (Layer 1 with AvatarMask)
            controller.AddLayer("UpperBody_Combat");
            var layers = controller.layers;
            layers[1].defaultWeight = 1.0f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;

            var upperSm = layers[1].stateMachine;

            var sAim = upperSm.AddState("Aim");
            sAim.motion = clips["Aim"];
            upperSm.defaultState = sAim;

            var sFire = upperSm.AddState("Fire");
            sFire.motion = clips["Fire"];

            var sReload = upperSm.AddState("Reload");
            sReload.motion = clips["Reload"];

            var sHit = upperSm.AddState("HitReaction");
            sHit.motion = clips["HitReaction"];

            // Combat Transitions
            var tAimFire = sAim.AddTransition(sFire);
            tAimFire.AddCondition(AnimatorConditionMode.If, 0, "IsFiring");
            tAimFire.hasExitTime = false;

            var tFireAim = sFire.AddTransition(sAim);
            tFireAim.hasExitTime = true;
            tFireAim.exitTime = 0.85f;

            var tAimReload = sAim.AddTransition(sReload);
            tAimReload.AddCondition(AnimatorConditionMode.If, 0, "IsReloading");
            tAimReload.hasExitTime = false;

            var tReloadAim = sReload.AddTransition(sAim);
            tReloadAim.hasExitTime = true;
            tReloadAim.exitTime = 0.90f;

            var tToHit = upperSm.AddAnyStateTransition(sHit);
            tToHit.AddCondition(AnimatorConditionMode.If, 0, "HitTrigger");
            tToHit.hasExitTime = false;

            var tHitAim = sHit.AddTransition(sAim);
            tHitAim.hasExitTime = true;
            tHitAim.exitTime = 0.85f;

            // Create AvatarMask for UpperBody Layer
            string maskPath = $"{SharedAnimationsDir}/Mask_UpperBody_Combat.mask";
            var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(maskPath);
            if (mask == null)
            {
                mask = new AvatarMask { name = "Mask_UpperBody_Combat" };
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Root, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Body, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFootIK, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFootIK, false);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftHandIK, true);
                mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightHandIK, true);
                AssetDatabase.CreateAsset(mask, maskPath);
            }
            layers[1].avatarMask = mask;
            controller.layers = layers;

            EditorUtility.SetDirty(controller);
            Debug.Log($"[CharacterAssetPipeline] Saved 13-State Animator Controller: {controllerPath}");
            return controller;
        }

        // ==============================================================================
        // PREFAB ASSEMBLY: REX & TRACE
        // ==============================================================================

        public static readonly string[] RequiredAttachmentSockets = new string[]
        {
            "RightHandWeaponSocket",
            "LeftHandSupportSocket",
            "PrimaryWeaponHolster",
            "SecondaryWeaponHolster",
            "BackpackSocket",
            "HelmetAttachmentSocket",
            "ShoulderLeftSocket",
            "ShoulderRightSocket",
            "UtilityBeltSocket"
        };

        private static void BuildOperatorPrefab(string opId, string opFaction, Avatar avatar, Mesh sharedMesh, Material matUnderlayer, Material matGear, AnimatorController animController)
        {
            string opUpper = opId.ToUpperInvariant();
            string prefabName = $"PF_Operator_{opUpper}";
            string prefabPath = opId == "rex" ? $"{RexPrefabsDir}/{prefabName}.prefab" : $"{TracePrefabsDir}/{prefabName}.prefab";
            string resPath = $"{ResourcesCharactersDir}/{prefabName}.prefab";

            var rootGo = new GameObject(prefabName);
            try
            {
                // 1. Root Components
                var cc = rootGo.AddComponent<CharacterController>();
                cc.radius = 0.38f;
                cc.height = 1.85f;
                cc.center = new Vector3(0f, 0.925f, 0f);

                var animator = rootGo.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = animController;
                animator.applyRootMotion = false;

                var visualController = rootGo.AddComponent<OperatorVisualController>();

                // 2. Presentations
                var tpRoot = new GameObject("ThirdPersonPresentation");
                tpRoot.transform.SetParent(rootGo.transform, false);

                var fpRoot = new GameObject("FirstPersonPresentation");
                fpRoot.transform.SetParent(rootGo.transform, false);
                fpRoot.SetActive(false); // Default inactive until local owner in first-person

                // 3. Build Skeleton Hierarchy inside ThirdPersonPresentation
                var skelGo = new GameObject("Skeleton");
                skelGo.transform.SetParent(tpRoot.transform, false);

                Transform bRoot = CreateBone("Root", skelGo.transform, Vector3.zero, Quaternion.identity);
                Transform bHips = CreateBone("Hips", bRoot, new Vector3(0f, 0.92f, 0f), Quaternion.identity);
                Transform bSpine = CreateBone("Spine", bHips, new Vector3(0f, 0.14f, 0f), Quaternion.identity);
                Transform bChest = CreateBone("Chest", bSpine, new Vector3(0f, 0.14f, 0f), Quaternion.identity);
                Transform bUpperChest = CreateBone("UpperChest", bChest, new Vector3(0f, 0.14f, 0f), Quaternion.identity);
                Transform bNeck = CreateBone("Neck", bUpperChest, new Vector3(0f, 0.12f, 0f), Quaternion.identity);
                Transform bHead = CreateBone("Head", bNeck, new Vector3(0f, 0.14f, 0f), Quaternion.identity);

                Transform bLClavicle = CreateBone("LeftClavicle", bUpperChest, new Vector3(-0.08f, 0.08f, 0.02f), Quaternion.identity);
                Transform bLUpperArm = CreateBone("LeftUpperArm", bLClavicle, new Vector3(-0.16f, -0.02f, -0.02f), Quaternion.identity);
                Transform bLLowerArm = CreateBone("LeftLowerArm", bLUpperArm, new Vector3(-0.24f, 0f, 0f), Quaternion.identity);
                Transform bLHand = CreateBone("LeftHand", bLLowerArm, new Vector3(-0.22f, 0f, 0f), Quaternion.identity);
                Transform bLFingers = CreateBone("LeftHandFingers", bLHand, new Vector3(-0.08f, 0f, 0f), Quaternion.identity);

                Transform bRClavicle = CreateBone("RightClavicle", bUpperChest, new Vector3(0.08f, 0.08f, 0.02f), Quaternion.identity);
                Transform bRUpperArm = CreateBone("RightUpperArm", bRClavicle, new Vector3(0.16f, -0.02f, -0.02f), Quaternion.identity);
                Transform bRLowerArm = CreateBone("RightLowerArm", bRUpperArm, new Vector3(0.24f, 0f, 0f), Quaternion.identity);
                Transform bRHand = CreateBone("RightHand", bRLowerArm, new Vector3(0.22f, 0f, 0f), Quaternion.identity);
                Transform bRFingers = CreateBone("RightHandFingers", bRHand, new Vector3(0.08f, 0f, 0f), Quaternion.identity);

                Transform bLThigh = CreateBone("LeftThigh", bHips, new Vector3(-0.12f, -0.06f, 0f), Quaternion.identity);
                Transform bLCalf = CreateBone("LeftCalf", bLThigh, new Vector3(0f, -0.40f, 0f), Quaternion.identity);
                Transform bLFoot = CreateBone("LeftFoot", bLCalf, new Vector3(0f, -0.36f, 0f), Quaternion.identity);
                Transform bLToe = CreateBone("LeftToe", bLFoot, new Vector3(0f, -0.08f, 0.12f), Quaternion.identity);

                Transform bRThigh = CreateBone("RightThigh", bHips, new Vector3(0.12f, -0.06f, 0f), Quaternion.identity);
                Transform bRCalf = CreateBone("RightCalf", bRThigh, new Vector3(0f, -0.40f, 0f), Quaternion.identity);
                Transform bRFoot = CreateBone("RightFoot", bRCalf, new Vector3(0f, -0.36f, 0f), Quaternion.identity);
                Transform bRToe = CreateBone("RightToe", bRFoot, new Vector3(0f, -0.08f, 0.12f), Quaternion.identity);

                Transform[] bones = new Transform[]
                {
                    bRoot, bHips, bSpine, bChest, bUpperChest, bNeck, bHead,
                    bLClavicle, bLUpperArm, bLLowerArm, bLHand, bLFingers,
                    bRClavicle, bRUpperArm, bRLowerArm, bRHand, bRFingers,
                    bLThigh, bLCalf, bLFoot, bLToe,
                    bRThigh, bRCalf, bRFoot, bRToe
                };

                // 4. SkinnedMeshRenderer on ThirdPersonPresentation
                var smrGo = new GameObject("Operator_Body_Skinned");
                smrGo.transform.SetParent(tpRoot.transform, false);
                var smr = smrGo.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = sharedMesh;
                smr.bones = bones;
                smr.rootBone = bRoot;
                smr.sharedMaterial = matUnderlayer;
                smr.shadowCastingMode = ShadowCastingMode.On;
                smr.receiveShadows = true;

                // 5. Install Sockets on Skeleton Bones
                Transform sockRHand = CreateSocket("RightHandWeaponSocket", bRHand, new Vector3(0.04f, -0.04f, 0.08f), Quaternion.identity);
                Transform sockLHand = CreateSocket("LeftHandSupportSocket", bLHand, new Vector3(-0.04f, -0.02f, 0.06f), Quaternion.identity);
                Transform sockHolsterPri = CreateSocket("PrimaryWeaponHolster", bChest, new Vector3(0.12f, 0.05f, -0.16f), Quaternion.Euler(15f, 30f, 160f));
                Transform sockHolsterSec = CreateSocket("SecondaryWeaponHolster", bRThigh, new Vector3(0.14f, -0.06f, 0.02f), Quaternion.Euler(0f, 0f, 0f));
                Transform sockBackpack = CreateSocket("BackpackSocket", bUpperChest, new Vector3(0f, 0.02f, -0.14f), Quaternion.identity);
                Transform sockHelmet = CreateSocket("HelmetAttachmentSocket", bHead, new Vector3(0f, 0.04f, 0f), Quaternion.identity);
                Transform sockShoulderL = CreateSocket("ShoulderLeftSocket", bLClavicle, new Vector3(-0.08f, 0f, 0f), Quaternion.identity);
                Transform sockShoulderR = CreateSocket("ShoulderRightSocket", bRClavicle, new Vector3(0.08f, 0f, 0f), Quaternion.identity);
                Transform sockBelt = CreateSocket("UtilityBeltSocket", bHips, new Vector3(0f, -0.02f, 0f), Quaternion.identity);

                // 6. Attach Modular Operator Equipment
                string opMeshesDir = $"Assets/Art/Characters/Operators/{opUpper}/Meshes";
                if (opId == "rex")
                {
                    AttachMeshProp("REX_Helmet_Combat", $"{opMeshesDir}/REX_Helmet_Combat.obj", sockHelmet, matGear);
                    AttachMeshProp("REX_Vest_TacticalAssault", $"{opMeshesDir}/REX_Vest_TacticalAssault.obj", bChest, matGear);
                    AttachMeshProp("REX_Pauldrons_Armored", $"{opMeshesDir}/REX_Pauldrons_Armored.obj", bUpperChest, matGear);
                    AttachMeshProp("REX_Backpack_Assault", $"{opMeshesDir}/REX_Backpack_Assault.obj", sockBackpack, matGear);
                    AttachMeshProp("REX_Holster_Sidearm", $"{opMeshesDir}/REX_Holster_Sidearm.obj", sockHolsterSec, matGear);

                    // First Person Arms
                    AttachMeshProp("FP_Arms_REX", $"{opMeshesDir}/FP_Arms_REX.obj", fpRoot.transform, matGear);
                }
                else
                {
                    AttachMeshProp("TRACE_Headset_ReconComms", $"{opMeshesDir}/TRACE_Headset_ReconComms.obj", sockHelmet, matGear);
                    AttachMeshProp("TRACE_Harness_ReconChest", $"{opMeshesDir}/TRACE_Harness_ReconChest.obj", bChest, matGear);
                    AttachMeshProp("TRACE_Backpack_ReconComms", $"{opMeshesDir}/TRACE_Backpack_ReconComms.obj", sockBackpack, matGear);
                    AttachMeshProp("TRACE_Pouches_UtilityLeg", $"{opMeshesDir}/TRACE_Pouches_UtilityLeg.obj", bLThigh, matGear);
                    AttachMeshProp("TRACE_Holster_Recon", $"{opMeshesDir}/TRACE_Holster_Recon.obj", sockHolsterSec, matGear);

                    // First Person Arms
                    AttachMeshProp("FP_Arms_TRACE", $"{opMeshesDir}/FP_Arms_TRACE.obj", fpRoot.transform, matGear);
                }

                // Attach Weapon Props in Sockets (Active Vanguard AR in RightHand, Holstered Shotgun / Pistol)
                var matWeapon = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/M_Weapon_TacticalSteel.mat");
                AttachMeshProp("Vanguard_AR_Hand", "Assets/Art/Meshes/Props/Weapon_Vanguard_AssaultRifle.obj", sockRHand, matWeapon);
                
                string holsterWeaponObj = opId == "rex" 
                    ? "Assets/Art/Meshes/Props/Weapon_Breacher_Shotgun.obj" 
                    : "Assets/Art/Meshes/Props/Weapon_Sidekick_Pistol.obj";
                Transform holsterTarget = opId == "rex" ? sockHolsterPri : sockHolsterSec;
                AttachMeshProp("Holstered_Weapon", holsterWeaponObj, holsterTarget, matWeapon);

                // 7. Setup Visual Controller Discovery
                visualController.DiscoverSocketsAndRenderers();

                // 8. Save Prefab to disk
                PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
                PrefabUtility.SaveAsPrefabAsset(rootGo, resPath);
                Debug.Log($"[CharacterAssetPipeline] Successfully saved Operator Prefab: {prefabPath} and {resPath}");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rootGo);
            }
        }

        private static Transform CreateSocket(string socketName, Transform parent, Vector3 localPos, Quaternion localRot)
        {
            var go = new GameObject(socketName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = Vector3.one;
            return go.transform;
        }

        private static void AttachMeshProp(string propName, string objPath, Transform parent, Material mat)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(objPath);
            if (mesh == null)
            {
                var allAssets = AssetDatabase.LoadAllAssetsAtPath(objPath);
                foreach (var a in allAssets)
                {
                    if (a is Mesh m)
                    {
                        mesh = m;
                        break;
                    }
                }
            }

            if (mesh == null)
            {
                Debug.LogWarning($"[CharacterAssetPipeline] Missing mesh at {objPath} for {propName}");
                return;
            }

            var go = new GameObject(propName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            mr.receiveShadows = true;
        }
    }
}
