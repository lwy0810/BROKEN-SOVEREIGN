# -*- coding: utf-8 -*-
"""
BrokenSovereign - 리깅(스키닝) 품질 측정·개선

Mixamo 자동 리깅 결과의 웨이트를 게임용으로 다듬습니다.

  measure(arm, mesh)  : 시험 포즈(팔 내림, 달리기, 휘두르기)에서 엣지 늘어남을 측정
  improve(arm, mesh)  : 웨이트 정리 + 늘어나는 부위만 스무딩
      1. 0.01 미만 웨이트 삭제
      2. 늘어남이 큰 정점과 주변 2링을 라플라시안 스무딩
      3. 정점당 영향 본 4개 제한 (Unity 기본 Skin Weights = 4 Bones)
      4. 정규화

사용 예
    import bs_rig_quality as rq
    before = rq.measure(arm, mesh)
    rq.improve(arm, mesh)
    after = rq.measure(arm, mesh)
"""

import math

import bpy
from mathutils import Matrix, Vector

PREFIX = "mixamorig:"

# (본, 월드 축, 각도) — 부모부터 순서대로 적용
POSES = {
    "arms_down": [
        ("LeftArm", "Y", 70), ("RightArm", "Y", -70),
        ("LeftForeArm", "X", -20), ("RightForeArm", "X", -20),
    ],
    "run": [
        ("LeftArm", "Y", 70), ("RightArm", "Y", -70),
        ("LeftUpLeg", "X", -50), ("LeftLeg", "X", 80),
        ("RightUpLeg", "X", 35), ("RightLeg", "X", 40),
        ("Spine1", "Z", 10),
    ],
    "swing": [
        ("Spine1", "Z", -30),
        ("RightArm", "X", -60), ("RightArm", "Y", -30),
        ("RightForeArm", "Z", -40),
        ("LeftArm", "Y", 60),
        ("LeftUpLeg", "X", -25), ("RightUpLeg", "X", 20),
    ],
}

STRETCH_HI = 1.35
STRETCH_LO = 0.65


# ---------------------------------------------------------------------------
# 포즈
# ---------------------------------------------------------------------------

def reset_pose(arm):
    if arm.animation_data:
        arm.animation_data.action = None
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


def apply_pose(arm, name):
    reset_pose(arm)
    axes = {"X": Vector((1, 0, 0)), "Y": Vector((0, 1, 0)), "Z": Vector((0, 0, 1))}
    inv = arm.matrix_world.inverted().to_3x3()
    for bone, axis, deg in POSES[name]:
        pb = arm.pose.bones.get(PREFIX + bone)
        if pb is None:
            continue
        ax = (inv @ axes[axis]).normalized()
        head = pb.matrix.translation.copy()
        rot = Matrix.Translation(head) @ Matrix.Rotation(math.radians(deg), 4, ax) @ Matrix.Translation(-head)
        pb.matrix = rot @ pb.matrix
        bpy.context.view_layer.update()


# ---------------------------------------------------------------------------
# 측정
# ---------------------------------------------------------------------------

def _deformed_coords(mesh):
    dg = bpy.context.evaluated_depsgraph_get()
    ev = mesh.evaluated_get(dg)
    m = ev.to_mesh()
    co = [v.co.copy() for v in m.vertices]
    ev.to_mesh_clear()
    return co


def _dominant_bone(mesh, v):
    best = max(v.groups, key=lambda g: g.weight, default=None)
    return mesh.vertex_groups[best.group].name.replace(PREFIX, "") if best else "-"


def measure(arm, mesh, poses=None):
    """포즈별로 늘어남/줄어듦 기준을 넘는 엣지 수와 부위(주 본)를 반환."""
    me = mesh.data
    reset_pose(arm)
    rest = _deformed_coords(mesh)
    rest_len = [(rest[e.vertices[0]] - rest[e.vertices[1]]).length for e in me.edges]
    report = {}
    flagged = set()
    for name in poses or POSES:
        apply_pose(arm, name)
        co = _deformed_coords(mesh)
        bad = 0
        worst = 1.0
        regions = {}
        for e, l0 in zip(me.edges, rest_len):
            if l0 < 1e-6:
                continue
            a, b = e.vertices
            r = (co[a] - co[b]).length / l0
            dev = max(r, 1.0 / max(r, 1e-6))
            worst = max(worst, dev)
            if r > STRETCH_HI or r < STRETCH_LO:
                bad += 1
                flagged.update((a, b))
                bone = _dominant_bone(mesh, me.vertices[a])
                regions[bone] = regions.get(bone, 0) + 1
        top = sorted(regions.items(), key=lambda kv: -kv[1])[:5]
        report[name] = dict(bad_edges=bad, worst=round(worst, 2), regions=top)
    reset_pose(arm)
    return report, flagged


