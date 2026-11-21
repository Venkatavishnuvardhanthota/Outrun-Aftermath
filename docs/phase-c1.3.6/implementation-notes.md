# Phase C.1.3.6 Implementation Notes

## Architecture & Code Changes

### 1. `CharacterPlayModeAcceptanceDriver.cs`
- **Location:** `client/Assets/Game/Runtime/Character/CharacterPlayModeAcceptanceDriver.cs`
- **Purpose:** Standalone runtime driver executing during real Unity Play Mode.
- **Key Methods:**
  - `GetProjectRoot()`: Dynamically searches parent directories for `.git`, `docs`, `client`, and `shared` to guarantee portable relative paths regardless of whether the executable runs in the Editor, standalone player (`client/Builds/AcceptancePlayer/`), or CI environment.
  - `SetupStudioEnvironment()`: Constructs studio lighting rig (Key, Fill, Rim) and a large ground floor plane to capture realistic directional shadows.
  - `ProcessOperator()`: Spawns the live operator prefab, attaches `WeaponSystem`, `CameraRig`, and `OperatorVisualController`, sets up perspective modes, evaluates Two-Bone IK, exercises 13 animation states, and cycles weapon slots.
  - `ProcessMultiplayerScene()`: Spawns side-by-side Client A (Local Owner) and Client B (Remote Proxy) to demonstrate perspective differentiation, shadow casting, and replicated upper-body combat states.
  - `StampHudBanner()`: Custom lightweight 5x7 bitmap font renderer that overlays live telemetry data (active slot, transform count, IK distance, elbow angle) onto weapon switching and multiplayer correlation frames.

### 2. `CharacterPlayModeAcceptance.cs`
- **Location:** `client/Assets/Editor/CharacterPlayModeAcceptance.cs`
- **Purpose:** Editor automation to ensure scene clean state and compile `OutrunAftermath_Acceptance.exe`.
- **Key Methods:**
  - `EnsureDriverInScene()`: Opens `CharacterVisualAcceptance.unity`, removes any lingering static character objects, and ensures `CharacterPlayModeAcceptanceDriver` is mounted.
  - `BuildAcceptancePlayerBatchMode()`: Invoked via `-executeMethod` to compile the standalone acceptance player.

### 3. `run_acceptance_c136.ps1`
- **Location:** `run_acceptance_c136.ps1`
- **Purpose:** Full end-to-end 6-stage verification script:
  1. `CharacterAssetPipeline.BuildAllCharacterAssetsBatchMode`
  2. `CharacterPlayModeAcceptance.BuildAcceptancePlayerBatchMode`
  3. `OutrunAftermath_Acceptance.exe -batchmode`
  4. `BuildScript.BuildAllWindows` (WindowsServer & WindowsClient)
  5. `test_two_client_multiplayer.ps1 -ServerPort 7788`
  6. `dotnet test shared/OutrunAftermath.Simulation.Tests` & `dotnet test backend/tests/OutrunAftermath.Api.Tests`

### 4. Hardware Acceleration & Licensing Handling
- Unity Editor in batchmode requires Windows Management Instrumentation (WMI) to resolve licensing credentials. Running batchmode compilation with bypassed sandbox isolation ensures proper WMI IPC connectivity to `Unity.Licensing.Client`.
- Standalone player execution runs with hardware GPU acceleration via DirectX 12 / RTX 3050, ensuring proper URP render passes and shadow map generation.
