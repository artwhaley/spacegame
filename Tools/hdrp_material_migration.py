#!/usr/bin/env python3
"""Repoint legacy (Built-in) and Synty-graph (Built-in/URP) materials at the HDRP
shaders already used by this project's converted materials.

Targets (same shaders the already-working Synty materials in the scenes use):
  HDRP/Lit   guid 6e4ae4064600d784cac1e41a9e6f2e59
  HDRP/Unlit guid c4edd00ff2db5b24391a4fcb1762e459

Handles three source families:
  1. Unity built-in shaders (Standard -> Lit, particle/sprite family -> Unlit)
  2. Synty's hand-written Built-in shaders (planets, ship rim, triplanar) -> Lit
  3. Synty's own shader graphs (Generic_*, SciFiHorror_*), which only declare
     Built-in + URP targets and therefore render magenta under HDRP -> Lit/Unlit

Per material it swaps m_Shader, renames the texture/colour properties whose
HDRP equivalents differ, and sets the surface/alpha-clip state. Materials
already on an HDRP shader, UI materials and skybox materials are left alone.

Usage:  python Tools/hdrp_material_migration.py [--dry-run]
"""

import argparse
import os
import re
import sys
from collections import Counter

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

LIT_GUID = "6e4ae4064600d784cac1e41a9e6f2e59"
UNLIT_GUID = "c4edd00ff2db5b24391a4fcb1762e459"

# Unity built-in shaders live in a reserved library guid.
BUILTIN_GUID = "0000000000000000f000000000000000"
STANDARD_FID = 46           # Standard (lit) -> HDRP/Lit
SKYBOX_FIDS = {104}         # Skybox/Procedural -> HDRP drives skies from volumes

# Synty's hand-written (Built-in only) shaders shipped inside the packs.
SYNTY_LEGACY_GUIDS = {
    "eaaf78c712eddcb47a6a599d04aea29b",  # SyntyStudios/Planets
    "6c02f021372cf984b95c361a21269d0e",  # SyntyStudios/PlanetsLines
    "9d2e5af2a60ed6b47b96468f458e7d30",  # SyntyStudios/SpaceShip_Rim
    "635c1feff29c4104cbecb3050ed126e3",  # POLYGON/Triplanar
    "461640c605bbae241a38e2763bcaf258",  # SyntyStudios_Triplanar_ObjectSpace
    "031e5f82c47f1ad4a999675926dfca11",  # SyntyStudios_ColourChange
    "8e3a7be9db42981418988bc59e1916cb",  # SyntyStudios_EnvTriplanar
    "2b5804ffd3081d344bed894a653e3014",  # SyntyStudios_Holographic_Sign
    "511e821c788ce374e9310abe9316cdd5",  # SyntyStudios_NoFogUnlit
    "c48a4461fec61fc45a01e7d6a50e520f",  # SyntyStudios_SciFiPlant
    "8eed85a5cc01a114b8f3274bdbb76013",  # SyntyStudios_Triplanar_Worlds
    "b01549df096303345b44bd2d188a2265",  # SyntyStudios_Asteroid
}

# Synty shader graphs: Built-in/URP targets only, so HDRP renders them magenta.
# Lit-looking surfaces:
SYNTY_GRAPH_LIT = {
    "3b44a38ec6f81134ab0f820ac54d6a93",  # Generic_Standard
    "0730dae39bc73f34796280af9875ce14",  # Generic_Basic
    "f3534f26c7b573c45a1346e0634d57fc",  # Generic_Basic_Bloody
    "d79125f9702e62b49acba79746359880",  # Generic_Basic_Specular
    "7e152165942817942a0ce6f06a871016",  # Generic_Basic_Specular_Bloody
    "e17f8fe2503580447a3784d34b316d11",  # Triplanar_Basic
    "19e269a311c45cd4482cf0ac0e694503",  # PolygonShader
    "fdea4239d29733541b44cd6960afefcd",  # PolygonShaderTransparent
    "77e5bdd170fa4a4459dea431aba43e3c",  # SciFiHorror_Decals
    "dfec08fb273e4674bb5398df25a5932c",  # Generic_ParticlesLit
}
# Unlit / emissive surfaces (screens, FX, blinking lights):
SYNTY_GRAPH_UNLIT = {
    "0736e099ec10c9e46b9551b2337d0cc7",  # Generic_ParticlesUnlit
    "5c2ccdfe181d55b42bd5313305f194e4",  # SciFiHorror_Screens
    "972cd3fede1c33342b0f52ad57f47d90",  # SciFiHorror_BlinkingLights
}
# Sky graphs: HDRP drives skies from volume profiles, so leave them alone.
SYNTY_GRAPH_SKY = {
    "3d532bc2d70158948859b7839127e562",  # Skybox_Generic
    "de1d86872962c37429cb628a7de53613",  # SkyDome
}

