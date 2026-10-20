"""석천 난천무 모션 생성 (2026-10-20).
양손으로 봉을 정면 수평으로 들고 위에서 볼 때 시계 방향으로 1회전 (15프레임, 30fps = 0.5초, 반복용).

방식
1. 성기사 대기 모션(PaladinIdle) 한 프레임을 기본 자세로 사용 (양손이 이미 봉을 쥔 자세)
2. Unity 무기 그립(WeaponCelestialStaff 에셋 값)으로 오른손 기준 봉 위치 계산
   (Unity 손 로컬 = Blender 본 로컬 X 반전, 봉 메시 X 반전)
3. 봉을 정면 수평으로 돌리고, 두 손이 봉의 같은 위치를 쥐도록 팔 2본 IK로 풀이 (손목 꺾임이 작은 롤 자동 선택)
4. 골반을 수직축으로 −360° 회전 키프레임 → 뼈대만 FBX로 내보냄 (Unity Humanoid, 루트 회전은 포즈에 굽기)

실행: blender -b -P bs_whirl_anim.py -- <출력 폴더> '{"origin":[-0.04,-0.42,1.06],"export":"<경로>/PaladinSkillHeavenlyWhirl.fbx"}'
조절값(JSON): origin(오른손 그립 위치), pitch(봉 끝 들어올림°), roll(봉 축 회전°, 생략 시 자동), frames(1회전 프레임), idle_frame
"""
import bpy, math, os, sys, json
from mathutils import Matrix, Vector, Euler, Quaternion
U = os.environ.get("BS_ASSETS") or os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets") + "/"
OUT = sys.argv[sys.argv.index("--")+1] if "--" in sys.argv else os.getcwd()
P = json.loads(sys.argv[sys.argv.index("--")+2]) if "--" in sys.argv and len(sys.argv) > sys.argv.index("--")+2 else {}

# --- 조절값 ---
STAFF_ORIGIN = Vector(P.get("origin", (-0.06, -0.30, 1.12)))   # 오른손(그립) 위치 (Blender 월드, 정면 -Y)
STAFF_PITCH = math.radians(P.get("pitch", 0.0))                # 봉 끝 들어올림 (+ 위)
LEFT_ALONG = P.get("left_along", 0.32)                          # 왼손: 그립에서 봉 앞쪽으로 (m)
ROLL = P.get("roll", None)                                      # 봉 축 회전 (None이면 자동)
FRAMES = P.get("frames", 15)                                    # 1회전 프레임 (30fps → 0.5초)

GRIP_POS = Vector((-0.0070677414, 0.077724025, 0.026000325))
GRIP_EULER = (274.5078, 212.75606, 241.4274)

def unity_euler_to_matrix(e):
    # Unity Euler: Z → X → Y 순서 적용 (R = Ry * Rx * Rz)
    x, y, z = (math.radians(a) for a in e)
    Rx = Matrix.Rotation(x, 3, 'X'); Ry = Matrix.Rotation(y, 3, 'Y'); Rz = Matrix.Rotation(z, 3, 'Z')
    return Ry @ Rx @ Rz

F = Matrix(((-1,0,0),(0,1,0),(0,0,1)))
G = unity_euler_to_matrix(GRIP_EULER)
# 오른손 본 로컬(Blender) 기준 봉 오브젝트 변환: Unity 손 로컬 = Blender 본 로컬 X 반전, 봉 메시 X 반전
STAFF_IN_HAND = Matrix.Translation(F @ GRIP_POS) @ (F @ G @ F).to_4x4()

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=U+"Models/Paladin/Paladin.fbx")
arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
body = [o for o in bpy.data.objects if o.type == 'MESH'][0]
bpy.ops.import_scene.fbx(filepath=U+"Models/WeaponCelestialStaff/WeaponCelestialStaff.fbx")
staff = [o for o in bpy.data.objects if o.name.startswith("WeaponCelestialStaff")][0]
staff_mesh_basis = staff.matrix_world.copy()  # 가져온 오브젝트 회전은 Unity에서 그립 회전으로 덮어씀 → 메시만 사용
staff.matrix_world = Matrix.Identity(4)
# 메시 데이터 기준(오브젝트 회전 없음)으로 맞추기 위해 회전 제거
staff.rotation_euler = (0,0,0); staff.location = (0,0,0); staff.scale = (1,1,1)

