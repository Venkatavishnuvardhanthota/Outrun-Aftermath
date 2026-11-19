---
name: character-art-pipeline
description: Production standard for character modeling, human anatomy, retopology, tactical equipment integration, UV mapping, PBR texturing, and DCC FBX export for OUTRUN: AFTERMATH.
---

# Character Art Pipeline Skill

This skill defines the technical art and DCC modeling standards for production characters (such as REX and TRACE) in OUTRUN: AFTERMATH.

## 1. Modeling Standards & Anatomical Proportions
- **Reference Height**: 1.82 m (standing tall in combat boots).
- **Proportion Guidelines**:
  - Head-to-body ratio: 7.5 to 8 heads tall.
  - Shoulder width: 2.2 to 2.5 head widths across acromion processes.
  - Spine & Pelvis: Natural lumbar lordosis and thoracic kyphosis, pelvis tilted naturally without boxy extrusion.
  - Arms: Biceps and triceps clearly defined; forearm tapering into wrist with radius/ulna definition.
  - Hands: Defined palms, knuckles, and 5 distinct jointed fingers (thumb with 2 joints, 4 fingers with 3 joints each). Never create paddle hands or featureless mittens.
  - Legs: Defined quadriceps, hamstrings, knee patella structure, calf gastrocnemius curve, and defined ankles (medial and lateral malleolus).
  - Feet: Realistic military combat boots with sole thickness, heel lift, arch, and defined toe box.
- **Silhouette Integrity**: The character silhouette must be instantly recognizable as an elite tactical operator even at distance or under harsh lighting.

## 2. Retopology Standards
- **Target Polygon Count**:
  - Full Third-Person Character (Base body + tactical gear): 25,000 to 45,000 triangles (LOD0).
  - First-Person Viewmodel Arms & Hands: 12,000 to 20,000 triangles.
- **Edge Loops & Deformable Topology**:
  - Concentric edge loops around major articulation joints: 3 loops across shoulder girdle, 3-4 loops across elbows and knees, 2 loops across finger knuckles.
  - Clean quad-dominant topology in deformable zones to prevent pinching, collapsing, or volume loss during extreme bends.
  - Watertight manifold mesh; no non-manifold vertices, zero-area faces, or flipped face normals.

## 3. Tactical Equipment Integration
- **Zero Floating Geometry**:
  - Helmet & Headset: Snugly fitted to cranial contour and ears; chinstrap anchored under jaw.
  - Tactical Vest / Plate Carrier: Snug against chest and upper torso, resting firmly on clavicles without air gaps.
  - Pauldrons / Shoulder Armor: Contoured to deltoids, anchored with strap geometry.
  - Belt & Pouches: Utility belt fitted around waist/iliac crest; mag pouches flush mounted onto MOLLE webbing.
  - Holster: Thigh rig anchored with dual leg straps flush against quadriceps and drop-loop anchored to gun belt.
  - Backpack: Straps wrapping over shoulders, chest sternum clip fastened, pack body resting firmly against upper thoracic spine.
  - Gloves: Modeled as functional tactical gloves with reinforced palm pads, knuckle guards, and wrist cinch straps.

## 4. UV Layout & Material Standards
- **Texel Density**: Consistent 10.24 px/cm across 2048x2048 texture sheets (or 20.48 px/cm for FP arms at 2048x2048).
- **UV Packing**:
  - UDIM or single 0-1 UV space per material ID.
  - Padding: Minimum 16px border padding at 2048x2048 to prevent mipmap bleed.
  - Seams placed along natural clothing seams (inner leg, arm undersides, collar, waist).
- **PBR Texture Maps**:
  - Albedo / Base Color: Linear color with neutral lighting (no baked direct shadows or highlights).
  - Metallic-Smoothness / Mask: Red = Metallic, Green = Occlusion, Blue = Detail Mask, Alpha = Smoothness.
  - Normal Map: Tangent-space DirectX/OpenGL standard (Unity expects MikkTSpace, tangent +Y up).

## 5. FBX Export & Unity Import Settings
- **DCC Coordinate System**: Right-handed Z-up (Blender) or Y-up (Maya) converted on export to Unity's Left-handed Y-up (`-Z forward`, `+Y up`).
- **Scale Factor**: 1.0 (Metric, 1 unit = 1 meter). Never export with 0.01 scale compensation.
- **Export Flags**:
  - Include: Mesh, Armature/Skeleton, Smoothing Groups, Tangents.
  - Exclude: Cameras, Lights, Animation (unless exporting dedicated clip FBX).
- **Unity Asset Import**:
  - Model Tab: Read/Write Enabled (if runtime mesh access needed), Generate Lightmap UVs = False, Normals = Import, Tangents = Calculate MikkTSpace.
  - Rig Tab: Animation Type = Humanoid, Avatar Definition = Create From This Model.
