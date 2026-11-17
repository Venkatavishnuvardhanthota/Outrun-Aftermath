using System;
using System.IO;
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

        [MenuItem("Outrun/Capture Character Visual Evidence (Phase C.1)")]
        public static void CaptureAllEvidence()
        {
            Debug.Log("[CaptureCharacterVisuals] Setting up capture studio...");

            // Create temporary studio environment
            var studioGo = new GameObject("CaptureStudio");

            var lightGo = new GameObject("KeyLight");
            var keyLight = lightGo.AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.3f;
            lightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            var fillGo = new GameObject("FillLight");
            var fillLight = fillGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.6f;
            fillGo.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

            var groundGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundGo.transform.localScale = new Vector3(5f, 1f, 5f);
            var groundMr = groundGo.GetComponent<MeshRenderer>();
            var groundMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            groundMat.color = new Color(0.18f, 0.20f, 0.22f);
            groundMat.SetFloat("_Smoothness", 0.4f);
            groundMr.sharedMaterial = groundMat;

            var camGo = new GameObject("StudioCamera");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 50f;
            cam.clearFlags = CameraClearFlags.Color;
            cam.backgroundColor = new Color(0.12f, 0.13f, 0.15f);

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
            vc.DiscoverSocketsAndRenderers();

            var anim = rex.GetComponent<Animator>();

            try
            {
                // 1. REX Front View (Full Body)
                vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);
                PositionCamera(cam, new Vector3(0f, 1.15f, 2.7f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_front.png");

                // 2. REX Back View
                PositionCamera(cam, new Vector3(0f, 1.15f, -2.7f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_back.png");

                // 3. REX Side View (Profile)
                PositionCamera(cam, new Vector3(2.6f, 1.15f, 0f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_side.png");

                // 4. REX Third-Person View (Over the shoulder)
                PositionCamera(cam, new Vector3(0.55f, 1.45f, -1.8f), new Vector3(0.15f, 1.35f, 2.0f));
                CaptureFrame(cam, "screenshot_rex_thirdperson.png");

                // 5. REX First-Person View (Viewmodel Arms & ADS Gun)
                vc.ApplyPerspectiveMode(CameraPerspective.FirstPerson);
                PositionCamera(cam, new Vector3(0f, 1.55f, 0.15f), new Vector3(0f, 1.55f, 3.0f));
                CaptureFrame(cam, "screenshot_rex_firstperson.png");

                // 6. REX Weapon Switching (Shotgun in hand, Rifle slung on back)
                vc.ApplyPerspectiveMode(CameraPerspective.ThirdPerson);
                vc.SetActiveSlotIndex(1); // Breacher-12 Shotgun active
                PositionCamera(cam, new Vector3(0.7f, 1.25f, 1.8f), new Vector3(0f, 1.1f, 0f));
                CaptureFrame(cam, "screenshot_rex_weapon_switch.png");

                // Sample Animation States via AnimationClip evaluation
                string animDir = "Assets/Art/Characters/Shared/Animations";

                // 7. REX Idle Pose
                SampleClip(rex, $"{animDir}/Operator_Idle.anim", 0.5f);
                PositionCamera(cam, new Vector3(0.4f, 1.2f, 2.4f), new Vector3(0f, 1.05f, 0f));
                CaptureFrame(cam, "screenshot_rex_idle.png");

                // 8. REX Walk / Run Stride
                SampleClip(rex, $"{animDir}/Operator_Run.anim", 0.25f);
                PositionCamera(cam, new Vector3(1.8f, 1.15f, 1.6f), new Vector3(0f, 1.0f, 0f));
                CaptureFrame(cam, "screenshot_rex_walk.png");

                // 9. REX Aim & Fire (Recoil muzzle climb)
                SampleClip(rex, $"{animDir}/Operator_Fire.anim", 0.04f);
                PositionCamera(cam, new Vector3(0.85f, 1.35f, 1.7f), new Vector3(0.1f, 1.30f, 0f));
                CaptureFrame(cam, "screenshot_rex_aim_fire.png");

                // 10. REX Crouch
                SampleClip(rex, $"{animDir}/Operator_Crouch.anim", 0.5f);
                PositionCamera(cam, new Vector3(0.5f, 0.9f, 2.2f), new Vector3(0f, 0.75f, 0f));
                CaptureFrame(cam, "screenshot_rex_crouch.png");

                // 11. REX Reload (Slapping fresh magazine into magwell)
                SampleClip(rex, $"{animDir}/Operator_Reload.anim", 1.45f);
                PositionCamera(cam, new Vector3(0.6f, 1.3f, 1.6f), new Vector3(0f, 1.2f, 0f));
                CaptureFrame(cam, "screenshot_rex_reload.png");

                // 12. REX Downed / Death (Ground collapse pose)
                SampleClip(rex, $"{animDir}/Operator_Downed.anim", 0.8f);
                PositionCamera(cam, new Vector3(1.2f, 0.95f, 1.5f), new Vector3(0f, 0.35f, 0f));
                CaptureFrame(cam, "screenshot_rex_downed_death.png");

                Debug.Log("[CaptureCharacterVisuals] All 12 required screenshots successfully captured!");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(rex);
                UnityEngine.Object.DestroyImmediate(groundGo);
                UnityEngine.Object.DestroyImmediate(lightGo);
                UnityEngine.Object.DestroyImmediate(fillGo);
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

            byte[] bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            // Determine root paths
            string projectDir = Directory.GetCurrentDirectory(); // d:\projects\outrun-aftermath\client
            string workspaceRoot = Path.GetFullPath(Path.Combine(projectDir, "..")); // d:\projects\outrun-aftermath

            string rootPath = Path.Combine(workspaceRoot, filename);
            string clientPath = Path.Combine(projectDir, filename);

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