pb = arm.pose.bones

# 기본 자세: 대기 모션 프레임 (양손이 봉을 쥔 상태) → 포즈로 굳힘
before = set(bpy.data.objects)
bpy.ops.import_scene.fbx(filepath=U+"Animations/Paladin/PaladinIdle.fbx")
newobj = [o for o in bpy.data.objects if o not in before]
act = [o for o in newobj if o.type == 'ARMATURE'][0].animation_data.action
for o in newobj: bpy.data.objects.remove(o)
arm.animation_data_create(); arm.animation_data.action = act
bpy.context.scene.frame_set(int(P.get("idle_frame", 10)))
bpy.context.view_layer.update()
BASE = {b.name: b.matrix.copy() for b in arm.pose.bones}
arm.animation_data.action = None
for b in arm.pose.bones:
    b.matrix_basis = Matrix.Identity(4)
bpy.context.view_layer.update()
for b in arm.pose.bones:   # 부모부터 순서대로
    b.matrix = BASE[b.name]
    bpy.context.view_layer.update()
BASIS = {b.name: b.matrix_basis.copy() for b in arm.pose.bones}
STAFF_IDLE = BASE["mixamorig:RightHand"] @ STAFF_IN_HAND
LEFT_IN_STAFF = STAFF_IDLE.inverted() @ BASE["mixamorig:LeftHand"]

HAND_R, HAND_L = "mixamorig:RightHand", "mixamorig:LeftHand"

def staff_world(roll):
    # 대기 자세의 봉 회전을 최소 회전으로 정면 수평(+피치)으로 돌린 뒤 축 기준 roll
    fwd = Vector((0, -math.cos(STAFF_PITCH), math.sin(STAFF_PITCH)))
    idle_rot = STAFF_IDLE.to_3x3()
    idle_axis = idle_rot @ Vector((0, 1, 0))
    rot = Matrix.Rotation(roll, 3, fwd) @ idle_axis.rotation_difference(fwd).to_matrix() @ idle_rot
    return Matrix.Translation(STAFF_ORIGIN) @ rot.to_4x4()

def hand_world_for_staff(sw):
    # 봉 월드 = 손 본 월드 @ STAFF_IN_HAND → 손 본 월드 = 봉 월드 @ inv
    return sw @ STAFF_IN_HAND.inverted()

def set_world_rot(bone, rot3, loc=None):
    m = pb[bone].matrix.copy()
    t = m.translation if loc is None else loc
    pb[bone].matrix = Matrix.Translation(t) @ rot3.to_4x4()
    bpy.context.view_layer.update()

def aim_bone(bone, target, pole):
    """본 Y축을 target 방향으로 (스윙), 본 Z축(또는 X)을 pole 쪽으로 롤"""
    m = pb[bone].matrix
    head = m.translation
    y = (target - head).normalized()
    # 극점 방향 성분으로 X축 결정 (팔꿈치 굽힘 축)
    side = (pole - head); side = (side - y * side.dot(y)).normalized()
    cur = m.to_3x3()
    swing = (cur @ Vector((0,1,0))).rotation_difference(y).to_matrix() @ cur
    # 스윙 결과의 Z축을 side에 가깝게 롤
    z = swing @ Vector((0,0,1))
    zp = (z - y * z.dot(y)).normalized()
    ang = zp.angle(side) if zp.length > 0 else 0
    sign = 1 if y.dot(zp.cross(side)) > 0 else -1
    rot = Matrix.Rotation(sign * ang, 3, y) @ swing
    set_world_rot(bone, rot)

def two_bone(upper, lower, hand, target, pole, hand_rot):
    S = pb[upper].matrix.translation.copy()
    L1 = (pb[lower].matrix.translation - S).length
    L2 = (pb[hand].matrix.translation - pb[lower].matrix.translation).length
    d = target - S
    dist = min(d.length, (L1 + L2) * 0.999)
    dn = d.normalized()
    # 팔꿈치 위치: 코사인 법칙
    a = (L1*L1 - L2*L2 + dist*dist) / (2*dist)
    h = math.sqrt(max(0.0, L1*L1 - a*a))
    pv = (pole - S); pv = (pv - dn * pv.dot(dn)).normalized()
    E = S + dn * a + pv * h
    aim_bone(upper, E, pole)
    aim_bone(lower, S + dn * dist, pole)
    set_world_rot(hand, hand_rot)
    return (pb[hand].matrix.translation - target).length

