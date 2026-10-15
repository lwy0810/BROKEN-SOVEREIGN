# -*- coding: utf-8 -*-
"""
BrokenSovereign - VARCO 생성 모델 정리 스크립트

대상: Blender 4.2 이상 (5.2 LTS 확인)
용도: VARCO 3D에서 받은 모델(.glb / .gltf / .fbx / .obj)을 게임용으로 정리하고 FBX로 내보냅니다.

처리 순서
  1. 불러오기
  2. 계층 해제, 메시 외 오브젝트 삭제, 메시 합치기
  3. 변환 적용, 중복 정점 병합, 노멀 재계산
  4. 작은 파편 삭제 (min_island_ratio > 0 일 때만)
  5. 폴리곤 감축 (프리셋 상한 초과 시 Decimate)
  6. 방향 보정 (rotate_z)
  7. 크기 맞춤 (높이 또는 길이 기준)
  8. 피벗을 밑면 중앙으로
  9. 머티리얼·텍스처 정리 (이름 규칙, 해상도 상한)
 10. FBX 내보내기 (Unity 축 설정), 텍스처 PNG 저장, 캐릭터는 Mixamo용 OBJ(메시만) 추가
 11. 보고서(dict) 반환

사용 예 (Blender Text Editor 또는 MCP)
    import sys, importlib
    sys.path.append(r"C:\\github\\K_ND\\BrokenSovereign\\Tools\\Blender")
    import bs_model_cleanup as bs
    importlib.reload(bs)
    report = bs.process(r"C:\\github\\K_ND\\VarcoWork\\Raw\\Paladin.glb", "Paladin")
    reports = bs.batch(r"C:\\github\\K_ND\\VarcoWork\\Raw")   # 파일 이름 = 프리셋 이름

Mixamo 리깅 후 (Mixamo에서 받은 FBX → Unity용 FBX)
    bs.finalize_rigged("Paladin", r"C:\\github\\K_ND\\VarcoWork\\Mixamo\\Paladin_Mixamo.fbx")
    → 이어서 bs_rig_quality.py로 웨이트 측정·보정

옵션은 프리셋 값을 덮어쓸 수 있습니다.
    bs.process(path, "Paladin", rotate_z=180, max_texture=1024)
"""

import math
import os
import tempfile

import bmesh
import bpy
from mathutils import Matrix

# ---------------------------------------------------------------------------
# 경로
# ---------------------------------------------------------------------------

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
WORK_ROOT = os.path.abspath(os.path.join(REPO_ROOT, "..", "VarcoWork"))

DEST_DIRS = {
    # 캐릭터: Mixamo 자동 리깅 업로드용 (리깅 후 Unity로)
    "mixamo": os.path.join(WORK_ROOT, "Mixamo"),
    # 소품: 바로 Unity 프로젝트로
    "unity": os.path.join(REPO_ROOT, "Assets", "Models"),
    # 테스트용
    "temp": os.path.join(tempfile.gettempdir(), "BsModelCleanup"),
}

# ---------------------------------------------------------------------------
# 프리셋 (VARCO 프롬프트 문서 체크리스트 기준)
#   size : 맞출 크기(m)
#   fit  : "height" = 높이(Z), "length" = 가로 최장(X/Y)
# ---------------------------------------------------------------------------

DEFAULTS = dict(
    size=1.8,
    fit="height",
    max_tris=20000,
    dest="mixamo",
    max_texture=2048,
    merge_distance=0.0001,
    recalc_normals=True,
    min_island_ratio=0.0,   # 전체 정점 대비 이 비율보다 작은 덩어리 삭제 (0 = 끔)
    rotate_z=0.0,           # 정면 보정 각도(도). Blender 정면은 -Y
    max_file_mb=10.0,
    clear_scene=True,
    save_blend=True,
)

