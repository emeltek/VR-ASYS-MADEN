# GNN Tabanlı Maden Göçüğü Risk Puanlaması

Sınav sahnesinde kullanıcının durduğu noktanın **güvenli** (ahşap destek direğinin yanı), **açık**
(desteksiz tavan altı) veya **tehlikeli** (göçükte taş düşen bölge) olduğunu eğitilmiş bir **GraphSAGE**
modeli belirler. GNN puanlayıcı yoksa veya model yüklenemezse eski bonus puanlaması (ekipman 40 +
güvenli bölge 40 + süre 20) otomatik devreye girer; eğitim sahnesi eski puanlamayla çalışır.

## Akış

1. **Graf** (`Assets/_Scripts/GNN/GnnGraphBuilder.cs`): Tünel zemini 0.5 m'lik hücrelere bölünür
   (sahne ölçeği 1.75). Tünel modüllerinin çoğunda collider olmadığı için (oyuncu kinematik, NavMesh üzerinde
   yürür; NavMesh de modüller arasında kopuk) dışa aktarım süresince collider'ı olmayan her modele gizli,
   geçici bir MeshCollider eklenir ve graf gerçek geometriden çıkarılır; sonra kopyalar silinir, sahne değişmez.
   Tünel zemini neredeyse düz olduğu için alan, oyuncunun zemin yüksekliği etrafındaki bir bantta taranır;
   aynı modellerin LOD1/LOD2 kopyalarının collider'ları yok sayılır, 10 düğümden küçük kopuk parçalar
   (kaya / eşya tepeleri) atılır.
2. **Düğüm özellikleri** (8 adet, yalnızca geometriden): en yakın dikey desteğe yatay uzaklık (diz ve göğüs hizasında 32 yöne yatay ışınla, 3 m'ye kadar:
   direk dibi / koridor ortası; destekler isimden: `wood_el…`, `timber`, `support` … ve
   `GnnRiskPredictor > Graph Settings > Extra Support Names` listesindeki adlar), baş üstünde destek kirişi
   var mı, tavan yüksekliği, tavanda taş var mı, taşa uzaklık, tünel genişliği (en yakın / ortalama), açık yön oranı.
3. **Etiket**: Herhangi bir dikey destek direğine 0.6 m'den yakın zemin `Safe` (uzman kuralı: göçükte ahşap
   desteğin dibine sığınılır); göçükte düşen taşların (MiningSequence > Rock Object) zemindeki izdüşümü 2 m
   genişletilerek `Danger`; kalan tünel (koridor ortası) `Exposed`. Tasarımcının SafeZone kutuları etikete
   katılmaz (yalnızca 17 direkte vardı ve bazıları koridorun ortasına taşıyordu); oyunda ise SafeZone içindeki
   oyuncu yine güvende sayılır. Raporda Güvenli etiketinin bu kuraldan (0.6 m; dar tünelde koridor ortası direklere 0.6-0.9 m) geldiği belirtilmelidir.
4. **Eğitim** (`GNN/train_gnn.py`): 2 katmanlı GraphSAGE (mean aggregator) + softmax.
5. **Çalışma zamanı**: Graf Editor'de bir kez çıkarılıp `Assets/Resources/GNN/gnn_graph_<sahne>.json`
   olarak kaydedilir; oyunda model sahne açılınca **bir kez** çalışır.

## Puanlama (`GnnRiskScorer`)

Puan 100'den başlar. Toplanma alanına girilince (ya da süre dolunca) puan kesinleşir ve standart risk
etiketi gösterilir (75+ Güvenli, 50–74 Orta Riskli, 25–49 Riskli, 25 altı Yüksek Riskli).

