using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sınıf indeksleri. Python eğitim betiğiyle (GNN/train_gnn.py) aynı sırada olmalı.
/// </summary>
public enum GnnRiskClass
{
    Safe = 0,     // Güvenli: ahşap destek direğinin yanı (SafeZone)
    Exposed = 1,  // Açık: desteksiz tavan altı
    Danger = 2    // Tehlikeli: göçükte taş düşen bölge
}

/// <summary>
/// Grafın sahneden nasıl çıkarılacağını belirleyen ayarlar. Mesafeler "gerçek metre" cinsindendir,
/// worldScale ile dünya birimine çevrilir (bu sahnede oyuncu ölçeği 1.75 olduğu için varsayılan 1.75).
/// </summary>
[Serializable]
public class GnnGraphSettings
{
    [Tooltip("Sahne ölçeği: 1 gerçek metre kaç dünya birimi (PlayerController scale = 1.75)")]
    public float worldScale = 1.75f;

    [Tooltip("Grid hücre boyu (m). Her yürünebilir hücre grafta bir düğüm olur.")]
    public float cellSize = 0.5f;

    [Tooltip("Alan sınırı: güvenli / tehlikeli bölgelerin ve oyuncunun kapsadığı alan bu kadar (m) genişletilir. Site Area atanmışsa kullanılmaz.")]
    public float siteMargin = 10f;

    [Tooltip("Etiket: düşen taşların zemindeki izdüşümü bu kadar (m) genişletilerek 'Tehlikeli' sayılır")]
    public float dangerMargin = 2f;

    [Tooltip("Taş arama mesafesi (m)")]
    public float hazardProbeDistance = 15f;

    [Tooltip("Dikey destek direğine yatay uzaklığın ölçüldüğü mesafe (m): direk dibi ile koridor ortasını ayırır")]
    public float postProbeDistance = 3f;

    [Tooltip("Etiket: herhangi bir dikey destek direğine bu mesafeden (m) yakın zemin 'Güvenli' sayılır. Dar tünelde koridor ortası direklere 0.6-0.9 m, direk dibi 0.1-0.4 m. 0.6 m: kenarlar geniş, orta büyük ölçüde açık")]
    public float postShelterRadius = 0.6f;

    [Tooltip("Bu sayıdan az düğümlü kopuk parçalar (kaya / eşya tepeleri) graftan atılır")]
    public int minComponentSize = 10;

    [Tooltip("Yatay tarama mesafesi (m): tünel genişliği")]
    public float clearanceProbeDistance = 3f;

    [Tooltip("Yukarı tarama mesafesi (m): tavan, tavan kirişi, tavandaki taşlar")]
    public float overheadProbeDistance = 5f;

    [Tooltip("Hücrenin boş sayılması için kontrol sütununun yarıçapı (m)")]
    public float clearanceRadius = 0.1f;

    [Tooltip("Boşluk kontrolünün zeminden başladığı yükseklik (m): altındaki moloz / tümsek engel sayılmaz")]
    public float stepOverHeight = 0.5f;

    [Tooltip("Boşluk kontrolünün bittiği yükseklik (m). Kemerli tünelde baş hizası daralır, bu yüzden göğüs hizası")]
    public float headroomHeight = 1.2f;

    [Tooltip("Zemin, oyuncunun zemin yüksekliğinden en fazla bu kadar (m) farklı olabilir (moloz / eşya tepesi zemin sayılmaz)")]
    public float floorBand = 1f;

    [Tooltip("Yürünebilir sayılan en dik eğim (derece)")]
    public float maxSlope = 40f;

    [Tooltip("Komşu hücreler arasında en fazla yükseklik farkı (m)")]
    public float maxStep = 0.5f;

    [Tooltip("Ahşap destek (direk / kiriş) olarak kabul edilecek obje / materyal isim anahtar kelimeleri")]
    public string[] supportKeywords = { "wood_el", "timber", "support", "destek", "beam" };

    [Tooltip("Ahşap destek olarak kabul edilecek ek obje adları (birebir; '(Clone)' ve ' (3)' ekleri yok sayılır)")]
    public string[] extraSupportNames = { };

    [Tooltip("Taş olarak kabul edilecek obje / materyal isim anahtar kelimeleri")]
    public string[] rockKeywords = { "rock", "stone", "kaya" };

    public LayerMask layerMask = ~0;

    [Tooltip("Grid başlangıç kaydırması (hücre cinsinden). Eğitim verisi çoğaltmak için kullanılır.")]
    public Vector2 gridOffset = Vector2.zero;

    public GnnGraphSettings Clone() => (GnnGraphSettings)MemberwiseClone();
}