PRESETS = {
    # 캐릭터 (리깅 전, 2만 폴리곤 이하)
    "Paladin":        dict(size=1.8, max_tris=20000, dest="mixamo"),
    "HeavenSoldier":  dict(size=1.8, max_tris=20000, dest="mixamo"),
    "TempleGuardian": dict(size=2.2, max_tris=20000, dest="mixamo"),
    "ErlangShen":     dict(size=2.4, max_tris=20000, dest="mixamo"),
    # 환경 소품 (정적)
    "PropStonePillar":  dict(size=4.0, max_tris=5000, dest="unity"),
    "PropPaifang":      dict(size=6.0, max_tris=5000, dest="unity"),
    "PropStoneLantern": dict(size=1.5, max_tris=5000, dest="unity"),
    "PropCloudRailing": dict(size=2.0, fit="length", max_tris=2000, dest="unity"),
}

IMPORT_EXTS = (".glb", ".gltf", ".fbx", ".obj")


# ---------------------------------------------------------------------------
# 공개 함수
# ---------------------------------------------------------------------------

def process(path, preset, out_dir=None, **overrides):
    """모델 1개를 불러와 정리하고 FBX로 내보냅니다. 보고서 dict를 반환합니다."""
    opts = _options(preset, overrides)
    name = preset
    warnings = []

    if opts["clear_scene"]:
        clear_scene()

    imported = import_model(path)
    obj = _flatten_and_join(imported, warnings)
    obj.name = name
    obj.data.name = name
    me = obj.data

    before_tris = triangle_count(me)
    islands = _clean_geometry(me, opts, warnings)

    after_tris = _decimate(obj, opts["max_tris"])
    me = obj.data

    if opts["rotate_z"]:
        me.transform(Matrix.Rotation(math.radians(opts["rotate_z"]), 4, "Z"))
    scale = _fit_size(me, opts["size"], opts["fit"])
    _pivot_to_bottom_center(me)
    me.update()

    materials, textures = _clean_materials(obj, name, opts["max_texture"])

    out_dir = out_dir or DEST_DIRS[opts["dest"]]
    fbx_path = export_fbx(obj, os.path.join(out_dir, name + ".fbx"))
    file_mb = os.path.getsize(fbx_path) / (1024 * 1024)

    # 텍스처 PNG 별도 저장 (Unity 머티리얼용)
    tex_dir = save_textures(obj, os.path.join(WORK_ROOT, "Textures", name))

    # Mixamo 업로드용 OBJ (메시만)
    mixamo_obj = None
    if opts["dest"] == "mixamo":
        mixamo_obj = export_mixamo_obj(obj, name, out_dir)

    blend_path = None
    if opts["save_blend"]:
        blend_dir = os.path.join(WORK_ROOT, "Blend")
        os.makedirs(blend_dir, exist_ok=True)
        blend_path = os.path.join(blend_dir, name + ".blend")
        bpy.ops.wm.save_as_mainfile(filepath=blend_path, copy=True)

    if after_tris > opts["max_tris"]:
        warnings.append("폴리곤 상한 초과: %d > %d" % (after_tris, opts["max_tris"]))
    if file_mb > opts["max_file_mb"]:
        warnings.append("FBX 용량 초과: %.1fMB > %.1fMB (max_texture=1024 권장)" % (file_mb, opts["max_file_mb"]))

    return dict(
        name=name,
        source=path,
        fbx=fbx_path,
        mixamo_obj=mixamo_obj,
        textures_dir=tex_dir,
        blend=blend_path,
        tris_before=before_tris,
        tris_after=after_tris,
        islands=islands,
        scale_applied=round(scale, 4),
        dimensions=[round(v, 3) for v in _bbox_size(me)],
        materials=materials,
        textures=textures,
        file_mb=round(file_mb, 2),
        warnings=warnings,
    )


def batch(src_dir, mapping=None, out_dir=None, **overrides):
    """폴더 안 모델을 일괄 처리합니다.
    mapping이 없으면 파일 이름(확장자 제외)이 프리셋 이름과 같을 때 처리합니다(대소문자 무시)."""
    lookup = {k.lower(): k for k in PRESETS}
    reports = []
    for fname in sorted(os.listdir(src_dir)):
        stem, ext = os.path.splitext(fname)
        if ext.lower() not in IMPORT_EXTS:
            continue
        preset = (mapping or {}).get(fname) or (mapping or {}).get(stem) or lookup.get(stem.lower())
        if not preset:
            reports.append(dict(name=stem, skipped="프리셋 없음"))
            continue
        try:
            reports.append(process(os.path.join(src_dir, fname), preset, out_dir, **overrides))
        except Exception as e:  # 한 파일 실패가 전체를 멈추지 않게
            reports.append(dict(name=stem, error=repr(e)))
    return reports