SHADER_RE = re.compile(r"^(\s*)m_Shader:\s*\{fileID:\s*(-?\d+),\s*guid:\s*([0-9a-f]{32})")
FLOAT_RE = re.compile(r"^\s*-\s*(_\w+):\s*(-?[\d.eE+]+)\s*$")
TEX_ENTRY_RE = re.compile(r"^\s*-\s*(_\w+):\s*$")
TEXTURE_RE = re.compile(r"^\s*m_Texture:\s*\{fileID:\s*(\d+)")
KEYWORDS_RE = re.compile(r"^(\s*m_ShaderKeywords:\s*)(.*)$")

LIT_RENAMES = {
    "_MainTex": "_BaseColorMap",
    "_Color": "_BaseColor",
    "_Glossiness": "_Smoothness",
    "_BumpMap": "_NormalMap",
    "_EmissionMap": "_EmissiveColorMap",
    "_EmissionColor": "_EmissiveColor",
}
UNLIT_RENAMES = {
    "_MainTex": "_UnlitColorMap",
    "_Color": "_UnlitColor",
    "_TintColor": "_UnlitColor",
    "_EmissionMap": "_EmissiveColorMap",
    "_EmissionColor": "_EmissiveColor",
}
ASE_RENAMES = {
    "_PlanetSurfaceColor": "_BaseColorMap",
    "_Texture": "_BaseColorMap",
    "_Emissive": "_EmissiveColorMap",
    "_Detail": "_DetailMap",
}

# Candidate legacy slots for the same HDRP texture, best first.
BASE_CANDIDATES = ["_Albedo_Map", "_BaseMap", "_Base_Map", "_MainTex", "_Texture", "_PlanetSurfaceColor"]
NORMAL_CANDIDATES = ["_Normal_Map", "_BumpMap"]
EMISSION_CANDIDATES = ["_Emission_Map", "_EmissionMap", "_Emissive"]

UI_MARKERS = ("_UseUIAlphaClip", "_StencilComp", "_ColorMask")


def split_lines(text):
    return text.splitlines(keepends=True)


def rename_property(lines, old, new, taken):
    if new in taken:
        return 0
    pattern = re.compile(r"^(\s*-\s*)" + re.escape(old) + r"(:.*)$")
    hits = 0
    for i, line in enumerate(lines):
        m = pattern.match(line.rstrip("\r\n"))
        if m:
            ending = line[len(line.rstrip("\r\n")):]
            lines[i] = f"{m.group(1)}{new}{m.group(2)}{ending}"
            hits += 1
    if hits:
        taken.add(new)
    return hits


def set_scalar(lines, name, value):
    pattern = re.compile(r"^(\s*-\s*)" + re.escape(name) + r"(:\s*)(-?[\d.eE+]+)(.*)$")
    for i, line in enumerate(lines):
        m = pattern.match(line.rstrip("\r\n"))
        if m:
            ending = line[len(line.rstrip("\r\n")):]
            lines[i] = f"{m.group(1)}{name}: {value}{ending}"
            return True
    for i, line in enumerate(lines):
        if line.strip() == "m_Floats:":
            indent = re.match(r"^(\s*)", line).group(1)
            lines.insert(i + 1, f"{indent}- {name}: {value}\n")
            return True
    return False