def weight_stats(mesh):
    me = mesh.data
    infl = [sum(1 for g in v.groups if g.weight > 1e-4) for v in me.vertices]
    return dict(
        max_influences=max(infl),
        verts_over4=sum(1 for n in infl if n > 4),
        tiny_weights=sum(1 for v in me.vertices for g in v.groups if 0 < g.weight < 0.01),
    )


# ---------------------------------------------------------------------------
# 개선
# ---------------------------------------------------------------------------

def _neighbors(me):
    nb = [set() for _ in me.vertices]
    for e in me.edges:
        a, b = e.vertices
        nb[a].add(b)
        nb[b].add(a)
    return nb


def _read_weights(mesh):
    return [{g.group: g.weight for g in v.groups if g.weight > 0} for v in mesh.data.vertices]


def _write_weights(mesh, weights, touched):
    vgs = mesh.vertex_groups
    for i in touched:
        v = mesh.data.vertices[i]
        for g in list(v.groups):
            vgs[g.group].remove([i])
        for gi, w in weights[i].items():
            if w > 0:
                vgs[gi].add([i], w, "REPLACE")


def _normalize_limit(wd, limit=4, min_w=0.01):
    items = sorted(wd.items(), key=lambda kv: -kv[1])[:limit]
    items = [(g, w) for g, w in items if w >= min_w] or items[:1]
    s = sum(w for _, w in items)
    return {g: w / s for g, w in items}


def improve(arm, mesh, flagged=None, rings=2, iterations=4, factor=0.5, limit=4):
    """웨이트를 정리하고, flagged 정점 주변만 스무딩한다. 처리한 정점 수를 반환."""
    me = mesh.data
    nb = _neighbors(me)
    weights = _read_weights(mesh)

    region = set(flagged or [])
    frontier = set(region)
    for _ in range(rings):
        nxt = set()
        for i in frontier:
            nxt |= nb[i]
        nxt -= region
        region |= nxt
        frontier = nxt

    # 라플라시안 스무딩 (region 안에서만)
    for _ in range(iterations):
        new = {}
        for i in region:
            acc = {}
            ns = nb[i]
            if not ns:
                continue
            for j in ns:
                for g, w in weights[j].items():
                    acc[g] = acc.get(g, 0.0) + w / len(ns)
            mixed = {}
            for g in set(acc) | set(weights[i]):
                mixed[g] = (1 - factor) * weights[i].get(g, 0.0) + factor * acc.get(g, 0.0)
            new[i] = mixed
        for i, wd in new.items():
            weights[i] = wd

    # 전 정점 정리: 미세 웨이트 삭제, 4본 제한, 정규화
    touched = range(len(me.vertices))
    weights = [_normalize_limit(wd, limit) if wd else wd for wd in weights]
    _write_weights(mesh, weights, touched)
    return len(region)


# ---------------------------------------------------------------------------
# 리깅 결과 일괄 마무리
# ---------------------------------------------------------------------------

LEGS_TORSO = ("Hips", "LeftUpLeg", "RightUpLeg", "LeftLeg", "RightLeg", "Spine", "Spine1")


FINGER_NAMES = ("Thumb", "Index", "Middle", "Ring", "Pinky")


def fix_finger_weights(arm, mesh, sides=("Left", "Right"), blend=1.5):
    """손가락이 붙어 휘는 문제 보정: 손 계열(손·손가락) 웨이트를 가장 가까운 본에 다시 나눈다.

    정점마다 손 계열 웨이트 합을 가장 가까운 본(선분 거리)에 주고,
    같은 손가락(또는 손바닥) 안의 두 번째로 가까운 본이 blend배 거리 안이면 거리 반비례로 나눈다.
    다른 손가락끼리는 섞지 않는다 → 손가락을 굽혀도 옆 손가락이 끌려오지 않음.
    """
    reset_pose(arm)
    bpy.context.view_layer.update()
    mw_arm = arm.matrix_world
    mw_mesh = mesh.matrix_world
    report = {}
    for side in sides:
        hand = PREFIX + side + "Hand"
        family = [b for b in arm.data.bones
                  if b.name == hand or (b.name.startswith(PREFIX + side + "Hand") and b.use_deform)]
        segs = []
        for b in family:
            chain = "Palm"
            for f in FINGER_NAMES:
                if f in b.name:
                    chain = f
            segs.append((b.name, chain, mw_arm @ b.head_local, mw_arm @ b.tail_local))
        names = {s[0] for s in segs}
        groups = {g.name: g for g in mesh.vertex_groups if g.name in names}
        for n in names:
            if n not in groups:
                groups[n] = mesh.vertex_groups.new(name=n)
        index_to_name = {g.index: g.name for g in mesh.vertex_groups}

        changed = 0
        for v in mesh.data.vertices:
            total = sum(g.weight for g in v.groups if index_to_name.get(g.group) in names)
            if total < 0.01:
                continue
            p = mw_mesh @ v.co
            dists = sorted(((_seg_dist(p, a, b), n, c) for n, c, a, b in segs), key=lambda x: x[0])
            d1, n1, c1 = dists[0]
            assign = {n1: 1.0}
            for d2, n2, c2 in dists[1:]:
                if c2 == c1 or "Palm" in (c1, c2):
                    if d2 < d1 * blend and d2 > 1e-6:
                        w1, w2 = 1.0 / max(d1, 1e-5), 1.0 / d2
                        assign = {n1: w1 / (w1 + w2), n2: w2 / (w1 + w2)}
                    break
            for n in names:
                groups[n].remove([v.index])
            for n, w in assign.items():
                groups[n].add([v.index], total * w, "REPLACE")
            changed += 1
        report[side] = dict(bones=len(segs), vertices=changed)
    return report


