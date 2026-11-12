# Unity client (skeleton)

**Not playable.** Unity 6 Editor was not installed when this folder was created.

1. Install Unity Hub and Editor **6000.0 LTS** (ProjectVersion targets 6000.0.36f1; Hub may upgrade).
2. Add this `client` folder as a project.
3. Let Hub resolve URP / Input System / NGO packages. If versions mismatch, accept Hub’s compatible set for 6000.0.
4. Create a Boot scene, attach `GameBootstrap`, do not claim a match works until Phase 1 tests pass.

Scripts here are Unity-only. Authoritative rules live in `../shared/OutrunAftermath.Simulation`.
