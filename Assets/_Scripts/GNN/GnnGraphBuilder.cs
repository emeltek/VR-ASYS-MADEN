using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Etiket kaynağı: SafeZone trigger'ı (Safe) veya düşen taşların zemindeki izdüşümü (Danger).
/// Taş grupları göçük başlayana kadar kapalı olduğu için tehlike bölgesi Bounds olarak tutulur.
/// </summary>
public struct GnnLabelZone
{
    public Collider Collider;  // Safe
    public Bounds Footprint;   // Danger (yalnızca XZ kullanılır)
    public GnnRiskClass Class;

    public bool Contains(Vector3 worldPoint)
    {
        if (Collider != null) return (Collider.ClosestPoint(worldPoint) - worldPoint).sqrMagnitude < 1e-4f;
        return worldPoint.x >= Footprint.min.x && worldPoint.x <= Footprint.max.x &&
               worldPoint.z >= Footprint.min.z && worldPoint.z <= Footprint.max.z;
    }

    public Bounds WorldBounds => Collider != null ? Collider.bounds : Footprint;
}

/// <summary>
/// Maden tünelinin zemininden GNN grafı kurar. Eğitim verisi ve runtime grafı aynı kodla Editor'de
/// üretilir; böylece özellikler birebir aynı hesaplanır.
///
/// Zemin fizik ışınlarıyla bulunur (tünel modüllerinin collider'ı yoksa dışa aktarıcı geçici collider ekler).
/// Tünel zemini neredeyse düz olduğu için alan, başlangıç zemini yüksekliği etrafındaki bir bantta taranır:
/// tünel tavanının üstü ve başka kotlardaki yüzeyler grafa girmez.
///
/// ÖNEMLİ: SafeZone trigger kutuları özellik olarak KULLANILMAZ (trigger'lar fizik sorgularından hariç
/// tutulur). Kutular, direk yakını kuralı ve taş izdüşümleri yalnızca eğitimde etiket kaynağıdır (ComputeLabels).
/// </summary>
public sealed class GnnGraphBuilder
{
    public static readonly string[] FeatureNames =
    {
        "post_distance",       // 0: en yakın dikey ahşap direğe yatay uzaklık (direk dibi = güvenli)
        "overhead_support",    // 1: baş üstündeki ilk yüzey destek kirişi mi (1) yoksa çıplak tavan mı (0)
        "overhead_clearance",  // 2: tavan yüksekliği
        "rock_overhead",       // 3: yukarı atılan ışınlardan kaçı taşa çarpıyor (tavanda gevşek taş)
        "rock_distance",       // 4: en yakın taşa uzaklık
        "min_clearance",       // 5: 8 yönde en yakın duvar / engel
        "mean_clearance",      // 6: 8 yönde ortalama açıklık (tünel genişliği)
        "openness"             // 7: açık yön oranı
    };

    public static readonly string[] ClassNames = { "Safe", "Exposed", "Danger" };
    public static int FeatureCount => FeatureNames.Length;

    private static readonly Vector3[] Dirs8 =
    {
        new Vector3(1, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1),
        new Vector3(0.7071f, 0, 0.7071f), new Vector3(-0.7071f, 0, 0.7071f),
        new Vector3(0.7071f, 0, -0.7071f), new Vector3(-0.7071f, 0, -0.7071f)
    };

    private static readonly Vector2Int[] ForwardNeighbors =
    {
        new Vector2Int(1, 0), new Vector2Int(0, 1), new Vector2Int(1, 1), new Vector2Int(1, -1)
    };

    private readonly GnnGraphSettings settings;
    private readonly float S;
    private readonly HashSet<Collider> ignored = new HashSet<Collider>();
    private readonly List<Bounds> supports = new List<Bounds>();
    private readonly List<Bounds> rocks = new List<Bounds>();
    private readonly HashSet<Collider> supportColliders = new HashSet<Collider>();
    private readonly HashSet<Collider> rockColliders = new HashSet<Collider>();
    private readonly RaycastHit[] hitBuffer = new RaycastHit[64];
    private readonly Collider[] colliderBuffer = new Collider[256];
    private readonly Bounds area;
    private readonly float minWalkableNormalY;
    private readonly Vector3 startPoint;