def solve(roll):
    sw = staff_world(roll)
    hr = hand_world_for_staff(sw)
    er = two_bone("mixamorig:RightArm", "mixamorig:RightForeArm", HAND_R, hr.translation,
                  Vector(P.get("pole_r", (-0.7, 0.3, 0.7))), hr.to_3x3())
    lw = sw @ LEFT_IN_STAFF
    p_left = lw.translation
    lr = lw.to_3x3()
    el = two_bone("mixamorig:LeftArm", "mixamorig:LeftForeArm", HAND_L, p_left,
                  Vector(P.get("pole_l", (0.7, 0.3, 0.7))), lr)
    return sw, er + el

def reset():
    for b in pb:
        b.matrix_basis = BASIS[b.name].copy()
    bpy.context.view_layer.update()

best = None
rolls = [math.radians(ROLL)] if ROLL is not None else [math.radians(a) for a in range(-90, 91, 15)]
for r in rolls:
    reset()
    sw, e = solve(r)
    bend = 0
    for hb, fb in ((HAND_R, "mixamorig:RightForeArm"), (HAND_L, "mixamorig:LeftForeArm")):
        hy = pb[hb].matrix.to_3x3() @ Vector((0,1,0))
        fy = pb[fb].matrix.to_3x3() @ Vector((0,1,0))
        bend += hy.angle(fy)
    score = e * 10 + bend
    if best is None or score < best[0]: best = (score, r, e, bend)
print("BEST roll", round(math.degrees(best[1])), "reach err", round(best[2],3), "bend", round(math.degrees(best[3])))
reset()
sw, e = solve(best[1])

# --- 회전 애니메이션: 고정 자세 + 골반을 수직축으로 시계 방향(위에서 볼 때) 1회전 ---
sc = bpy.context.scene
sc.render.fps = 30
sc.frame_start = 0
sc.frame_end = FRAMES
POSE = {b.name: b.matrix_basis.copy() for b in pb}
HIPS = "mixamorig:Hips"
hips_base = pb[HIPS].matrix.copy()
pivot = hips_base.translation.copy()
arm.animation_data_create()
arm.animation_data.action = bpy.data.actions.new("HeavenlyWhirl")
prev_q = None
for f in range(FRAMES + 1):
    theta = -2 * math.pi * f / FRAMES
    for b in pb:
        b.rotation_mode = 'QUATERNION'
        b.matrix_basis = POSE[b.name]
    bpy.context.view_layer.update()
    spin = Matrix.Translation(pivot) @ Matrix.Rotation(theta, 4, 'Z') @ Matrix.Translation(-pivot)
    pb[HIPS].matrix = spin @ hips_base
    bpy.context.view_layer.update()
    q = pb[HIPS].rotation_quaternion.copy()
    if prev_q is not None:
        q.make_compatible(prev_q)
        pb[HIPS].rotation_quaternion = q
    prev_q = q
    for b in pb:
        if b.name == HIPS or f in (0, FRAMES):
            b.keyframe_insert("rotation_quaternion", frame=f)
            b.keyframe_insert("location", frame=f)
for fc in arm.animation_data.action.fcurves:
    for k in fc.keyframe_points:
        k.interpolation = 'LINEAR'

bpy.ops.wm.save_as_mainfile(filepath=OUT + "/pose_ready.blend")
print("SAVED", OUT + "/pose_ready.blend")

if P.get("export"):
    bpy.ops.object.select_all(action='DESELECT')
    arm.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=P["export"], use_selection=True, object_types={"ARMATURE"},
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
        bake_space_transform=False, add_leaf_bones=False, use_armature_deform_only=True,
        bake_anim=True, bake_anim_use_all_bones=True, bake_anim_use_nla_strips=False,
        bake_anim_use_all_actions=False, bake_anim_force_startend_keying=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.0)
    print("EXPORTED", P["export"])
