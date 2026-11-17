"""
OUTRUN: AFTERMATH - Phase C.1 Character Asset Generation Engine
Procedurally authors authentic, high-quality PBR textures and 3D Wavefront OBJ models
for Operator REX (Rogue Assault) and Operator TRACE (Sentinel Tracker).
"""

import os
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

TEXTURES_CHAR_ROOT = "client/Assets/Art/Textures/Characters"
CHARACTERS_ROOT = "client/Assets/Art/Characters"

def ensure_dirs():
    dirs = [
        f"{TEXTURES_CHAR_ROOT}/Shared",
        f"{TEXTURES_CHAR_ROOT}/REX",
        f"{TEXTURES_CHAR_ROOT}/TRACE",
        f"{CHARACTERS_ROOT}/Shared/Skeletons",
        f"{CHARACTERS_ROOT}/Shared/Meshes",
        f"{CHARACTERS_ROOT}/Shared/Materials",
        f"{CHARACTERS_ROOT}/Shared/Animations",
        f"{CHARACTERS_ROOT}/Shared/Equipment",
        f"{CHARACTERS_ROOT}/Shared/Sockets",
        f"{CHARACTERS_ROOT}/Operators/REX/Meshes",
        f"{CHARACTERS_ROOT}/Operators/REX/Materials",
        f"{CHARACTERS_ROOT}/Operators/REX/Prefabs",
        f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes",
        f"{CHARACTERS_ROOT}/Operators/TRACE/Materials",
        f"{CHARACTERS_ROOT}/Operators/TRACE/Prefabs",
        "client/Assets/Resources/Art/Characters"
    ]
    for d in dirs:
        os.makedirs(d, exist_ok=True)

# ==============================================================================
# 1. PBR TEXTURE UTILITIES
# ==============================================================================

def generate_normal_map(height_map: np.ndarray, strength: float = 2.5) -> Image.Image:
    """Computes tangent-space normal map from a grayscale height map using Sobel filter."""
    h, w = height_map.shape
    sobel_x = np.array([[-1, 0, 1], [-2, 0, 2], [-1, 0, 1]], dtype=np.float32)
    sobel_y = np.array([[-1, -2, -1], [0, 0, 0], [1, 2, 1]], dtype=np.float32)

    padded = np.pad(height_map, 1, mode='wrap')
    gx = np.zeros((h, w), dtype=np.float32)
    gy = np.zeros((h, w), dtype=np.float32)

    for i in range(3):
        for j in range(3):
            gx += sobel_x[i, j] * padded[i:i+h, j:j+w]
            gy += sobel_y[i, j] * padded[i:i+h, j:j+w]

    gx *= strength / 8.0
    gy *= strength / 8.0

    gz = np.ones((h, w), dtype=np.float32)
    norm = np.sqrt(gx*gx + gy*gy + gz*gz)
    nx = -gx / norm
    ny = -gy / norm
    nz = gz / norm

    r = ((nx * 0.5 + 0.5) * 255.0).astype(np.uint8)
    g = ((ny * 0.5 + 0.5) * 255.0).astype(np.uint8)
    b = ((nz * 0.5 + 0.5) * 255.0).astype(np.uint8)

    normal_rgb = np.stack([r, g, b], axis=-1)
    return Image.fromarray(normal_rgb, mode='RGB')

def create_mask_map(metallic: np.ndarray, ao: np.ndarray, smoothness: np.ndarray) -> Image.Image:
    """Unity URP MaskMap: R=Metallic, G=AO, B=DetailMask(0), A=Smoothness."""
    h, w = metallic.shape
    b = np.zeros((h, w), dtype=np.uint8)
    rgba = np.stack([
        metallic.astype(np.uint8),
        ao.astype(np.uint8),
        b,
        smoothness.astype(np.uint8)
    ], axis=-1)
    return Image.fromarray(rgba, mode='RGBA')

# ==============================================================================
# 2. PBR TEXTURE GENERATION
# ==============================================================================

