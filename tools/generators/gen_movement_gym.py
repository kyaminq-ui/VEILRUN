"""Generates Dev/TestMaps/MovementGym.tscn — the parkour metrics greybox.

Regenerate after changing metrics:  python tools/generators/gen_movement_gym.py
Layout is documented in docs/PARKOUR_METRICS.md (section "Movement Gym").
Coordinates: meters, Y up, spawn faces -Z. All collision comes from CSG boxes (use_collision).
NOTE: this file is the source of truth for the gym; hand edits to the .tscn are overwritten.
"""
import math
import os

nodes = []
labels = []


def fmt(v):
    return f"{v:.6g}"


def xform(pos, rot_x_deg=0.0):
    c, s = math.cos(math.radians(rot_x_deg)), math.sin(math.radians(rot_x_deg))
    # Godot serializes the Transform3D basis row-major: rows [1,0,0],[0,c,-s],[0,s,c]
    vals = [1, 0, 0, 0, c, -s, 0, s, c, *pos]
    return "Transform3D(" + ", ".join(fmt(v) for v in vals) + ")"


def box(name, pos, size, mat, rot_x=0.0):
    nodes.append((name, pos, size, mat, rot_x))


def label(text, pos, color="Color(1, 1, 1, 1)", size=72):
    labels.append((text, pos, color, size))


def plank(name, x, width, z0, y0, z1, y1, mat, thickness=0.4):
    """Sloped plank whose TOP surface runs from (z0, y0) to (z1, y1). Rises toward -Z when z1 < z0."""
    dz, dy = z0 - z1, y1 - y0
    length = math.hypot(dz, dy)
    ang = math.degrees(math.atan2(dy, dz))
    ny, nz = math.cos(math.radians(ang)), math.sin(math.radians(ang))
    mid_y, mid_z = (y0 + y1) / 2, (z0 + z1) / 2
    center = (x, mid_y - ny * thickness / 2, mid_z - nz * thickness / 2)
    box(name, center, (width, thickness, length), mat, ang)


# ---------------- Floor ----------------
box("Floor", (0, -0.5, 0), (200, 1, 200), "floor")
label("SPAWN", (0, 2.6, 8), "Color(0.4, 1, 0.9, 1)")

# ---------------- A. Gap lanes (deck top y=2, gaps measured edge to edge) ----------------
DECK_H = 2.0
box("GapDeck", (-20, DECK_H / 2, -6), (24, DECK_H, 12), "obstacle")
plank("GapDeckRamp", -20, 24, 10.0, 0.0, 0.0, DECK_H, "ramp")
label("GAP JUMPS  (deck +2 m)", (-20, 4.2, -1), "Color(1, 0.8, 0.4, 1)")
for i, gap in enumerate([2, 3, 4, 5, 6]):
    x = -30 + i * 5
    z_center = -12 - gap - 4
    box(f"GapLanding_{gap}m", (x, DECK_H / 2, z_center), (3, DECK_H, 8), "obstacle")
    label(f"{gap} m", (x, DECK_H + 1.2, -12 - gap - 0.5))

# ---------------- B. Height blocks (vault / mantle / ledge references) ----------------
label("HEIGHTS", (13, 4.5, -6), "Color(1, 0.8, 0.4, 1)")
for i, h in enumerate([0.5, 0.9, 1.2, 1.5, 2.0, 2.5, 3.0]):
    x = 4 + i * 3
    box(f"Height_{h}m", (x, h / 2, -6), (2, h, 2), "obstacle")
    label(f"{h} m", (x, h + 0.7, -6))