    private static readonly System.Text.RegularExpressions.Regex LodCopyName =
        new System.Text.RegularExpressions.Regex(@"_lod[1-9]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public GnnGraph Graph { get; } = new GnnGraph();

    /// <summary>
    /// Dışa aktarıcının oluşturduğu gizli geçici collider'lar. FindObjectsOfType gizli (HideAndDontSave) objeleri
    /// döndürmediği için ayrıca verilir; verilmezse ışınlar bu kopyalara çarpar ama destek / taş olarak tanınmaz.
    /// </summary>
    public static readonly List<Collider> AdditionalColliders = new List<Collider>();

    private static IEnumerable<Collider> AllColliders()
    {
        foreach (Collider c in Object.FindObjectsOfType<Collider>()) yield return c;
        foreach (Collider c in AdditionalColliders)
            if (c != null) yield return c;
    }
    public bool HasArea { get; }
    public bool HasStart { get; private set; }
    /// <summary>Başlangıçta zemin bulunamazsa teşhis için: ışının çarptığı yüzeyler.</summary>
    public string StartDiagnostics { get; private set; } = "";

    // Teşhis: hücreleri reddeden collider'lar (gövde boşluğu dolu) ve zemin bulunamayan hücre sayısı
    private readonly Dictionary<string, int> blockers = new Dictionary<string, int>();
    private int noFloorCount;

    /// <summary>Hücreleri en çok reddeden objeler (dışa aktarım penceresi için).</summary>
    public string RejectionSummary(int top)
    {
        var list = new List<KeyValuePair<string, int>>(blockers);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        var sb = new System.Text.StringBuilder($"Zemin bulunamayan hücre: {noFloorCount}. Gövde boşluğunu dolduran en sık objeler:");
        for (int i = 0; i < Mathf.Min(top, list.Count); i++) sb.Append($"\n  {list[i].Value} hücre: {list[i].Key}");
        return sb.ToString();
    }
    public int SupportCount => supports.Count;
    public int RockCount => rocks.Count;

    /// <param name="siteArea">Grafın kurulacağı alan (dünya). Boyutu sıfırsa graf kurulmaz.</param>
    /// <param name="startPoint">Zemin yüksekliğinin referans alınacağı nokta (oyuncu)</param>
    /// <param name="ignoreRoots">Collider'ları ve renderer'ları yok sayılacak objeler (oyuncu)</param>
    public GnnGraphBuilder(GnnGraphSettings settings, Bounds siteArea, Vector3 startPoint, IEnumerable<Transform> ignoreRoots)
    {
        this.settings = settings;
        S = settings.worldScale;
        area = siteArea;
        this.startPoint = startPoint;
        minWalkableNormalY = Mathf.Cos(settings.maxSlope * Mathf.Deg2Rad);
        HasArea = siteArea.size.x > 0f && siteArea.size.z > 0f;

        var roots = new List<Transform>();
        foreach (Transform root in ignoreRoots)
        {
            if (root == null) continue;
            roots.Add(root);
            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
                ignored.Add(c);
        }

        // LOD1 / LOD2 kopyaları: aynı modelin kaba geometrisi tünelin içine taşıp hücreleri "dolu" gösteriyor;
        // asıl model (LOD0) zaten temsil edildiği için yok sayılır
        foreach (Collider c in AllColliders())
        {
            if (LodCopyName.IsMatch(c.name)) ignored.Add(c);
        }

        if (HasArea) CollectSources(roots);

        Graph.FeatureCount = FeatureCount;
        Graph.CellSize = settings.cellSize * S;
        Graph.GridOrigin = new Vector3(area.min.x, area.min.y, area.min.z);
        Graph.GridOffset = settings.gridOffset;
    }

    /// <summary>Grafı birkaç kareye yayarak kurar.</summary>
    public IEnumerator Build(int nodesPerStep)
    {
        if (!HasArea) yield break;
        Physics.SyncTransforms();
        int budget = 0;

        // 1) Düğümler: tünel zemini neredeyse düz olduğu için tüm alan, başlangıç zemini yüksekliği etrafındaki
        //    bantta taranır (adım adım yayılma modüller arası kopuk zeminlerde duruyordu)
        HasStart = FindStartCell(startPoint, out _, out Vector3 startFloor);
        if (!HasStart) { StartDiagnostics = DescribeStart(); yield break; }

        float cs = Graph.CellSize;
        int countX = Mathf.CeilToInt(area.size.x / cs);
        int countZ = Mathf.CeilToInt(area.size.z / cs);
        for (int ix = 0; ix < countX; ix++)
        {
            for (int iz = 0; iz < countZ; iz++)
            {
                var cell = new Vector2Int(ix, iz);
                if (TrySampleGround(CellCenter(cell), startFloor.y, out Vector3 floor) &&
                    Mathf.Abs(floor.y - startFloor.y) <= settings.floorBand * S)
                    AddNode(cell, floor);
                if (++budget >= nodesPerStep * 2) { budget = 0; yield return null; }
            }
        }

        // 2) Kenarlar: arada engel olmayan 8-komşular
        int n = Graph.NodeCount;
        var adjacency = new List<int>[n];
        for (int i = 0; i < n; i++) adjacency[i] = new List<int>(8);
        for (int i = 0; i < n; i++)
        {
            Vector2Int cell = Graph.Cells[i];
            foreach (Vector2Int d in ForwardNeighbors)
            {
                if (!Graph.CellToNode.TryGetValue(cell + d, out int j)) continue;
                if (!IsConnected(Graph.Positions[i], Graph.Positions[j])) continue;
                adjacency[i].Add(j);
                adjacency[j].Add(i);
            }
            if (++budget >= nodesPerStep * 2) { budget = 0; yield return null; }
        }

        // 3) Küçük kopuk parçaları at (kaya / eşya tepeleri, duvar üstleri)
        adjacency = KeepLargeComponents(adjacency, settings.minComponentSize);
        n = Graph.NodeCount;

        Graph.NeighborOffsets = new int[n + 1];
        for (int i = 0; i < n; i++) Graph.NeighborOffsets[i + 1] = Graph.NeighborOffsets[i] + adjacency[i].Count;
        Graph.NeighborIndices = new int[Graph.NeighborOffsets[n]];
        for (int i = 0; i < n; i++) adjacency[i].CopyTo(Graph.NeighborIndices, Graph.NeighborOffsets[i]);

        // 4) Düğüm özellikleri
        int f = FeatureCount;
        Graph.Features = new float[n * f];
        for (int i = 0; i < n; i++)
        {
            ComputeFeatures(Graph.Positions[i], Graph.Features, i * f);
            if (++budget >= nodesPerStep) { budget = 0; yield return null; }
        }
    }

    /// <summary>minSize'dan küçük bağlı parçaları graftan çıkarır; düğüm listeleri ve komşuluk yeniden numaralanır.</summary>
    private List<int>[] KeepLargeComponents(List<int>[] adjacency, int minSize)
    {
        int n = adjacency.Length;
        var remap = new int[n];
        var component = new List<int>();
        var stack = new Stack<int>();
        var seen = new bool[n];
        var keep = new bool[n];
        for (int i = 0; i < n; i++)
        {
            if (seen[i]) continue;
            component.Clear();
            stack.Push(i);
            seen[i] = true;
            while (stack.Count > 0)
            {
                int u = stack.Pop();
                component.Add(u);
                foreach (int v in adjacency[u])
                {
                    if (!seen[v]) { seen[v] = true; stack.Push(v); }
                }
            }
            if (component.Count >= minSize)
                foreach (int u in component) keep[u] = true;
        }

        var positions = new List<Vector3>(Graph.Positions);
        var cells = new List<Vector2Int>(Graph.Cells);
        Graph.Positions.Clear();
        Graph.Cells.Clear();
        Graph.CellToNode.Clear();
        for (int i = 0; i < n; i++)
        {
            remap[i] = -1;
            if (keep[i]) remap[i] = AddNode(cells[i], positions[i]);
        }

        var result = new List<int>[Graph.NodeCount];
        for (int i = 0; i < n; i++)
        {
            if (remap[i] < 0) continue;
            var list = new List<int>(adjacency[i].Count);
            foreach (int j in adjacency[i]) list.Add(remap[j]);
            result[remap[i]] = list;
        }
        return result;
    }

    public GnnGraph BuildImmediate()
    {
        IEnumerator routine = Build(int.MaxValue);
        while (routine.MoveNext()) { }
        return Graph;
    }

    /// <summary>
    /// Noktanın hücresi uygun değilse (dolap, eşya, kiriş) çevresindeki 3 m içindeki hücreler denenir.
    /// </summary>
    private bool FindStartCell(Vector3 point, out Vector2Int start, out Vector3 floor)
    {
        Vector2Int center = Graph.CellOf(point);
        int radius = Mathf.CeilToInt(3f * S / Graph.CellSize);
        for (int r = 0; r <= radius; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                    start = center + new Vector2Int(dx, dz);
                    if (TrySampleGround(CellCenter(start), point.y, out floor)) return true;
                }
            }
        }

