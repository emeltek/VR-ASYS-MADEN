using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Açık sahneden GNN verisini üretir:
///   - GNN/data/&lt;sahne&gt;_graph_{0..3}.json : etiketli eğitim grafları (grid yarım hücre kaydırılarak 4 kopya)
///   - Assets/Resources/GNN/gnn_graph_&lt;sahne&gt;.json : oyunda kullanılan graf (runtime sahne taramaz)
/// Etiketler SafeZone trigger'larından (Safe) ve göçükte düşen taşların izdüşümünden (Danger) gelir.
///
/// Tünel modüllerinin çoğunda collider yok (oyuncu NavMesh üzerinde yürüyor, NavMesh de parça parça).
/// Dışa aktarım süresince collider'ı olmayan her MeshRenderer'a gizli, geçici bir MeshCollider kopyası
/// eklenir; graf gerçek geometriden (zemin, duvar, tavan) çıkarılır, sonra kopyalar silinir. Sahne değişmez.
/// </summary>
public static class GnnDatasetExporter
{
    private const string ExamScenePath = "Assets/Scenes/VR-ASYS-MADEN-SINAV.unity";

    private static readonly Vector2[] GridOffsets =
    {
        new Vector2(0f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f)
    };

    public static string DataDirectory => Path.Combine(Path.GetDirectoryName(Application.dataPath), "GNN", "data");
    private static string ResourcesDirectory => Path.Combine(Application.dataPath, "Resources", "GNN");

    [MenuItem("Tools/GNN/Eğitim Verisini ve Grafı Dışa Aktar (Açık Sahne)")]
    public static void ExportOpenScene()
    {
        string summary = Export();
        EditorUtility.DisplayDialog("GNN Dışa Aktarım", summary, "Tamam");
    }

    /// <summary>
    /// Komut satırı: Unity -batchmode -quit -projectPath . -executeMethod GnnDatasetExporter.ExportFromCommandLine
    /// </summary>
    public static void ExportFromCommandLine()
    {
        EditorSceneManager.OpenScene(ExamScenePath, OpenSceneMode.Single);
        Debug.Log(Export());
    }