def add_keywords(line_text, keywords, ending):
    tokens = line_text.split()
    for kw in keywords:
        if kw not in tokens:
            tokens.append(kw)
    return (" " + " ".join(tokens) if tokens else "") + ending


def pick(lines, candidates, assigned, present):
    """First candidate with a texture assigned, else the first that exists."""
    for name in candidates:
        if assigned.get(name, 0):
            return name
    for name in candidates:
        if name in present:
            return name
    return None


def convert(path, dry_run):
    with open(path, "r", encoding="utf-8", newline="") as fh:
        text = fh.read()

    original = text
    lines = split_lines(text)

    shader_guid = None
    shader_fid = None
    shader_line_idx = None
    for i, line in enumerate(lines):
        m = SHADER_RE.match(line.rstrip("\r\n"))
        if m:
            shader_fid = int(m.group(2))
            shader_guid = m.group(3)
            shader_line_idx = i
            break

    if shader_guid is None:
        return None, "no-shader-line"

    is_ase = shader_guid in SYNTY_LEGACY_GUIDS
    is_graph = (shader_guid in SYNTY_GRAPH_LIT or shader_guid in SYNTY_GRAPH_UNLIT
                or shader_guid in SYNTY_GRAPH_SKY)
    if not (is_ase or is_graph or shader_guid == BUILTIN_GUID):
        return None, "already-hdrp-or-unknown"

    if any(marker in text for marker in UI_MARKERS):
        return None, "ui-material-skipped"
    if shader_guid in SYNTY_GRAPH_SKY:
        return None, "skybox-left-alone"
    if shader_guid == BUILTIN_GUID and shader_fid in SKYBOX_FIDS:
        return None, "skybox-left-alone"

    floats = {}
    for line in lines:
        m = FLOAT_RE.match(line.rstrip("\r\n"))
        if m:
            try:
                floats[m.group(1)] = float(m.group(2))
            except ValueError:
                pass

    present = set(floats)
    for line in lines:
        m = re.match(r"^\s*-\s*(_\w+):", line)
        if m:
            present.add(m.group(1))

    assigned = {}
    current = None
    for line in lines:
        m = TEX_ENTRY_RE.match(line.rstrip("\r\n"))
        if m:
            current = m.group(1)
            continue
        m = TEXTURE_RE.match(line.rstrip("\r\n"))
        if m and current:
            assigned[current] = int(m.group(1))

    mode = floats.get("_Mode")
    dst_blend = floats.get("_DstBlend")
    src_blend = floats.get("_SrcBlend")
    keywords_text = ""
    kw_idx = None
    for i, line in enumerate(lines):
        m = KEYWORDS_RE.match(line.rstrip("\r\n"))
        if m:
            keywords_text = m.group(2)
            kw_idx = i
            break

    cutout = (
        mode == 1
        or floats.get("_AlphaClip") == 1
        or floats.get("_BUILTIN_AlphaClip") == 1
        or "_ALPHATEST_ON" in keywords_text
    )
    transparent = (
        (mode is not None and mode >= 2)
        or floats.get("_Surface") == 1
        or "_ALPHABLEND_ON" in keywords_text
        or "_ALPHAPREMULTIPLY_ON" in keywords_text
        or "_SURFACE_TYPE_TRANSPARENT" in keywords_text
    )
    additive = dst_blend == 1

    if is_ase or shader_guid in SYNTY_GRAPH_LIT or shader_fid == STANDARD_FID:
        target = "lit"
    else:
        # particle/sprite/legacy-unlit FX and unlit Synty graphs: keep them
        # unlit so additive effects do not punch opaque quads into the scene
        target = "unlit"
        if not cutout and mode is None and floats.get("_Surface") is None:
            opaque = dst_blend == 0 and src_blend in (None, 1)
            transparent = not opaque
            if not transparent and "_TintColor" in present:
                transparent = True

    if is_graph:
        base_target = "_BaseColorMap" if target == "lit" else "_UnlitColorMap"
        renames = {}
        base = pick(lines, BASE_CANDIDATES, assigned, present)
        if base:
            renames[base] = base_target
        normal = pick(lines, NORMAL_CANDIDATES, assigned, present)
        if normal and normal != base:
            renames[normal] = "_NormalMap"
        emission = pick(lines, EMISSION_CANDIDATES, assigned, present)
        if emission and emission not in (base, normal):
            renames[emission] = "_EmissiveColorMap"
        if "_EmissionColor" in present:
            renames["_EmissionColor"] = "_EmissiveColor"
        if "_BaseColor" not in present and "_Color" in present:
            renames["_Color"] = "_BaseColor" if target == "lit" else "_UnlitColor"
    else:
        renames = dict(LIT_RENAMES if target == "lit" else UNLIT_RENAMES)
        if is_ase:
            renames = dict(ASE_RENAMES)
            renames.update({"_Color": "_BaseColor", "_Glossiness": "_Smoothness"})

    renamed = 0
    for old, new in renames.items():
        renamed += rename_property(lines, old, new, present)

    ending = lines[shader_line_idx][len(lines[shader_line_idx].rstrip("\r\n")):]
    guid = LIT_GUID if target == "lit" else UNLIT_GUID
    lines[shader_line_idx] = f"  m_Shader: {{fileID: 4800000, guid: {guid}, type: 3}}{ending}"

    new_keywords = []
    if transparent:
        new_keywords.append("_SURFACE_TYPE_TRANSPARENT")
        set_scalar(lines, "_SurfaceType", 1)
        set_scalar(lines, "_BlendMode", 1 if additive else 0)
        set_scalar(lines, "_EnableFogOnTransparent", 1)
    if cutout:
        new_keywords.append("_ALPHATEST_ON")
        set_scalar(lines, "_AlphaCutoffEnable", 1)
        cutoff = floats.get("_Alpha_Clip_Threshold", floats.get("_Cutoff", 0.5))
        set_scalar(lines, "_AlphaCutoff", cutoff)

    if new_keywords and kw_idx is not None:
        line = lines[kw_idx]
        line_ending = line[len(line.rstrip("\r\n")):]
        body = KEYWORDS_RE.match(line.rstrip("\r\n")).group(2)
        lines[kw_idx] = KEYWORDS_RE.match(line.rstrip("\r\n")).group(1) + add_keywords(body, new_keywords, line_ending)

    if transparent:
        for i, line in enumerate(lines):
            if line.strip().startswith("m_CustomRenderQueue:"):
                line_ending = line[len(line.rstrip("\r\n")):]
                lines[i] = f"  m_CustomRenderQueue: 3000{line_ending}"
                break

    text = "".join(lines)
    if text == original:
        return None, "no-change"

    if not dry_run:
        with open(path, "w", encoding="utf-8", newline="") as fh:
            fh.write(text)

    kind = target
    if transparent:
        kind += "/additive" if additive else "/transparent"
    elif cutout:
        kind += "/cutout"
    else:
        kind += "/opaque"
    return kind, f"renamed {renamed} properties"


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--dry-run", action="store_true")
    args = parser.parse_args()

    counts = Counter()
    assets = os.path.join(PROJECT_ROOT, "Assets")
    skipped_ui = []
    skipped_sky = []

    for root, _dirs, files in os.walk(assets):
        for name in sorted(files):
            if not name.endswith(".mat"):
                continue
            path = os.path.join(root, name)
            kind, note = convert(path, args.dry_run)
            rel = os.path.relpath(path, PROJECT_ROOT)
            if kind is None:
                counts[note] += 1
                if note == "ui-material-skipped":
                    skipped_ui.append(rel)
                if note == "skybox-left-alone":
                    skipped_sky.append(rel)
                continue
            counts[kind] += 1

    print(f"{'DRY RUN - ' if args.dry_run else ''}materials rewritten: {sum(v for k, v in counts.items() if '/' in k)}")
    for kind, n in sorted(counts.items()):
        print(f"  {kind}: {n}")
    if skipped_ui:
        print("\nleft alone (UI):")
        for rel in skipped_ui:
            print("  " + rel)
    if skipped_sky:
        print("\nleft alone (skybox):")
        for rel in skipped_sky:
            print("  " + rel)
    return 0


if __name__ == "__main__":
    sys.exit(main())