def inspect(obj_name=None):
    """정리 전 상태 확인용. 씬의 메시 오브젝트 정보를 반환합니다."""
    objs = [bpy.data.objects[obj_name]] if obj_name else [o for o in bpy.data.objects if o.type == "MESH"]
    out = []
    for o in objs:
        out.append(dict(
            name=o.name,
            tris=triangle_count(o.data),
            verts=len(o.data.vertices),
            dimensions=[round(v, 3) for v in o.dimensions],
            materials=[s.material.name for s in o.material_slots if s.material],
        ))
    return out


# ---------------------------------------------------------------------------
# 단계별 함수
# ---------------------------------------------------------------------------

def clear_scene():
    for o in list(bpy.data.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    bpy.data.orphans_purge(do_local_ids=True, do_linked_ids=True, do_recursive=True)


def import_model(path):
    ext = os.path.splitext(path)[1].lower()
    before = set(bpy.data.objects)
    if ext in (".glb", ".gltf"):
        bpy.ops.import_scene.gltf(filepath=path)
    elif ext == ".fbx":
        bpy.ops.import_scene.fbx(filepath=path)
    elif ext == ".obj":
        bpy.ops.wm.obj_import(filepath=path)
    else:
        raise ValueError("지원하지 않는 형식: " + ext)
    new = [o for o in bpy.data.objects if o not in before]
    if not new:
        raise RuntimeError("불러온 오브젝트가 없습니다: " + path)
    return new


def export_fbx(obj, fbx_path):
    os.makedirs(os.path.dirname(fbx_path), exist_ok=True)
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.export_scene.fbx(
        filepath=fbx_path,
        use_selection=True,
        object_types={"MESH"},
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        axis_forward="-Z",
        axis_up="Y",
        bake_space_transform=True,   # Unity에서 회전 0, 스케일 1로 들어오게
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        add_leaf_bones=False,
        path_mode="COPY",
        embed_textures=True,
    )
    return fbx_path


def save_textures(obj, tex_dir):
    """머티리얼의 이미지를 PNG로 저장한다 (이름 = 이미지 이름)."""
    os.makedirs(tex_dir, exist_ok=True)
    done = set()
    for slot in obj.material_slots:
        m = slot.material
        if not m or not m.use_nodes:
            continue
        for node in m.node_tree.nodes:
            img = getattr(node, "image", None)
            if node.type != "TEX_IMAGE" or not img or img in done:
                continue
            done.add(img)
            path = os.path.join(tex_dir, img.name + ".png")
            src_path, src_fmt = img.filepath_raw, img.file_format
            img.filepath_raw = path
            img.file_format = "PNG"
            img.save()
            img.filepath_raw, img.file_format = src_path, src_fmt
    return tex_dir


def export_mixamo_obj(obj, name, out_dir):
    """Mixamo 업로드용 OBJ (메시만, 머티리얼·텍스처 없음).

    10-15 시험 결과: Blender FBX(텍스처 포함/미포함)와 OBJ+텍스처 zip은
    'unable to map your existing skeleton' 오류로 실패, OBJ 메시만 성공.
    Mixamo는 OBJ 단위를 cm로 해석하므로 리깅 결과가 0.01배로 돌아온다
    → finalize_rigged()에서 1배로 보정.
    """
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    os.makedirs(out_dir, exist_ok=True)
    obj_path = os.path.join(out_dir, name + "_Mixamo.obj")
    bpy.ops.wm.obj_export(
        filepath=obj_path,
        export_selected_objects=True,
        forward_axis="NEGATIVE_Z",
        up_axis="Y",
        export_materials=False,
        export_normals=True,
        export_uv=True,
    )
    return obj_path


def make_metallic_smoothness(src_png, dst_png):
    """URP Lit용 MetallicSmoothness 텍스처(R = 금속성, A = 매끄러움)를 만든다.

    VARCO 텍스처는 glTF 방식으로 채널이 묶여 있다 (R = AO, G = 거칠기, B = 금속성).
    이름이 Roughness여도 R 채널만 읽으면 거칠기가 아니라 AO(거의 1)를 읽게 되어
    매끄러움 0·금속성 0의 무광 재질이 된다 (10-15 성기사에서 발생).
    채널 값이 서로 다르면 묶인 텍스처로, 같으면 흑백 거칠기 텍스처로 판단한다.
    """
    import numpy as np

    img = bpy.data.images.load(src_png, check_existing=False)
    img.colorspace_settings.name = "Non-Color"
    w, h = img.size
    px = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(px)
    px = px.reshape(-1, 4)

    packed = float(np.abs(px[:, 0] - px[:, 1]).mean()) > 0.05
    rough = px[:, 1] if packed else px[:, 0]
    metal = px[:, 2] if packed else np.zeros_like(rough)

    out = np.zeros_like(px)
    out[:, 0] = metal
    out[:, 1] = metal
    out[:, 2] = metal
    out[:, 3] = 1.0 - rough

    res = bpy.data.images.new("BsMetallicSmoothness", w, h, alpha=True)
    res.colorspace_settings.name = "Non-Color"
    res.pixels.foreach_set(out.ravel())
    res.filepath_raw = dst_png
    res.file_format = "PNG"
    res.save()
    bpy.data.images.remove(img)
    bpy.data.images.remove(res)
    return dict(packed=packed, metallic_mean=round(float(metal.mean()), 3), smoothness_mean=round(float(out[:, 3].mean()), 3))


def triangle_count(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


# ---------------------------------------------------------------------------
# 내부 구현
# ---------------------------------------------------------------------------

def _options(preset, overrides):
    if preset not in PRESETS:
        raise KeyError("프리셋 없음: %s (가능: %s)" % (preset, ", ".join(PRESETS)))
    opts = dict(DEFAULTS)
    opts.update(PRESETS[preset])
    unknown = set(overrides) - set(opts)
    if unknown:
        raise KeyError("알 수 없는 옵션: " + ", ".join(sorted(unknown)))
    opts.update(overrides)
    return opts


def _flatten_and_join(objs, warnings):
    meshes = [o for o in objs if o.type == "MESH"]
    if not meshes:
        raise RuntimeError("메시가 없습니다.")
    others = [o for o in objs if o.type != "MESH"]
    if any(o.type == "ARMATURE" for o in others):
        warnings.append("아마추어가 포함되어 삭제했습니다 (리깅은 Mixamo에서).")

    for o in meshes:
        for m in list(o.modifiers):
            if m.type == "ARMATURE":
                o.modifiers.remove(m)
        mw = o.matrix_world.copy()
        o.parent = None
        if o.data.users > 1:
            o.data = o.data.copy()
        o.data.transform(mw)
        o.matrix_world = Matrix.Identity(4)
        o.data.shape_keys and o.shape_key_clear()

    for o in others:
        bpy.data.objects.remove(o, do_unlink=True)

    main = meshes[0]
    if len(meshes) > 1:
        with bpy.context.temp_override(
            active_object=main, object=main,
            selected_objects=meshes, selected_editable_objects=meshes,
        ):
            bpy.ops.object.join()
    return main


def _clean_geometry(me, opts, warnings):
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=opts["merge_distance"])
    if opts["recalc_normals"]:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    islands = _islands(bm)
    total = len(bm.verts)
    removed = 0
    if opts["min_island_ratio"] > 0 and len(islands) > 1:
        limit = total * opts["min_island_ratio"]
        doomed = [v for isl in islands if len(isl) < limit for v in isl]
        removed = sum(1 for isl in islands if len(isl) < limit)
        bmesh.ops.delete(bm, geom=doomed, context="VERTS")

    bm.to_mesh(me)
    bm.free()
    me.update()

    sizes = sorted((len(i) for i in islands), reverse=True)
    if removed:
        warnings.append("작은 파편 %d개 삭제" % removed)
    return dict(count=len(sizes), removed=removed, largest=sizes[:3], smallest=sizes[-3:])


def _islands(bm):
    bm.verts.ensure_lookup_table()
    seen = set()
    result = []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack = [v]
        seen.add(v.index)
        group = []
        while stack:
            cur = stack.pop()
            group.append(cur)
            for e in cur.link_edges:
                nxt = e.other_vert(cur)
                if nxt.index not in seen:
                    seen.add(nxt.index)
                    stack.append(nxt)
        result.append(group)
    return result


def _decimate(obj, max_tris):
    for _ in range(3):
        tris = triangle_count(obj.data)
        if tris <= max_tris:
            return tris
        mod = obj.modifiers.new("BsDecimate", "DECIMATE")
        mod.decimate_type = "COLLAPSE"
        mod.ratio = max(0.01, (max_tris / tris) * 0.97)
        _apply_modifiers(obj)
    return triangle_count(obj.data)


def _apply_modifiers(obj):
    dg = bpy.context.evaluated_depsgraph_get()
    new_me = bpy.data.meshes.new_from_object(obj.evaluated_get(dg))
    old = obj.data
    obj.modifiers.clear()
    obj.data = new_me
    new_me.name = old.name
    if old.users == 0:
        bpy.data.meshes.remove(old)


def _bbox(me):
    xs = [v.co.x for v in me.vertices]
    ys = [v.co.y for v in me.vertices]
    zs = [v.co.z for v in me.vertices]
    return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))