def generate_character_textures():
    print("Generating Character PBR Textures (Shared, REX, TRACE)...")
    size = 1024

    # -------------------------------------------------------------
    # 1. SHARED OPERATOR UNDERSUIT (Tactical Ripstop Fabric)
    # -------------------------------------------------------------
    np.random.seed(101)
    undersuit_albedo = np.full((size, size, 3), [40, 42, 46], dtype=np.float32)
    y_coords, x_coords = np.indices((size, size))
    grid_pattern = ((x_coords // 12) + (y_coords // 12)) % 2 == 0
    undersuit_albedo[grid_pattern] += [6, 6, 8]

    # Reinforced Kevlar knee and elbow patches
    kneepad_mask = ((y_coords > 600) & (y_coords < 800) & (x_coords > 300) & (x_coords < 724))
    undersuit_albedo[kneepad_mask] = [28, 30, 33]

    noise = np.random.normal(0, 3.5, (size, size, 3))
    undersuit_albedo = np.clip(undersuit_albedo + noise, 0, 255)

    undersuit_height = np.full((size, size), 128, dtype=np.float32)
    undersuit_height[grid_pattern] += 12.0
    undersuit_height[kneepad_mask] += 30.0
    undersuit_height += np.random.normal(0, 4, (size, size))
    undersuit_height = np.clip(undersuit_height, 0, 255)

    undersuit_albedo_img = Image.fromarray(undersuit_albedo.astype(np.uint8), mode='RGB')
    undersuit_normal_img = generate_normal_map(undersuit_height, strength=2.2)
    undersuit_metallic = np.where(kneepad_mask, 40, 15).astype(np.uint8)
    undersuit_smoothness = np.where(kneepad_mask, 110, 65).astype(np.uint8)
    undersuit_ao = np.full((size, size), 235, dtype=np.uint8)
    undersuit_ao[kneepad_mask] = 200
    undersuit_mask = create_mask_map(undersuit_metallic, undersuit_ao, undersuit_smoothness)

    undersuit_albedo_img.save(f"{TEXTURES_CHAR_ROOT}/Shared/T_Operator_Shared_Albedo.png")
    undersuit_normal_img.save(f"{TEXTURES_CHAR_ROOT}/Shared/T_Operator_Shared_Normal.png")
    undersuit_mask.save(f"{TEXTURES_CHAR_ROOT}/Shared/T_Operator_Shared_MaskMap.png")

    # -------------------------------------------------------------
    # 2. OPERATOR REX (Rogue Assault: Matte Charcoal, Amber Accents)
    # -------------------------------------------------------------
    np.random.seed(102)
    rex_albedo = np.full((size, size, 3), [48, 50, 54], dtype=np.float32)

    # Burnt amber accent hazard stripes
    accent_stripes = ((x_coords + y_coords) // 48) % 8 == 0
    amber_color = np.array([218, 108, 28], dtype=np.float32)
    rex_albedo[accent_stripes] = amber_color

    # Carbon steel armor plate zones
    plate_zones = ((x_coords > 200) & (x_coords < 824) & (y_coords > 150) & (y_coords < 850))
    rex_albedo[plate_zones & ~accent_stripes] = [36, 38, 42]

    # Ballistic visor zone
    visor_zone = ((y_coords > 80) & (y_coords < 150) & (x_coords > 350) & (x_coords < 674))
    rex_albedo[visor_zone] = [235, 120, 20]

    noise = np.random.normal(0, 4, (size, size, 3))
    rex_albedo = np.clip(rex_albedo + noise, 0, 255)

    rex_height = np.full((size, size), 128, dtype=np.float32)
    rex_height[plate_zones] += 35.0
    rex_height[accent_stripes] += 15.0
    rex_height[visor_zone] += 25.0
    rex_height += np.random.normal(0, 4, (size, size))
    rex_height = np.clip(rex_height, 0, 255)

    rex_albedo_img = Image.fromarray(rex_albedo.astype(np.uint8), mode='RGB')
    rex_normal_img = generate_normal_map(rex_height, strength=2.8)
    rex_metallic = np.where(plate_zones, 190, np.where(visor_zone, 85, 30)).astype(np.uint8)
    rex_smoothness = np.where(visor_zone, 245, np.where(plate_zones, 160, 95)).astype(np.uint8)
    rex_ao = np.full((size, size), 225, dtype=np.uint8)
    rex_ao[plate_zones] = 245
    rex_mask = create_mask_map(rex_metallic, rex_ao, rex_smoothness)

    rex_albedo_img.save(f"{TEXTURES_CHAR_ROOT}/REX/T_Operator_REX_Albedo.png")
    rex_normal_img.save(f"{TEXTURES_CHAR_ROOT}/REX/T_Operator_REX_Normal.png")
    rex_mask.save(f"{TEXTURES_CHAR_ROOT}/REX/T_Operator_REX_MaskMap.png")

    # -------------------------------------------------------------
    # 3. OPERATOR TRACE (Sentinel Tracker: Slate Blue, Recon Cyan/Yellow)
    # -------------------------------------------------------------
    np.random.seed(103)
    trace_albedo = np.full((size, size, 3), [52, 64, 78], dtype=np.float32)

    # Sensor optic lenses and telemetry lines
    lens_mask = ((x_coords > 400) & (x_coords < 624) & (y_coords > 180) & (y_coords < 340))
    trace_albedo[lens_mask] = [45, 195, 235]

    clip_mask = ((x_coords // 32) % 4 == 0) & ((y_coords > 400) & (y_coords < 800))
    trace_albedo[clip_mask] = [185, 195, 205]

    noise = np.random.normal(0, 4, (size, size, 3))
    trace_albedo = np.clip(trace_albedo + noise, 0, 255)

    trace_height = np.full((size, size), 128, dtype=np.float32)
    trace_height[lens_mask] += 40.0
    trace_height[clip_mask] += 30.0
    trace_height += np.random.normal(0, 4, (size, size))
    trace_height = np.clip(trace_height, 0, 255)

    trace_albedo_img = Image.fromarray(trace_albedo.astype(np.uint8), mode='RGB')
    trace_normal_img = generate_normal_map(trace_height, strength=2.6)
    trace_metallic = np.where(clip_mask, 220, np.where(lens_mask, 80, 45)).astype(np.uint8)
    trace_smoothness = np.where(lens_mask, 240, np.where(clip_mask, 190, 110)).astype(np.uint8)
    trace_mask = create_mask_map(trace_metallic, np.full((size, size), 230, dtype=np.uint8), trace_smoothness)

    trace_albedo_img.save(f"{TEXTURES_CHAR_ROOT}/TRACE/T_Operator_TRACE_Albedo.png")
    trace_normal_img.save(f"{TEXTURES_CHAR_ROOT}/TRACE/T_Operator_TRACE_Normal.png")
    trace_mask.save(f"{TEXTURES_CHAR_ROOT}/TRACE/T_Operator_TRACE_MaskMap.png")

    print("Character PBR Textures successfully generated!")

# ==============================================================================
# 3. 3D OBJ MESH GENERATOR UTILITIES
# ==============================================================================

class ObjMeshBuilder:
    def __init__(self, name: str):
        self.name = name
        self.vertices = []
        self.uvs = []
        self.normals = []
        self.faces = []

    def add_vertex(self, x: float, y: float, z: float) -> int:
        self.vertices.append((x, y, z))
        return len(self.vertices)

    def add_uv(self, u: float, v: float) -> int:
        self.uvs.append((u, v))
        return len(self.uvs)

    def add_normal(self, nx: float, ny: float, nz: float) -> int:
        length = math.sqrt(nx*nx + ny*ny + nz*nz)
        if length > 1e-6:
            nx, ny, nz = nx / length, ny / length, nz / length
        self.normals.append((nx, ny, nz))
        return len(self.normals)

    def add_quad(self, v1, v2, v3, v4, uv1, uv2, uv3, uv4, normal):
        n_idx = self.add_normal(*normal)
        u1_idx = self.add_uv(*uv1)
        u2_idx = self.add_uv(*uv2)
        u3_idx = self.add_uv(*uv3)
        u4_idx = self.add_uv(*uv4)
        self.faces.append([(v1, u1_idx, n_idx), (v2, u2_idx, n_idx), (v3, u3_idx, n_idx)])
        self.faces.append([(v1, u1_idx, n_idx), (v3, u3_idx, n_idx), (v4, u4_idx, n_idx)])

    def add_box(self, cx: float, cy: float, cz: float, sx: float, sy: float, sz: float):
        hx, hy, hz = sx * 0.5, sy * 0.5, sz * 0.5
        v000 = self.add_vertex(cx - hx, cy - hy, cz - hz)
        v100 = self.add_vertex(cx + hx, cy - hy, cz - hz)
        v110 = self.add_vertex(cx + hx, cy + hy, cz - hz)
        v010 = self.add_vertex(cx - hx, cy + hy, cz - hz)
        v001 = self.add_vertex(cx - hx, cy - hy, cz + hz)
        v101 = self.add_vertex(cx + hx, cy - hy, cz + hz)
        v111 = self.add_vertex(cx + hx, cy + hy, cz + hz)
        v011 = self.add_vertex(cx - hx, cy + hy, cz + hz)

        # Front (+Z)
        self.add_quad(v001, v101, v111, v011, (0, 0), (1, 0), (1, 1), (0, 1), (0, 0, 1))
        # Back (-Z)
        self.add_quad(v100, v000, v010, v110, (0, 0), (1, 0), (1, 1), (0, 1), (0, 0, -1))
        # Top (+Y)
        self.add_quad(v011, v111, v110, v010, (0, 0), (1, 0), (1, 1), (0, 1), (0, 1, 0))
        # Bottom (-Y)
        self.add_quad(v000, v100, v101, v001, (0, 0), (1, 0), (1, 1), (0, 1), (0, -1, 0))
        # Right (+X)
        self.add_quad(v101, v100, v110, v111, (0, 0), (1, 0), (1, 1), (0, 1), (1, 0, 0))
        # Left (-X)
        self.add_quad(v000, v001, v011, v010, (0, 0), (1, 0), (1, 1), (0, 1), (-1, 0, 0))

    def add_cylinder(self, cx: float, cy: float, cz: float, radius: float, height: float, segments: int = 12, axis: str = 'y'):
        hh = height * 0.5
        top_verts = []
        bot_verts = []
        for i in range(segments):
            angle = 2.0 * math.pi * i / segments
            ca, sa = math.cos(angle) * radius, math.sin(angle) * radius
            if axis == 'y':
                top_verts.append(self.add_vertex(cx + ca, cy + hh, cz + sa))
                bot_verts.append(self.add_vertex(cx + ca, cy - hh, cz + sa))
            elif axis == 'z':
                top_verts.append(self.add_vertex(cx + ca, cy + sa, cz + hh))
                bot_verts.append(self.add_vertex(cx + ca, cy + sa, cz - hh))
            elif axis == 'x':
                top_verts.append(self.add_vertex(cx + hh, cy + ca, cz + sa))
                bot_verts.append(self.add_vertex(cx - hh, cy + ca, cz + sa))

        for i in range(segments):
            next_i = (i + 1) % segments
            v_t1 = top_verts[i]
            v_t2 = top_verts[next_i]
            v_b1 = bot_verts[i]
            v_b2 = bot_verts[next_i]
            angle_mid = 2.0 * math.pi * (i + 0.5) / segments
            ca, sa = math.cos(angle_mid), math.sin(angle_mid)
            norm = (ca, 0, sa) if axis == 'y' else ((ca, sa, 0) if axis == 'z' else (0, ca, sa))
            self.add_quad(v_b1, v_b2, v_t2, v_t1, (i/segments, 0), ((i+1)/segments, 0), ((i+1)/segments, 1), (i/segments, 1), norm)

    def write_obj(self, filepath: str):
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(f"# OUTRUN: AFTERMATH Character Asset - {self.name}\n")
            f.write(f"o {self.name}\n\n")
            for v in self.vertices:
                f.write(f"v {v[0]:.4f} {v[1]:.4f} {v[2]:.4f}\n")
            f.write("\n")
            for uv in self.uvs:
                f.write(f"vt {uv[0]:.4f} {uv[1]:.4f}\n")
            f.write("\n")
            for n in self.normals:
                f.write(f"vn {n[0]:.4f} {n[1]:.4f} {n[2]:.4f}\n")
            f.write("\ns 1\n")
            for face in self.faces:
                face_str = " ".join([f"{v}/{uv}/{n}" for v, uv, n in face])
                f.write(f"f {face_str}\n")
        print(f"Authored 3D OBJ: {filepath} ({len(self.vertices)} verts, {len(self.faces)} tris)")

# ==============================================================================
# 4. MODULAR OPERATOR EQUIPMENT GENERATION
# ==============================================================================

def generate_rex_equipment():
    print("Generating Operator REX Equipment Models...")

    # 1. REX Combat Helmet (Assault ballistic helmet with visor & ear covers)
    m = ObjMeshBuilder("REX_Helmet_Combat")
    # Main helmet dome shell
    m.add_cylinder(0.0, 0.08, 0.0, 0.14, 0.16, segments=16, axis='y')
    m.add_box(0.0, 0.12, 0.0, 0.24, 0.08, 0.26)
    # Front visor brow / sun visor
    m.add_box(0.0, 0.06, 0.13, 0.22, 0.04, 0.06)
    # Tinted tactical visor lens
    m.add_box(0.0, 0.02, 0.14, 0.20, 0.05, 0.03)
    # Side communications ear cups
    m.add_cylinder(0.14, 0.02, -0.01, 0.045, 0.03, segments=10, axis='x')
    m.add_cylinder(-0.14, 0.02, -0.01, 0.045, 0.03, segments=10, axis='x')
    # Forehead NVG shroud mount
    m.add_box(0.0, 0.11, 0.13, 0.06, 0.06, 0.03)
    # Chin strap buckle
    m.add_box(0.0, -0.10, 0.04, 0.08, 0.02, 0.04)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Helmet_Combat.obj")

    # 2. REX Tactical Assault Vest (Heavy plate carrier with triple mag pouches & radio)
    m = ObjMeshBuilder("REX_Vest_TacticalAssault")
    # Front ESAPI chest plate
    m.add_box(0.0, 0.0, 0.12, 0.32, 0.38, 0.08)
    # Back armor plate
    m.add_box(0.0, 0.0, -0.12, 0.32, 0.38, 0.08)
    # Padded shoulder straps
    m.add_box(-0.13, 0.20, 0.0, 0.08, 0.05, 0.24)
    m.add_box(0.13, 0.20, 0.0, 0.08, 0.05, 0.24)
    # Cummerbund side straps
    m.add_box(0.17, -0.04, 0.0, 0.04, 0.22, 0.22)
    m.add_box(-0.17, -0.04, 0.0, 0.04, 0.22, 0.22)
    # Triple magazine pouches (front chest)
    m.add_box(-0.09, -0.08, 0.17, 0.065, 0.15, 0.05)
    m.add_box(0.0, -0.08, 0.17, 0.065, 0.15, 0.05)
    m.add_box(0.09, -0.08, 0.17, 0.065, 0.15, 0.05)
    # MOLLE webbing ribs across front
    m.add_box(0.0, 0.06, 0.165, 0.28, 0.02, 0.015)
    m.add_box(0.0, 0.01, 0.165, 0.28, 0.02, 0.015)
    # Tactical radio with antenna (left upper chest)
    m.add_box(-0.13, 0.10, 0.15, 0.055, 0.12, 0.045)
    m.add_cylinder(-0.13, 0.22, 0.15, 0.007, 0.14, segments=6, axis='y')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Vest_TacticalAssault.obj")

    # 3. REX Armored Pauldrons (Articulated assault shoulder plates)
    m = ObjMeshBuilder("REX_Pauldrons_Armored")
    # Right shoulder curved plate
    m.add_box(0.24, 0.01, 0.0, 0.11, 0.16, 0.15)
    m.add_box(0.26, -0.04, 0.0, 0.08, 0.10, 0.13)
    # Left shoulder curved plate
    m.add_box(-0.24, 0.01, 0.0, 0.11, 0.16, 0.15)
    m.add_box(-0.26, -0.04, 0.0, 0.08, 0.10, 0.13)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Pauldrons_Armored.obj")

    # 4. REX Assault Backpack (Rucksack with bedroll & side pods)
    m = ObjMeshBuilder("REX_Backpack_Assault")
    # Main rucksack body
    m.add_box(0.0, 0.0, -0.16, 0.30, 0.40, 0.18)
    # Top roll / sleeping pad
    m.add_cylinder(0.0, 0.23, -0.16, 0.065, 0.32, segments=12, axis='x')
    # Left & right utility pods
    m.add_box(-0.17, -0.04, -0.16, 0.07, 0.20, 0.13)
    m.add_box(0.17, -0.04, -0.16, 0.07, 0.20, 0.13)
    # Hydration tube guide
    m.add_cylinder(0.08, 0.12, -0.08, 0.01, 0.16, segments=6, axis='y')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Backpack_Assault.obj")

    # 5. REX Drop-Leg Sidearm Holster
    m = ObjMeshBuilder("REX_Holster_Sidearm")
    # Thigh mounting plate
    m.add_box(0.02, 0.0, 0.0, 0.035, 0.20, 0.13)
    # Kydex retention bucket
    m.add_box(0.05, -0.02, 0.0, 0.055, 0.16, 0.09)
    # Dual leg straps with buckle
    m.add_box(0.0, 0.06, 0.0, 0.04, 0.025, 0.16)
    m.add_box(0.0, -0.06, 0.0, 0.04, 0.025, 0.16)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Holster_Sidearm.obj")

    # 6. REX First-Person Arms
    m = ObjMeshBuilder("FP_Arms_REX")
    # Right forearm (combat sleeve)
    m.add_cylinder(0.24, -0.18, 0.34, 0.055, 0.34, segments=12, axis='z')
    # Right tactical glove with carbon knuckle protector
    m.add_box(0.24, -0.16, 0.52, 0.085, 0.08, 0.12)
    m.add_box(0.24, -0.13, 0.52, 0.075, 0.025, 0.06) # Knuckle plate
    # Left forearm reaching forward to support handguard
    m.add_cylinder(-0.14, -0.22, 0.44, 0.052, 0.32, segments=12, axis='z')
    # Left tactical glove
    m.add_box(-0.08, -0.18, 0.60, 0.08, 0.08, 0.12)
    m.add_box(-0.08, -0.15, 0.60, 0.07, 0.025, 0.06) # Knuckle plate
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/FP_Arms_REX.obj")

def generate_trace_equipment():
    print("Generating Operator TRACE Equipment Models...")

    # 1. TRACE Recon Comms Headset (Cap with comms & optical monocle)
    m = ObjMeshBuilder("TRACE_Headset_ReconComms")
    m.add_cylinder(0.0, 0.06, 0.0, 0.13, 0.12, segments=16, axis='y')
    # Backward cap brim
    m.add_box(0.0, 0.02, -0.16, 0.16, 0.02, 0.08)
    # Headset ear cups
    m.add_cylinder(0.13, 0.01, -0.01, 0.04, 0.03, segments=10, axis='x')
    m.add_cylinder(-0.13, 0.01, -0.01, 0.04, 0.03, segments=10, axis='x')
    # Boom mic arm curving around mouth
    m.add_box(0.12, -0.06, 0.07, 0.015, 0.015, 0.12)
    m.add_box(0.06, -0.06, 0.14, 0.07, 0.015, 0.02)
    # Recon optical monocle over right eye
    m.add_box(0.055, 0.03, 0.14, 0.045, 0.045, 0.05)
    m.add_cylinder(0.055, 0.03, 0.17, 0.018, 0.025, segments=8, axis='z')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Headset_ReconComms.obj")

    # 2. TRACE Recon Chest Harness (Sleek chest rig with sensor unit)
    m = ObjMeshBuilder("TRACE_Harness_ReconChest")
    m.add_box(0.0, 0.02, 0.10, 0.22, 0.24, 0.07)
    # Triple sensor lenses
    m.add_cylinder(-0.05, 0.04, 0.14, 0.022, 0.02, segments=8, axis='z')
    m.add_cylinder(0.05, 0.04, 0.14, 0.022, 0.02, segments=8, axis='z')
    m.add_cylinder(0.0, -0.04, 0.14, 0.018, 0.02, segments=8, axis='z')
    # Cross webbing harness straps
    m.add_box(0.11, 0.0, 0.08, 0.04, 0.30, 0.03)
    m.add_box(-0.11, 0.0, 0.08, 0.04, 0.30, 0.03)
    # Compact mag pouches
    m.add_box(0.15, -0.06, 0.09, 0.05, 0.13, 0.05)
    m.add_box(-0.15, -0.06, 0.09, 0.05, 0.13, 0.05)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Harness_ReconChest.obj")

    # 3. TRACE Recon Electronics Backpack (with whip antenna)
    m = ObjMeshBuilder("TRACE_Backpack_ReconComms")
    m.add_box(0.0, 0.04, -0.14, 0.24, 0.32, 0.13)
    # Heat sink fins
    m.add_box(0.0, 0.04, -0.21, 0.18, 0.24, 0.03)
    # Whip antenna
    m.add_cylinder(0.10, 0.18, -0.14, 0.014, 0.07, segments=8, axis='y')
    m.add_cylinder(0.10, 0.44, -0.14, 0.005, 0.46, segments=6, axis='y')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Backpack_ReconComms.obj")

    # 4. TRACE Thigh Utility & Sensor Probe Pouch
    m = ObjMeshBuilder("TRACE_Pouches_UtilityLeg")
    m.add_box(-0.02, 0.0, 0.0, 0.035, 0.18, 0.11)
    m.add_cylinder(-0.035, 0.03, 0.0, 0.016, 0.16, segments=8, axis='y')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Pouches_UtilityLeg.obj")

    # 5. TRACE Hip Holster
    m = ObjMeshBuilder("TRACE_Holster_Recon")
    m.add_box(0.02, 0.0, 0.0, 0.035, 0.17, 0.09)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Holster_Recon.obj")

    # 6. TRACE First-Person Arms
    m = ObjMeshBuilder("FP_Arms_TRACE")
    m.add_cylinder(0.24, -0.18, 0.34, 0.05, 0.34, segments=12, axis='z')
    # Left wrist data-pad display
    m.add_box(-0.12, -0.15, 0.38, 0.06, 0.025, 0.11)
    m.add_box(0.24, -0.16, 0.52, 0.075, 0.08, 0.11)
    m.add_cylinder(-0.14, -0.22, 0.44, 0.048, 0.32, segments=12, axis='z')
    m.add_box(-0.08, -0.18, 0.60, 0.075, 0.075, 0.11)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/FP_Arms_TRACE.obj")

def main():
    ensure_dirs()
    generate_character_textures()
    generate_rex_equipment()
    generate_trace_equipment()
    print("\n>>> All Phase C.1 Character Assets Generated Successfully! <<<")

if __name__ == "__main__":
    main()
