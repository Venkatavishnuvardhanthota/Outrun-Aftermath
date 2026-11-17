"""
OUTRUN: AFTERMATH - Phase C Character Asset Generation Engine
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

def generate_normal_map(height_map: np.ndarray, strength: float = 2.0) -> Image.Image:
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
# 2. TEXTURE GENERATION
# ==============================================================================

def generate_character_textures():
    print("Generating Character PBR Textures (Shared, REX, TRACE)...")
    size = 1024

    # -------------------------------------------------------------
    # 1. SHARED OPERATOR UNDERSUIT (Tactical Ripstop Fabric)
    # -------------------------------------------------------------
    np.random.seed(101)
    # Dark charcoal base with ballistic micro-weave
    undersuit_albedo = np.full((size, size, 3), [42, 44, 48], dtype=np.float32)
    
    # Hexagonal / diamond micro-grid weave
    y_coords, x_coords = np.indices((size, size))
    weave_x = (x_coords % 8 < 2).astype(np.float32)
    weave_y = (y_coords % 8 < 2).astype(np.float32)
    weave = np.clip(weave_x + weave_y, 0, 1) * 12.0
    undersuit_albedo += weave[:, :, np.newaxis]
    
    # Reinforced knee/elbow patches (darker composite rubber pads)
    pad_mask = np.zeros((size, size), dtype=np.float32)
    pad_mask[128:384, 128:384] = 1.0 # patch 1
    pad_mask[512:768, 512:768] = 1.0 # patch 2
    undersuit_albedo[pad_mask > 0.5] = [28, 30, 32]
    
    # Micro noise
    noise = np.random.normal(0, 4, (size, size, 3))
    undersuit_albedo = np.clip(undersuit_albedo + noise, 0, 255)
    
    undersuit_height = np.full((size, size), 128, dtype=np.float32)
    undersuit_height += weave * 2.5
    undersuit_height += np.random.normal(0, 6, (size, size))
    undersuit_height = np.clip(undersuit_height, 0, 255)
    
    undersuit_albedo_img = Image.fromarray(undersuit_albedo.astype(np.uint8), mode='RGB')
    undersuit_normal_img = generate_normal_map(undersuit_height, strength=2.2)
    undersuit_mask = create_mask_map(
        np.full((size, size), 20, dtype=np.uint8),
        np.full((size, size), 210, dtype=np.uint8),
        np.full((size, size), 75, dtype=np.uint8)
    )
    
    undersuit_albedo_img.save(f"{TEXTURES_CHAR_ROOT}/Shared/T_Operator_Shared_Albedo.png")
    undersuit_normal_img.save(f"{TEXTURES_CHAR_ROOT}/Shared/T_Operator_Shared_Normal.png")
    undersuit_mask.save(f"{TEXTURES_CHAR_ROOT}/Shared/T_Operator_Shared_MaskMap.png")

    # -------------------------------------------------------------
    # 2. OPERATOR REX (Rogue Assault: Charcoal, Carbon Steel, Burnt Amber)
    # -------------------------------------------------------------
    np.random.seed(202)
    rex_albedo = np.full((size, size, 3), [38, 40, 44], dtype=np.float32) # Dark tactical grey
    
    # Rogue faction accents: burnt amber / tactical orange markings [225, 115, 30]
    orange_color = np.array([225, 115, 30], dtype=np.float32)
    # Add tactical stripes and hazard chevron markings
    stripe_mask = ((x_coords + y_coords) % 64 < 14) & (x_coords < 256)
    rex_albedo[stripe_mask] = orange_color
    
    # Ballistic plate segments (carbon composite)
    plate_mask = ((x_coords % 128 < 118) & (y_coords % 128 < 118)).astype(np.float32)
    plate_color = np.array([32, 34, 38], dtype=np.float32)
    rex_albedo = rex_albedo * (1 - plate_mask[:, :, np.newaxis] * 0.2)
    
    # Steel hardware and buckles [165, 170, 175]
    buckle_mask = (x_coords % 64 < 8) & (y_coords % 64 < 8)
    rex_albedo[buckle_mask] = [170, 175, 180]
    
    noise = np.random.normal(0, 5, (size, size, 3))
    rex_albedo = np.clip(rex_albedo + noise, 0, 255)
    
    rex_height = np.full((size, size), 128, dtype=np.float32)
    rex_height += plate_mask * 20.0
    rex_height[buckle_mask] += 35.0
    rex_height += np.random.normal(0, 5, (size, size))
    rex_height = np.clip(rex_height, 0, 255)
    
    rex_albedo_img = Image.fromarray(rex_albedo.astype(np.uint8), mode='RGB')
    rex_normal_img = generate_normal_map(rex_height, strength=2.8)
    rex_metallic = np.where(buckle_mask, 210, 40).astype(np.uint8)
    rex_smoothness = np.where(buckle_mask, 180, np.where(stripe_mask, 130, 95)).astype(np.uint8)
    rex_mask = create_mask_map(
        rex_metallic,
        np.full((size, size), 220, dtype=np.uint8),
        rex_smoothness
    )
    
    rex_albedo_img.save(f"{TEXTURES_CHAR_ROOT}/REX/T_Operator_REX_Albedo.png")
    rex_normal_img.save(f"{TEXTURES_CHAR_ROOT}/REX/T_Operator_REX_Normal.png")
    rex_mask.save(f"{TEXTURES_CHAR_ROOT}/REX/T_Operator_REX_MaskMap.png")

    # -------------------------------------------------------------
    # 3. OPERATOR TRACE (Sentinel Tracker: Matte Navy, Slate, Cyan / Teal)
    # -------------------------------------------------------------
    np.random.seed(303)
    trace_albedo = np.full((size, size, 3), [34, 46, 62], dtype=np.float32) # Matte navy slate
    
    # Sentinel faction accents: tactical cyan / electric teal [45, 195, 215]
    cyan_color = np.array([45, 195, 215], dtype=np.float32)
    
    # Digital sensor traces & indicator stripes
    trace_stripe = ((x_coords - y_coords) % 48 < 10) & (y_coords < 384)
    trace_albedo[trace_stripe] = cyan_color
    
    # Optical lens circles (sensor suite)
    lens_centers = [(128, 128), (128, 256), (256, 128)]
    lens_mask = np.zeros((size, size), dtype=bool)
    for cx, cy in lens_centers:
        dist = np.sqrt((x_coords - cx)**2 + (y_coords - cy)**2)
        lens_mask |= (dist < 32)
    trace_albedo[lens_mask] = [20, 210, 230]
    
    # Titanium hardware and clips [180, 190, 200]
    clip_mask = (x_coords % 64 > 56) & (y_coords % 64 > 56)
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
    trace_mask = create_mask_map(
        trace_metallic,
        np.full((size, size), 230, dtype=np.uint8),
        trace_smoothness
    )
    
    trace_albedo_img.save(f"{TEXTURES_CHAR_ROOT}/TRACE/T_Operator_TRACE_Albedo.png")
    trace_normal_img.save(f"{TEXTURES_CHAR_ROOT}/TRACE/T_Operator_TRACE_Normal.png")
    trace_mask.save(f"{TEXTURES_CHAR_ROOT}/TRACE/T_Operator_TRACE_MaskMap.png")

    print("Character PBR Textures successfully generated!")

# ==============================================================================
# 3. 3D OBJ MESH GENERATOR
# ==============================================================================

class ObjMeshBuilder:
    def __init__(self, name: str):
        self.name = name
        self.vertices = []
        self.uvs = []
        self.normals = []
        self.faces = []

    def add_vertex(self, x: float, y: float, z: float):
        self.vertices.append((x, y, z))
        return len(self.vertices)

    def add_uv(self, u: float, v: float):
        self.uvs.append((u, v))
        return len(self.uvs)

    def add_normal(self, nx: float, ny: float, nz: float):
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

        # Side quads
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
    # Main helmet shell
    m.add_box(0.0, 0.08, -0.02, 0.28, 0.22, 0.32)
    # Front visor brow
    m.add_box(0.0, 0.04, 0.14, 0.24, 0.08, 0.08)
    # Tinted visor lens
    m.add_box(0.0, 0.01, 0.15, 0.22, 0.06, 0.04)
    # Side communications ear covers
    m.add_box(0.14, 0.02, -0.02, 0.04, 0.12, 0.14)
    m.add_box(-0.14, 0.02, -0.02, 0.04, 0.12, 0.14)
    # NVG mount shroud on forehead
    m.add_box(0.0, 0.12, 0.13, 0.08, 0.08, 0.04)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Helmet_Combat.obj")

    # 2. REX Tactical Assault Vest (Heavy plate carrier with triple mag pouches)
    m = ObjMeshBuilder("REX_Vest_TacticalAssault")
    # Front chest plate
    m.add_box(0.0, 0.0, 0.11, 0.34, 0.40, 0.10)
    # Back armor plate
    m.add_box(0.0, 0.0, -0.11, 0.34, 0.40, 0.10)
    # Side cummerbund straps
    m.add_box(0.18, -0.04, 0.0, 0.06, 0.24, 0.24)
    m.add_box(-0.18, -0.04, 0.0, 0.06, 0.24, 0.24)
    # Triple magazine pouches (front)
    m.add_box(-0.09, -0.10, 0.17, 0.07, 0.16, 0.06)
    m.add_box(0.0, -0.10, 0.17, 0.07, 0.16, 0.06)
    m.add_box(0.09, -0.10, 0.17, 0.07, 0.16, 0.06)
    # Tactical radio with antenna (left chest)
    m.add_box(-0.14, 0.12, 0.15, 0.06, 0.12, 0.05)
    m.add_cylinder(-0.14, 0.24, 0.15, 0.008, 0.16, segments=6, axis='y')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Vest_TacticalAssault.obj")

    # 3. REX Armored Pauldrons (Left & Right shoulder armor plates)
    m = ObjMeshBuilder("REX_Pauldrons_Armored")
    # Right shoulder plate
    m.add_box(0.24, 0.0, 0.0, 0.12, 0.16, 0.16)
    # Left shoulder plate
    m.add_box(-0.24, 0.0, 0.0, 0.12, 0.16, 0.16)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Pauldrons_Armored.obj")

    # 4. REX Assault Backpack (Rucksack with bedroll)
    m = ObjMeshBuilder("REX_Backpack_Assault")
    # Main rucksack body
    m.add_box(0.0, 0.0, -0.16, 0.32, 0.42, 0.20)
    # Top bedroll cylinder
    m.add_cylinder(0.0, 0.24, -0.16, 0.08, 0.34, segments=10, axis='x')
    # Left utility pouch
    m.add_box(-0.18, -0.04, -0.16, 0.08, 0.22, 0.14)
    # Right utility pouch
    m.add_box(0.18, -0.04, -0.16, 0.08, 0.22, 0.14)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Backpack_Assault.obj")

    # 5. REX Drop-Leg Sidearm Holster
    m = ObjMeshBuilder("REX_Holster_Sidearm")
    # Thigh mounting plate
    m.add_box(0.02, 0.0, 0.0, 0.04, 0.22, 0.14)
    # Holster bucket
    m.add_box(0.05, -0.02, 0.0, 0.06, 0.18, 0.10)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Holster_Sidearm.obj")

    # 6. REX First-Person Arms
    m = ObjMeshBuilder("FP_Arms_REX")
    # Right forearm (heavy combat sleeve)
    m.add_box(0.24, -0.18, 0.32, 0.10, 0.10, 0.36)
    # Right gloved hand holding rifle grip
    m.add_box(0.24, -0.16, 0.52, 0.09, 0.10, 0.12)
    # Left forearm reaching forward for rifle handguard
    m.add_box(-0.12, -0.22, 0.45, 0.10, 0.10, 0.32)
    # Left hand supporting barrel
    m.add_box(-0.06, -0.18, 0.62, 0.09, 0.09, 0.12)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/FP_Arms_REX.obj")

def generate_trace_equipment():
    print("Generating Operator TRACE Equipment Models...")

    # 1. TRACE Recon Comms Headset (Cap with comms & optical monocle)
    m = ObjMeshBuilder("TRACE_Headset_ReconComms")
    # Low profile tactical cap
    m.add_box(0.0, 0.08, -0.02, 0.24, 0.16, 0.28)
    # Backward brim
    m.add_box(0.0, 0.04, -0.18, 0.18, 0.03, 0.10)
    # Headset ear cups
    m.add_box(0.13, 0.02, -0.02, 0.04, 0.10, 0.10)
    m.add_box(-0.13, 0.02, -0.02, 0.04, 0.10, 0.10)
    # Boom mic arm curving around mouth
    m.add_box(0.12, -0.06, 0.08, 0.02, 0.02, 0.14)
    m.add_box(0.06, -0.06, 0.15, 0.08, 0.02, 0.02)
    # Optical recon monocle over right eye
    m.add_box(0.06, 0.04, 0.15, 0.05, 0.05, 0.06)
    m.add_cylinder(0.06, 0.04, 0.18, 0.022, 0.03, segments=8, axis='z')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Headset_ReconComms.obj")

    # 2. TRACE Recon Chest Harness (Sleek chest rig with sensor unit)
    m = ObjMeshBuilder("TRACE_Harness_ReconChest")
    # Center sensor pack housing
    m.add_box(0.0, 0.02, 0.10, 0.22, 0.24, 0.08)
    # Multi-spectrum sensor lenses on chest
    m.add_cylinder(-0.05, 0.04, 0.14, 0.025, 0.02, segments=8, axis='z')
    m.add_cylinder(0.05, 0.04, 0.14, 0.025, 0.02, segments=8, axis='z')
    m.add_cylinder(0.0, -0.04, 0.14, 0.020, 0.02, segments=8, axis='z')
    # Cross webbing harness straps
    m.add_box(0.12, 0.0, 0.08, 0.05, 0.32, 0.04)
    m.add_box(-0.12, 0.0, 0.08, 0.05, 0.32, 0.04)
    # Dual compact mag pouches
    m.add_box(0.16, -0.08, 0.10, 0.06, 0.14, 0.06)
    m.add_box(-0.16, -0.08, 0.10, 0.06, 0.14, 0.06)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Harness_ReconChest.obj")

    # 3. TRACE Recon Electronics Backpack (with whip antenna)
    m = ObjMeshBuilder("TRACE_Backpack_ReconComms")
    # Compact telemetry pack
    m.add_box(0.0, 0.04, -0.14, 0.26, 0.34, 0.14)
    # Cooling heat-sink ribs
    m.add_box(0.0, 0.04, -0.22, 0.20, 0.26, 0.04)
    # High-gain whip antenna on right side extending up
    m.add_cylinder(0.11, 0.20, -0.14, 0.016, 0.08, segments=8, axis='y')
    m.add_cylinder(0.11, 0.48, -0.14, 0.006, 0.50, segments=6, axis='y')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Backpack_ReconComms.obj")

    # 4. TRACE Thigh Utility & Sensor Probe Pouch
    m = ObjMeshBuilder("TRACE_Pouches_UtilityLeg")
    # Left thigh utility pouch
    m.add_box(-0.02, 0.0, 0.0, 0.04, 0.20, 0.12)
    # Scanning probe cylinder in pouch
    m.add_cylinder(-0.04, 0.04, 0.0, 0.02, 0.18, segments=8, axis='y')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Pouches_UtilityLeg.obj")

    # 5. TRACE Hip Holster
    m = ObjMeshBuilder("TRACE_Holster_Recon")
    m.add_box(0.02, 0.0, 0.0, 0.04, 0.18, 0.10)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Holster_Recon.obj")

    # 6. TRACE First-Person Arms
    m = ObjMeshBuilder("FP_Arms_TRACE")
    # Right forearm (streamlined sleeve with wrist terminal)
    m.add_box(0.24, -0.18, 0.32, 0.09, 0.09, 0.36)
    # Wrist terminal data pad on left forearm
    m.add_box(-0.12, -0.16, 0.38, 0.07, 0.03, 0.12)
    # Right gloved hand holding rifle grip
    m.add_box(0.24, -0.16, 0.52, 0.08, 0.09, 0.12)
    # Left forearm reaching forward
    m.add_box(-0.12, -0.22, 0.45, 0.09, 0.09, 0.32)
    # Left hand supporting barrel
    m.add_box(-0.06, -0.18, 0.62, 0.08, 0.08, 0.12)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/FP_Arms_TRACE.obj")

def main():
    ensure_dirs()
    generate_character_textures()
    generate_rex_equipment()
    generate_trace_equipment()
    print("\n>>> All Phase C Character Assets Generated Successfully! <<<")

if __name__ == "__main__":
    main()
