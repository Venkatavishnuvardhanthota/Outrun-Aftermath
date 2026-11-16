"""
OUTRUN: AFTERMATH - Phase B Asset Generation Engine
Procedurally authors authentic, high-quality PBR textures and 3D Wavefront OBJ models
for the Dead City Sector 4 visual overhaul.
"""

import os
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

TEXTURES_ROOT = "client/Assets/Art/Textures"
MESHES_ROOT = "client/Assets/Art/Meshes"

def ensure_dirs():
    dirs = [
        f"{TEXTURES_ROOT}/Roads",
        f"{TEXTURES_ROOT}/Buildings",
        f"{TEXTURES_ROOT}/Props",
        f"{TEXTURES_ROOT}/Vegetation",
        f"{MESHES_ROOT}/Roads",
        f"{MESHES_ROOT}/Buildings",
        f"{MESHES_ROOT}/Props",
        f"{MESHES_ROOT}/Debris"
    ]
    for d in dirs:
        os.makedirs(d, exist_ok=True)

# ==============================================================================
# 1. PBR TEXTURE GENERATION
# ==============================================================================

def generate_normal_map(height_map: np.ndarray, strength: float = 2.0) -> Image.Image:
    """Computes tangent-space normal map from a grayscale height map using Sobel filter."""
    h, w = height_map.shape
    # Sobel kernels
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

    # Normal vector = (-gx, -gy, 1) normalized
    gz = np.ones((h, w), dtype=np.float32)
    norm = np.sqrt(gx*gx + gy*gy + gz*gz)
    nx = -gx / norm
    ny = -gy / norm
    nz = gz / norm

    # Map [-1, 1] to [0, 255]
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