/// <summary>
/// Tünel zemininden çıkarılmış graf: düğüm = yürünebilir grid hücresi,
/// kenar = engelsiz komşu hücreler. Komşuluk CSR formatında tutulur.
/// </summary>
public class GnnGraph
{
    public int FeatureCount;
    public float CellSize;
    public Vector3 GridOrigin;
    public Vector2 GridOffset;

    public readonly List<Vector3> Positions = new List<Vector3>(); // zemin noktası (dünya)
    public readonly List<Vector2Int> Cells = new List<Vector2Int>();
    public readonly Dictionary<Vector2Int, int> CellToNode = new Dictionary<Vector2Int, int>();

    public float[] Features;   // N * FeatureCount
    public int[] Labels;       // N (yalnızca eğitim verisi dışa aktarılırken dolu)

    public int[] NeighborOffsets; // N + 1
    public int[] NeighborIndices; // toplam kenar

    public int NodeCount => Positions.Count;
    public int EdgeCount => NeighborIndices?.Length ?? 0;

    public Vector2Int CellOf(Vector3 worldPosition)
    {
        return new Vector2Int(
            Mathf.FloorToInt((worldPosition.x - GridOrigin.x) / CellSize - GridOffset.x),
            Mathf.FloorToInt((worldPosition.z - GridOrigin.z) / CellSize - GridOffset.y));
    }

    /// <summary>Dışa aktarılmış JSON'dan grafı geri kurar (runtime'da sahne taranmaz).</summary>
    public static GnnGraph FromExport(GnnGraphExport e)
    {
        var g = new GnnGraph
        {
            FeatureCount = e.numFeatures,
            CellSize = e.cellSize,
            GridOrigin = new Vector3(e.gridOrigin[0], e.gridOrigin[1], e.gridOrigin[2]),
            GridOffset = new Vector2(e.gridOffset[0], e.gridOffset[1]),
            Features = e.features,
            Labels = e.labels
        };

        for (int i = 0; i < e.numNodes; i++)
        {
            g.Positions.Add(new Vector3(e.positions[i * 3], e.positions[i * 3 + 1], e.positions[i * 3 + 2]));
            var cell = new Vector2Int(e.cells[i * 2], e.cells[i * 2 + 1]);
            g.Cells.Add(cell);
            g.CellToNode[cell] = i;
        }

        // edgeSrc kaynak düğüme göre sıralı yazılır (bkz. ToExport), CSR doğrudan çıkarılır
        g.NeighborOffsets = new int[e.numNodes + 1];
        foreach (int s in e.edgeSrc) g.NeighborOffsets[s + 1]++;
        for (int i = 0; i < e.numNodes; i++) g.NeighborOffsets[i + 1] += g.NeighborOffsets[i];
        g.NeighborIndices = e.edgeDst;
        return g;
    }

    public GnnGraphExport ToExport(string sceneName, string[] featureNames, string[] classNames, float worldScale)
    {
        int n = NodeCount;
        var export = new GnnGraphExport
        {
            sceneName = sceneName,
            featureNames = featureNames,
            classNames = classNames,
            worldScale = worldScale,
            cellSize = CellSize,
            gridOrigin = new[] { GridOrigin.x, GridOrigin.y, GridOrigin.z },
            gridOffset = new[] { GridOffset.x, GridOffset.y },
            numNodes = n,
            numFeatures = FeatureCount,
            positions = new float[n * 3],
            cells = new int[n * 2],
            features = Features,
            labels = Labels ?? new int[0],
            edgeSrc = new int[EdgeCount],
            edgeDst = new int[EdgeCount]
        };

        for (int i = 0; i < n; i++)
        {
            Vector3 p = Positions[i];
            export.positions[i * 3] = p.x;
            export.positions[i * 3 + 1] = p.y;
            export.positions[i * 3 + 2] = p.z;
            export.cells[i * 2] = Cells[i].x;
            export.cells[i * 2 + 1] = Cells[i].y;

            for (int e = NeighborOffsets[i]; e < NeighborOffsets[i + 1]; e++)
            {
                export.edgeSrc[e] = i;
                export.edgeDst[e] = NeighborIndices[e];
            }
        }

        return export;
    }
}

/// <summary>
/// Dışa aktarılan graf JSON'u. Python eğitimi bunu okur; runtime da aynı dosyadan grafı yükler.
/// </summary>
[Serializable]
public class GnnGraphExport
{
    public string sceneName;
    public string[] featureNames;
    public string[] classNames;
    public float worldScale;
    public float cellSize;
    public float[] gridOrigin; // 3
    public float[] gridOffset; // 2
    public int numNodes;
    public int numFeatures;
    public float[] positions; // N * 3
    public int[] cells;       // N * 2
    public float[] features;  // N * F
    public int[] labels;      // N
    public int[] edgeSrc;
    public int[] edgeDst;
}
