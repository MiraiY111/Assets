#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Alat SEMENTARA (boleh dihapus setelah dipakai). Buka scene Stage1 (tanpa Play), lalu menu
/// Tools > Capture Stage Background: merender kamera utama 1920x1080 (tanpa HUD)
/// ke Assets/Sprites/Stage1_Background.png.
/// </summary>
public static class CaptureStageBackground
{
    const string OutPath = "Assets/Sprites/Stage1_Background.png";

    [MenuItem("Tools/Capture Stage Background")]
    static void Run()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            var all = Object.FindObjectsByType<Camera>();
            if (all.Length > 0) cam = all[0];
        }
        if (cam == null) { Debug.LogError("[Capture] Tidak ada Camera di scene ini."); return; }

        var hidden = new List<Canvas>();
        foreach (var c in Object.FindObjectsByType<Canvas>())
            if (c.isRootCanvas && c.enabled && c.renderMode == RenderMode.ScreenSpaceCamera) { c.enabled = false; hidden.Add(c); }

        const int W = 1920, H = 1080;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;

        cam.targetTexture = rt;
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
        tex.Apply();

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;
        foreach (var c in hidden) if (c != null) c.enabled = true;

        File.WriteAllBytes(OutPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        rt.Release();
        Object.DestroyImmediate(rt);
        AssetDatabase.ImportAsset(OutPath);

        var imp = AssetImporter.GetAtPath(OutPath) as TextureImporter;
        if (imp != null)
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.filterMode = FilterMode.Point;
            imp.mipmapEnabled = false;
            imp.SaveAndReimport();
        }
        Debug.Log($"[Capture] Tersimpan: {OutPath} dari kamera '{cam.name}'");
    }
}
#endif