        start = center;
        floor = point;
        return false;
    }

    private string DescribeStart()
    {
        var origin = new Vector3(startPoint.x, startPoint.y + 1.5f * S, startPoint.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hitBuffer, 6f * S, settings.layerMask, QueryTriggerInteraction.Ignore);
        if (count == 0) return "Başlangıç noktasının altında hiçbir collider yok.";
        var sb = new System.Text.StringBuilder("Başlangıçtan aşağı ışının çarptıkları:");
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            sb.Append($"\n  {h.collider.name} (y={h.point.y:F2}, normal.y={h.normal.y:F2}{(ignored.Contains(h.collider) ? ", yok sayıldı" : "")})");
        }
        return sb.ToString();
    }

    private int AddNode(Vector2Int cell, Vector3 floor)
    {
        int index = Graph.Positions.Count;
        Graph.CellToNode[cell] = index;
        Graph.Positions.Add(floor);
        Graph.Cells.Add(cell);
        return index;
    }

    private Vector3 CellCenter(Vector2Int cell)
    {
        float cs = Graph.CellSize;
        return new Vector3(
            Graph.GridOrigin.x + (cell.x + 0.5f + settings.gridOffset.x) * cs,
            0f,
            Graph.GridOrigin.z + (cell.y + 0.5f + settings.gridOffset.y) * cs);
    }

    /// <summary>
    /// Komşu hücrenin yüksekliği etrafında zemin arar: o yüksekliğin 1.5 m üstünden aşağı ışın atılır;
    /// yakından uzağa, yürünebilir eğimdeki ve üstünde gövde sığan ilk yüzey alınır (kiriş / eşya üstü atlanır).
    /// </summary>
    private bool TrySampleGround(Vector3 center, float nearHeight, out Vector3 floorPoint)
    {
        floorPoint = center;
        Vector3 origin = new Vector3(center.x, nearHeight + 1.5f * S, center.z);
        int count = Physics.RaycastNonAlloc(origin, Vector3.down, hitBuffer, 3f * S, settings.layerMask, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hitBuffer, 0, count, HitDistanceComparer);

        float r = settings.clearanceRadius * S;
        string blocker = null;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (ignored.Contains(h.collider) || h.normal.y < minWalkableNormalY) continue;

            Vector3 bottom = h.point + Vector3.up * (r + settings.stepOverHeight * S);
            Vector3 head = h.point + Vector3.up * settings.headroomHeight * S;
            if (OverlapCapsule(bottom, head, r) != 0)
            {
                blocker ??= colliderBuffer[0].name;
                continue;
            }

            floorPoint = h.point;
            return true;
        }

        if (blocker == null) noFloorCount++;
        else
        {
            if (blocker.Length > 90) blocker = "…" + blocker.Substring(blocker.Length - 90);
            blockers[blocker] = blockers.TryGetValue(blocker, out int c) ? c + 1 : 1;
        }
        return false;
    }

    private static readonly IComparer<RaycastHit> HitDistanceComparer =
        Comparer<RaycastHit>.Create((a, b) => a.distance.CompareTo(b.distance));

    private void ComputeFeatures(Vector3 fp, float[] output, int offset)
    {
        Vector3 up = Vector3.up;
        float probe = settings.hazardProbeDistance * S;
        Vector3 chest = fp + up * (1.2f * S);
        Vector3 waist = fp + up * (0.8f * S);

        // 0: Dikey direğe yatay uzaklık
        float postProbe = settings.postProbeDistance * S;
        float post = Mathf.Min(postProbe, HorizontalPostDistance(fp));

        // 1-2: Baş üstü: tavan yüksekliği ve ilk yüzey destek kirişi mi
        float upLength = settings.overheadProbeDistance * S;
        float overhead = 1f, overheadSupport = 0f;
        if (NearestHit(fp + up * (1.5f * S), up, upLength, out RaycastHit upHit))
        {
            overhead = upHit.distance / upLength;
            overheadSupport = supportColliders.Contains(upHit.collider) ? 1f : 0f;
        }

        // 3: Tavandaki taşlar: merkez + 4 yöne 0.5 m kaydırılmış yukarı ışınlar
        int rockHits = 0;
        Vector3 head = fp + up * (1.5f * S);
        for (int k = 0; k < 5; k++)
        {
            Vector3 o = k == 0 ? head : head + Dirs8[k - 1] * (0.5f * S);
            if (NearestHit(o, up, upLength, out RaycastHit h) && rockColliders.Contains(h.collider)) rockHits++;
        }

        // 4: Taşa uzaklık
        float rock = probe;
        foreach (Bounds b in rocks) rock = Mathf.Min(rock, Mathf.Sqrt(b.SqrDistance(chest)));

        // 5-7: Tünel genişliği / açıklık
        float clearProbe = settings.clearanceProbeDistance * S;
        float minClear = clearProbe, sumClear = 0f;
        int open = 0;
        foreach (Vector3 dir in Dirs8)
        {
            float d = NearestHit(waist, dir, clearProbe, out RaycastHit h) ? h.distance : clearProbe;
            minClear = Mathf.Min(minClear, d);
            sumClear += d;
            if (d >= clearProbe * 0.5f) open++;
        }

        output[offset + 0] = post / postProbe;
        output[offset + 1] = overheadSupport;
        output[offset + 2] = overhead;
        output[offset + 3] = rockHits / 5f;
        output[offset + 4] = rock / probe;
        output[offset + 5] = minClear / clearProbe;
        output[offset + 6] = sumClear / Dirs8.Length / clearProbe;
        output[offset + 7] = open / (float)Dirs8.Length;
    }

    private bool IsConnected(Vector3 a, Vector3 b)
    {
        if (Mathf.Abs(a.y - b.y) > settings.maxStep * S) return false;
        Vector3 from = a + Vector3.up * (0.5f * S);
        Vector3 delta = b - a;
        return !NearestHit(from, delta.normalized, delta.magnitude, out _);
    }

    private bool NearestHit(Vector3 origin, Vector3 dir, float length, out RaycastHit best)
    {
        int count = Physics.RaycastNonAlloc(origin, dir, hitBuffer, length, settings.layerMask, QueryTriggerInteraction.Ignore);
        best = default;
        bool found = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hitBuffer[i];
            if (ignored.Contains(h.collider)) continue;
            if (!found || h.distance < best.distance) { best = h; found = true; }
        }
        return found;
    }

    private int OverlapCapsule(Vector3 a, Vector3 b, float radius)
    {
        int count = Physics.OverlapCapsuleNonAlloc(a, b, radius, colliderBuffer, settings.layerMask, QueryTriggerInteraction.Ignore);
        int kept = 0;
        for (int i = 0; i < count; i++)
        {
            if (ignored.Contains(colliderBuffer[i])) continue;
            colliderBuffer[kept++] = colliderBuffer[i];
        }
        return kept;
    }

    private void CollectSources(List<Transform> ignoreRoots)
    {
        Bounds searchArea = area;
        searchArea.Expand(new Vector3(2f, 0f, 2f) * settings.hazardProbeDistance * S);

        foreach (Collider c in AllColliders())
        {
            if (c.isTrigger || ignored.Contains(c) || !searchArea.Intersects(c.bounds)) continue;
            if (MatchesAny(c.transform, settings.supportKeywords) || MatchesName(c.transform, settings.extraSupportNames))
            {
                supportColliders.Add(c);
                supports.Add(c.bounds);
            }
            else if (MatchesAny(c.transform, settings.rockKeywords)) { rockColliders.Add(c); rocks.Add(c.bounds); }
        }
    }

    /// <summary>
    /// Objenin ya da üst objelerinin adı listedeki bir adla birebir aynı mı ("(Clone)" ve " (3)" ekleri yok sayılır).
    /// Geçici collider kopyalarının adı "Üst/Alt/Obje" biçiminde tam yol olduğu için her parça ayrı karşılaştırılır.
    /// </summary>
    private static bool MatchesName(Transform start, string[] names)
    {
        for (Transform t = start; t != null; t = t.parent)
        {
            foreach (string part in t.name.Split('/'))
            {
                string baseName = CloneSuffix.Replace(part, "").Trim();
                foreach (string n in names)
                {
                    if (!string.IsNullOrEmpty(n) && string.Equals(baseName, n, System.StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
        }
        return false;
    }

    private static readonly System.Text.RegularExpressions.Regex CloneSuffix =
        new System.Text.RegularExpressions.Regex(@"\(Clone\)|\s*\(\d+\)");

    /// <summary>Objenin kendi adı veya üst objelerinin adı anahtar kelimeyi içeriyor mu.</summary>
    private static bool MatchesAny(Transform start, string[] keywords)
    {
        for (Transform t = start; t != null; t = t.parent)
        {
            string lower = t.name.ToLowerInvariant();
            foreach (string k in keywords)
            {
                if (!string.IsNullOrEmpty(k) && lower.Contains(k.ToLowerInvariant())) return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Etiket kaynakları: SafeZone trigger'ları (Safe) ve verilen taş gruplarının collider'larının
    /// zemindeki izdüşümü (Danger, dangerMargin kadar genişletilmiş). Taş grupları kapalı olabilir.
    /// </summary>
    public static List<GnnLabelZone> FindLabelZones(IEnumerable<GameObject> rockGroups, float dangerMargin)
    {
        var zones = new List<GnnLabelZone>();
        foreach (SafeZone safe in Object.FindObjectsOfType<SafeZone>(true))
        {
            foreach (Collider c in safe.GetComponents<Collider>())
            {
                if (c.isTrigger) zones.Add(new GnnLabelZone { Collider = c, Class = GnnRiskClass.Safe });
            }
        }

        foreach (GameObject group in rockGroups)
        {
            if (group == null) continue;
            foreach (Collider c in group.GetComponentsInChildren<Collider>(true))
            {
                Bounds b = LocalBoundsToWorld(c);
                b.Expand(new Vector3(2f * dangerMargin, 0f, 2f * dangerMargin));
                zones.Add(new GnnLabelZone { Footprint = b, Class = GnnRiskClass.Danger });
            }
        }
        return zones;
    }

    /// <summary>Kapalı collider'ın bounds'u boş döner; boyutu collider tipinden hesaplanır.</summary>
    private static Bounds LocalBoundsToWorld(Collider c)
    {
        Bounds local;
        switch (c)
        {
            case BoxCollider box: local = new Bounds(box.center, box.size); break;
            case SphereCollider sphere: local = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f); break;
            case CapsuleCollider cap: local = new Bounds(cap.center, new Vector3(cap.radius * 2f, cap.height, cap.radius * 2f)); break;
            case MeshCollider mesh when mesh.sharedMesh != null: local = mesh.sharedMesh.bounds; break;
            default: return new Bounds(c.transform.position, Vector3.zero);
        }

        var world = new Bounds(c.transform.TransformPoint(local.center), Vector3.zero);
        Vector3 e = local.extents;
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
            world.Encapsulate(c.transform.TransformPoint(local.center + corner));
        }
        return world;
    }

    /// <summary>SafeZone objelerinden kaç tanesinin trigger collider'ı yok (eksikse o bölge etiketlenmez).</summary>
    public static int SafeZonesWithoutTrigger()
    {
        int missing = 0;
        foreach (SafeZone safe in Object.FindObjectsOfType<SafeZone>(true))
        {
            bool hasTrigger = false;
            foreach (Collider c in safe.GetComponents<Collider>()) hasTrigger |= c.isTrigger;
            if (!hasTrigger) missing++;
        }
        return missing;
    }

    /// <summary>
    /// Eğitim etiketleri: taş izdüşümü Danger; herhangi bir dikey destek direğine postShelterRadius'tan yakın
    /// zemin Safe (uzman kuralı: göçükte ahşap desteğin dibine sığınılır); kalan tünel Exposed.
    /// SafeZone kutuları etikete katılmaz: bazıları koridorun ortasına taşıyordu. Oyunda ise SafeZone içindeki
    /// oyuncu GnnRiskScorer tarafından yine güvende sayılır.
    /// </summary>
    public int[] ComputeLabels(List<GnnLabelZone> zones)
    {
        var labels = new int[Graph.NodeCount];
        float shelter = settings.postShelterRadius * S;
        for (int i = 0; i < Graph.NodeCount; i++)
        {
            Vector3 fp = Graph.Positions[i];
            Vector3 p = fp + Vector3.up * (1f * S);
            int label = HorizontalPostDistance(fp) <= shelter ? (int)GnnRiskClass.Safe : (int)GnnRiskClass.Exposed;
            foreach (GnnLabelZone z in zones)
            {
                if (z.Class == GnnRiskClass.Danger && z.Contains(p)) { label = (int)GnnRiskClass.Danger; break; }
            }
            labels[i] = label;
        }
        return labels;
    }

    /// <summary>
    /// Dikey desteğe yatay uzaklık: diz ve göğüs hizasında 32 yöne yatay ışın atılır, ilk çarptığı şey bir destek
    /// (direk ya da çerçevenin dikey kısmı) ise mesafesi alınır. Kirişler baş üstünde kaldığı için sayılmaz;
    /// tek direk ya da iki direk + kirişten oluşan tek parça çerçeve fark etmez.
    /// </summary>
    private float HorizontalPostDistance(Vector3 fp)
    {
        float probe = settings.postProbeDistance * S;
        float best = float.MaxValue;
        foreach (float h in PostProbeHeights)
        {
            Vector3 origin = fp + Vector3.up * (h * S);
            for (int k = 0; k < PostRayCount; k++)
            {
                float a = k * Mathf.PI * 2f / PostRayCount;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                if (NearestHit(origin, dir, probe, out RaycastHit hit) && supportColliders.Contains(hit.collider))
                    best = Mathf.Min(best, hit.distance);
            }
        }
        return best;
    }

    private static readonly float[] PostProbeHeights = { 0.5f, 1.0f };
    private const int PostRayCount = 32;
}
