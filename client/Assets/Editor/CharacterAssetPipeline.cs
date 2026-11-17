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
            Debug.Log("[CharacterAssetPipeline] Starting Character Pipeline Configuration (Phase C.1)...");

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

            var matNeutralClay = CreateNeutralClayMaterial("M_Operator_Neutral_Clay", SharedMaterialsDir);

            // 2. Build Continuous Anatomical Skinned Mesh, Skeleton & Humanoid Avatar
            var (avatar, sharedMesh) = BuildSharedSkeletonAndAvatar(matUnderlayer);

            // 3. Build 13-State Animator Controller with Real Humanoid Skeletal Curves
            var animController = BuildAnimatorController(avatar);

            // 4. Assemble Operator Prefabs: REX and TRACE
            BuildOperatorPrefab("rex", "Rogue", avatar, sharedMesh, matUnderlayer, matRexAssault, animController);
            BuildOperatorPrefab("trace", "Sentinel", avatar, sharedMesh, matUnderlayer, matTraceRecon, animController);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[CharacterAssetPipeline] Phase C.1 Character Foundation successfully configured!");
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

        public static Material CreateNeutralClayMaterial(string matName, string folder)
        {
            string matPath = $"{folder}/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = matName;
                AssetDatabase.CreateAsset(mat, matPath);
                Debug.Log($"[CharacterAssetPipeline] Created Neutral Clay Material: {matPath}");
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            Color clayColor = new Color(0.62f, 0.64f, 0.66f, 1.0f);
            mat.color = clayColor;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", clayColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", clayColor);

            mat.SetTexture("_BaseMap", null);
            mat.SetTexture("_MainTex", null);
            mat.SetTexture("_BumpMap", null);
            mat.DisableKeyword("_NORMALMAP");
            mat.SetTexture("_MetallicGlossMap", null);
            mat.DisableKeyword("_METALLICSPECGLOSSMAP");

            mat.SetFloat("_Smoothness", 0.22f);
            mat.SetFloat("_Metallic", 0.0f);
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

                // Build Bone Hierarchy Transforms in perfect anatomical T-Pose
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

                // Generate Continuous Anatomical Skinned Humanoid Mesh (Phase C.1)
                Mesh skinnedMesh = GenerateContinuousHumanoidSkinnedMesh(bones, root);
                string meshAssetPath = $"{SharedMeshesDir}/SK_Humanoid_Operator.asset";
                AssetDatabase.CreateAsset(skinnedMesh, meshAssetPath);
                Debug.Log($"[CharacterAssetPipeline] Saved Continuous Anatomical Skinned Mesh: {meshAssetPath} ({skinnedMesh.vertexCount} verts, {skinnedMesh.triangles.Length / 3} tris)");

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

        // ==============================================================================
        // C.1.1 CONTINUOUS ANATOMICAL HUMANOID MESH GENERATOR (100% Watertight 1-Component)
        // ==============================================================================

        private static Mesh GenerateContinuousHumanoidSkinnedMesh(Transform[] bones, Transform root)
        {
            var mesh = new Mesh { name = "SK_Humanoid_Operator" };
            var verts = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var triangles = new List<int>();

            int AddVertex(Vector3 pos, Vector3 norm, Vector2 uv, int b0, float w0, int b1 = 0, float w1 = 0f)
            {
                verts.Add(pos);
                normals.Add(norm);
                uvs.Add(uv);
                weights.Add(new BoneWeight
                {
                    boneIndex0 = b0,
                    weight0 = Mathf.Clamp01(w0),
                    boneIndex1 = b1,
                    weight1 = Mathf.Clamp01(w1)
                });
                return verts.Count - 1;
            }

            void AddTriangle(int i0, int i1, int i2)
            {
                triangles.Add(i0);
                triangles.Add(i1);
                triangles.Add(i2);
            }

            void AddQuad(int i0, int i1, int i2, int i3)
            {
                triangles.Add(i0);
                triangles.Add(i1);
                triangles.Add(i2);

                triangles.Add(i0);
                triangles.Add(i2);
                triangles.Add(i3);
            }

            void StitchRings(int[] ringA, int[] ringB, int[] skipIndices = null)
            {
                int n = ringA.Length;
                var skip = skipIndices != null ? new HashSet<int>(skipIndices) : null;
                for (int i = 0; i < n; i++)
                {
                    if (skip != null && skip.Contains(i)) continue;
                    int next = (i + 1) % n;
                    AddQuad(ringA[i], ringA[next], ringB[next], ringB[i]);
                }
            }

            const int nBody = 16;

            // 1. HEAD & SKULL WITH HIGH-DEFINITION FACIAL FEATURES (15 slices)
            int headTop = AddVertex(new Vector3(0f, 1.84f, 0.01f), Vector3.up, new Vector2(0.5f, 1.0f), 6, 1.0f);

            var headLevels = new (float y, float rx, float rz, float cz, string feat, int b0, float w0, int b1, float w1)[]
            {
                (1.815f, 0.052f, 0.062f,  0.010f, "cranium",     6, 1.00f, 5, 0.00f),
                (1.785f, 0.078f, 0.088f,  0.012f, "cranium",     6, 1.00f, 5, 0.00f),
                (1.745f, 0.092f, 0.104f,  0.015f, "cranium",     6, 1.00f, 5, 0.00f),
                (1.705f, 0.098f, 0.114f,  0.018f, "forehead",    6, 1.00f, 5, 0.00f),
                (1.665f, 0.100f, 0.118f,  0.022f, "brow",        6, 1.00f, 5, 0.00f),
                (1.635f, 0.096f, 0.112f,  0.020f, "eyesockets",  6, 1.00f, 5, 0.00f),
                (1.605f, 0.092f, 0.126f,  0.032f, "nose_bridge", 6, 1.00f, 5, 0.00f),
                (1.575f, 0.088f, 0.138f,  0.042f, "nose_tip",    6, 1.00f, 5, 0.00f),
                (1.545f, 0.084f, 0.118f,  0.022f, "philtrum",    6, 1.00f, 5, 0.00f),
                (1.515f, 0.082f, 0.116f,  0.020f, "lips",        6, 0.95f, 5, 0.05f),
                (1.485f, 0.078f, 0.108f,  0.016f, "chin",        6, 0.85f, 5, 0.15f),
                (1.455f, 0.072f, 0.094f,  0.008f, "jaw",         6, 0.65f, 5, 0.35f),
                (1.425f, 0.068f, 0.078f, -0.005f, "throat",      5, 0.85f, 6, 0.15f),
                (1.395f, 0.072f, 0.082f, -0.005f, "neck",        5, 0.85f, 4, 0.15f),
                (1.360f, 0.086f, 0.094f,  0.000f, "collar",      4, 0.80f, 5, 0.20f)
            };

            var headRings = new List<int[]>();
            for (int r = 0; r < headLevels.Length; r++)
            {
                var lvl = headLevels[r];
                int[] ring = new int[nBody];
                for (int i = 0; i < nBody; i++)
                {
                    float theta = 2.0f * Mathf.PI * i / nBody;
                    float cosT = Mathf.Cos(theta);
                    float sinT = Mathf.Sin(theta);

                    float localRx = lvl.rx;
                    float localRz = lvl.rz;
                    float localCz = lvl.cz;

                    if (sinT > 0.4f)
                    {
                        float factor = (sinT - 0.4f) / 0.6f;
                        if (lvl.feat == "brow")
                            localRz += 0.014f * factor;
                        else if (lvl.feat == "eyesockets")
                            localRz -= 0.010f * factor;
                        else if (lvl.feat == "nose_bridge" && Mathf.Abs(cosT) < 0.35f)
                            localRz += 0.022f * factor;
                        else if (lvl.feat == "nose_tip")
                        {
                            if (Mathf.Abs(cosT) < 0.30f)
                                localRz += 0.032f * factor;
                            else
                                localRz -= 0.006f * factor;
                        }
                        else if (lvl.feat == "philtrum" && Mathf.Abs(cosT) < 0.20f)
                            localRz += 0.008f * factor;
                        else if (lvl.feat == "lips" && Mathf.Abs(cosT) < 0.35f)
                            localRz += 0.016f * factor;
                        else if (lvl.feat == "chin" && Mathf.Abs(cosT) < 0.35f)
                            localRz += 0.022f * factor;
                        else if (lvl.feat == "jaw" && Mathf.Abs(cosT) > 0.4f)
                            localRx += 0.010f;
                    }
                    else if (sinT < -0.3f)
                    {
                        localRz += 0.012f;
                    }

                    float vx = localRx * cosT;
                    float vz = localCz + localRz * sinT;
                    Vector3 pos = new Vector3(vx, lvl.y, vz);
                    Vector3 norm = new Vector3(cosT, 0.1f, sinT).normalized;
                    Vector2 uv = new Vector2(0.5f + 0.42f * sinT, Mathf.Clamp01((lvl.y - 1.30f) / 0.55f));
                    ring[i] = AddVertex(pos, norm, uv, lvl.b0, lvl.w0, lvl.b1, lvl.w1);
                }
                headRings.Add(ring);
            }

            // Top pole fan
            for (int i = 0; i < nBody; i++)
            {
                int next = (i + 1) % nBody;
                AddTriangle(headTop, headRings[0][i], headRings[0][next]);
            }

            for (int r = 0; r < headRings.Count - 1; r++)
            {
                StitchRings(headRings[r], headRings[r + 1]);
            }

            int[] collarRing = headRings[headRings.Count - 1];

            // 2. TORSO WITH ANATOMICAL PECTORALS, STERNUM, WAIST, GLUTEALS (16 slices)
            var torsoLevels = new (float y, float rx, float rz, float cz, string feat, int b0, float w0, int b1, float w1)[]
            {
                (1.335f, 0.232f, 0.126f, -0.005f, "shoulders_traps", 4, 0.90f, 3,  0.10f),
                (1.295f, 0.224f, 0.148f,  0.014f, "upper_pecs",       4, 0.75f, 3,  0.25f),
                (1.250f, 0.218f, 0.150f,  0.015f, "mid_pecs",         3, 0.85f, 4,  0.15f),
                (1.205f, 0.208f, 0.144f,  0.010f, "lower_pecs",       3, 0.90f, 2,  0.10f),
                (1.160f, 0.198f, 0.138f,  0.005f, "infra_pecs",       3, 0.70f, 2,  0.30f),
                (1.115f, 0.188f, 0.130f,  0.000f, "ribcage_arch",     2, 0.70f, 3,  0.30f),
                (1.070f, 0.176f, 0.124f,  0.000f, "mid_ribs",         2, 0.85f, 3,  0.15f),
                (1.025f, 0.165f, 0.118f,  0.000f, "floating_ribs",    2, 0.90f, 1,  0.10f),
                (0.980f, 0.158f, 0.114f,  0.000f, "narrow_waist",     2, 0.80f, 1,  0.20f),
                (0.935f, 0.164f, 0.118f,  0.004f, "navel_abdomen",    2, 0.50f, 1,  0.50f),
                (0.890f, 0.172f, 0.125f,  0.002f, "lower_abdomen",    1, 0.80f, 2,  0.20f),
                (0.850f, 0.182f, 0.134f, -0.004f, "iliac_crest",      1, 0.90f, 2,  0.10f),
                (0.810f, 0.188f, 0.144f, -0.014f, "gluteus_max",      1, 0.95f, 17, 0.05f),
                (0.770f, 0.182f, 0.138f, -0.012f, "mid_pelvis",       1, 0.98f, 17, 0.02f),
                (0.735f, 0.170f, 0.126f, -0.006f, "lower_pelvis",     1, 1.00f, 0,  0.00f),
                (0.700f, 0.152f, 0.112f,  0.000f, "crotch_ring",      1, 1.00f, 0,  0.00f)
            };

            var torsoRings = new List<int[]>();
            int[] prevTorsoRing = collarRing;
            for (int r = 0; r < torsoLevels.Length; r++)
            {
                var lvl = torsoLevels[r];
                int[] ring = new int[nBody];
                for (int i = 0; i < nBody; i++)
                {
                    float theta = 2.0f * Mathf.PI * i / nBody;
                    float cosT = Mathf.Cos(theta);
                    float sinT = Mathf.Sin(theta);

                    float localRx = lvl.rx;
                    float localRz = lvl.rz;

                    if (lvl.feat.Contains("pec") && sinT > 0.35f)
                    {
                        if (Mathf.Abs(cosT) < 0.22f)
                            localRz -= 0.015f;
                        else
                            localRz += 0.018f;
                    }
                    else if (lvl.feat == "gluteus_max" && sinT < -0.35f)
                    {
                        if (Mathf.Abs(cosT) < 0.20f)
                            localRz -= 0.014f;
                        else
                            localRz += 0.016f;
                    }
                    else if (lvl.feat.Contains("abdomen") && sinT > 0.4f)
                    {
                        if (Mathf.Abs(cosT) < 0.20f)
                            localRz -= 0.006f;
                        else
                            localRz += 0.008f;
                    }

                    float vx = localRx * cosT;
                    float vz = lvl.cz + localRz * sinT;
                    Vector3 pos = new Vector3(vx, lvl.y, vz);
                    Vector3 norm = new Vector3(cosT, 0f, sinT).normalized;
                    Vector2 uv = new Vector2(0.5f + 0.42f * sinT, Mathf.Clamp01((lvl.y - 0.65f) / 0.75f));
                    ring[i] = AddVertex(pos, norm, uv, lvl.b0, lvl.w0, lvl.b1, lvl.w1);
                }

                // Skip quads where left and right armholes open!
                if (r == 1)
                {
                    StitchRings(prevTorsoRing, ring, new[] { 7, 8, 15, 0 });
                }
                else
                {
                    StitchRings(prevTorsoRing, ring);
                }

                prevTorsoRing = ring;
                torsoRings.Add(ring);
            }

            int[] pelvisBottom = torsoRings[torsoRings.Count - 1];

            // 3. LEGS & BOOTS (Continuous Pelvis to Thigh, Knee, Calf, Ankle, and Boot)
            int crotchMid = AddVertex(new Vector3(0f, 0.68f, 0f), Vector3.down, new Vector2(0.5f, 0.35f), 1, 1.0f);

            int[] leftHip = new[]
            {
                pelvisBottom[4], pelvisBottom[5], pelvisBottom[6], pelvisBottom[7],
                pelvisBottom[8], pelvisBottom[9], pelvisBottom[10], pelvisBottom[11],
                pelvisBottom[12], crotchMid
            };

            int[] rightHip = new[]
            {
                pelvisBottom[12], pelvisBottom[13], pelvisBottom[14], pelvisBottom[15],
                pelvisBottom[0], pelvisBottom[1], pelvisBottom[2], pelvisBottom[3],
                pelvisBottom[4], crotchMid
            };

            void BuildContinuousLeg(int[] hipRing, bool isLeft)
            {
                float sign = isLeft ? -1.0f : 1.0f;
                float legCx = sign * 0.115f;
                int nLeg = hipRing.Length; // 10 vertices
                int thighB = isLeft ? 17 : 21;
                int calfB = isLeft ? 18 : 22;
                int footB = isLeft ? 19 : 23;
                int toeB = isLeft ? 20 : 24;

                var legLevels = new (float y, float rx, float rz, float cz, string feat, int b0, float w0, int b1, float w1)[]
                {
                    (0.665f, 0.094f, 0.094f,  0.000f, "upper_groin",        thighB, 0.90f, 1,      0.10f),
                    (0.625f, 0.090f, 0.090f,  0.004f, "upper_thigh",        thighB, 0.95f, calfB,  0.05f),
                    (0.575f, 0.086f, 0.086f,  0.006f, "quad_belly",         thighB, 0.92f, calfB,  0.08f),
                    (0.525f, 0.080f, 0.080f,  0.004f, "lower_quad",         thighB, 0.82f, calfB,  0.18f),
                    (0.480f, 0.074f, 0.074f,  0.000f, "suprapatellar",      thighB, 0.65f, calfB,  0.35f),
                    (0.450f, 0.069f, 0.076f,  0.016f, "patella_upper",      thighB, 0.52f, calfB,  0.48f),
                    (0.425f, 0.068f, 0.078f,  0.018f, "patella_apex",       calfB,  0.52f, thighB, 0.48f),
                    (0.395f, 0.066f, 0.070f,  0.005f, "infrapatellar",      calfB,  0.75f, thighB, 0.25f),
                    (0.360f, 0.071f, 0.078f, -0.012f, "gastrocnemius_upper",calfB,  0.92f, thighB, 0.08f),
                    (0.320f, 0.074f, 0.082f, -0.016f, "gastrocnemius_peak", calfB,  0.96f, footB,  0.04f),
                    (0.275f, 0.070f, 0.076f, -0.012f, "soleus_calf",        calfB,  0.92f, footB,  0.08f),
                    (0.225f, 0.062f, 0.064f, -0.006f, "achilles_upper",     calfB,  0.82f, footB,  0.18f),
                    (0.175f, 0.054f, 0.054f,  0.000f, "supramalleolar",     calfB,  0.65f, footB,  0.35f),
                    (0.135f, 0.050f, 0.052f,  0.000f, "malleoli",           footB,  0.75f, calfB,  0.25f),
                    (0.095f, 0.046f, 0.068f,  0.014f, "calcaneus_heel",     footB,  0.88f, toeB,   0.12f),
                    (0.060f, 0.046f, 0.086f,  0.034f, "instep_arch",        footB,  0.92f, toeB,   0.08f),
                    (0.030f, 0.046f, 0.104f,  0.052f, "metatarsal_ball",    toeB,   0.70f, footB,  0.30f),
                    (0.012f, 0.044f, 0.118f,  0.066f, "toe_box",            toeB,   0.95f, footB,  0.05f)
                };

                int[] prevLegRing = hipRing;
                for (int r = 0; r < legLevels.Length; r++)
                {
                    var lvl = legLevels[r];
                    int[] ring = new int[nLeg];
                    for (int i = 0; i < nLeg; i++)
                    {
                        float theta = 2.0f * Mathf.PI * i / nLeg;
                        float cosT = Mathf.Cos(theta);
                        float sinT = Mathf.Sin(theta);

                        float localRx = lvl.rx;
                        float localRz = lvl.rz;

                        if (lvl.feat.Contains("patella") && sinT > 0.35f)
                            localRz += 0.018f * (sinT - 0.35f) / 0.65f;
                        else if (lvl.feat.Contains("patella") && sinT < -0.4f)
                            localRz -= 0.010f;
                        else if (lvl.feat.Contains("calcaneus") && sinT < -0.3f)
                            localRz += 0.026f * Mathf.Abs(sinT);
                        else if (lvl.feat == "malleoli" && Mathf.Abs(cosT) > 0.45f)
                            localRx += 0.010f;

                        float vx = legCx + localRx * cosT;
                        float vz = lvl.cz + localRz * sinT;
                        Vector3 pos = new Vector3(vx, lvl.y, vz);
                        Vector3 norm = new Vector3(cosT, 0f, sinT).normalized;
                        Vector2 uv = new Vector2((isLeft ? 0.30f : 0.70f) + 0.18f * sinT, Mathf.Clamp01(lvl.y / 0.70f));
                        ring[i] = AddVertex(pos, norm, uv, lvl.b0, lvl.w0, lvl.b1, lvl.w1);
                    }
                    StitchRings(prevLegRing, ring);
                    prevLegRing = ring;
                }

                // Flat sole toe tip
                int toeTip = AddVertex(new Vector3(legCx, 0.012f, 0.192f), Vector3.forward, new Vector2(isLeft ? 0.30f : 0.70f, 0.02f), toeB, 1.0f);
                for (int i = 0; i < nLeg; i++)
                {
                    int next = (i + 1) % nLeg;
                    AddTriangle(toeTip, prevLegRing[i], prevLegRing[next]);
                }
            }

            BuildContinuousLeg(leftHip, true);
            BuildContinuousLeg(rightHip, false);

            // 4. ARMS & HANDS (Continuous Shoulder to Deltoid, Bicep, Elbow, Forearm, Wrist, Glove)
            int[] leftArmhole = new[]
            {
                torsoRings[0][7], torsoRings[0][8], torsoRings[0][9],
                torsoRings[1][9], torsoRings[1][8], torsoRings[1][7]
            };

            int[] rightArmhole = new[]
            {
                torsoRings[0][15], torsoRings[0][0], torsoRings[0][1],
                torsoRings[1][1], torsoRings[1][0], torsoRings[1][15]
            };

            void BuildContinuousArm(int[] armhole, bool isLeft)
            {
                float sign = isLeft ? -1.0f : 1.0f;
                int nArm = armhole.Length; // 6 vertices
                int clavB = isLeft ? 7 : 12;
                int upB = isLeft ? 8 : 13;
                int lowB = isLeft ? 9 : 14;
                int handB = isLeft ? 10 : 15;
                int fingB = isLeft ? 11 : 16;

                var armLevels = new (float x, float ry, float rz, float cz, string feat, int b0, float w0, int b1, float w1)[]
                {
                    (sign * 0.235f, 0.084f, 0.084f,  0.000f, "deltoid_root",        clavB, 0.40f, upB,   0.60f),
                    (sign * 0.275f, 0.082f, 0.082f,  0.000f, "deltoid_cap",         upB,   0.80f, clavB, 0.20f),
                    (sign * 0.320f, 0.076f, 0.076f,  0.004f, "deltoid_insertion",   upB,   0.92f, clavB, 0.08f),
                    (sign * 0.365f, 0.070f, 0.070f,  0.006f, "bicep_tricep",        upB,   0.96f, lowB,  0.04f),
                    (sign * 0.410f, 0.064f, 0.064f,  0.002f, "lower_upperarm",      upB,   0.88f, lowB,  0.12f),
                    (sign * 0.445f, 0.058f, 0.058f,  0.000f, "pre_elbow",           upB,   0.70f, lowB,  0.30f),
                    (sign * 0.475f, 0.053f, 0.059f, -0.014f, "elbow_olecranon",     upB,   0.50f, lowB,  0.50f),
                    (sign * 0.505f, 0.055f, 0.056f,  0.000f, "brachioradialis",     lowB,  0.70f, upB,   0.30f),
                    (sign * 0.550f, 0.058f, 0.058f,  0.000f, "forearm_belly",       lowB,  0.92f, handB, 0.08f),
                    (sign * 0.605f, 0.054f, 0.052f,  0.000f, "forearm_mid",         lowB,  0.95f, handB, 0.05f),
                    (sign * 0.655f, 0.048f, 0.042f,  0.000f, "forearm_taper",       lowB,  0.85f, handB, 0.15f),
                    (sign * 0.695f, 0.042f, 0.032f,  0.000f, "wrist_cuff",          handB, 0.60f, lowB,  0.40f),
                    (sign * 0.730f, 0.044f, 0.026f,  0.010f, "palm_thenar",         handB, 0.80f, fingB, 0.20f),
                    (sign * 0.765f, 0.046f, 0.024f,  0.018f, "palm_thumb",          handB, 0.85f, fingB, 0.15f),
                    (sign * 0.800f, 0.046f, 0.022f,  0.012f, "metacarpal_knuckles", handB, 0.50f, fingB, 0.50f),
                    (sign * 0.835f, 0.042f, 0.020f,  0.004f, "finger_proximal",     fingB, 0.75f, handB, 0.25f),
                    (sign * 0.865f, 0.036f, 0.018f,  0.000f, "finger_intermediate", fingB, 0.90f, handB, 0.10f),
                    (sign * 0.890f, 0.028f, 0.015f,  0.000f, "fingertips",          fingB, 1.00f, handB, 0.00f)
                };

                int[] prevArmRing = armhole;
                for (int r = 0; r < armLevels.Length; r++)
                {
                    var lvl = armLevels[r];
                    int[] ring = new int[nArm];
                    for (int i = 0; i < nArm; i++)
                    {
                        float theta = 2.0f * Mathf.PI * i / nArm;
                        float cosT = Mathf.Cos(theta);
                        float sinT = Mathf.Sin(theta);

                        float localRy = lvl.ry;
                        float localRz = lvl.rz;

                        if (lvl.feat.Contains("thumb") && sinT > 0.35f)
                            localRz += 0.018f * (sinT - 0.35f) / 0.65f;
                        else if (lvl.feat == "metacarpal_knuckles" && Mathf.Abs(cosT) > 0.4f)
                            localRy += 0.006f;

                        float vy = 1.34f + localRy * cosT;
                        float vz = lvl.cz + localRz * sinT;
                        Vector3 pos = new Vector3(lvl.x, vy, vz);
                        Vector3 norm = new Vector3(sign * 0.2f, cosT, sinT).normalized;
                        Vector2 uv = new Vector2((isLeft ? 0.25f : 0.75f) + 0.18f * sinT, Mathf.Clamp01(Mathf.Abs(lvl.x) / 0.95f));
                        ring[i] = AddVertex(pos, norm, uv, lvl.b0, lvl.w0, lvl.b1, lvl.w1);
                    }
                    StitchRings(prevArmRing, ring);
                    prevArmRing = ring;
                }

                // Fingertip cap pole
                int tipPole = AddVertex(new Vector3(sign * 0.910f, 1.34f, 0f), new Vector3(sign, 0f, 0f), new Vector2(isLeft ? 0.25f : 0.75f, 0.5f), fingB, 1.0f);
                for (int i = 0; i < nArm; i++)
                {
                    int next = (i + 1) % nArm;
                    if (isLeft)
                    {
                        AddTriangle(tipPole, prevArmRing[i], prevArmRing[next]);
                    }
                    else
                    {
                        AddTriangle(tipPole, prevArmRing[next], prevArmRing[i]);
                    }
                }
            }

            BuildContinuousArm(leftArmhole, true);
            BuildContinuousArm(rightArmhole, false);

            // Set Mesh Buffers
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
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
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
            skeletonBones.Add(new SkeletonBone
            {
                name = rootGo.name,
                position = rootGo.transform.localPosition,
                rotation = rootGo.transform.localRotation,
                scale = rootGo.transform.localScale
            });

            void AddSkeletonBonesRecursively(Transform current)
            {
                foreach (Transform child in current)
                {
                    skeletonBones.Add(new SkeletonBone
                    {
                        name = child.name,
                        position = child.localPosition,
                        rotation = child.localRotation,
                        scale = child.localScale
                    });
                    AddSkeletonBonesRecursively(child);
                }
            }

            AddSkeletonBonesRecursively(rootGo.transform);

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
                feetSpacing = 0f,
                hasTranslationDoF = false
            };

            Avatar avatar = AvatarBuilder.BuildHumanAvatar(rootGo, humanDesc);
            avatar.name = "OperatorHumanoidAvatar";
            return avatar;
        }

        // ==============================================================================
        // C.1.3 REAL HUMAN KEYFRAMED SKELETAL ANIMATION CLIPS & CONTROLLER
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

        // Standard bone relative transform paths
        private const string PathHips = "ThirdPersonPresentation/Skeleton/Root/Hips";
        private const string PathSpine = "ThirdPersonPresentation/Skeleton/Root/Hips/Spine";
        private const string PathChest = "ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest";
        private const string PathUpperChest = "ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest";
        private const string PathHead = "ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest/Neck/Head";
        private const string PathLUpperArm = "ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest/LeftClavicle/LeftUpperArm";
        private const string PathLLowerArm = "ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest/LeftClavicle/LeftUpperArm/LeftLowerArm";
        private const string PathLHand = "ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest/LeftClavicle/LeftUpperArm/LeftLowerArm/LeftHand";
        private const string PathRUpperArm = "ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest/RightClavicle/RightUpperArm";
        private const string PathRLowerArm = "ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest/RightClavicle/RightUpperArm/RightLowerArm";
        private const string PathRHand = "ThirdPersonPresentation/Skeleton/Root/Hips/Spine/Chest/UpperChest/RightClavicle/RightUpperArm/RightLowerArm/RightHand";
        private const string PathLThigh = "ThirdPersonPresentation/Skeleton/Root/Hips/LeftThigh";
        private const string PathLCalf = "ThirdPersonPresentation/Skeleton/Root/Hips/LeftThigh/LeftCalf";
        private const string PathLFoot = "ThirdPersonPresentation/Skeleton/Root/Hips/LeftThigh/LeftCalf/LeftFoot";
        private const string PathRThigh = "ThirdPersonPresentation/Skeleton/Root/Hips/RightThigh";
        private const string PathRCalf = "ThirdPersonPresentation/Skeleton/Root/Hips/RightThigh/RightCalf";
        private const string PathRFoot = "ThirdPersonPresentation/Skeleton/Root/Hips/RightThigh/RightCalf/RightFoot";

        private static void AddRotCurve(AnimationClip clip, string path, float[] times, float[] eulerX, float[] eulerY, float[] eulerZ)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();

            for (int i = 0; i < times.Length; i++)
            {
                curveX.AddKey(new Keyframe(times[i], eulerX[i]));
                curveY.AddKey(new Keyframe(times[i], eulerY[i]));
                curveZ.AddKey(new Keyframe(times[i], eulerZ[i]));
            }

            clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.x", curveX);
            clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.y", curveY);
            clip.SetCurve(path, typeof(Transform), "localEulerAnglesRaw.z", curveZ);
        }

        private static void AddPosCurve(AnimationClip clip, string path, float[] times, float[] posX, float[] posY, float[] posZ)
        {
            var curveX = new AnimationCurve();
            var curveY = new AnimationCurve();
            var curveZ = new AnimationCurve();

            for (int i = 0; i < times.Length; i++)
            {
                curveX.AddKey(new Keyframe(times[i], posX[i]));
                curveY.AddKey(new Keyframe(times[i], posY[i]));
                curveZ.AddKey(new Keyframe(times[i], posZ[i]));
            }

            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", curveX);
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", curveY);
            clip.SetCurve(path, typeof(Transform), "m_LocalPosition.z", curveZ);
        }

        private static AnimationClip CreateIdleClip()
        {
            var clip = new AnimationClip { name = "Operator_Idle", legacy = false, wrapMode = WrapMode.Loop };
            float[] times = new float[] { 0.0f, 0.5f, 1.0f, 1.5f, 2.0f };

            AddPosCurve(clip, PathHips, times,
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0.92f, 0.924f, 0.92f, 0.916f, 0.92f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathHips, times,
                new float[] { 0f, 0.5f, 0f, -0.5f, 0f },
                new float[] { 0f, 1.2f, 0f, -1.2f, 0f },
                new float[] { 0f, 0.4f, 0f, -0.4f, 0f });

            AddRotCurve(clip, PathSpine, times,
                new float[] { 0f, 1.2f, 0f, -0.8f, 0f },
                new float[] { 0f, -0.8f, 0f, 0.8f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathChest, times,
                new float[] { 2.0f, 3.2f, 2.0f, 1.0f, 2.0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathHead, times,
                new float[] { 0f, -1.5f, 0f, 1.5f, 0f },
                new float[] { 0f, 2.0f, 0f, -2.0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            // Tactical low-ready arms
            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 34f, 35f, 34f, 33f, 34f },
                new float[] { -15f, -14f, -15f, -16f, -15f },
                new float[] { -10f, -10f, -10f, -10f, -10f });
            AddRotCurve(clip, PathRLowerArm, times,
                new float[] { 55f, 56f, 55f, 54f, 55f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathLUpperArm, times,
                new float[] { 24f, 25f, 24f, 23f, 24f },
                new float[] { 25f, 25f, 25f, 25f, 25f },
                new float[] { 15f, 15f, 15f, 15f, 15f });
            AddRotCurve(clip, PathLLowerArm, times,
                new float[] { 64f, 65f, 64f, 63f, 64f },
                new float[] { -15f, -15f, -15f, -15f, -15f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathLThigh, times,
                new float[] { 2f, 2f, 2f, 2f, 2f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { -2f, -2f, -2f, -2f, -2f });
            AddRotCurve(clip, PathRThigh, times,
                new float[] { 2f, 2f, 2f, 2f, 2f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 2f, 2f, 2f, 2f, 2f });

            return clip;
        }

        private static AnimationClip CreateWalkClip()
        {
            var clip = new AnimationClip { name = "Operator_Walk", legacy = false, wrapMode = WrapMode.Loop };
            float[] times = new float[] { 0.0f, 0.25f, 0.50f, 0.75f, 1.0f };

            AddPosCurve(clip, PathHips, times,
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0.92f, 0.945f, 0.92f, 0.945f, 0.92f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathHips, times,
                new float[] { 2f, 0f, 2f, 0f, 2f },
                new float[] { 0f, 4.5f, 0f, -4.5f, 0f },
                new float[] { 0f, 2.5f, 0f, -2.5f, 0f });

            AddRotCurve(clip, PathSpine, times,
                new float[] { 4f, 5f, 4f, 5f, 4f },
                new float[] { 0f, -3.5f, 0f, 3.5f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            // Legs alternating stride
            AddRotCurve(clip, PathLThigh, times,
                new float[] { 0f, 24f, 0f, -24f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });
            AddRotCurve(clip, PathLCalf, times,
                new float[] { 0f, -5f, 0f, -42f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });
            AddRotCurve(clip, PathLFoot, times,
                new float[] { 0f, 14f, 0f, -12f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathRThigh, times,
                new float[] { 0f, -24f, 0f, 24f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });
            AddRotCurve(clip, PathRCalf, times,
                new float[] { 0f, -42f, 0f, -5f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });
            AddRotCurve(clip, PathRFoot, times,
                new float[] { 0f, -12f, 0f, 14f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            // Arms natural counter-swing
            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 34f, 16f, 34f, 52f, 34f },
                new float[] { -15f, -15f, -15f, -15f, -15f },
                new float[] { -10f, -10f, -10f, -10f, -10f });
            AddRotCurve(clip, PathRLowerArm, times,
                new float[] { 55f, 65f, 55f, 45f, 55f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathLUpperArm, times,
                new float[] { 24f, 44f, 24f, 4f, 24f },
                new float[] { 25f, 25f, 25f, 25f, 25f },
                new float[] { 15f, 15f, 15f, 15f, 15f });
            AddRotCurve(clip, PathLLowerArm, times,
                new float[] { 64f, 55f, 64f, 75f, 64f },
                new float[] { -15f, -15f, -15f, -15f, -15f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            return clip;
        }

        private static AnimationClip CreateRunClip()
        {
            var clip = new AnimationClip { name = "Operator_Run", legacy = false, wrapMode = WrapMode.Loop };
            float[] times = new float[] { 0.0f, 0.1875f, 0.375f, 0.5625f, 0.75f };

            AddPosCurve(clip, PathHips, times,
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0.90f, 0.945f, 0.90f, 0.945f, 0.90f },
                new float[] { 0f, 0.02f, 0f, 0.02f, 0f });

            AddRotCurve(clip, PathHips, times,
                new float[] { 5f, 3f, 5f, 3f, 5f },
                new float[] { 0f, 6.5f, 0f, -6.5f, 0f },
                new float[] { 0f, 3.5f, 0f, -3.5f, 0f });

            AddRotCurve(clip, PathSpine, times,
                new float[] { 12f, 14f, 12f, 14f, 12f },
                new float[] { 0f, -5.0f, 0f, 5.0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathLThigh, times,
                new float[] { 0f, 38f, 0f, -38f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });
            AddRotCurve(clip, PathLCalf, times,
                new float[] { 0f, -10f, 0f, -65f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathRThigh, times,
                new float[] { 0f, -38f, 0f, 38f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });
            AddRotCurve(clip, PathRCalf, times,
                new float[] { 0f, -65f, 0f, -10f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 34f, 2f, 34f, 66f, 34f },
                new float[] { -15f, -15f, -15f, -15f, -15f },
                new float[] { -10f, -10f, -10f, -10f, -10f });
            AddRotCurve(clip, PathLUpperArm, times,
                new float[] { 24f, 58f, 24f, -10f, 24f },
                new float[] { 25f, 25f, 25f, 25f, 25f },
                new float[] { 15f, 15f, 15f, 15f, 15f });

            return clip;
        }

        private static AnimationClip CreateSprintClip()
        {
            var clip = new AnimationClip { name = "Operator_Sprint", legacy = false, wrapMode = WrapMode.Loop };
            float[] times = new float[] { 0.0f, 0.15f, 0.30f, 0.45f, 0.60f };

            AddPosCurve(clip, PathHips, times,
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0.88f, 0.94f, 0.88f, 0.94f, 0.88f },
                new float[] { 0.02f, 0.05f, 0.02f, 0.05f, 0.02f });

            AddRotCurve(clip, PathHips, times,
                new float[] { 8f, 5f, 8f, 5f, 8f },
                new float[] { 0f, 8.5f, 0f, -8.5f, 0f },
                new float[] { 0f, 4.0f, 0f, -4.0f, 0f });

            AddRotCurve(clip, PathSpine, times,
                new float[] { 18f, 20f, 18f, 20f, 18f },
                new float[] { 0f, -6.0f, 0f, 6.0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathLThigh, times,
                new float[] { 0f, 48f, 0f, -48f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });
            AddRotCurve(clip, PathLCalf, times,
                new float[] { 0f, -15f, 0f, -85f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathRThigh, times,
                new float[] { 0f, -48f, 0f, 48f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });
            AddRotCurve(clip, PathRCalf, times,
                new float[] { 0f, -85f, 0f, -15f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 34f, -10f, 34f, 78f, 34f },
                new float[] { -15f, -15f, -15f, -15f, -15f },
                new float[] { -10f, -10f, -10f, -10f, -10f });
            AddRotCurve(clip, PathLUpperArm, times,
                new float[] { 24f, 68f, 24f, -20f, 24f },
                new float[] { 25f, 25f, 25f, 25f, 25f },
                new float[] { 15f, 15f, 15f, 15f, 15f });

            return clip;
        }

        private static AnimationClip CreateCrouchClip()
        {
            var clip = new AnimationClip { name = "Operator_Crouch", legacy = false, wrapMode = WrapMode.Loop };
            float[] times = new float[] { 0.0f, 0.75f, 1.50f };

            AddPosCurve(clip, PathHips, times,
                new float[] { 0f, 0f, 0f },
                new float[] { 0.58f, 0.585f, 0.58f },
                new float[] { -0.06f, -0.06f, -0.06f });

            AddRotCurve(clip, PathHips, times,
                new float[] { 8f, 8.5f, 8f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });

            AddRotCurve(clip, PathSpine, times,
                new float[] { 18f, 19.5f, 18f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });

            AddRotCurve(clip, PathLThigh, times,
                new float[] { 58f, 58f, 58f },
                new float[] { 0f, 0f, 0f },
                new float[] { -10f, -10f, -10f });
            AddRotCurve(clip, PathLCalf, times,
                new float[] { -72f, -72f, -72f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });
            AddRotCurve(clip, PathLFoot, times,
                new float[] { 18f, 18f, 18f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });

            AddRotCurve(clip, PathRThigh, times,
                new float[] { 58f, 58f, 58f },
                new float[] { 0f, 0f, 0f },
                new float[] { 10f, 10f, 10f });
            AddRotCurve(clip, PathRCalf, times,
                new float[] { -72f, -72f, -72f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });
            AddRotCurve(clip, PathRFoot, times,
                new float[] { 18f, 18f, 18f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });

            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 42f, 43f, 42f },
                new float[] { -12f, -12f, -12f },
                new float[] { -10f, -10f, -10f });
            AddRotCurve(clip, PathRLowerArm, times,
                new float[] { 68f, 68f, 68f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });

            return clip;
        }

        private static AnimationClip CreateJumpClip()
        {
            var clip = new AnimationClip { name = "Operator_Jump", legacy = false, wrapMode = WrapMode.Once };
            float[] times = new float[] { 0.0f, 0.12f, 0.32f, 0.62f, 0.80f, 0.90f };

            AddPosCurve(clip, PathHips, times,
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f },
                new float[] { 0.92f, 0.82f, 1.25f, 1.15f, 0.80f, 0.92f },
                new float[] { 0f, 0f, 0.05f, 0.08f, 0.02f, 0f });

            AddRotCurve(clip, PathSpine, times,
                new float[] { 0f, 15f, -6f, 8f, 18f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathLThigh, times,
                new float[] { 0f, 25f, -10f, 35f, 45f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f });
            AddRotCurve(clip, PathLCalf, times,
                new float[] { 0f, -30f, 0f, -50f, -55f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathRThigh, times,
                new float[] { 0f, 25f, -10f, 35f, 45f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f });
            AddRotCurve(clip, PathRCalf, times,
                new float[] { 0f, -30f, 0f, -50f, -55f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 34f, -15f, 45f, 40f, 15f, 34f },
                new float[] { -15f, -15f, -15f, -15f, -15f, -15f },
                new float[] { -10f, -10f, -10f, -10f, -10f, -10f });

            return clip;
        }

        private static AnimationClip CreateAimClip()
        {
            var clip = new AnimationClip { name = "Operator_Aim", legacy = false, wrapMode = WrapMode.Loop };
            float[] times = new float[] { 0.0f, 0.5f, 1.0f };

            AddRotCurve(clip, PathSpine, times,
                new float[] { 4f, 4.4f, 4f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });

            AddRotCurve(clip, PathChest, times,
                new float[] { 0f, 0.5f, 0f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });

            AddRotCurve(clip, PathHead, times,
                new float[] { 4f, 4f, 4f },
                new float[] { -3f, -3f, -3f },
                new float[] { 4f, 4f, 4f });

            // ADS weapon raised in right hand
            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 68.0f, 68.3f, 68.0f },
                new float[] { -14.0f, -14.0f, -14.0f },
                new float[] { -12.0f, -12.0f, -12.0f });
            AddRotCurve(clip, PathRLowerArm, times,
                new float[] { 78.0f, 78.2f, 78.0f },
                new float[] { 4.0f, 4.0f, 4.0f },
                new float[] { 0.0f, 0.0f, 0.0f });
            AddRotCurve(clip, PathRHand, times,
                new float[] { 12.0f, 12.0f, 12.0f },
                new float[] { -6.0f, -6.0f, -6.0f },
                new float[] { 0.0f, 0.0f, 0.0f });

            // Left hand supporting barrel / foregrip
            AddRotCurve(clip, PathLUpperArm, times,
                new float[] { 54.0f, 54.2f, 54.0f },
                new float[] { 28.0f, 28.0f, 28.0f },
                new float[] { 18.0f, 18.0f, 18.0f });
            AddRotCurve(clip, PathLLowerArm, times,
                new float[] { 82.0f, 82.1f, 82.0f },
                new float[] { -18.0f, -18.0f, -18.0f },
                new float[] { 0.0f, 0.0f, 0.0f });

            return clip;
        }

        private static AnimationClip CreateFireClip()
        {
            var clip = new AnimationClip { name = "Operator_Fire", legacy = false, wrapMode = WrapMode.Once };
            float[] times = new float[] { 0.0f, 0.03f, 0.08f, 0.16f, 0.25f };

            // Recoil kick impulse
            AddRotCurve(clip, PathChest, times,
                new float[] { 0f, -3.5f, -1.8f, -0.4f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 68.0f, 82.0f, 74.0f, 69.5f, 68.0f },
                new float[] { -14.0f, -16.0f, -14.8f, -14.2f, -14.0f },
                new float[] { -12.0f, -16.0f, -13.5f, -12.3f, -12.0f });

            AddRotCurve(clip, PathRLowerArm, times,
                new float[] { 78.0f, 70.0f, 75.0f, 77.2f, 78.0f },
                new float[] { 4.0f, 6.0f, 4.8f, 4.2f, 4.0f },
                new float[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

            AddRotCurve(clip, PathLUpperArm, times,
                new float[] { 54.0f, 62.0f, 57.0f, 54.8f, 54.0f },
                new float[] { 28.0f, 26.0f, 27.5f, 28.0f, 28.0f },
                new float[] { 18.0f, 15.0f, 17.0f, 18.0f, 18.0f });

            return clip;
        }

        private static AnimationClip CreateReloadClip()
        {
            var clip = new AnimationClip { name = "Operator_Reload", legacy = false, wrapMode = WrapMode.Once };
            float[] times = new float[] { 0.0f, 0.35f, 0.70f, 1.15f, 1.45f, 1.80f, 2.20f };

            // Right arm lowers slightly, then rises back to aim
            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 68.0f, 52.0f, 50.0f, 54.0f, 58.0f, 65.0f, 68.0f },
                new float[] { -14.0f, -10.0f, -10.0f, -12.0f, -14.0f, -14.0f, -14.0f },
                new float[] { -12.0f, -8.0f, -8.0f, -10.0f, -12.0f, -12.0f, -12.0f });

            // Left arm reaches to hip pouch, pulls mag, slaps into magwell, returns to handguard
            AddRotCurve(clip, PathLUpperArm, times,
                new float[] { 54.0f, 18.0f, 12.0f, 48.0f, 58.0f, 52.0f, 54.0f },
                new float[] { 28.0f, 8.0f, 6.0f, 20.0f, 24.0f, 26.0f, 28.0f },
                new float[] { 18.0f, 5.0f, 4.0f, 12.0f, 16.0f, 18.0f, 18.0f });

            AddRotCurve(clip, PathLLowerArm, times,
                new float[] { 82.0f, 45.0f, 42.0f, 85.0f, 92.0f, 84.0f, 82.0f },
                new float[] { -18.0f, -5.0f, -5.0f, -15.0f, -18.0f, -18.0f, -18.0f },
                new float[] { 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f, 0.0f });

            // Upward slap impulse on chest at t=1.45s
            AddRotCurve(clip, PathChest, times,
                new float[] { 0f, 2f, 2f, 1f, -3f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f });

            return clip;
        }

        private static AnimationClip CreateHitReactionClip()
        {
            var clip = new AnimationClip { name = "Operator_HitReaction", legacy = false, wrapMode = WrapMode.Once };
            float[] times = new float[] { 0.0f, 0.06f, 0.18f, 0.30f, 0.40f };

            AddRotCurve(clip, PathHips, times,
                new float[] { 0f, 4.0f, 2.0f, 0f, 0f },
                new float[] { 0f, -3.0f, -1.0f, 0f, 0f },
                new float[] { 0f, 2.0f, 1.0f, 0f, 0f });

            AddRotCurve(clip, PathSpine, times,
                new float[] { 0f, -18.0f, -9.0f, -2.0f, 0f },
                new float[] { 0f, -10.0f, -5.0f, -1.0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathChest, times,
                new float[] { 0f, -12.0f, -6.0f, -1.0f, 0f },
                new float[] { 0f, -6.0f, -3.0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathUpperChest, times,
                new float[] { 0f, -8.0f, -4.0f, 0f, 0f },
                new float[] { 0f, -4.0f, -2.0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathHead, times,
                new float[] { 0f, -15.0f, -7.0f, 2.0f, 0f },
                new float[] { 0f, 8.0f, 4.0f, 0f, 0f },
                new float[] { 0f, 6.0f, 3.0f, 0f, 0f });

            AddRotCurve(clip, PathLUpperArm, times,
                new float[] { 22f, 35f, 28f, 24f, 22f },
                new float[] { 10f, 18f, 14f, 11f, 10f },
                new float[] { 5f, 12f, 8f, 6f, 5f });

            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 34f, 48f, 40f, 36f, 34f },
                new float[] { -15f, -28f, -20f, -16f, -15f },
                new float[] { -10f, -25f, -18f, -12f, -10f });

            return clip;
        }

        private static AnimationClip CreateDownedClip()
        {
            var clip = new AnimationClip { name = "Operator_Downed", legacy = false, wrapMode = WrapMode.Loop };
            float[] times = new float[] { 0.0f, 1.0f, 2.0f };

            AddPosCurve(clip, PathHips, times,
                new float[] { 0f, 0f, 0f },
                new float[] { 0.22f, 0.228f, 0.22f },
                new float[] { 0f, 0f, 0f });

            AddRotCurve(clip, PathHips, times,
                new float[] { 10f, 11f, 10f },
                new float[] { -15f, -14f, -15f },
                new float[] { 22f, 23f, 22f });

            AddRotCurve(clip, PathSpine, times,
                new float[] { 25f, 27f, 25f },
                new float[] { 12f, 12f, 12f },
                new float[] { -18f, -18f, -18f });

            AddRotCurve(clip, PathHead, times,
                new float[] { 25f, 28f, 25f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });

            // Propped on left arm
            AddRotCurve(clip, PathLUpperArm, times,
                new float[] { 40f, 40f, 40f },
                new float[] { 35f, 35f, 35f },
                new float[] { 30f, 30f, 30f });
            AddRotCurve(clip, PathLLowerArm, times,
                new float[] { 85f, 85f, 85f },
                new float[] { 0f, 0f, 0f },
                new float[] { 15f, 15f, 15f });

            // Right arm clutching chest
            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 55f, 57f, 55f },
                new float[] { -30f, -30f, -30f },
                new float[] { -20f, -20f, -20f });
            AddRotCurve(clip, PathRLowerArm, times,
                new float[] { 85f, 86f, 85f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });

            // Sprawled legs
            AddRotCurve(clip, PathLThigh, times,
                new float[] { 65f, 65f, 65f },
                new float[] { 0f, 0f, 0f },
                new float[] { -35f, -35f, -35f });
            AddRotCurve(clip, PathLCalf, times,
                new float[] { -75f, -75f, -75f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });

            AddRotCurve(clip, PathRThigh, times,
                new float[] { 15f, 15f, 15f },
                new float[] { 0f, 0f, 0f },
                new float[] { 25f, 25f, 25f });
            AddRotCurve(clip, PathRCalf, times,
                new float[] { -30f, -30f, -30f },
                new float[] { 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f });

            return clip;
        }

        private static AnimationClip CreateReviveClip()
        {
            var clip = new AnimationClip { name = "Operator_Revive", legacy = false, wrapMode = WrapMode.Once };
            float[] times = new float[] { 0.0f, 0.6f, 1.2f, 1.8f, 2.3f, 2.5f };

            AddPosCurve(clip, PathHips, times,
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f },
                new float[] { 0.22f, 0.38f, 0.60f, 0.82f, 0.92f, 0.92f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathHips, times,
                new float[] { 10f, 8f, 15f, 5f, 0f, 0f },
                new float[] { -15f, -8f, 0f, 0f, 0f, 0f },
                new float[] { 22f, 10f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathSpine, times,
                new float[] { 25f, 15f, 25f, 10f, 2f, 0f },
                new float[] { 12f, 6f, 0f, 0f, 0f, 0f },
                new float[] { -18f, -8f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathLThigh, times,
                new float[] { 65f, 50f, 60f, 25f, 2f, 2f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f },
                new float[] { -35f, -20f, 0f, 0f, -2f, -2f });
            AddRotCurve(clip, PathLCalf, times,
                new float[] { -75f, -60f, -80f, -30f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathRThigh, times,
                new float[] { 15f, 30f, 60f, 10f, 2f, 2f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f },
                new float[] { 25f, 10f, 0f, 0f, 2f, 2f });
            AddRotCurve(clip, PathRCalf, times,
                new float[] { -30f, -40f, -80f, -15f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 55f, 40f, 20f, 30f, 34f, 34f },
                new float[] { -30f, -20f, -15f, -15f, -15f, -15f },
                new float[] { -20f, -10f, -10f, -10f, -10f, -10f });

            return clip;
        }

        private static AnimationClip CreateDeathClip()
        {
            var clip = new AnimationClip { name = "Operator_Death", legacy = false, wrapMode = WrapMode.Once };
            float[] times = new float[] { 0.0f, 0.25f, 0.70f, 1.10f, 1.50f };

            AddPosCurve(clip, PathHips, times,
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0.92f, 0.72f, 0.20f, 0.14f, 0.14f },
                new float[] { 0f, -0.10f, -0.40f, -0.45f, -0.45f });

            AddRotCurve(clip, PathHips, times,
                new float[] { 0f, -15f, -65f, -85f, -85f },
                new float[] { 0f, 5f, 15f, 20f, 20f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathSpine, times,
                new float[] { 0f, -20f, -45f, -60f, -60f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathLThigh, times,
                new float[] { 0f, 30f, 15f, 5f, 5f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, -15f, -25f, -30f, -30f });
            AddRotCurve(clip, PathLCalf, times,
                new float[] { 0f, -50f, -20f, -10f, -10f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathRThigh, times,
                new float[] { 0f, 30f, 15f, 5f, 5f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 15f, 25f, 30f, 30f });
            AddRotCurve(clip, PathRCalf, times,
                new float[] { 0f, -50f, -20f, -10f, -10f },
                new float[] { 0f, 0f, 0f, 0f, 0f },
                new float[] { 0f, 0f, 0f, 0f, 0f });

            AddRotCurve(clip, PathRUpperArm, times,
                new float[] { 34f, 60f, 45f, 20f, 15f },
                new float[] { -15f, -25f, -35f, -45f, -45f },
                new float[] { -10f, -20f, -30f, -35f, -35f });

            AddRotCurve(clip, PathLUpperArm, times,
                new float[] { 24f, 50f, 35f, 20f, 15f },
                new float[] { 25f, 35f, 45f, 50f, 50f },
                new float[] { 15f, 25f, 30f, 35f, 35f });

            return clip;
        }

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

            // 1. Author and Save Real Animation Clips for each required state
            var clipFactories = new Dictionary<string, Func<AnimationClip>>
            {
                { "Idle", CreateIdleClip },
                { "Walk", CreateWalkClip },
                { "Run", CreateRunClip },
                { "Sprint", CreateSprintClip },
                { "Crouch", CreateCrouchClip },
                { "Jump", CreateJumpClip },
                { "Aim", CreateAimClip },
                { "Fire", CreateFireClip },
                { "Reload", CreateReloadClip },
                { "HitReaction", CreateHitReactionClip },
                { "Downed", CreateDownedClip },
                { "Revive", CreateReviveClip },
                { "Death", CreateDeathClip }
            };

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var kvp in clipFactories)
            {
                string clipPath = $"{SharedAnimationsDir}/Operator_{kvp.Key}.anim";
                var clip = kvp.Value();
                AssetDatabase.CreateAsset(clip, clipPath);
                clips[kvp.Key] = clip;
                Debug.Log($"[CharacterAssetPipeline] Authored skeletal AnimationClip: {clipPath}");
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
            tIdleWalk.duration = 0.15f;

            var tWalkIdle = sWalk.AddTransition(sIdle);
            tWalkIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tWalkIdle.hasExitTime = false;
            tWalkIdle.duration = 0.15f;

            var tWalkRun = sWalk.AddTransition(sRun);
            tWalkRun.AddCondition(AnimatorConditionMode.Greater, 3.0f, "Speed");
            tWalkRun.hasExitTime = false;
            tWalkRun.duration = 0.18f;

            var tRunWalk = sRun.AddTransition(sWalk);
            tRunWalk.AddCondition(AnimatorConditionMode.Less, 3.0f, "Speed");
            tRunWalk.hasExitTime = false;
            tRunWalk.duration = 0.18f;

            var tRunSprint = sRun.AddTransition(sSprint);
            tRunSprint.AddCondition(AnimatorConditionMode.Greater, 6.0f, "Speed");
            tRunSprint.hasExitTime = false;
            tRunSprint.duration = 0.18f;

            var tSprintRun = sSprint.AddTransition(sRun);
            tSprintRun.AddCondition(AnimatorConditionMode.Less, 6.0f, "Speed");
            tSprintRun.hasExitTime = false;
            tSprintRun.duration = 0.18f;

            // Crouch Transitions
            var tToCrouch = rootSm.AddAnyStateTransition(sCrouch);
            tToCrouch.AddCondition(AnimatorConditionMode.If, 0, "IsCrouched");
            tToCrouch.AddCondition(AnimatorConditionMode.Equals, 0, "LifeState");
            tToCrouch.hasExitTime = false;
            tToCrouch.duration = 0.20f;

            var tFromCrouchIdle = sCrouch.AddTransition(sIdle);
            tFromCrouchIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouched");
            tFromCrouchIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tFromCrouchIdle.hasExitTime = false;
            tFromCrouchIdle.duration = 0.20f;

            var tFromCrouchWalk = sCrouch.AddTransition(sWalk);
            tFromCrouchWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsCrouched");
            tFromCrouchWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tFromCrouchWalk.hasExitTime = false;
            tFromCrouchWalk.duration = 0.20f;

            // Jump Transition
            var tToJump = rootSm.AddAnyStateTransition(sJump);
            tToJump.AddCondition(AnimatorConditionMode.IfNot, 0, "IsGrounded");
            tToJump.AddCondition(AnimatorConditionMode.Equals, 0, "LifeState");
            tToJump.hasExitTime = false;
            tToJump.duration = 0.10f;

            var tFromJumpIdle = sJump.AddTransition(sIdle);
            tFromJumpIdle.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            tFromJumpIdle.AddCondition(AnimatorConditionMode.Less, 0.1f, "Speed");
            tFromJumpIdle.hasExitTime = false;
            tFromJumpIdle.duration = 0.15f;

            var tFromJumpWalk = sJump.AddTransition(sWalk);
            tFromJumpWalk.AddCondition(AnimatorConditionMode.If, 0, "IsGrounded");
            tFromJumpWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tFromJumpWalk.hasExitTime = false;
            tFromJumpWalk.duration = 0.15f;

            // LifeState Transitions (Downed, Revive, Death)
            var tToDowned = rootSm.AddAnyStateTransition(sDowned);
            tToDowned.AddCondition(AnimatorConditionMode.Equals, 1, "LifeState");
            tToDowned.hasExitTime = false;
            tToDowned.duration = 0.25f;

            var tToRevive = sDowned.AddTransition(sRevive);
            tToRevive.AddCondition(AnimatorConditionMode.If, 0, "IsReviving");
            tToRevive.hasExitTime = false;
            tToRevive.duration = 0.20f;

            var tReviveToDowned = sRevive.AddTransition(sDowned);
            tReviveToDowned.AddCondition(AnimatorConditionMode.IfNot, 0, "IsReviving");
            tReviveToDowned.AddCondition(AnimatorConditionMode.Equals, 1, "LifeState");
            tReviveToDowned.hasExitTime = false;
            tReviveToDowned.duration = 0.20f;

            var tReviveToIdle = sRevive.AddTransition(sIdle);
            tReviveToIdle.AddCondition(AnimatorConditionMode.Equals, 0, "LifeState");
            tReviveToIdle.hasExitTime = false;
            tReviveToIdle.duration = 0.25f;

            var tToDeath = rootSm.AddAnyStateTransition(sDeath);
            tToDeath.AddCondition(AnimatorConditionMode.Equals, 2, "LifeState");
            tToDeath.hasExitTime = false;
            tToDeath.duration = 0.20f;

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
            tAimFire.duration = 0.05f;

            var tFireAim = sFire.AddTransition(sAim);
            tFireAim.hasExitTime = true;
            tFireAim.exitTime = 0.85f;
            tFireAim.duration = 0.15f;

            var tAimReload = sAim.AddTransition(sReload);
            tAimReload.AddCondition(AnimatorConditionMode.If, 0, "IsReloading");
            tAimReload.hasExitTime = false;
            tAimReload.duration = 0.15f;

            var tReloadAim = sReload.AddTransition(sAim);
            tReloadAim.hasExitTime = true;
            tReloadAim.exitTime = 0.90f;
            tReloadAim.duration = 0.20f;

            var tToHit = upperSm.AddAnyStateTransition(sHit);
            tToHit.AddCondition(AnimatorConditionMode.If, 0, "HitTrigger");
            tToHit.hasExitTime = false;
            tToHit.duration = 0.08f;

            var tHitAim = sHit.AddTransition(sAim);
            tHitAim.hasExitTime = true;
            tHitAim.exitTime = 0.85f;
            tHitAim.duration = 0.15f;

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
            Debug.Log($"[CharacterAssetPipeline] Saved 13-State Animator Controller with Real Humanoid Skeletal Curves: {controllerPath}");
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
                Transform sockHolsterPri = CreateSocket("PrimaryWeaponHolster", bChest, new Vector3(0.12f, 0.05f, -0.16f), Quaternion.Euler(15f, 35f, 160f));
                Transform sockHolsterSec = CreateSocket("SecondaryWeaponHolster", bRThigh, new Vector3(0.14f, -0.06f, 0.02f), Quaternion.identity);
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

                // 7. Setup Visual Controller Discovery & Weapon Attachment
                visualController.DiscoverSocketsAndRenderers();
                visualController.UpdateSocketedWeaponVisuals();

                // 8. Save Prefab to disk and Resources
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
