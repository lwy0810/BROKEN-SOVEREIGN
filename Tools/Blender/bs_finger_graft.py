# -*- coding: utf-8 -*-
"""
BrokenSovereign - 손가락 본 이식

Mixamo가 손가락을 구분하지 못해 검지 체인 하나만 만든 리깅(33본)에
메시 형태를 분석해 다섯 손가락 본(Thumb·Index·Middle·Ring·Pinky, 각 1~4)을 만들어 넣고
손 부분 웨이트를 다시 나눈다. 결과는 Mixamo Standard(65본)와 같은 이름 구성.

전제: T포즈, 팔이 ±X 방향, 손바닥이 아래(-Z), 캐릭터 정면이 -Y.

사용 예
    import bs_finger_graft as fg
    report = fg.graft(arm, mesh)        # 양손
"""

import math

import bmesh
import bpy
from mathutils import Vector

PREFIX = "mixamorig:"
FINGERS = ("Index", "Middle", "Ring", "Pinky")   # 정면(-Y)에서 뒤쪽 순서
SEGMENT_RATIO = (0.45, 0.30, 0.25)               # 1·2·3번 마디 길이 비율
KNUCKLE_RATIO = 0.55                              # 손목→손끝 거리 중 손가락 시작 지점
END_RATIO = 0.35                                  # 4번(끝) 본 길이 = 3번 마디 × 비율


def graft(arm, mesh, sides=("Left", "Right")):
    if arm.animation_data:
        arm.animation_data.action = None
    report = {}
    for side in sides:
        report[side] = _graft_side(arm, mesh, side)
    return report


# ---------------------------------------------------------------------------