def generate_asphalt_pbr():
    size = 1024
    print("Generating Asphalt PBR textures...")
    np.random.seed(42)

    # Base noise
    fine_noise = np.random.normal(128, 14, (size, size)).clip(0, 255)
    macro_noise = np.random.normal(128, 28, (size // 8, size // 8)).clip(0, 255)
    macro_img = Image.fromarray(macro_noise.astype(np.uint8)).resize((size, size), Image.Resampling.BILINEAR)
    macro = np.array(macro_img, dtype=np.float32)

    height = (fine_noise * 0.4 + macro * 0.6).clip(0, 255)

    # Add crack networks
    cracks = np.zeros((size, size), dtype=np.float32)
    for _ in range(12):
        x0, y0 = np.random.randint(0, size, 2)
        angle = np.random.uniform(0, 2 * math.pi)
        length = np.random.randint(80, 280)
        cx, cy = x0, y0
        for _ in range(length):
            angle += np.random.normal(0, 0.25)
            cx = int((cx + math.cos(angle)) % size)
            cy = int((cy + math.sin(angle)) % size)
            cracks[max(0, cy-1):min(size, cy+2), max(0, cx-1):min(size, cx+2)] = 1.0

    height = np.where(cracks > 0, (height * 0.35).clip(0, 255), height)

    # Albedo: Dark grey asphalt with painted stripes
    base_color = np.array([52, 54, 56], dtype=np.float32) # dark asphalt
    albedo = np.zeros((size, size, 3), dtype=np.float32)
    noise_factor = (height - 128.0) / 255.0
    for c in range(3):
        albedo[:, :, c] = np.clip(base_color[c] + noise_factor * 26.0, 20, 110)

    # Center double yellow stripes (centered at u = 0.5 -> x ~ 512)
    stripe_w = 16
    gap = 14
    left_stripe = (size // 2) - gap // 2 - stripe_w
    right_stripe = (size // 2) + gap // 2
    yellow_rgb = np.array([215, 175, 45], dtype=np.float32)

    # White edge lines (at x = 60 and x = size - 60)
    white_rgb = np.array([210, 212, 215], dtype=np.float32)
    edge_w = 14

    for y in range(size):
        # Weathering / dashed effect or worn stripes
        wear = (height[y, :] / 255.0) * 0.5 + 0.5
        for x in range(left_stripe, left_stripe + stripe_w):
            if np.random.rand() > 0.08:
                albedo[y, x] = albedo[y, x] * (1 - wear[x]*0.8) + yellow_rgb * (wear[x]*0.8)
        for x in range(right_stripe, right_stripe + stripe_w):
            if np.random.rand() > 0.08:
                albedo[y, x] = albedo[y, x] * (1 - wear[x]*0.8) + yellow_rgb * (wear[x]*0.8)
        for x in range(60, 60 + edge_w):
            if np.random.rand() > 0.12:
                albedo[y, x] = albedo[y, x] * (1 - wear[x]*0.75) + white_rgb * (wear[x]*0.75)
        for x in range(size - 60 - edge_w, size - 60):
            if np.random.rand() > 0.12:
                albedo[y, x] = albedo[y, x] * (1 - wear[x]*0.75) + white_rgb * (wear[x]*0.75)

    # Oil leak stains in center of lanes
    oil_mask = np.zeros((size, size), dtype=np.float32)
    for lane_x in [size // 4, 3 * size // 4]:
        for _ in range(8):
            spot_y = np.random.randint(0, size)
            rx, ry = np.random.randint(12, 35), np.random.randint(25, 75)
            y_idx, x_idx = np.ogrid[:size, :size]
            dist = ((x_idx - lane_x)**2 / (rx**2) + (y_idx - spot_y)**2 / (ry**2))
            oil_mask += np.exp(-dist * 2.0)

    oil_mask = oil_mask.clip(0, 1)
    albedo = albedo * (1.0 - oil_mask[:, :, None] * 0.45)

    albedo_img = Image.fromarray(albedo.astype(np.uint8), mode='RGB')
    normal_img = generate_normal_map(height, strength=2.8)

    metallic = np.zeros((size, size), dtype=np.uint8) # Asphalt is non-metallic
    ao = np.clip(height * 0.6 + 100, 70, 255).astype(np.uint8)
    smoothness = np.clip((height / 255.0) * 45.0 + 15.0 + (oil_mask * 70.0), 10, 150).astype(np.uint8)
    mask_img = create_mask_map(metallic, ao, smoothness)

    albedo_img.save(f"{TEXTURES_ROOT}/Roads/T_Asphalt_Albedo.png")
    normal_img.save(f"{TEXTURES_ROOT}/Roads/T_Asphalt_Normal.png")
    mask_img.save(f"{TEXTURES_ROOT}/Roads/T_Asphalt_MaskMap.png")

def generate_concrete_pbr():
    size = 1024
    print("Generating Concrete PBR textures...")
    np.random.seed(101)

    fine = np.random.normal(128, 12, (size, size))
    macro = np.array(Image.fromarray(np.random.normal(128, 25, (64, 64)).clip(0, 255).astype(np.uint8)).resize((size, size), Image.Resampling.BILINEAR), dtype=np.float32)
    height = (fine * 0.35 + macro * 0.65).clip(0, 255)

    # Concrete panel formwork horizontal lines every 256 pixels
    for seam_y in range(0, size, 256):
        height[max(0, seam_y-2):min(size, seam_y+3), :] *= 0.4

    # Vertical water drip streaks
    water_drips = np.zeros((size, size), dtype=np.float32)
    for _ in range(25):
        dx = np.random.randint(0, size)
        d_len = np.random.randint(150, 600)
        d_width = np.random.randint(4, 18)
        for y in range(0, min(size, d_len)):
            decay = 1.0 - (y / d_len)
            water_drips[y, max(0, dx-d_width//2):min(size, dx+d_width//2)] += decay * 0.6

    water_drips = water_drips.clip(0, 1)

    base_concrete = np.array([135, 133, 128], dtype=np.float32) # brutalist weathered concrete
    albedo = np.zeros((size, size, 3), dtype=np.float32)
    for c in range(3):
        albedo[:, :, c] = np.clip(base_concrete[c] + (height - 128.0) * 0.22, 60, 210)

    # Darken water drips and add subtle greenish moss in crevices
    moss_color = np.array([68, 85, 52], dtype=np.float32)
    albedo = albedo * (1.0 - water_drips[:, :, None] * 0.32) + moss_color * (water_drips[:, :, None] * 0.12)
    albedo = albedo.clip(0, 255)

    albedo_img = Image.fromarray(albedo.astype(np.uint8), mode='RGB')
    normal_img = generate_normal_map(height, strength=1.8)

    metallic = np.zeros((size, size), dtype=np.uint8)
    ao = np.clip(height * 0.7 + 76 - (water_drips * 40), 60, 255).astype(np.uint8)
    smoothness = np.clip((height / 255.0) * 35.0 + 20.0, 15, 80).astype(np.uint8)
    mask_img = create_mask_map(metallic, ao, smoothness)

    albedo_img.save(f"{TEXTURES_ROOT}/Buildings/T_Concrete_Weathered_Albedo.png")
    normal_img.save(f"{TEXTURES_ROOT}/Buildings/T_Concrete_Weathered_Normal.png")
    mask_img.save(f"{TEXTURES_ROOT}/Buildings/T_Concrete_Weathered_MaskMap.png")

def generate_corrugated_metal_pbr():
    size = 1024
    print("Generating Corrugated Metal PBR textures...")
    np.random.seed(202)

    # Sinusoidal ridges along x axis (pitch of 32 pixels)
    x = np.arange(size)
    wave = np.sin(x * (2 * math.pi / 32.0)) * 0.5 + 0.5
    height = np.tile(wave, (size, 1)) * 180.0

    # Add rust noise patches
    rust_noise = np.array(Image.fromarray(np.random.normal(128, 45, (32, 32)).clip(0, 255).astype(np.uint8)).resize((size, size), Image.Resampling.BILINEAR), dtype=np.float32)
    rust_mask = np.where(rust_noise > 135, (rust_noise - 135) / 120.0, 0.0).clip(0, 1)

    # Rust gathers more in the valleys (wave < 0.3)
    valleys = np.where(wave < 0.35, 1.0 - (wave / 0.35), 0.0)
    rust_mask = (rust_mask * 0.7 + valleys * 0.45).clip(0, 1)

    height += np.random.normal(0, 6, (size, size))
    height = np.clip(height - rust_mask * 20.0, 0, 255)

    # Albedo: Galvanized steel (grey-blue) + Rust (burnt orange-brown)
    steel_rgb = np.array([125, 138, 145], dtype=np.float32)
    rust_rgb = np.array([142, 62, 28], dtype=np.float32)

    albedo = np.zeros((size, size, 3), dtype=np.float32)
    for c in range(3):
        albedo[:, :, c] = (steel_rgb[c] * (1.0 - rust_mask) + rust_rgb[c] * rust_mask) + np.random.normal(0, 5, (size, size))
    albedo = albedo.clip(0, 255)

    albedo_img = Image.fromarray(albedo.astype(np.uint8), mode='RGB')
    normal_img = generate_normal_map(height, strength=3.2)

    metallic = ((1.0 - rust_mask) * 215).astype(np.uint8) # Steel is metallic, rust is dielectric (0)
    ao = np.clip(height * 0.6 + 95 - rust_mask * 40, 50, 255).astype(np.uint8)
    smoothness = ((1.0 - rust_mask) * 140 + rust_mask * 30).astype(np.uint8)
    mask_img = create_mask_map(metallic, ao, smoothness)

    albedo_img.save(f"{TEXTURES_ROOT}/Buildings/T_CorrugatedMetal_Albedo.png")
    normal_img.save(f"{TEXTURES_ROOT}/Buildings/T_CorrugatedMetal_Normal.png")
    mask_img.save(f"{TEXTURES_ROOT}/Buildings/T_CorrugatedMetal_MaskMap.png")

def generate_props_and_vehicles_pbr():
    size = 1024
    print("Generating Props & Vehicle PBR textures...")
    np.random.seed(303)

    # 1. Jersey Barrier with Hazard Stripes
    barrier_albedo = np.ones((size, size, 3), dtype=np.float32) * 155.0
    # Yellow/Black hazard stripes at bottom half
    stripe_w = 64
    for y in range(size // 2, size):
        for x in range(size):
            stripe_phase = ((x + y) // stripe_w) % 2
            if stripe_phase == 0:
                barrier_albedo[y, x] = [210, 170, 30] # Yellow
            else:
                barrier_albedo[y, x] = [45, 45, 48] # Dark grey / black
    # Weathering / chips
    barrier_albedo += np.random.normal(0, 15, (size, size, 3))
    barrier_albedo = barrier_albedo.clip(0, 255)

    barrier_height = np.random.normal(128, 14, (size, size)).clip(0, 255)
    barrier_albedo_img = Image.fromarray(barrier_albedo.astype(np.uint8), mode='RGB')
    barrier_normal_img = generate_normal_map(barrier_height, strength=2.0)
    barrier_mask = create_mask_map(
        np.zeros((size, size), dtype=np.uint8),
        np.full((size, size), 230, dtype=np.uint8),
        np.full((size, size), 40, dtype=np.uint8)
    )

    barrier_albedo_img.save(f"{TEXTURES_ROOT}/Props/T_Barrier_Jersey_Albedo.png")
    barrier_normal_img.save(f"{TEXTURES_ROOT}/Props/T_Barrier_Jersey_Normal.png")
    barrier_mask.save(f"{TEXTURES_ROOT}/Props/T_Barrier_Jersey_MaskMap.png")

    # 2. Explosive Fuel Barrel
    barrel_albedo = np.zeros((size, size, 3), dtype=np.float32)
    # Industrial Red finish with yellow hazard band in middle
    for y in range(size):
        if size // 3 < y < 2 * size // 3:
            barrel_albedo[y, :] = [215, 165, 25] # Yellow warning band
        else:
            barrel_albedo[y, :] = [185, 35, 28] # Hazardous red body

    # Stencil hazard diamond icon in center
    cx, cy = size // 2, size // 2
    r_dia = 90
    for y in range(cy - r_dia, cy + r_dia):
        for x in range(cx - r_dia, cx + r_dia):
            if abs(x - cx) + abs(y - cy) < r_dia:
                barrel_albedo[y, x] = [30, 30, 35] # Black diamond symbol

    # Corrosion / rust on top and bottom rims
    for y in range(0, 60):
        barrel_albedo[y, :] = [120, 60, 30] # Rusted rim
    for y in range(size - 60, size):
        barrel_albedo[y, :] = [120, 60, 30]

    barrel_albedo += np.random.normal(0, 10, (size, size, 3))
    barrel_albedo = barrel_albedo.clip(0, 255)

    # 2 Rolling hoop rib bulges in height
    barrel_height = np.full((size, size), 128.0, dtype=np.float32)
    for rib_y in [size // 3, 2 * size // 3]:
        for dy in range(-25, 26):
            profile = math.cos((dy / 25.0) * (math.pi / 2.0)) * 60.0
            barrel_height[rib_y + dy, :] += profile

    barrel_albedo_img = Image.fromarray(barrel_albedo.astype(np.uint8), mode='RGB')
    barrel_normal_img = generate_normal_map(barrel_height, strength=2.5)
    barrel_mask = create_mask_map(
        np.full((size, size), 190, dtype=np.uint8),
        np.full((size, size), 240, dtype=np.uint8),
        np.full((size, size), 130, dtype=np.uint8)
    )

    barrel_albedo_img.save(f"{TEXTURES_ROOT}/Props/T_FuelBarrel_Albedo.png")
    barrel_normal_img.save(f"{TEXTURES_ROOT}/Props/T_FuelBarrel_Normal.png")
    barrel_mask.save(f"{TEXTURES_ROOT}/Props/T_FuelBarrel_MaskMap.png")

    # 3. Scorched Vehicle Wreck
    wreck_albedo = np.zeros((size, size, 3), dtype=np.float32)
    # Patches of faded blue paint, rust, and fire soot
    car_blue = np.array([45, 75, 110], dtype=np.float32)
    rust = np.array([135, 55, 25], dtype=np.float32)
    soot = np.array([28, 28, 30], dtype=np.float32)

    noise = np.array(Image.fromarray(np.random.normal(128, 50, (64, 64)).clip(0, 255).astype(np.uint8)).resize((size, size), Image.Resampling.BILINEAR), dtype=np.float32)
    for c in range(3):
        wreck_albedo[:, :, c] = np.where(noise < 90, soot[c], np.where(noise < 165, rust[c], car_blue[c]))
    wreck_albedo += np.random.normal(0, 8, (size, size, 3))
    wreck_albedo = wreck_albedo.clip(0, 255)

    wreck_height = np.random.normal(128, 16, (size, size)).clip(0, 255)
    wreck_albedo_img = Image.fromarray(wreck_albedo.astype(np.uint8), mode='RGB')
    wreck_normal_img = generate_normal_map(wreck_height, strength=2.2)
    wreck_mask = create_mask_map(
        np.where(noise > 165, 180, 20).astype(np.uint8),
        np.full((size, size), 220, dtype=np.uint8),
        np.where(noise > 165, 110, 35).astype(np.uint8)
    )

    wreck_albedo_img.save(f"{TEXTURES_ROOT}/Props/T_VehicleWreck_Albedo.png")
    wreck_normal_img.save(f"{TEXTURES_ROOT}/Props/T_VehicleWreck_Normal.png")
    wreck_mask.save(f"{TEXTURES_ROOT}/Props/T_VehicleWreck_MaskMap.png")

    # 4. Foliage / Urban Weeds (with alpha cutout)
    veg_size = 512
    veg_rgba = np.zeros((veg_size, veg_size, 4), dtype=np.uint8)
    veg_img = Image.fromarray(veg_rgba, mode='RGBA')
    draw = ImageDraw.Draw(veg_img)

    # Draw leafy weed clump
    np.random.seed(404)
    stem_base = (veg_size // 2, veg_size - 20)
    for _ in range(45):
        blade_len = np.random.randint(120, 320)
        bend = np.random.uniform(-140, 140)
        w = np.random.randint(6, 16)
        x_tip = stem_base[0] + bend
        y_tip = stem_base[1] - blade_len
        ctrl_x = stem_base[0] + bend * 0.45 + np.random.uniform(-20, 20)
        ctrl_y = stem_base[1] - blade_len * 0.6

        # Draw bezier-like blade polygon
        green = np.random.randint(45, 85)
        leaf_color = (green + 10, green + 45, green - 15, 255)
        points = [stem_base, (ctrl_x - w, ctrl_y), (x_tip, y_tip), (ctrl_x + w, ctrl_y)]
        draw.polygon(points, fill=leaf_color)

    veg_img.save(f"{TEXTURES_ROOT}/Vegetation/T_Vegetation_Weeds_Albedo.png")

# ==============================================================================
# 2. 3D OBJ MESH GENERATION HELPERS
# ==============================================================================

class ObjMeshBuilder:
    def __init__(self, name: str):
        self.name = name
        self.vertices = []
        self.uvs = []
        self.normals = []
        self.faces = [] # list of [(v_idx, uv_idx, n_idx), ...]

    def add_vertex(self, x: float, y: float, z: float):
        self.vertices.append((x, y, z))
        return len(self.vertices)

    def add_uv(self, u: float, v: float):
        self.uvs.append((u, v))
        return len(self.uvs)

    def add_normal(self, nx: float, ny: float, nz: float):
        # Normalize
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

        # 2 Triangles: (1, 2, 3) and (1, 3, 4)
        self.faces.append([(v1, u1_idx, n_idx), (v2, u2_idx, n_idx), (v3, u3_idx, n_idx)])
        self.faces.append([(v1, u1_idx, n_idx), (v3, u3_idx, n_idx), (v4, u4_idx, n_idx)])

    def add_box(self, cx: float, cy: float, cz: float, sx: float, sy: float, sz: float, u_scale: float = 1.0, v_scale: float = 1.0):
        hx, hy, hz = sx * 0.5, sy * 0.5, sz * 0.5
        # 8 corners
        v000 = self.add_vertex(cx - hx, cy - hy, cz - hz)
        v100 = self.add_vertex(cx + hx, cy - hy, cz - hz)
        v110 = self.add_vertex(cx + hx, cy + hy, cz - hz)
        v010 = self.add_vertex(cx - hx, cy + hy, cz - hz)
        v001 = self.add_vertex(cx - hx, cy - hy, cz + hz)
        v101 = self.add_vertex(cx + hx, cy - hy, cz + hz)
        v111 = self.add_vertex(cx + hx, cy + hy, cz + hz)
        v011 = self.add_vertex(cx - hx, cy + hy, cz + hz)

        # Front (+Z)
        self.add_quad(v001, v101, v111, v011, (0, 0), (u_scale, 0), (u_scale, v_scale), (0, v_scale), (0, 0, 1))
        # Back (-Z)
        self.add_quad(v100, v000, v010, v110, (0, 0), (u_scale, 0), (u_scale, v_scale), (0, v_scale), (0, 0, -1))
        # Top (+Y)
        self.add_quad(v011, v111, v110, v010, (0, 0), (u_scale, 0), (u_scale, v_scale), (0, v_scale), (0, 1, 0))
        # Bottom (-Y)
        self.add_quad(v000, v100, v101, v001, (0, 0), (u_scale, 0), (u_scale, v_scale), (0, v_scale), (0, -1, 0))
        # Right (+X)
        self.add_quad(v101, v100, v110, v111, (0, 0), (u_scale, 0), (u_scale, v_scale), (0, v_scale), (1, 0, 0))
        # Left (-X)
        self.add_quad(v000, v001, v011, v010, (0, 0), (u_scale, 0), (u_scale, v_scale), (0, v_scale), (-1, 0, 0))

    def write_obj(self, filepath: str):
        with open(filepath, "w", encoding="utf-8") as f:
            f.write(f"# OUTRUN: AFTERMATH 3D Asset - {self.name}\n")
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
# 3. 3D MODELS GENERATION
# ==============================================================================

def generate_road_models():
    print("Generating Road 3D models...")
    # 1. Road_2Lane_Straight_12m (width 12m, length 20m)
    # Roadbed (width 9m, asphalt), curbs (0.25m wide, 0.2m high), sidewalk (1.25m wide on each side)
    m = ObjMeshBuilder("Road_2Lane_Straight_12m")
    length = 20.0
    half_l = length * 0.5

    # Roadbed asphalt quad: X from -4.5 to +4.5, Y = 0.05 (slight crown)
    # Cross section profile:
    # X coords:
    # -6.0 (sidewalk outer edge left)
    # -4.75 (curb top left)
    # -4.5 (curb bottom / gutter left)
    # 0.0 (road center crown)
    # 4.5 (gutter right)
    # 4.75 (curb top right)
    # 6.0 (sidewalk outer edge right)
    xs = [-6.0, -4.75, -4.5, 0.0, 4.5, 4.75, 6.0]
    ys = [0.25, 0.25, 0.0, 0.06, 0.0, 0.25, 0.25]
    uv_xs = [0.0, 0.10, 0.12, 0.50, 0.88, 0.90, 1.0]

    # Create strip of quads along Z (-half_l to +half_l)
    for i in range(len(xs) - 1):
        x_a, x_b = xs[i], xs[i+1]
        y_a, y_b = ys[i], ys[i+1]
        u_a, u_b = uv_xs[i], uv_xs[i+1]

        v1 = m.add_vertex(x_a, y_a, -half_l)
        v2 = m.add_vertex(x_b, y_b, -half_l)
        v3 = m.add_vertex(x_b, y_b, half_l)
        v4 = m.add_vertex(x_a, y_a, half_l)

        # Compute face normal
        dx, dy = x_b - x_a, y_b - y_a
        normal = (-dy, dx, 0.0)
        m.add_quad(v1, v2, v3, v4, (u_a, 0), (u_b, 0), (u_b, 4.0), (u_a, 4.0), normal)

    m.write_obj(f"{MESHES_ROOT}/Roads/Road_2Lane_Straight_12m.obj")

    # 2. Road_Intersection_Square (16m x 16m)
    m_int = ObjMeshBuilder("Road_Intersection_Square")
    hl = 8.0
    # Central asphalt surface
    v1 = m_int.add_vertex(-hl, 0.02, -hl)
    v2 = m_int.add_vertex(hl, 0.02, -hl)
    v3 = m_int.add_vertex(hl, 0.02, hl)
    v4 = m_int.add_vertex(-hl, 0.02, hl)
    m_int.add_quad(v1, v2, v3, v4, (0, 0), (2, 0), (2, 2), (0, 2), (0, 1, 0))

    # Center manhole cover disc
    m_int.add_box(0, 0.04, 0, 1.2, 0.04, 1.2)
    m_int.write_obj(f"{MESHES_ROOT}/Roads/Road_Intersection_Square.obj")

    # 3. Road_Crater_Section_12m (Road with deep bomb crater)
    m_crat = ObjMeshBuilder("Road_Crater_Section_12m")
    # Outer flat road bed
    m_crat.add_box(0, -0.05, 0, 12.0, 0.1, 20.0, u_scale=1.0, v_scale=3.0)
    # Raised broken asphalt rim around crater
    crater_radius = 2.8
    num_pts = 16
    rim_h = 0.35
    depth = -0.85

    # Crater floor
    v_bottom = m_crat.add_vertex(0, depth, 0)
    u_bottom = m_crat.add_uv(0.5, 0.5)
    n_up = m_crat.add_normal(0, 1, 0)

    rim_verts = []
    rim_uvs = []
    for step in range(num_pts):
        angle = step * (2 * math.pi / num_pts)
        rx = crater_radius * (1.0 + 0.18 * math.sin(angle * 3))
        rz = crater_radius * (1.0 + 0.18 * math.cos(angle * 4))
        px = math.cos(angle) * rx
        pz = math.sin(angle) * rz
        v_idx = m_crat.add_vertex(px, rim_h * (0.8 + 0.3 * math.sin(angle*5)), pz)
        u_idx = m_crat.add_uv(0.5 + 0.45 * math.cos(angle), 0.5 + 0.45 * math.sin(angle))
        rim_verts.append(v_idx)
        rim_uvs.append(u_idx)

    # Fan triangles connecting crater floor to rim
    for step in range(num_pts):
        next_step = (step + 1) % num_pts
        v_a = rim_verts[step]
        v_b = rim_verts[next_step]
        uv_a = rim_uvs[step]
        uv_b = rim_uvs[next_step]
        # Inward sloping normal
        m_crat.faces.append([(v_bottom, u_bottom, n_up), (v_b, uv_b, n_up), (v_a, uv_a, n_up)])

    m_crat.write_obj(f"{MESHES_ROOT}/Roads/Road_Crater_Section_12m.obj")

def generate_building_models():
    print("Generating Building 3D models...")
    # 1. Building_Bank_GF_Modular (18m wide x 24m deep x 6m high)
    # Features front colonnade pillars, recessed entrance, exterior concrete lintel, window cutouts
    m_gf = ObjMeshBuilder("Building_Bank_GF_Modular")
    # Base foundation slab
    m_gf.add_box(0, 0.25, 0, 18.0, 0.5, 24.0, 3.0, 4.0)
    # Ceiling slab
    m_gf.add_box(0, 5.8, 0, 18.2, 0.6, 24.2, 3.0, 4.0)

    # Solid back wall (North, +Z = 12m)
    m_gf.add_box(0, 3.0, 11.8, 18.0, 5.0, 0.6, 3.0, 1.0)
    # West wall (-X = -8.7m) with window apertures
    m_gf.add_box(-8.7, 3.0, 1.0, 0.6, 5.0, 22.0, 4.0, 1.0)
    # East wall (+X = +8.7m)
    m_gf.add_box(8.7, 3.0, 1.0, 0.6, 5.0, 22.0, 4.0, 1.0)

    # Front Entrance (South, -Z = -12m): 4 Brutalist Columns
    for col_x in [-7.0, -2.5, 2.5, 7.0]:
        m_gf.add_box(col_x, 3.0, -11.5, 1.4, 5.0, 1.4, 0.5, 1.0)

    # Recessed glass vestibule wall at Z = -8.0m
    m_gf.add_box(-5.0, 3.0, -8.0, 5.5, 5.0, 0.3, 1.0, 1.0)
    m_gf.add_box(5.0, 3.0, -8.0, 5.5, 5.0, 0.3, 1.0, 1.0)
    m_gf.add_box(0, 4.8, -8.0, 4.5, 1.4, 0.3, 1.0, 0.4) # Header over double door aperture

    # Interior central bank teller counter / cover wall
    m_gf.add_box(0, 1.1, 0, 10.0, 1.2, 0.8, 2.0, 0.5)

    m_gf.write_obj(f"{MESHES_ROOT}/Buildings/Building_Bank_GF_Modular.obj")

    # 2. Building_Bank_2F_Modular (Upper floor with balcony)
    m_2f = ObjMeshBuilder("Building_Bank_2F_Modular")
    # Floor slab
    m_2f.add_box(0, 0.25, 0, 18.0, 0.5, 24.0, 3.0, 4.0)
    # Ceiling slab
    m_2f.add_box(0, 4.8, 0, 18.2, 0.6, 24.2, 3.0, 4.0)
    # Perimeter walls with regular window piers
    m_2f.add_box(0, 2.5, 11.8, 18.0, 4.0, 0.6, 3.0, 1.0) # North wall
    m_2f.add_box(-8.7, 2.5, 0, 0.6, 4.0, 24.0, 4.0, 1.0) # West wall
    m_2f.add_box(8.7, 2.5, 0, 0.6, 4.0, 24.0, 4.0, 1.0) # East wall

    # Front Balcony (+Z = -12m): Low 1.1m concrete parapet with open sniper firing slots
    m_2f.add_box(-5.5, 0.8, -11.8, 6.0, 1.1, 0.5, 1.0, 0.3)
    m_2f.add_box(5.5, 0.8, -11.8, 6.0, 1.1, 0.5, 1.0, 0.3)
    m_2f.add_box(0, 0.8, -11.8, 2.0, 1.1, 0.5, 0.5, 0.3)

    # Interior dividing office partitions
    m_2f.add_box(-2.5, 2.5, 3.0, 0.3, 4.0, 10.0, 2.0, 1.0)
    m_2f.add_box(3.5, 2.5, -2.0, 8.0, 4.0, 0.3, 1.5, 1.0)

    m_2f.write_obj(f"{MESHES_ROOT}/Buildings/Building_Bank_2F_Modular.obj")

    # 3. Building_Rooftop_Perch (Sniper rooftop with HVAC and parapet)
    m_roof = ObjMeshBuilder("Building_Rooftop_Perch")
    # Gravel roof bed
    m_roof.add_box(0, 0.1, 0, 18.0, 0.2, 24.0, 4.0, 5.0)
    # Continuous 1.2m parapet wall around roof perimeter
    m_roof.add_box(0, 0.7, -11.8, 18.0, 1.2, 0.4, 3.0, 0.3)
    m_roof.add_box(0, 0.7, 11.8, 18.0, 1.2, 0.4, 3.0, 0.3)
    m_roof.add_box(-8.8, 0.7, 0, 0.4, 1.2, 24.0, 4.0, 0.3)
    m_roof.add_box(8.8, 0.7, 0, 0.4, 1.2, 24.0, 4.0, 0.3)

    # Rooftop elevator / stair penthouse bulkhead
    m_roof.add_box(-4.0, 1.6, 5.0, 4.5, 3.0, 5.0, 1.0, 1.0)

    # Dual Industrial HVAC units with condenser fans
    m_roof.add_box(3.5, 1.0, -3.0, 2.8, 1.8, 3.5, 1.0, 1.0)
    m_roof.add_box(3.5, 1.0, 4.0, 2.8, 1.8, 3.5, 1.0, 1.0)

    m_roof.write_obj(f"{MESHES_ROOT}/Buildings/Building_Rooftop_Perch.obj")

    # 4. Building_Warehouse_Modular (Corrugated industrial depot)
    m_wh = ObjMeshBuilder("Building_Warehouse_Modular")
    # Main warehouse shell (20m wide x 32m long x 8m high)
    # Foundation
    m_wh.add_box(0, 0.2, 0, 20.0, 0.4, 32.0, 3.0, 5.0)
    # Side walls (East & West)
    m_wh.add_box(-9.8, 4.2, 0, 0.4, 7.6, 32.0, 5.0, 2.0)
    m_wh.add_box(9.8, 4.2, 0, 0.4, 7.6, 32.0, 5.0, 2.0)
    # Back wall (North)
    m_wh.add_box(0, 4.2, 15.8, 19.6, 7.6, 0.4, 3.0, 2.0)

    # Front wall with oversized freight roll-up bay (width 8m, height 5.5m)
    m_wh.add_box(-7.0, 4.2, -15.8, 5.6, 7.6, 0.4, 1.0, 2.0)
    m_wh.add_box(7.0, 4.2, -15.8, 5.6, 7.6, 0.4, 1.0, 2.0)
    m_wh.add_box(0, 6.7, -15.8, 8.4, 2.6, 0.4, 1.5, 0.5)

    # Steel frame pitched roof
    m_wh.add_box(0, 8.5, 0, 20.6, 0.5, 32.6, 3.0, 5.0)

    m_wh.write_obj(f"{MESHES_ROOT}/Buildings/Building_Warehouse_Modular.obj")

def generate_prop_models():
    print("Generating Prop & Vehicle 3D models...")
    # 1. Prop_Barrier_Jersey (Concrete highway barrier)
    m_bar = ObjMeshBuilder("Prop_Barrier_Jersey")
    # Base width 0.6m, height 0.85m, top width 0.25m, length 3.0m
    l = 3.0
    hl = l * 0.5
    # Cross section at z:
    # y=0: x from -0.3 to +0.3
    # y=0.25: x from -0.28 to +0.28 (sloped lower flange)
    # y=0.85: x from -0.125 to +0.125 (tapered top)
    # Base box
    m_bar.add_box(0, 0.125, 0, 0.6, 0.25, l, 0.5, 0.3)
    # Upper tapered riser
    m_bar.add_box(0, 0.55, 0, 0.30, 0.60, l, 0.5, 0.6)
    # Rounded top cap
    m_bar.add_box(0, 0.87, 0, 0.24, 0.05, l, 0.4, 0.1)

    m_bar.write_obj(f"{MESHES_ROOT}/Props/Prop_Barrier_Jersey.obj")

    # 2. Prop_Barrel_FuelRusted (Ribbed 55-gal drum)
    m_barr = ObjMeshBuilder("Prop_Barrel_FuelRusted")
    # 16-sided cylinder
    num_sides = 16
    radius = 0.30
    h = 0.90
    top_y = h
    bot_y = 0.0

    # Center vertices
    v_top_c = m_barr.add_vertex(0, top_y, 0)
    v_bot_c = m_barr.add_vertex(0, bot_y, 0)
    uv_c = m_barr.add_uv(0.5, 0.5)
    n_up = m_barr.add_normal(0, 1, 0)
    n_dn = m_barr.add_normal(0, -1, 0)

    top_ring = []
    bot_ring = []
    side_normals = []
    side_uvs_top = []
    side_uvs_bot = []

    for i in range(num_sides):
        theta = i * (2 * math.pi / num_sides)
        cos_t = math.cos(theta)
        sin_t = math.sin(theta)
        x = cos_t * radius
        z = sin_t * radius

        top_ring.append(m_barr.add_vertex(x, top_y, z))
        bot_ring.append(m_barr.add_vertex(x, bot_y, z))
        side_normals.append(m_barr.add_normal(cos_t, 0, sin_t))

        u = i / float(num_sides)
        side_uvs_top.append(m_barr.add_uv(u, 1.0))
        side_uvs_bot.append(m_barr.add_uv(u, 0.0))

    # Add end caps and cylinder sides
    for i in range(num_sides):
        next_i = (i + 1) % num_sides
        # Top cap triangle
        m_barr.faces.append([(v_top_c, uv_c, n_up), (top_ring[i], uv_c, n_up), (top_ring[next_i], uv_c, n_up)])
        # Bottom cap triangle
        m_barr.faces.append([(v_bot_c, uv_c, n_dn), (bot_ring[next_i], uv_c, n_dn), (bot_ring[i], uv_c, n_dn)])
        # Side quad
        v1 = bot_ring[i]
        v2 = bot_ring[next_i]
        v3 = top_ring[next_i]
        v4 = top_ring[i]
        u1 = side_uvs_bot[i]
        u2 = side_uvs_bot[next_i]
        u3 = side_uvs_top[next_i]
        u4 = side_uvs_top[i]
        n1 = side_normals[i]
        n2 = side_normals[next_i]

        m_barr.faces.append([(v1, u1, n1), (v2, u2, n2), (v3, u3, n2)])
        m_barr.faces.append([(v1, u1, n1), (v3, u3, n2), (v4, u4, n1)])

    # Add 2 rolling chime hoops
    m_barr.add_box(0, 0.32, 0, 0.63, 0.04, 0.63)
    m_barr.add_box(0, 0.62, 0, 0.63, 0.04, 0.63)

    m_barr.write_obj(f"{MESHES_ROOT}/Props/Prop_Barrel_FuelRusted.obj")

    # 3. Prop_Vehicle_Sedan_Wreck (4-door civilian sedan wreck)
    m_car = ObjMeshBuilder("Prop_Vehicle_Sedan_Wreck")
    # Dimensions: 4.6m length (Z), 1.9m width (X), 1.4m height (Y)
    # Main lower chassis & floorboard
    m_car.add_box(0, 0.35, 0, 1.85, 0.30, 4.4, 1.0, 2.0)
    # Hood (crumpled / skewed angle)
    m_car.add_box(0, 0.70, 1.4, 1.75, 0.40, 1.5, 1.0, 1.0)
    # Cabin body (middle)
    m_car.add_box(0, 0.95, -0.15, 1.70, 0.85, 1.8, 1.0, 1.0)
    # Trunk
    m_car.add_box(0, 0.65, -1.6, 1.75, 0.35, 1.1, 1.0, 0.8)

    # 4 Wheels (deflated / tilted)
    wheel_w, wheel_d = 0.28, 0.62
    for wx, wz in [(-0.95, 1.3), (0.95, 1.3), (-0.95, -1.3), (0.95, -1.3)]:
        m_car.add_box(wx, 0.28, wz, wheel_w, wheel_d, wheel_d)

    # Engine block in exposed bay
    m_car.add_box(0, 0.65, 1.5, 0.8, 0.45, 0.9)
    # Front crumpled bumper
    m_car.add_box(0, 0.30, 2.25, 1.9, 0.25, 0.2)

    m_car.write_obj(f"{MESHES_ROOT}/Props/Prop_Vehicle_Sedan_Wreck.obj")

    # 4. Prop_ShippingContainer_20ft (6.06m x 2.44m x 2.59m)
    m_con = ObjMeshBuilder("Prop_ShippingContainer_20ft")
    m_con.add_box(0, 1.3, 0, 2.44, 2.59, 6.06, 1.0, 3.0)
    # 4 heavy corner posts
    for cx in [-1.18, 1.18]:
        for cz in [-2.98, 2.98]:
            m_con.add_box(cx, 1.3, cz, 0.2, 2.65, 0.2)
    # Door locking vertical cam rods on rear door (+Z)
    m_con.add_box(-0.35, 1.3, 3.05, 0.06, 2.3, 0.06)
    m_con.add_box(0.35, 1.3, 3.05, 0.06, 2.3, 0.06)

    m_con.write_obj(f"{MESHES_ROOT}/Props/Prop_ShippingContainer_20ft.obj")

    # 5. Prop_StreetLight_Broken (7.5m broken luminaire pole)
    m_pole = ObjMeshBuilder("Prop_StreetLight_Broken")
    # Concrete base pedestal
    m_pole.add_box(0, 0.25, 0, 0.6, 0.5, 0.6)
    # Vertical steel shaft
    m_pole.add_box(0, 3.25, 0, 0.22, 5.5, 0.22)
    # Bent angled upper arm (tilted towards +X)
    m_pole.add_box(0.8, 6.3, 0, 1.6, 0.16, 0.16)
    # Cobra-head light fixture housing
    m_pole.add_box(1.7, 6.2, 0, 0.6, 0.20, 0.35)

    m_pole.write_obj(f"{MESHES_ROOT}/Props/Prop_StreetLight_Broken.obj")

    # 6. Debris_ConcreteRubble_Pile (Angular rubble pile with rebar)
    m_rub = ObjMeshBuilder("Debris_ConcreteRubble_Pile")
    # Base spread of cracked masonry blocks
    m_rub.add_box(0, 0.25, 0, 3.2, 0.5, 2.8, 1.0, 1.0)
    m_rub.add_box(-0.5, 0.65, 0.2, 1.6, 0.6, 1.4, 0.6, 0.6)
    m_rub.add_box(0.6, 0.55, -0.4, 1.4, 0.5, 1.3, 0.5, 0.5)
    m_rub.add_box(0.1, 0.95, 0.0, 1.1, 0.45, 1.0, 0.4, 0.4)

    # 3 Twisted metal rebar rods sticking out at compound angles
    m_rub.add_box(0.8, 0.8, 0.5, 0.05, 1.4, 0.05)
    m_rub.add_box(-0.7, 0.7, -0.6, 0.05, 1.2, 0.05)
    m_rub.add_box(0.2, 1.2, -0.3, 0.05, 1.1, 0.05)

    m_rub.write_obj(f"{MESHES_ROOT}/Debris/Debris_ConcreteRubble_Pile.obj")

def generate_weapon_models():
    print("Generating Tactical Weapon 3D models...")
    # 1. Weapon_Vanguard_AssaultRifle (5.56mm Tactical Assault Rifle)
    m_ar = ObjMeshBuilder("Weapon_Vanguard_AssaultRifle")
    # Receiver / Upper & Lower
    m_ar.add_box(0, 0, 0.05, 0.045, 0.07, 0.28, 1.0, 1.0)
    # Barrel
    m_ar.add_box(0, 0.015, 0.32, 0.02, 0.02, 0.28, 0.5, 1.0)
    # Muzzle Brake / Flash Hider
    m_ar.add_box(0, 0.015, 0.47, 0.028, 0.028, 0.04, 0.3, 0.3)
    # Handguard / Picatinny Quad Rail
    m_ar.add_box(0, 0.01, 0.22, 0.042, 0.055, 0.16, 0.5, 0.5)
    # Curved 30-round STANAG Magazine
    m_ar.add_box(0, -0.09, 0.08, 0.032, 0.14, 0.065, 0.5, 1.0)
    # Tactical Ergonomic Pistol Grip
    m_ar.add_box(0, -0.075, -0.05, 0.034, 0.11, 0.05, 0.5, 0.5)
    # Telescopic Adjustable Carbine Stock
    m_ar.add_box(0, 0.005, -0.21, 0.036, 0.095, 0.24, 0.5, 1.0)
    # Holographic / Red Dot Combat Optic
    m_ar.add_box(0, 0.065, 0.02, 0.035, 0.045, 0.08, 0.5, 0.5)
    m_ar.write_obj(f"{MESHES_ROOT}/Props/Weapon_Vanguard_AssaultRifle.obj")

    # 2. Weapon_Breacher_Shotgun (12-Gauge Tactical Pump-Action Shotgun)
    m_sg = ObjMeshBuilder("Weapon_Breacher_Shotgun")
    # Main Steel Receiver
    m_sg.add_box(0, 0.002, 0.03, 0.044, 0.075, 0.26, 1.0, 1.0)
    # Heavy 12ga Smoothbore Barrel
    m_sg.add_box(0, 0.021, 0.35, 0.032, 0.032, 0.38, 0.5, 1.0)
    # Extended 8-round Magazine Tube under barrel
    m_sg.add_box(0, -0.016, 0.33, 0.028, 0.028, 0.34, 0.5, 1.0)
    # Ribbed Pump Forend / Slide
    m_sg.add_box(0, -0.016, 0.30, 0.048, 0.048, 0.16, 0.5, 0.5)
    # Tactical Pistol Grip
    m_sg.add_box(0, -0.075, -0.08, 0.036, 0.11, 0.06, 0.5, 0.5)
    # Fixed Polymer Tactical Stock with Recoil Pad
    m_sg.add_box(0, -0.015, -0.22, 0.036, 0.09, 0.24, 0.5, 1.0)
    # Ghost Ring Rear Sight & Front Bead
    m_sg.add_box(0, 0.049, -0.06, 0.024, 0.018, 0.03, 0.3, 0.3)
    m_sg.add_box(0, 0.044, 0.52, 0.012, 0.015, 0.02, 0.3, 0.3)
    # Ejection Port indicator
    m_sg.add_box(0.022, 0.012, 0.02, 0.005, 0.025, 0.08, 0.2, 0.2)
    m_sg.write_obj(f"{MESHES_ROOT}/Props/Weapon_Breacher_Shotgun.obj")

    # 3. Weapon_Sidekick_Pistol (9mm Tactical Combat Pistol)
    m_p = ObjMeshBuilder("Weapon_Sidekick_Pistol")
    # Steel Slide with Serrations
    m_p.add_box(0, 0.023, 0.055, 0.032, 0.036, 0.21, 1.0, 1.0)
    # Barrel Tip & Recoil Spring Bushing
    m_p.add_box(0, 0.021, 0.17, 0.022, 0.022, 0.03, 0.3, 0.3)
    # Polymer Frame & Accessory Rail
    m_p.add_box(0, -0.005, 0.045, 0.030, 0.022, 0.19, 0.8, 0.5)
    # Trigger Guard
    m_p.add_box(0, -0.032, 0.05, 0.020, 0.032, 0.06, 0.4, 0.4)
    # Trigger
    m_p.add_box(0, -0.027, 0.046, 0.008, 0.018, 0.012, 0.2, 0.2)
    # Ergonomic Textured Grip
    m_p.add_box(0, -0.075, -0.035, 0.032, 0.12, 0.052, 0.5, 0.8)
    # Extended Magazine Baseplate
    m_p.add_box(0, -0.141, -0.035, 0.036, 0.014, 0.062, 0.3, 0.3)
    # Tactical 3-Dot Combat Sights
    m_p.add_box(0, 0.047, -0.04, 0.022, 0.012, 0.012, 0.2, 0.2)
    m_p.add_box(0, 0.047, 0.15, 0.008, 0.012, 0.012, 0.2, 0.2)
    m_p.write_obj(f"{MESHES_ROOT}/Props/Weapon_Sidekick_Pistol.obj")

def main():
    print("=== Starting OUTRUN: AFTERMATH Phase B Asset Generation ===")
    ensure_dirs()
    generate_asphalt_pbr()
    generate_concrete_pbr()
    generate_corrugated_metal_pbr()
    generate_props_and_vehicles_pbr()
    generate_road_models()
    generate_building_models()
    generate_prop_models()
    generate_weapon_models()
    print("=== Phase B Asset Generation Completed Successfully! ===")


if __name__ == "__main__":
    main()