def _bbox_size(me):
    lo, hi = _bbox(me)
    return (hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2])


def _fit_size(me, size, fit):
    sx, sy, sz = _bbox_size(me)
    extent = sz if fit == "height" else max(sx, sy)
    if extent <= 1e-6:
        return 1.0
    s = size / extent
    me.transform(Matrix.Scale(s, 4))
    return s


def _pivot_to_bottom_center(me):
    lo, hi = _bbox(me)
    cx = (lo[0] + hi[0]) * 0.5
    cy = (lo[1] + hi[1]) * 0.5
    me.transform(Matrix.Translation((-cx, -cy, -lo[2])))


def _clean_materials(obj, name, max_texture):
    me = obj.data

    # 1) 중복 머티리얼(.001 등)을 원본으로 합치기
    for slot in obj.material_slots:
        m = slot.material
        if m and "." in m.name:
            base = bpy.data.materials.get(m.name.rsplit(".", 1)[0])
            if base and base != m:
                slot.material = base

    # 2) 쓰지 않는 슬롯 삭제
    used = {p.material_index for p in me.polygons}
    for i in reversed(range(len(obj.material_slots))):
        if i not in used:
            obj.active_material_index = i
            with bpy.context.temp_override(object=obj, active_object=obj):
                bpy.ops.object.material_slot_remove()
            used = {p.material_index for p in me.polygons}

    # 3) 이름 정리 + 텍스처 해상도 상한
    mats = [s.material for s in obj.material_slots if s.material]
    unique = list(dict.fromkeys(mats))
    mat_names, tex_info = [], []
    done_images = set()
    for i, m in enumerate(unique):
        suffix = "" if len(unique) == 1 else "_%02d" % (i + 1)
        m.name = "M_%s%s" % (name, suffix)
        mat_names.append(m.name)
        if not m.use_nodes or not m.node_tree:
            continue
        for node in m.node_tree.nodes:
            if node.type != "TEX_IMAGE" or not node.image or node.image in done_images:
                continue
            img = node.image
            done_images.add(img)
            role = _texture_role(node)
            img.name = "T_%s%s_%s" % (name, suffix, role)
            w, h = img.size
            if max(w, h) > max_texture:
                f = max_texture / float(max(w, h))
                img.scale(max(1, int(w * f)), max(1, int(h * f)))
            img.pack()
            tex_info.append(dict(name=img.name, size=list(img.size)))
    return mat_names, tex_info


def _texture_role(node):
    for link in node.outputs["Color"].links:
        to = link.to_node
        if to.type == "BSDF_PRINCIPLED":
            return {
                "Base Color": "BaseColor",
                "Metallic": "Metallic",
                "Roughness": "Roughness",
                "Emission Color": "Emission",
                "Alpha": "Alpha",
            }.get(link.to_socket.name, link.to_socket.name.replace(" ", ""))
        if to.type == "NORMAL_MAP":
            return "Normal"
        if to.type in ("SEPARATE_COLOR", "SEPRGB", "SEPARATE_RGB"):
            return "MetallicRoughness"
        if to.type == "EMISSION":
            return "Emission"
    return "Tex"
