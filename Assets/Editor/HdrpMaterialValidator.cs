#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Batch-mode pass over every material in the project:
///   * reports any material whose shader is missing / resolves to the error shader
///   * reports any material on a non-HDRP shader (informational)
///   * repairs the HDRP keyword state that a text-level conversion cannot set:
///     normal-map, emissive-map, alpha-clip and the transparent blending
///     keywords (_BLENDMODE_ALPHA/_BLENDMODE_ADDITIVE/_BLENDMODE_PREMULTIPLY),
///     which HDRP requires for a material to actually blend as intended.
///
/// Run headless:
///   Unity.exe -batchmode -nographics -projectPath &lt;project&gt; \
///             -executeMethod HdrpMaterialValidator.Run -logFile &lt;log&gt; -quit
/// </summary>
public static class HdrpMaterialValidator
{
    const string ReportPath = "Library/hdrp_material_report.txt";

    public static void Run()
    {
        var report = new StringBuilder();
        var broken = new List<string>();
        var nonHdrp = new List<string>();
        var repaired = new List<string>();
        int total = 0;

        try
        {
            AssetDatabase.Refresh();

            foreach (string guid in AssetDatabase.FindAssets("t:Material"))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // Only touch real material assets inside Assets/. Package-cache
                // assets are immutable, and materials embedded in a model are
                // owned by the model importer.
                if (!path.StartsWith("Assets/") || !path.EndsWith(".mat"))
                    continue;

                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    broken.Add(path + "  (failed to load)");
                    continue;
                }

                total++;
                var shader = mat.shader;
                if (shader == null || shader.name == "Hidden/InternalErrorShader")
                {
                    broken.Add(path + "  (shader missing: " +
                               (shader == null ? "null" : shader.name) + ")");
                    continue;
                }

                bool isHdrp = shader.name.StartsWith("HDRP/") || IsSyntyGraphMaterial(mat);
                if (!isHdrp)
                {
                    nonHdrp.Add(path + "  [" + shader.name + "]");
                    continue;
                }

                int changes = RepairKeywords(mat);
                if (changes > 0)
                {
                    EditorUtility.SetDirty(mat);
                    repaired.Add(path + "  (" + changes + " keyword/queue fixes)");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        catch (Exception e)
        {
            report.AppendLine("EXCEPTION: " + e);
            WriteReport(report);
            Debug.LogError("[HdrpMaterialValidator] " + e);
            EditorApplication.Exit(1);
            return;
        }

        report.AppendLine("materials scanned: " + total);
        report.AppendLine("materials keyword-repaired: " + repaired.Count);
        report.AppendLine("materials with broken/missing shader: " + broken.Count);
        report.AppendLine("materials not on an HDRP shader (informational): " + nonHdrp.Count);
        report.AppendLine();

        AppendSection(report, "BROKEN SHADERS", broken);
        AppendSection(report, "REPAIRED", repaired);
        AppendSection(report, "NOT HDRP", nonHdrp);

        WriteReport(report);
        Debug.Log("[HdrpMaterialValidator]\n" + report);

        EditorApplication.Exit(broken.Count > 0 ? 2 : 0);
    }

    static bool IsSyntyGraphMaterial(Material mat)
    {
        // Synty's own shader graphs resolve to HDRP-compatible shaders; they are
        // identifiable by their HDRP-oriented properties.
        return mat.HasProperty("_SurfaceType") && mat.HasProperty("_BaseColorMap");
    }

    static int RepairKeywords(Material m)
    {
        int changes = 0;

        bool transparent = m.HasProperty("_SurfaceType") && m.GetFloat("_SurfaceType") > 0.5f;
        if (transparent)
        {
            changes += SetKeyword(m, "_SURFACE_TYPE_TRANSPARENT", true);
            changes += SetKeyword(m, "_ENABLE_FOG_ON_TRANSPARENT", true);
            changes += SetKeyword(m, "_DISABLE_SSR_TRANSPARENT", true);

            int blend = m.HasProperty("_BlendMode") ? (int)m.GetFloat("_BlendMode") : 0;
            changes += SetKeyword(m, "_BLENDMODE_ALPHA", blend == 0);
            changes += SetKeyword(m, "_BLENDMODE_ADDITIVE", blend == 1);
            changes += SetKeyword(m, "_BLENDMODE_PREMULTIPLY", blend == 2 || blend == 3 || blend == 4);

            if (m.renderQueue != (int)UnityEngine.Rendering.RenderQueue.Transparent)
            {
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                changes++;
            }
        }

        if (m.HasProperty("_AlphaCutoffEnable"))
        {
            bool cutout = m.GetFloat("_AlphaCutoffEnable") > 0.5f;
            changes += SetKeyword(m, "_ALPHATEST_ON", cutout);
        }

        if (m.HasProperty("_NormalMap"))
        {
            changes += SetKeyword(m, "_NORMALMAP_TANGENT_SPACE", m.GetTexture("_NormalMap") != null);
        }

        if (m.HasProperty("_EmissiveColorMap"))
        {
            changes += SetKeyword(m, "_EMISSIVE_COLOR_MAP", m.GetTexture("_EmissiveColorMap") != null);
        }

        return changes;
    }

    static int SetKeyword(Material m, string keyword, bool on)
    {
        bool has = m.IsKeywordEnabled(keyword);
        if (on && !has) { m.EnableKeyword(keyword); return 1; }
        if (!on && has) { m.DisableKeyword(keyword); return 1; }
        return 0;
    }

    static void AppendSection(StringBuilder sb, string title, List<string> items)
    {
        if (items.Count == 0) return;
        sb.AppendLine("== " + title + " ==");
        foreach (string item in items) sb.AppendLine("  " + item);
        sb.AppendLine();
    }

    static void WriteReport(StringBuilder sb)
    {
        File.WriteAllText(ReportPath, sb.ToString());
    }
}
#endif