# ---------------- C. Drop ramp (fall height references) ----------------
RAMP_X, RAMP_Z0, RAMP_Z1, RAMP_TOP = 36.0, 10.0, -30.0, 12.0
plank("DropRamp", RAMP_X, 4, RAMP_Z0, 0.0, RAMP_Z1, RAMP_TOP, "ramp")
box("DropDeck", (RAMP_X, RAMP_TOP - 0.5, RAMP_Z1 - 3), (6, 1, 6), "obstacle")
label("DROP HEIGHTS", (RAMP_X, 3, 13), "Color(1, 0.8, 0.4, 1)")
slope = RAMP_TOP / (RAMP_Z0 - RAMP_Z1)
for h in [3.2, 5.12, 6.45, 8, 12]:  # landing tier thresholds (Medium, Heavy, Deadly) + references
    z = RAMP_Z0 - h / slope
    label(f"{h:g} m", (RAMP_X + 2.6, h + 0.6, z), "Color(1, 0.55, 0.45, 1)")

# ---------------- D. Slopes (walkable limit = MaxFloorAngleDegrees, default 46 deg) ----------------
label("SLOPES", (59, 6.5, -2), "Color(1, 0.8, 0.4, 1)")
for i, ang in enumerate([20, 35, 45, 50]):
    x = 50 + i * 6
    L = 12
    plank(f"Slope_{ang}deg", x, 4, 0.0, 0.0, -L * math.cos(math.radians(ang)), L * math.sin(math.radians(ang)), "ramp")
    label(f"{ang}°", (x, 1.5, 1.5))

# ---------------- E. Walls, corridor, door ----------------
box("WallRunWall", (-45, 2, -10), (1, 4, 24), "wall")
label("WALL (wall-run, M2)", (-43.5, 4.8, -10), "Color(1, 0.8, 0.4, 1)")
box("CorridorWallA", (-52.0, 1.5, -10), (0.5, 3, 10), "wall")
box("CorridorWallB", (-53.7, 1.5, -10), (0.5, 3, 10), "wall")
label("CORRIDOR 1.2 m", (-52.85, 3.6, -4.5))
label("DOOR 1.0 x 2.1 m", (-45, 3.6, 12))

# ---------------- Emit ----------------
FLOOR_TEX = "res://Dev/Textures/Grid/PNG/Dark_Floor/texture_03.png"
ORANGE_TEX = "res://Dev/Textures/Grid/PNG/Orange_Wall/texture_04.png"
mats = {
    "floor": (FLOOR_TEX, "Color(1, 1, 1, 1)"),
    "obstacle": (ORANGE_TEX, "Color(1, 1, 1, 1)"),
    "wall": (FLOOR_TEX, "Color(0.62, 0.66, 0.78, 1)"),
    "ramp": (FLOOR_TEX, "Color(0.45, 0.75, 1, 1)"),
}
UV_SCALE = 0.5  # world-triplanar: one 1024px tile per 2 m → major lines every 1 m (512px), minor every 0.25 m (128px)

ext = ['[ext_resource type="PackedScene" path="res://Scenes/Characters/Runner.tscn" id="1_runner"]',
       f'[ext_resource type="Texture2D" path="{FLOOR_TEX}" id="2_floor"]',
       f'[ext_resource type="Texture2D" path="{ORANGE_TEX}" id="3_orange"]']
tex_id = {FLOOR_TEX: "2_floor", ORANGE_TEX: "3_orange"}

subs = []
for key, (tex, color) in mats.items():
    subs.append(
        f'[sub_resource type="StandardMaterial3D" id="Mat_{key}"]\n'
        f'albedo_color = {color}\n'
        f'albedo_texture = ExtResource("{tex_id[tex]}")\n'
        f'roughness = 0.9\n'
        f'uv1_scale = Vector3({UV_SCALE}, {UV_SCALE}, {UV_SCALE})\n'
        f'uv1_triplanar = true\n'
        f'uv1_world_triplanar = true\n')
