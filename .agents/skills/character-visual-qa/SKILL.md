---
name: character-visual-qa
description: Visual quality assurance procedures, studio lighting, camera angles, visual defect checklists, and SHA-256 screenshot uniqueness validation for OUTRUN: AFTERMATH.
---

# Character Visual QA Skill

This skill defines the visual inspection protocol, studio lighting environment, capture pipeline, and cryptographic uniqueness checks required for character acceptance.

## 1. Studio Inspection Environment
- **Ground Plane**: Neutral matte floor (grid or gray neutral PBR material) with subtle roughness to exhibit contact shadows and ambient occlusion.
- **Three-Point Studio Lighting**:
  - Key Light: Directional warm light ($5500\text{K}$, intensity $1.2$), angled $45^\circ$ from camera left, $50^\circ$ elevation.
  - Fill Light: Soft cool light ($6500\text{K}$, intensity $0.4$), angled $45^\circ$ from camera right, $30^\circ$ elevation.
  - Rim / Back Light: Crisp edge light ($6000\text{K}$, intensity $0.8$), behind character pointing toward camera to accentuate silhouette and equipment separation.
- **Shadow Quality**: URP High Quality Soft Shadows (Cascades = 4, Shadow Distance = 20m, Resolution = 4096).

## 2. Standard Capture Angles & Framing
- **Full-Body 360° Assessment**:
  - Front View: Camera at chest height ($1.4\text{m}$), $2.8\text{m}$ distance, FOV $45^\circ$, centered on operator.
  - Back View: Camera behind character ($180^\circ$).
  - Left / Right Profile: Camera at $90^\circ$ and $270^\circ$.
  - Three-Quarter Front ($45^\circ$): Shows depth and equipment layering.
- **Close-Up Inspections**:
  - Head & Torso: Framing from pelvis to head crown; inspects helmet, headset, collar, plate carrier, shoulder straps.
  - Weapon Grip & Hands: Framing weapon receiver, grip, trigger finger, and support foregrip; inspects knuckle bends and IK contact.
  - Lower Body & Feet: Framing hips to boots; inspects thigh holster, knee pads, boots, and ground contact.
- **First-Person View**:
  - Camera at eye level ($1.62\text{m}$), FOV $75^\circ$, looking forward.
  - Inspects viewmodel arms, gloves, weapon alignment, and iron sight / optic framing.

## 3. Visual Defect Checklist
- [ ] **No Primitive Anatomy**: Characters must not look like stacked capsules, cylinders, or wooden mannequins.
- [ ] **Zero Floating Geometry**: Plate carrier, pauldrons, holster straps, and backpack must make physical contact with underlying clothing/body.
- [ ] **Natural Joint Poses**: Elbows and knees bend naturally; no hyperextension, inverted joints, or twisted wrists.
- [ ] **Correct Weapon Placement**: Weapon stock against shoulder pocket; grip resting firmly in palm; index finger on frame/trigger; non-firing hand gripping foregrip.
- [ ] **No Mesh Self-Clipping**: Weapons do not penetrate character torso; fingers do not clip through weapon receiver.
- [ ] **Dynamic Ground Shadows**: Contact shadows clearly visible beneath boots, grounding character in space.

## 4. Cryptographic Screenshot Uniqueness (SHA-256)
- Every visual capture file must be generated from a distinct camera angle, animation state, or visual presentation.
- **Mandatory Validation Step**: Compute SHA-256 hashes of all generated `.png` screenshots.
- **Zero-Tolerance Rule**: No two evidence files may have matching hashes. Any duplicate hash indicates an automated copy or static repeat, which automatically fails Visual QA.
- **Manifest Requirement**: All captures must be indexed in `screenshot_manifest.json` with filepath, resolution, capture timestamp, state/view metadata, and SHA-256 hash.