def _seg_dist(p, a, b):
    ab = b - a
    t = 0.0 if ab.length_squared < 1e-12 else max(0.0, min(1.0, (p - a).dot(ab) / ab.length_squared))
    return (p - (a + ab * t)).length


def curl_fingers(arm, angle=60.0):
    """시험 포즈: 엄지 외 손가락 1~3마디를 angle도씩 굽힘 (손바닥 아래 방향, 로컬 X축)."""
    for pb in arm.pose.bones:
        if any(f in pb.name for f in FINGER_NAMES[1:]) and pb.name[-1] in "123":
            pb.rotation_mode = "XYZ"
            pb.rotation_euler = (math.radians(angle), 0.0, 0.0)
    bpy.context.view_layer.update()


def finalize_and_improve(name, rigged_fbx, out_path=None, regions=None, poses=None):
    """Mixamo 리깅 FBX → finalize_rigged(스케일·텍스처) → 측정·웨이트 보정 → Unity용 FBX 재저장.
    regions: 보정할 부위(주 본 이름, 접두어 제외). None이면 전체. 예: LEGS_TORSO
    poses  : 문제 정점을 찾을 시험 포즈. None이면 전체. 예: ("run", "swing")
    보고서(dict)를 반환한다."""
    import bs_model_cleanup as bs

    info = bs.finalize_rigged(name, rigged_fbx, out_path=out_path)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    mesh = next(o for o in bpy.data.objects if o.type == "MESH")

    before, _ = measure(arm, mesh)
    _, flagged = measure(arm, mesh, poses=poses)
    if regions:
        flagged = {i for i in flagged if _dominant_bone(mesh, mesh.data.vertices[i]) in regions}
    improve(arm, mesh, flagged)
    after, _ = measure(arm, mesh)
    reset_pose(arm)

    for o in bpy.context.view_layer.objects:
        o.select_set(o in (arm, mesh))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=info["fbx"],
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        mesh_smooth_type="FACE",
        # 텍스처는 finalize_rigged가 Unity용 PNG로 따로 저장하므로 FBX에 넣지 않음 (용량 10MB 기준)
        path_mode="STRIP",
        embed_textures=False,
    )
    info["worst_before"] = {k: v["worst"] for k, v in before.items()}
    info["worst_after"] = {k: v["worst"] for k, v in after.items()}
    info["weights"] = weight_stats(mesh)
    return info


def finalize_npc(name, rigged_fbx):
    """마을 NPC용: finalize_rigged → (손가락 본이 모자라면) 다섯 손가락 이식 → 손가락 웨이트 분리 → 몸 웨이트 보정 → Unity FBX.
    10/19: Mixamo가 가늘거나 장갑 낀 손가락을 구분하지 못해 33·41본으로 리깅되고 손가락이 휘는 문제 대응."""
    import bs_model_cleanup as bs
    import bs_finger_graft as fg

    info = bs.finalize_rigged(name, rigged_fbx)
    arm = next(o for o in bpy.data.objects if o.type == "ARMATURE")
    mesh = next(o for o in bpy.data.objects if o.type == "MESH")
    if info["finger_bones"] < 20:
        fg.graft(arm, mesh)
        info["grafted"] = True
    info["finger_fix"] = fix_finger_weights(arm, mesh)

    before, _ = measure(arm, mesh)
    _, flagged = measure(arm, mesh)
    improve(arm, mesh, flagged)
    after, _ = measure(arm, mesh)
    reset_pose(arm)

    for o in bpy.context.view_layer.objects:
        o.select_set(o in (arm, mesh))
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=info["fbx"],
        use_selection=True,
        object_types={"ARMATURE", "MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_ALL",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=False,
        add_leaf_bones=False,
        use_armature_deform_only=True,
        bake_anim=False,
        mesh_smooth_type="FACE",
        path_mode="STRIP",
        embed_textures=False,
    )
    info["bones_out"] = len(arm.data.bones)
    info["worst_before"] = {k: v["worst"] for k, v in before.items()}
    info["worst_after"] = {k: v["worst"] for k, v in after.items()}
    info["weights"] = weight_stats(mesh)
    return info
