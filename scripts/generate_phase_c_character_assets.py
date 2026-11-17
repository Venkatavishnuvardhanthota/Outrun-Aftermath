"""
OUTRUN: AFTERMATH - Phase C.1.1 Character Asset Generation Engine
Procedurally authors high-quality PBR textures and refined 3D Wavefront OBJ models
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
# 2. PBR TEXTURE GENERATION (Calibrated for URP PBR Readability)
# ==============================================================================

def generate_character_textures():
    print("Generating Character PBR Textures (Shared, REX, TRACE)...")
    size = 1024

    # -------------------------------------------------------------
    # 1. SHARED OPERATOR UNDERSUIT (Tactical Ripstop Fabric)
    # Balanced tactical charcoal/slate (sRGB 54, 56, 60), 0 metallic, matte fabric
    # -------------------------------------------------------------
    np.random.seed(101)
    undersuit_albedo = np.full((size, size, 3), [54, 56, 60], dtype=np.float32)
    y_coords, x_coords = np.indices((size, size))
    
    # Micro ripstop cross-hatch weave
    ripstop_grid = ((x_coords // 8) % 2 == 0) & ((y_coords // 8) % 2 == 0)
    undersuit_albedo[ripstop_grid] += [5, 5, 6]

    # Reinforced Kevlar knee and elbow panels (slightly darker graphite)
    joint_reinforce_mask = ((y_coords > 550) & (y_coords < 780) & (x_coords > 250) & (x_coords < 750))
    undersuit_albedo[joint_reinforce_mask] = [42, 44, 48]

    # Subtle fabric micro-noise
    noise = np.random.normal(0, 2.0, (size, size, 3))
    undersuit_albedo = np.clip(undersuit_albedo + noise, 0, 255)

    undersuit_height = np.full((size, size), 128, dtype=np.float32)
    undersuit_height[ripstop_grid] += 8.0
    undersuit_height[joint_reinforce_mask] += 20.0
    undersuit_height += np.random.normal(0, 2.5, (size, size))
    undersuit_height = np.clip(undersuit_height, 0, 255)

    undersuit_albedo_img = Image.fromarray(undersuit_albedo.astype(np.uint8), mode='RGB')
    undersuit_normal_img = generate_normal_map(undersuit_height, strength=2.0)
    undersuit_metallic = np.full((size, size), 0, dtype=np.uint8) # Non-metallic fabric
    undersuit_smoothness = np.where(joint_reinforce_mask, 80, 55).astype(np.uint8) # 0.22 - 0.31 smoothness
    undersuit_ao = np.full((size, size), 245, dtype=np.uint8)
    undersuit_ao[joint_reinforce_mask] = 220
    undersuit_mask = create_mask_map(undersuit_metallic, undersuit_ao, undersuit_smoothness)

    undersuit_albedo_img.save(f"{TEXTURES_CHAR_ROOT}/Shared/T_Operator_Shared_Albedo.png")
    undersuit_normal_img.save(f"{TEXTURES_CHAR_ROOT}/Shared/T_Operator_Shared_Normal.png")
    undersuit_mask.save(f"{TEXTURES_CHAR_ROOT}/Shared/T_Operator_Shared_MaskMap.png")

    # -------------------------------------------------------------
    # 2. OPERATOR REX (Rogue Assault)
    # Tactical matte carbon plates (sRGB 48, 50, 54), steel hardware [72, 75, 80]
    # Localized burnt amber insignia & accents [205, 115, 30] (NO diagonal stripes!)
    # -------------------------------------------------------------
    np.random.seed(102)
    rex_albedo = np.full((size, size, 3), [50, 52, 56], dtype=np.float32)

    # Ceramic ballistic armor plate zones (fine stippled surface)
    plate_zones = ((x_coords > 180) & (x_coords < 844) & (y_coords > 180) & (y_coords < 840))
    rex_albedo[plate_zones] = [44, 46, 50]

    # Steel hardware, buckles, and D-rings
    hardware_mask = (((x_coords > 120) & (x_coords < 160) & (y_coords > 300) & (y_coords < 420)) |
                     ((x_coords > 864) & (x_coords < 904) & (y_coords > 300) & (y_coords < 420)) |
                     ((x_coords > 450) & (x_coords < 574) & (y_coords > 720) & (y_coords < 760)))
    rex_albedo[hardware_mask] = [78, 82, 88]

    # Specific localized tactical emblems & burnt amber chevron patch (upper chest/shoulder patch)
    chevron_mask = ((x_coords > 470) & (x_coords < 554) & (y_coords > 240) & (y_coords < 320))
    # Draw chevron shape inside the patch box
    for py in range(240, 320):
        for px in range(470, 554):
            offset_x = abs(px - 512)
            offset_y = py - 240
            if abs(offset_y - offset_x * 0.8) < 10:
                chevron_mask[py, px] = True
            else:
                chevron_mask[py, px] = False

    rex_albedo[chevron_mask] = [205, 115, 30] # Burnt amber accent

    # Ballistic helmet visor zone (polarized amber visor)
    visor_zone = ((y_coords > 60) & (y_coords < 140) & (x_coords > 360) & (x_coords < 664))
    rex_albedo[visor_zone] = [185, 110, 35]

    # Add gentle noise
    noise = np.random.normal(0, 2.5, (size, size, 3))
    rex_albedo = np.clip(rex_albedo + noise, 0, 255)

    rex_height = np.full((size, size), 128, dtype=np.float32)
    rex_height[plate_zones] += 25.0
    rex_height[hardware_mask] += 40.0
    rex_height[chevron_mask] += 15.0
    rex_height[visor_zone] += 30.0
    rex_height += np.random.normal(0, 3.0, (size, size))
    rex_height = np.clip(rex_height, 0, 255)

    rex_albedo_img = Image.fromarray(rex_albedo.astype(np.uint8), mode='RGB')
    rex_normal_img = generate_normal_map(rex_height, strength=2.6)
    
    # Proper PBR: Only metal hardware is metallic! Plates and fabric are dielectric!
    rex_metallic = np.where(hardware_mask, 210, np.where(visor_zone, 60, 20)).astype(np.uint8)
    # Smoothness: Visor is polished, hardware is semi-gloss, plates and fabric are matte
    rex_smoothness = np.where(visor_zone, 225, np.where(hardware_mask, 165, np.where(plate_zones, 95, 70))).astype(np.uint8)
    rex_ao = np.full((size, size), 245, dtype=np.uint8)
    rex_ao[plate_zones] = 230
    rex_ao[hardware_mask] = 210
    rex_mask = create_mask_map(rex_metallic, rex_ao, rex_smoothness)

    rex_albedo_img.save(f"{TEXTURES_CHAR_ROOT}/REX/T_Operator_REX_Albedo.png")
    rex_normal_img.save(f"{TEXTURES_CHAR_ROOT}/REX/T_Operator_REX_Normal.png")
    rex_mask.save(f"{TEXTURES_CHAR_ROOT}/REX/T_Operator_REX_MaskMap.png")

    # -------------------------------------------------------------
    # 3. OPERATOR TRACE (Sentinel Tracker: Matte Navy, Cyan Sensor Accents)
    # -------------------------------------------------------------
    np.random.seed(103)
    trace_albedo = np.full((size, size, 3), [56, 66, 78], dtype=np.float32)

    # Optical sensor elements and telemetry lenses
    sensor_mask = ((x_coords > 420) & (x_coords < 604) & (y_coords > 80) & (y_coords < 140))
    trace_albedo[sensor_mask] = [45, 195, 235]

    # Lightweight titanium buckle hardware
    clip_mask = ((x_coords > 140) & (x_coords < 180) & (y_coords > 400) & (y_coords < 520))
    trace_albedo[clip_mask] = [175, 185, 195]

    noise = np.random.normal(0, 2.5, (size, size, 3))
    trace_albedo = np.clip(trace_albedo + noise, 0, 255)

    trace_height = np.full((size, size), 128, dtype=np.float32)
    trace_height[sensor_mask] += 35.0
    trace_height[clip_mask] += 30.0
    trace_height += np.random.normal(0, 3.0, (size, size))
    trace_height = np.clip(trace_height, 0, 255)

    trace_albedo_img = Image.fromarray(trace_albedo.astype(np.uint8), mode='RGB')
    trace_normal_img = generate_normal_map(trace_height, strength=2.5)
    trace_metallic = np.where(clip_mask, 215, np.where(sensor_mask, 75, 25)).astype(np.uint8)
    trace_smoothness = np.where(sensor_mask, 235, np.where(clip_mask, 175, 80)).astype(np.uint8)
    trace_ao = np.full((size, size), 245, dtype=np.uint8)
    trace_ao[sensor_mask] = 215
    trace_mask = create_mask_map(trace_metallic, trace_ao, trace_smoothness)

    trace_albedo_img.save(f"{TEXTURES_CHAR_ROOT}/TRACE/T_Operator_TRACE_Albedo.png")
    trace_normal_img.save(f"{TEXTURES_CHAR_ROOT}/TRACE/T_Operator_TRACE_Normal.png")
    trace_mask.save(f"{TEXTURES_CHAR_ROOT}/TRACE/T_Operator_TRACE_MaskMap.png")

    print("Character PBR Textures successfully generated and calibrated!")

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

    def add_triangle(self, v1, v2, v3, uv1, uv2, uv3, normal):
        n_idx = self.add_normal(*normal)
        u1_idx = self.add_uv(*uv1)
        u2_idx = self.add_uv(*uv2)
        u3_idx = self.add_uv(*uv3)
        self.faces.append([(v1, u1_idx, n_idx), (v2, u2_idx, n_idx), (v3, u3_idx, n_idx)])

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
        # Left (-X)
        self.add_quad(v000, v001, v011, v010, (0, 0), (1, 0), (1, 1), (0, 1), (-1, 0, 0))
        # Right (+X)
        self.add_quad(v101, v100, v110, v111, (0, 0), (1, 0), (1, 1), (0, 1), (1, 0, 0))
        # Top (+Y)
        self.add_quad(v011, v111, v110, v010, (0, 0), (1, 0), (1, 1), (0, 1), (0, 1, 0))
        # Bottom (-Y)
        self.add_quad(v000, v100, v101, v001, (0, 0), (1, 0), (1, 1), (0, 1), (0, -1, 0))

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

    def add_frustum(self, cx: float, cy: float, cz: float, rx_top: float, rz_top: float, rx_bot: float, rz_bot: float, height: float, segments: int = 12):
        hh = height * 0.5
        top_verts = []
        bot_verts = []
        for i in range(segments):
            angle = 2.0 * math.pi * i / segments
            ca, sa = math.cos(angle), math.sin(angle)
            top_verts.append(self.add_vertex(cx + ca * rx_top, cy + hh, cz + sa * rz_top))
            bot_verts.append(self.add_vertex(cx + ca * rx_bot, cy - hh, cz + sa * rz_bot))

        for i in range(segments):
            next_i = (i + 1) % segments
            angle_mid = 2.0 * math.pi * (i + 0.5) / segments
            norm = (math.cos(angle_mid), 0.1, math.sin(angle_mid))
            self.add_quad(bot_verts[i], bot_verts[next_i], top_verts[next_i], top_verts[i],
                          (i/segments, 0), ((i+1)/segments, 0), ((i+1)/segments, 1), (i/segments, 1), norm)

    def add_dome(self, cx: float, cy: float, cz: float, rx: float, ry: float, rz: float, rings: int = 5, segments: int = 12):
        pole_v = self.add_vertex(cx, cy + ry, cz)
        prev_ring = []

        for r in range(1, rings + 1):
            phi = (math.pi * 0.5) * (r / rings)
            ring_y = cy + ry * math.cos(phi)
            scale = math.sin(phi)
            curr_ring = []
            for i in range(segments):
                theta = 2.0 * math.pi * i / segments
                vx = cx + rx * scale * math.cos(theta)
                vz = cz + rz * scale * math.sin(theta)
                curr_ring.append(self.add_vertex(vx, ring_y, vz))

            if r == 1:
                for i in range(segments):
                    next_i = (i + 1) % segments
                    self.add_triangle(pole_v, curr_ring[i], curr_ring[next_i], (0.5, 1.0), (i/segments, 0.8), ((i+1)/segments, 0.8), (0, 1, 0))
            else:
                for i in range(segments):
                    next_i = (i + 1) % segments
                    self.add_quad(prev_ring[i], prev_ring[next_i], curr_ring[next_i], curr_ring[i],
                                  (i/segments, 1.0 - (r-1)/rings), ((i+1)/segments, 1.0 - (r-1)/rings),
                                  ((i+1)/segments, 1.0 - r/rings), (i/segments, 1.0 - r/rings),
                                  (math.cos(2.0*math.pi*(i+0.5)/segments), 0.5, math.sin(2.0*math.pi*(i+0.5)/segments)))
            prev_ring = curr_ring

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
# 4. REFINED MODULAR OPERATOR EQUIPMENT GENERATION
# ==============================================================================

def generate_rex_equipment():
    print("Generating Refined Operator REX Equipment Models (C.1.1)...")

    # 1. REX Combat Helmet (Anatomical dome, ARC side rails, NVG shroud, visor, chin harness)
    m = ObjMeshBuilder("REX_Helmet_Combat")
    # Contoured ballistic shell dome
    m.add_dome(0.0, 0.04, -0.01, 0.125, 0.11, 0.145, rings=6, segments=16)
    # Lower rim flare & neck protection
    m.add_frustum(0.0, 0.01, -0.01, 0.125, 0.145, 0.132, 0.155, 0.05, segments=16)
    # Front visor brow overhang
    m.add_box(0.0, 0.035, 0.115, 0.18, 0.025, 0.05)
    # Polarized tactical ballistic visor lens
    m.add_frustum(0.0, 0.005, 0.125, 0.09, 0.02, 0.085, 0.018, 0.045, segments=10)
    # Side ARC rails (left & right) for tactical mounting
    m.add_box(0.122, 0.03, 0.01, 0.02, 0.03, 0.11)
    m.add_box(-0.122, 0.03, 0.01, 0.02, 0.03, 0.11)
    # Low-profile comms ear covers
    m.add_cylinder(0.118, -0.01, -0.015, 0.032, 0.025, segments=10, axis='x')
    m.add_cylinder(-0.118, -0.01, -0.015, 0.032, 0.025, segments=10, axis='x')
    # Forehead Wilcox NVG shroud mount
    m.add_box(0.0, 0.065, 0.128, 0.045, 0.045, 0.015)
    # Chin harness strap & buckle under jaw
    m.add_cylinder(0.0, -0.095, 0.02, 0.008, 0.08, segments=8, axis='x')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Helmet_Combat.obj")

    # 2. REX Tactical Assault Vest (SAPI-cut plate carrier, triple mag pouches, radio, cummerbund)
    m = ObjMeshBuilder("REX_Vest_TacticalAssault")
    # Front SAPI plate (tapered swimmer cut at top corners)
    m.add_frustum(0.0, 0.03, 0.115, 0.13, 0.04, 0.155, 0.04, 0.16, segments=12) # Upper chest plate
    m.add_frustum(0.0, -0.11, 0.12, 0.155, 0.04, 0.145, 0.04, 0.14, segments=12) # Lower torso plate
    # Rear back armor plate
    m.add_frustum(0.0, 0.0, -0.115, 0.145, 0.04, 0.15, 0.04, 0.30, segments=12)
    # Ergonomic padded shoulder straps
    m.add_box(-0.12, 0.18, 0.0, 0.065, 0.035, 0.22)
    m.add_box(0.12, 0.18, 0.0, 0.065, 0.035, 0.22)
    # Side elastic cummerbunds with MOLLE ribs
    m.add_box(0.16, -0.04, 0.0, 0.025, 0.18, 0.20)
    m.add_box(-0.16, -0.04, 0.0, 0.025, 0.18, 0.20)
    # Triple tactical rifle magazine pouches with bungee retention tabs
    m.add_box(-0.085, -0.08, 0.16, 0.06, 0.135, 0.04)
    m.add_box(0.0, -0.08, 0.165, 0.06, 0.135, 0.04)
    m.add_box(0.085, -0.08, 0.16, 0.06, 0.135, 0.04)
    # Bungee cord tabs on mag tops
    m.add_box(-0.085, -0.005, 0.16, 0.02, 0.015, 0.03)
    m.add_box(0.0, -0.005, 0.165, 0.02, 0.015, 0.03)
    m.add_box(0.085, -0.005, 0.16, 0.02, 0.015, 0.03)
    # Tactical radio with keypad and flexible whip antenna (left upper chest)
    m.add_box(-0.125, 0.08, 0.145, 0.045, 0.10, 0.035)
    m.add_cylinder(-0.125, 0.18, 0.145, 0.005, 0.12, segments=6, axis='y')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Vest_TacticalAssault.obj")

    # 3. REX Armored Pauldrons (Articulated composite shoulder armor contoured over deltoids)
    m = ObjMeshBuilder("REX_Pauldrons_Armored")
    # Left deltoid articulated plates
    m.add_frustum(-0.21, 0.01, 0.0, 0.055, 0.07, 0.045, 0.06, 0.10, segments=10)
    m.add_frustum(-0.22, -0.06, 0.0, 0.045, 0.06, 0.035, 0.05, 0.08, segments=10)
    # Right deltoid articulated plates
    m.add_frustum(0.21, 0.01, 0.0, 0.055, 0.07, 0.045, 0.06, 0.10, segments=10)
    m.add_frustum(0.22, -0.06, 0.0, 0.045, 0.06, 0.035, 0.05, 0.08, segments=10)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Pauldrons_Armored.obj")

    # 4. REX Assault Backpack (3-day tactical rucksack with dual pouches, hydration port & breacher roll)
    m = ObjMeshBuilder("REX_Backpack_Assault")
    # Main contoured cargo pack
    m.add_frustum(0.0, 0.02, -0.17, 0.125, 0.07, 0.14, 0.08, 0.32, segments=12)
    # Upper external zippered admin pouch
    m.add_box(0.0, 0.09, -0.23, 0.17, 0.11, 0.05)
    # Lower external sustainment pouch
    m.add_box(0.0, -0.06, -0.235, 0.19, 0.13, 0.06)
    # Side utility / canteen pouches
    m.add_box(-0.135, -0.02, -0.17, 0.045, 0.14, 0.07)
    m.add_box(0.135, -0.02, -0.17, 0.045, 0.14, 0.07)
    # Bottom breacher charge / thermal mat roll with twin cinch straps
    m.add_cylinder(0.0, -0.17, -0.165, 0.045, 0.26, segments=10, axis='x')
    m.add_box(-0.08, -0.17, -0.165, 0.015, 0.095, 0.095)
    m.add_box(0.08, -0.17, -0.165, 0.015, 0.095, 0.095)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Backpack_Assault.obj")

    # 5. REX Sidearm Holster (Formed Kydex drop-leg holster with dual rubberized thigh straps)
    m = ObjMeshBuilder("REX_Holster_Sidearm")
    # Rigid drop plate
    m.add_box(0.195, 0.02, 0.0, 0.012, 0.18, 0.11)
    # Molded pistol holster body (angled for tactical draw)
    m.add_frustum(0.208, -0.01, 0.01, 0.032, 0.065, 0.024, 0.045, 0.17, segments=10)
    # Dual leg retention straps
    m.add_cylinder(0.155, 0.05, 0.0, 0.092, 0.02, segments=12, axis='y')
    m.add_cylinder(0.155, -0.06, 0.0, 0.085, 0.02, segments=12, axis='y')
    # Retention hood & thumb-break
    m.add_box(0.208, 0.075, 0.01, 0.035, 0.015, 0.05)
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/REX_Holster_Sidearm.obj")

    # 6. FP Arms REX (High-fidelity viewmodel tactical sleeves and tactical assault gloves)
    m = ObjMeshBuilder("FP_Arms_REX")
    # Left forearm sleeve & tactical glove
    m.add_frustum(-0.18, -0.15, 0.28, 0.042, 0.042, 0.036, 0.036, 0.24, segments=12) # Forearm sleeve
    m.add_frustum(-0.18, -0.15, 0.43, 0.036, 0.036, 0.032, 0.032, 0.07, segments=10) # Wrist cuff
    m.add_box(-0.18, -0.15, 0.49, 0.075, 0.032, 0.07) # Glove palm
    m.add_box(-0.18, -0.14, 0.51, 0.072, 0.015, 0.035) # Armored polymer knuckle bar
    # Left fingers grouped around forward rifle handguard grip
    m.add_cylinder(-0.18, -0.15, 0.54, 0.022, 0.065, segments=8, axis='x')
    m.add_box(-0.145, -0.13, 0.49, 0.022, 0.022, 0.04) # Thumb

    # Right forearm sleeve & tactical glove (trigger hand)
    m.add_frustum(0.18, -0.15, 0.20, 0.042, 0.042, 0.036, 0.036, 0.24, segments=12) # Forearm sleeve
    m.add_frustum(0.18, -0.15, 0.35, 0.036, 0.036, 0.032, 0.032, 0.07, segments=10) # Wrist cuff
    m.add_box(0.18, -0.15, 0.41, 0.075, 0.032, 0.07) # Glove palm
    m.add_box(0.18, -0.14, 0.43, 0.072, 0.015, 0.035) # Armored polymer knuckle bar
    # Right fingers curled around pistol grip & trigger finger
    m.add_cylinder(0.18, -0.165, 0.42, 0.022, 0.065, segments=8, axis='x')
    m.add_box(0.15, -0.14, 0.44, 0.012, 0.012, 0.045) # Trigger finger indexing forward
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/REX/Meshes/FP_Arms_REX.obj")

def generate_trace_equipment():
    print("Generating Operator TRACE Equipment Models...")

    # 1. TRACE Recon Comms Headset
    m = ObjMeshBuilder("TRACE_Headset_ReconComms")
    m.add_frustum(0.0, 0.08, 0.0, 0.125, 0.135, 0.12, 0.13, 0.06, segments=12) # Overhead band
    m.add_cylinder(0.12, 0.01, 0.0, 0.038, 0.035, segments=10, axis='x') # Left ear cup
    m.add_cylinder(-0.12, 0.01, 0.0, 0.038, 0.035, segments=10, axis='x') # Right ear cup
    m.add_cylinder(0.10, -0.03, 0.06, 0.005, 0.09, segments=6, axis='z') # Flexible boom mic
    m.add_box(0.06, -0.03, 0.11, 0.02, 0.02, 0.025) # Mic element
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Headset_ReconComms.obj")

    # 2. TRACE Recon Chest Harness
    m = ObjMeshBuilder("TRACE_Harness_ReconChest")
    m.add_box(0.0, 0.0, 0.12, 0.28, 0.24, 0.06)
    m.add_box(-0.12, 0.16, 0.0, 0.05, 0.03, 0.22)
    m.add_box(0.12, 0.16, 0.0, 0.05, 0.03, 0.22)
    m.add_box(-0.07, -0.03, 0.16, 0.055, 0.12, 0.04)
    m.add_box(0.07, -0.03, 0.16, 0.055, 0.12, 0.04)
    m.add_cylinder(0.0, 0.03, 0.155, 0.025, 0.03, segments=8, axis='z')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Harness_ReconChest.obj")

    # 3. TRACE Recon Comms Backpack
    m = ObjMeshBuilder("TRACE_Backpack_ReconComms")
    m.add_box(0.0, 0.04, -0.16, 0.22, 0.30, 0.11)
    m.add_box(0.0, -0.04, -0.22, 0.18, 0.14, 0.05)
    m.add_cylinder(0.08, 0.22, -0.16, 0.006, 0.18, segments=6, axis='y')
    m.add_cylinder(-0.08, 0.19, -0.16, 0.006, 0.14, segments=6, axis='y')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Backpack_ReconComms.obj")

    # 4. TRACE Utility Leg Pouches
    m = ObjMeshBuilder("TRACE_Pouches_UtilityLeg")
    m.add_box(-0.18, -0.02, 0.0, 0.055, 0.14, 0.10)
    m.add_cylinder(-0.14, 0.03, 0.0, 0.085, 0.02, segments=12, axis='y')
    m.add_cylinder(-0.14, -0.06, 0.0, 0.080, 0.02, segments=12, axis='y')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Pouches_UtilityLeg.obj")

    # 5. TRACE Recon Sidearm Holster
    m = ObjMeshBuilder("TRACE_Holster_Recon")
    m.add_box(0.19, 0.0, 0.0, 0.012, 0.16, 0.10)
    m.add_box(0.20, -0.01, 0.01, 0.035, 0.16, 0.06)
    m.add_cylinder(0.15, 0.03, 0.0, 0.088, 0.02, segments=12, axis='y')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/TRACE_Holster_Recon.obj")

    # 6. FP Arms TRACE
    m = ObjMeshBuilder("FP_Arms_TRACE")
    m.add_frustum(-0.18, -0.15, 0.28, 0.040, 0.040, 0.034, 0.034, 0.24, segments=12)
    m.add_box(-0.18, -0.15, 0.48, 0.07, 0.03, 0.07)
    m.add_cylinder(-0.18, -0.15, 0.53, 0.02, 0.06, segments=8, axis='x')
    m.add_frustum(0.18, -0.15, 0.20, 0.040, 0.040, 0.034, 0.034, 0.24, segments=12)
    m.add_box(0.18, -0.15, 0.40, 0.07, 0.03, 0.07)
    m.add_cylinder(0.18, -0.16, 0.41, 0.02, 0.06, segments=8, axis='x')
    m.write_obj(f"{CHARACTERS_ROOT}/Operators/TRACE/Meshes/FP_Arms_TRACE.obj")

def main():
    ensure_dirs()
    generate_character_textures()
    generate_rex_equipment()
    generate_trace_equipment()
    print("Phase C.1.1 Character Assets generation complete!")

if __name__ == "__main__":
    main()