def _graft_side(arm, mesh, side):
    sign = 1.0 if side == "Left" else -1.0
    hand_name = PREFIX + side + "Hand"
    mw_arm = arm.matrix_world
    mw_mesh = mesh.matrix_world
    inv_arm = mw_arm.inverted()

    wrist = mw_arm @ arm.data.bones[hand_name].head_local
    vg_by_index = {g.index: g.name for g in mesh.vertex_groups}
    hand_family = {n for n in vg_by_index.values() if n.startswith(PREFIX + side + "Hand")}

    # 손 정점: 손 계열 그룹 웨이트를 가진 정점
    me = mesh.data
    world = [mw_mesh @ v.co for v in me.vertices]
    hand_verts = [v.index for v in me.vertices
                  if any(vg_by_index[g.group] in hand_family and g.weight > 0.01 for g in v.groups)]
    if not hand_verts:
        return {"error": "손 정점 없음"}

    def ax(p):  # 손 방향 좌표 (손목→손끝이 +)
        return (p.x - wrist.x) * sign

    tip_dist = max(ax(world[i]) for i in hand_verts)
    knuckle = KNUCKLE_RATIO * tip_dist

    # 엄지: 손가락 영역에서 정면(-Y)·아래(-Z)로 분리된 연결 덩어리
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.verts.ensure_lookup_table()
    hand_set = set(hand_verts)
    beyond = {i for i in hand_verts if ax(world[i]) > knuckle * 0.95}
    comps = _components(bm, beyond)
    bm.free()
    comps = [c for c in comps if len(c) >= 3]
    mean = lambda c, a: sum(getattr(world[i], a) for i in c) / len(c)
    thumb_comp = min(comps, key=lambda c: mean(c, "z") + mean(c, "y")) if len(comps) > 1 else []
    finger_set = set().union(*[set(c) for c in comps if c is not thumb_comp]) if comps else set()

    # 엄지 전체: 정면·아래쪽 손 정점 중 엄지 덩어리와 이어진 쪽
    palm_center = Vector((sum(world[i].x for i in hand_set) / len(hand_set),
                          sum(world[i].y for i in hand_set) / len(hand_set),
                          sum(world[i].z for i in hand_set) / len(hand_set)))
    thumb_set = set(thumb_comp)
    if thumb_comp:
        tc = Vector((mean(thumb_comp, "x"), mean(thumb_comp, "y"), mean(thumb_comp, "z")))
        for i in hand_verts:
            p = world[i]
            if ax(p) < knuckle and (p.y < palm_center.y - 0.02) and (p.z < palm_center.z - 0.01) \
                    and (p - tc).length < tip_dist * 0.5:
                thumb_set.add(i)
        thumb_tip = max(thumb_comp, key=lambda i: (world[i] - wrist).length)
        thumb_tip_p = world[thumb_tip]
        base_pts = sorted(thumb_set, key=lambda i: ax(world[i]))[: max(3, len(thumb_set) // 6)]
        thumb_base = sum((world[i] for i in base_pts), Vector()) / len(base_pts)
        thumb_base = thumb_base.lerp(wrist, 0.35)
    # 네 손가락: 손가락 영역 정점을 Y로 4묶음 (k-means)
    fpts = sorted(finger_set - thumb_set, key=lambda i: world[i].y)
    clusters = _kmeans_y([world[i] for i in fpts], fpts, 4)

    chains = {}
    for fname, idxs in zip(FINGERS, clusters):
        if not idxs:
            continue
        tip_i = max(idxs, key=lambda i: ax(world[i]))
        tip = world[tip_i]
        cy = sum(world[i].y for i in idxs) / len(idxs)
        cz = sum(world[i].z for i in idxs) / len(idxs)
        base = Vector((wrist.x + sign * knuckle, cy, cz))
        tip = Vector((tip.x, cy * 0.5 + tip.y * 0.5, cz * 0.5 + tip.z * 0.5))
        chains[fname] = _chain_points(base, tip)
    if thumb_comp:
        chains["Thumb"] = _chain_points(thumb_base, thumb_tip_p)

    # 본 편집: 기존 손가락 본 제거 후 새로 생성
    old_finger = [b.name for b in arm.data.bones
                  if b.name.startswith(PREFIX + side + "Hand") and b.name != hand_name]
    _edit_bones(arm, side, hand_name, old_finger, chains, inv_arm)

    # 웨이트: 손 계열 웨이트 총량을 새 본들에 재분배
    seg_list = []  # (group_name, a, b)
    hand_seg = (hand_name, wrist, Vector((wrist.x + sign * knuckle, wrist.y, wrist.z)))
    for fname, pts in chains.items():
        for k in range(3):
            seg_list.append((PREFIX + side + "Hand" + fname + str(k + 1), pts[k], pts[k + 1], fname))
    for fname in chains:
        for k in range(4):
            gname = PREFIX + side + "Hand" + fname + str(k + 1)
            if gname not in mesh.vertex_groups:
                mesh.vertex_groups.new(name=gname)
    for n in old_finger:
        g = mesh.vertex_groups.get(n)
        if g and not any(n == PREFIX + side + "Hand" + f + str(k + 1) for f in chains for k in range(4)):
            mesh.vertex_groups.remove(g)
    vg_by_index = {g.index: g.name for g in mesh.vertex_groups}
    hand_family = {n for n in vg_by_index.values() if n.startswith(PREFIX + side + "Hand")}
    groups = {g.name: g for g in mesh.vertex_groups}

    finger_of = {}
    for fname, idxs in zip(FINGERS, clusters):
        for i in idxs:
            finger_of[i] = fname
    for i in thumb_set:
        finger_of[i] = "Thumb"

    changed = 0
    for i in hand_verts:
        v = me.vertices[i]
        total = sum(g.weight for g in v.groups if vg_by_index.get(g.group) in hand_family)
        if total <= 0:
            continue
        p = world[i]
        own = finger_of.get(i)
        cands = [(hand_name, _seg_dist(p, hand_seg[1], hand_seg[2]))]
        for gname, a, b, fname in seg_list:
            if own is None:
                # 손바닥: 각 손가락 1번 마디만 (너클 근처 블렌드)
                if gname.endswith("1") and ax(p) > knuckle * 0.8:
                    cands.append((gname, _seg_dist(p, a, b)))
            elif fname == own:
                cands.append((gname, _seg_dist(p, a, b)))
        cands.sort(key=lambda c: c[1])
        cands = cands[:2]
        inv = [1.0 / max(d, 1e-4) ** 4 for _, d in cands]
        s = sum(inv)
        for g in list(v.groups):
            if vg_by_index.get(g.group) in hand_family:
                mesh.vertex_groups[g.group].remove([i])
        for (gname, _), w in zip(cands, inv):
            groups[gname].add([i], total * w / s, "ADD")
        changed += 1

    return {
        "tip_dist": round(tip_dist, 3),
        "knuckle": round(knuckle, 3),
        "thumb_verts": len(thumb_set),
        "finger_verts": {f: len(c) for f, c in zip(FINGERS, clusters)},
        "reweighted": changed,
        "bones_added": sorted(c for c in chains),
    }


def _components(bm, sel):
    seen, out = set(), []
    for i in sel:
        if i in seen:
            continue
        stack, group = [i], []
        seen.add(i)
        while stack:
            c = stack.pop()
            group.append(c)
            for e in bm.verts[c].link_edges:
                o = e.other_vert(bm.verts[c]).index
                if o in sel and o not in seen:
                    seen.add(o)
                    stack.append(o)
        out.append(group)
    return out


def _kmeans_y(points, idxs, k, iters=20):
    if len(points) < k:
        return [[] for _ in range(k)]
    ys = [p.y for p in points]
    srt = sorted(ys)
    centers = [srt[int((j + 0.5) / k * len(srt))] for j in range(k)]
    assign = [0] * len(ys)
    for _ in range(iters):
        assign = [min(range(k), key=lambda j: abs(y - centers[j])) for y in ys]
        for j in range(k):
            m = [y for y, a in zip(ys, assign) if a == j]
            if m:
                centers[j] = sum(m) / len(m)
    order = sorted(range(k), key=lambda j: centers[j])   # -Y(정면)부터
    return [[idxs[n] for n, a in enumerate(assign) if a == j] for j in order]


def _chain_points(base, tip):
    d = tip - base
    pts = [base]
    acc = 0.0
    for r in SEGMENT_RATIO:
        acc += r
        pts.append(base + d * acc)
    pts.append(tip + d.normalized() * d.length * SEGMENT_RATIO[2] * END_RATIO)
    return pts


def _seg_dist(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (a + ab * t - p).length


def _edit_bones(arm, side, hand_name, old_finger, chains, inv_arm):
    view = bpy.context.view_layer
    for o in view.objects:
        o.select_set(False)
    arm.select_set(True)
    view.objects.active = arm
    with bpy.context.temp_override(active_object=arm, object=arm, selected_objects=[arm], selected_editable_objects=[arm]):
        bpy.ops.object.mode_set(mode="EDIT")
        eb = arm.data.edit_bones
        for n in old_finger:
            if n in eb:
                eb.remove(eb[n])
        hand = eb[hand_name]
        for fname, pts in chains.items():
            parent = hand
            for k in range(4):
                b = eb.new(PREFIX + side + "Hand" + fname + str(k + 1))
                b.head = inv_arm @ pts[k]
                b.tail = inv_arm @ pts[k + 1]
                b.parent = parent
                b.use_connect = k > 0
                b.align_roll(inv_arm.to_3x3() @ Vector((0, 0, 1)))
                parent = b
        bpy.ops.object.mode_set(mode="OBJECT")