| Kalem | Ceza |
|---|---|
| Kişisel koruyucu donanım | Göçük başladığında 7 parçadan biri eksikse 30; eksikken göçük cezaları × 1.5 |
| Göçük sırasında açıkta kalma (GNN) | `30 × (1 − e^(−E / 10 s))`, `E = ∫ P(Açık) dt` |
| Düşen taşların altı (GNN) | `40 × [0.4 × w_giriş + 0.6 × (1 − e^(−E / 5 s))]` |
| Çök & tut | Göçük bitene kadar güvenli bölgede yapılmadıysa 20 |
| Tahliye | Göçük bittikten sonra 10 sn cezasız, sonra `20 × (1 − e^(−gecikme / 15 s))`; 70 sn'de gidilmezse 20 |

Oyuncu bir SafeZone kutusunun içindeyse model ne derse desin güvende sayılır.

| Senaryo | Puan | Etiket |
|---|---|---|
| Ekipman tam, desteğe sığındı, çömeldi, zamanında tahliye | 100 | Güvenli |
| Ekipman tam, tahliyede 30 sn gecikti | 83 | Güvenli |
| Ekipman tam, düşen taşların altından 1 sn geçti | 82 | Güvenli |
| Ekipman tam, 10 sn açıkta kaldıktan sonra sığındı | 81 | Güvenli |
| Ekipman tam, sığındı ama çömelip tutunmadı | 80 | Güvenli |
| Bir ekipman eksik, geri kalan her şey doğru | 70 | Orta Riskli |
| Ekipman tam, göçük boyunca açıkta kaldı | 52 | Orta Riskli |
| Ekipman tam, düşen taşların altında uzun kaldı | 40 | Riskli |
| Ekipman tam, açıkta kaldı + tahliyede 30 sn gecikti | 35 | Riskli |
| Ekipman eksik, göçük boyunca açıkta kaldı | 9 | Yüksek Riskli |
| Ekipman eksik, düşen taşların altında uzun kaldı | 0 | Yüksek Riskli |

## Kurulum

```bash
# 1) Unity: VR-ASYS-MADEN-SINAV sahnesini aç
#    Hierarchy > Create Empty > adı "GNN" > Add Component: GnnRiskPredictor ve GnnRiskScorer > sahneyi kaydet
#    Tools > GNN > Eğitim Verisini ve Grafı Dışa Aktar (Açık Sahne)

# 2) Eğit
python3 -m venv GNN/.venv && source GNN/.venv/bin/activate
pip install -r GNN/requirements.txt
python3 GNN/train_gnn.py

# 3) Assets/Resources/GNN/gnn_weights.json oluşur → Unity otomatik yükler
```

Görsel kontrol: `GnnRiskPredictor` bileşenine sağ tık → **Grafı kur ve etiketleri göster** /
**Kayıtlı grafla modeli çalıştır** (obje seçiliyken gizmo görünür).

## Bu dalda düzeltilen hatalar

- 18 SafeZone'un her biri ayrı ceza sayacı çalıştırıyordu: güvenli bölge dışında her 3 sn'de -5 yerine -90 kesiliyordu.
  Ceza artık ScoreManager'da tek yerden işliyor.
- Güvenli bölge doluluğu tek bir ortak bool'du: bir bölgeden komşu bölgeye geçerken oyuncu "dışarıda" sayılıyordu.
  Artık sayaçla tutuluyor; göçük başladığında zaten bölgede olan oyuncu da içeride sayılıyor.
- Statik durumlar (başla butonu, göçük, ekipman bayrakları, güvenli bölge) sahne değişince sıfırlanmıyordu:
  eğitimden sınava geçince başlamadan toplanma alanına girmek +20 veriyordu. `StaticStateReset` her sahne yüklenişinde sıfırlıyor;
  toplanma alanı da artık yalnızca göçük bittikten sonra puan veriyor.
- Göçük sırasında açıkta kalmanın ve düşen taşların altında durmanın cezası yoktu; puan küsuratlıydı ve sonuç etiketi yoktu.
- Space tuşuyla göçüğü başlatan test kodu (`SmokeInput`) build'de de çalışıyordu; artık yalnızca Editor'de.
- Boş `CheckHandPozition` script'i kaldırıldı.

Unity'de yapılması gereken: `02_2` objesindeki SafeZone'un collider'ı yok (kaldırın ya da trigger collider ekleyin).