subs.append('[sub_resource type="ProceduralSkyMaterial" id="Sky_mat"]\n'
            'sky_top_color = Color(0.32, 0.42, 0.6, 1)\n'
            'sky_horizon_color = Color(0.72, 0.74, 0.8, 1)\n'
            'ground_bottom_color = Color(0.12, 0.12, 0.14, 1)\n'
            'ground_horizon_color = Color(0.72, 0.74, 0.8, 1)\n')
subs.append('[sub_resource type="Sky" id="Sky_res"]\nsky_material = SubResource("Sky_mat")\n')
subs.append('[sub_resource type="Environment" id="Env"]\n'
            'background_mode = 2\n'
            'sky = SubResource("Sky_res")\n'
            'ambient_light_source = 3\n'
            'tonemap_mode = 2\n'
            'ssao_enabled = true\n'
            'glow_enabled = true\n')

out = [f"[gd_scene load_steps={len(ext) + len(subs) + 1} format=3]\n\n"]
out += [e + "\n" for e in ext]
out.append("\n")
out += [s + "\n" for s in subs]

out.append('[node name="MovementGym" type="Node3D"]\n\n')
out.append('[node name="WorldEnvironment" type="WorldEnvironment" parent="."]\n'
           'environment = SubResource("Env")\n\n')
out.append('[node name="Sun" type="DirectionalLight3D" parent="."]\n'
           'transform = Transform3D(0.819152, 0.40558, -0.40558, 0, 0.707107, 0.707107, 0.573576, -0.579228, 0.579228, 0, 20, 0)\n'
           'light_energy = 1.1\n'
           'shadow_enabled = true\n'
           'directional_shadow_max_distance = 120.0\n\n')
out.append('[node name="Geometry" type="Node3D" parent="."]\n\n')

SURFACE = {"ramp": "metal"}  # SurfaceType metadata per material (default Concrete). Blue ramps = metal.
for name, pos, size, mat, rot in nodes:
    meta = f'metadata/surface = "{SURFACE[mat]}"\n' if mat in SURFACE else ""
    out.append(f'[node name="{name}" type="CSGBox3D" parent="Geometry"]\n'
               f'transform = {xform(pos, rot)}\n'
               f'use_collision = true\n'
               f'size = Vector3({fmt(size[0])}, {fmt(size[1])}, {fmt(size[2])})\n'
               f'material = SubResource("Mat_{mat}")\n'
               f'{meta}\n')

# Door: wall with a subtracted opening (CSG child, operation 2 = subtraction). Opening bottom at floor level.
out.append(f'[node name="DoorWall" type="CSGBox3D" parent="Geometry"]\n'
           f'transform = {xform((-45, 1.5, 12))}\n'
           f'use_collision = true\n'
           f'size = Vector3(4, 3, 0.3)\n'
           f'material = SubResource("Mat_wall")\n\n')
out.append(f'[node name="DoorOpening" type="CSGBox3D" parent="Geometry/DoorWall"]\n'
           f'transform = {xform((0, -0.45, 0))}\n'
           f'operation = 2\n'
           f'size = Vector3(1, 2.1, 1)\n\n')

out.append('[node name="Labels" type="Node3D" parent="."]\n\n')
for i, (text, pos, color, size) in enumerate(labels):
    safe = "".join(ch if ch.isalnum() else "_" for ch in text)[:24]
    out.append(f'[node name="L{i:02d}_{safe}" type="Label3D" parent="Labels"]\n'
               f'transform = {xform(pos)}\n'
               f'billboard = 1\n'
               f'modulate = {color}\n'
               f'text = "{text}"\n'
               f'font_size = {size}\n'
               f'outline_size = 18\n\n')

out.append(f'[node name="Runner" parent="." instance=ExtResource("1_runner")]\n'
           f'transform = {xform((0, 0.05, 8))}\n')

path = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Dev", "TestMaps", "MovementGym.tscn")
with open(path, "w", encoding="utf-8", newline="\n") as f:
    f.write("".join(out))
print("wrote", os.path.normpath(path), len(nodes), "boxes,", len(labels), "labels")