    private static string Export()
    {
        GnnRiskPredictor predictor = Object.FindObjectOfType<GnnRiskPredictor>();
        GameObject temp = null;
        GameObject proxies = null;
        if (predictor == null)
        {
            temp = new GameObject("GNN Exporter (temp)") { hideFlags = HideFlags.HideAndDontSave };
            predictor = temp.AddComponent<GnnRiskPredictor>();
        }

        try
        {
            GnnGraphSettings baseSettings = predictor.GraphSettings;
            List<GnnLabelZone> zones = GnnGraphBuilder.FindLabelZones(predictor.RockGroups(), baseSettings.dangerMargin * baseSettings.worldScale);
            int dangerZones = zones.FindAll(z => z.Class == GnnRiskClass.Danger).Count;
            int safeZones = zones.Count - dangerZones;
            if (dangerZones == 0 || safeZones == 0)
                return $"Etiket kaynağı eksik: {dangerZones} düşen taş (MiningSequence > Rock Object), {safeZones} güvenli bölge (SafeZone trigger'ı).";

            string sceneName = EditorSceneManager.GetActiveScene().name;
            string fileStem = GnnRiskPredictor.GraphResourceName(sceneName).Substring(GnnRiskPredictor.GraphResourcePrefix.Length);
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(ResourcesDirectory);
            foreach (string old in Directory.GetFiles(DataDirectory, fileStem + "_graph_*.json")) File.Delete(old);

            var log = new System.Text.StringBuilder();
            log.AppendLine($"{safeZones} güvenli bölge, {dangerZones} düşen taş parçası bulundu.");
            proxies = CreateGeometryProxies(out int proxyCount);
            log.AppendLine($"Collider'ı olmayan {proxyCount} model için geçici collider oluşturuldu.");
            int missing = GnnGraphBuilder.SafeZonesWithoutTrigger();
            if (missing > 0)
                log.AppendLine($"UYARI: {missing} SafeZone objesinde trigger collider yok, o bölge etiketlenemedi.");

            for (int k = 0; k < GridOffsets.Length; k++)
            {
                GnnGraphSettings settings = predictor.GraphSettings.Clone();
                settings.gridOffset = GridOffsets[k];

                GnnGraphBuilder builder = predictor.CreateBuilder(settings, zones);
                if (!builder.HasArea) return "Alan hesaplanamadı.";
                if (k == 0)
                    log.AppendLine($"İsimden bulunan: destek {builder.SupportCount}, taş {builder.RockCount} parça");

                GnnGraph graph = builder.BuildImmediate();
                if (!builder.HasStart)
                    return "Başlangıç noktasında zemin bulunamadı (Player tag'li PlayerController tünel zemininde olmalı).\n" + builder.StartDiagnostics;
                graph.Labels = builder.ComputeLabels(zones);
                GnnGraphExport export = graph.ToExport(sceneName, GnnGraphBuilder.FeatureNames, GnnGraphBuilder.ClassNames, settings.worldScale);
                string json = JsonUtility.ToJson(export);

                File.WriteAllText(Path.Combine(DataDirectory, $"{fileStem}_graph_{k}.json"), json);
                if (k == 0) File.WriteAllText(Path.Combine(ResourcesDirectory, $"gnn_graph_{fileStem}.json"), json);

                int[] counts = new int[3];
                foreach (int l in graph.Labels) counts[l]++;
                log.AppendLine($"graph_{k}: {graph.NodeCount} düğüm, {graph.EdgeCount / 2} kenar | Güvenli {counts[0]}, Açık {counts[1]}, Tehlikeli {counts[2]}");
                if (k == 0)
                {
                    string summary = builder.RejectionSummary(6);
                    Debug.Log("[GNN] " + summary);
                    log.AppendLine(summary);
                }
            }

            AssetDatabase.Refresh();
            log.AppendLine("Eğitim verisi: " + DataDirectory);
            log.AppendLine($"Oyun grafı: Assets/Resources/GNN/gnn_graph_{fileStem}.json");
            return log.ToString();
        }
        finally
        {
            GnnGraphBuilder.AdditionalColliders.Clear();
            if (proxies != null) Object.DestroyImmediate(proxies);
            if (temp != null) Object.DestroyImmediate(temp);
            Physics.SyncTransforms();
        }
    }

    /// <summary>
    /// Collider'ı olmayan görünür modeller için gizli MeshCollider kopyaları. Kopyanın adı kaynağın tam
    /// hiyerarşi yolu olur; böylece isimden destek / taş tanıma (anahtar kelimeler) kopyalarda da çalışır.
    /// </summary>
    private static GameObject CreateGeometryProxies(out int count)
    {
        count = 0;
        GnnGraphBuilder.AdditionalColliders.Clear();
        var root = new GameObject("GNN Geometry Proxies (temp)") { hideFlags = HideFlags.HideAndDontSave };
        foreach (MeshRenderer r in Object.FindObjectsOfType<MeshRenderer>())
        {
            // Katı (trigger olmayan) collider'ı olmayan her model: SafeZone direklerinde yalnızca trigger var
            if (!r.enabled || HasSolidCollider(r.gameObject) || r.gameObject.CompareTag("Player")) continue;
            if (r.GetComponentInParent<Canvas>() != null || r.GetComponentInParent<GnnRiskPredictor>() != null) continue;
            MeshFilter filter = r.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            if (IsUnderPlayer(r.transform)) continue;

            var go = new GameObject(HierarchyPath(r.transform)) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(root.transform, false);
            go.transform.SetPositionAndRotation(r.transform.position, r.transform.rotation);
            go.transform.localScale = r.transform.lossyScale;
            go.layer = r.gameObject.layer;
            var collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = filter.sharedMesh;
            GnnGraphBuilder.AdditionalColliders.Add(collider);
            count++;
        }
        Physics.SyncTransforms();
        return root;
    }

    private static bool HasSolidCollider(GameObject go)
    {
        foreach (Collider c in go.GetComponents<Collider>())
        {
            if (!c.isTrigger) return true;
        }
        return false;
    }

    private static bool IsUnderPlayer(Transform t)
    {
        for (; t != null; t = t.parent)
        {
            if (t.CompareTag("Player")) return true;
        }
        return false;
    }

    private static string HierarchyPath(Transform t)
    {
        var path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
        return path;
    }
}
