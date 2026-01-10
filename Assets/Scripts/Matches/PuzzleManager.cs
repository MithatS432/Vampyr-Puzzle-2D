using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;

public class PuzzleManager : MonoBehaviour
{
    // ===================== GRID =====================
    [Header("Grid Settings")]
    public int width = 5;
    public int height = 5;
    public float tileSpacing = 1f;

    [Header("Grid Position Offset")]
    public float gridYOffset = -1.0f;

    [Header("Normal Tile Prefabs")]
    public GameObject redPrefab;
    public GameObject yellowPrefab;
    public GameObject greenPrefab;
    public GameObject bluePrefab;
    public GameObject bloodDropPrefab;
    public GameObject batPrefab;

    Tile[,] grid;

    // ===================== MOVE & TARGET =====================
    [Header("Move & Target System")]
    public int totalMoves = 20;
    public int targetCount = 20; // 20–100 arası

    public TMP_Text movesText;
    public TMP_Text targetText;

    // ===================== BLOOD SYSTEM =====================
    [Header("Blood System")]
    public Image bloodFillImage;

    [Range(0f, 1f)]
    public float currentBlood = 0.5f;

    public float bloodIncreasePerSecond = 0.02f;
    public float bloodDecreasePerMatch = 0.03f;

    [Header("Vampyr Visuals")]
    public Image vampyrImage;

    public Sprite vampyrNormal;   // 0–33
    public Sprite vampyrMedium;   // 33–66
    public Sprite vampyrHungry;   // 66–100

    // ===================== END GAME =====================
    [Header("End Game")]
    public Animator loseGameAnimator;
    public Animator winGameAnimator;
    public GameObject winGamePanel;
    public GameObject loseGamePanel;
    public AudioClip winSound;
    public AudioClip loseSound;
    public GameObject winVFX;
    public GameObject loseVFX;
    public Transform winvfxSpawnPoint;
    public Transform losevfxSpawnPoint;

    private bool gameEnded = false;

    // ==================================================

    [Header("Match System")]
    public ParticleEffectManager particleEffectManager;

    private Tile selectedTile = null;
    private bool isDragging = false;
    private bool boardBusy = false;
    public AudioClip matchSound;
    public AudioClip bloodDropSound;
    public AudioClip batSound;
    Vector2Int dragDirection = Vector2Int.zero;
    bool directionLocked = false;
    private bool shouldCreateSpecialTile = false;
    private int specialTileX, specialTileY;
    private int matchLengthForSpecial = 0;
    private Vector2 dragStartPos;
    private bool isResolvingBoard = false;
    private bool shouldCreateBat = false;
    private int batTileX, batTileY;

    [Header("Performance")]
    public bool enableFastAnimations = true;
    public float fastDropDuration = 0.1f;
    public float normalDropDuration = 0.2f;

    private void Awake()
    {
        Physics2D.queriesHitTriggers = true;
    }

    void Start()
    {
        targetCount = Random.Range(100, 201);
        totalMoves = 15;

        currentBlood = 0.1f;
        gameEnded = false;

        CreateGrid();

        isResolvingBoard = true;
        StartCoroutine(ResolveBoardBeforePlay());

        UpdateUI();
        UpdateBloodUI();
    }

    void Update()
    {
        if (gameEnded || boardBusy || isResolvingBoard) return;
        IncreaseBloodOverTime();
        HandleMouseDrag();
    }

    void IncreaseBloodOverTime()
    {
        currentBlood += bloodIncreasePerSecond * Time.deltaTime;
        currentBlood = Mathf.Clamp01(currentBlood);

        UpdateBloodUI();
        CheckGameState();
    }

    IEnumerator ResolveBoardBeforePlay()
    {
        yield return new WaitForSeconds(0.1f);

        bool foundMatch = true;

        while (foundMatch)
        {
            List<Tile> matches = FindAllMatches();

            if (matches.Count > 0)
            {
                yield return StartCoroutine(HandleMatches(matches));
                yield return StartCoroutine(DropTiles());
                yield return StartCoroutine(RefillTiles());
            }
            else
            {
                foundMatch = false;
            }
        }

        isResolvingBoard = false;
        Debug.Log("[Init] Oyun hazır!");
    }

    void HandleMouseDrag()
    {
        if (gameEnded || boardBusy || isResolvingBoard) return;

        // Mouse basıldı
        if (Input.GetMouseButtonDown(0))
        {
            Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(worldPos);

            if (hit == null) return;

            Tile tile = hit.GetComponent<Tile>();
            if (tile == null) return;

            selectedTile = tile;
            dragStartPos = worldPos;
            isDragging = true;
            directionLocked = false;
            dragDirection = Vector2Int.zero;

            Debug.Log($"[Drag] Tile seçildi: ({tile.x},{tile.y}) - Tip: {tile.tileType}");
        }

        // Drag devam ediyor
        if (isDragging && selectedTile != null)
        {
            Vector2 currentMousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 delta = currentMousePos - dragStartPos;

            // Yön henüz kilitlenmemişse
            if (!directionLocked)
            {
                // Minimum drag mesafesi kontrolü
                if (Mathf.Abs(delta.x) < 0.3f && Mathf.Abs(delta.y) < 0.3f)
                    return;

                // Yönü belirle (yatay veya dikey)
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                {
                    dragDirection = new Vector2Int(delta.x > 0 ? 1 : -1, 0);
                }
                else
                {
                    dragDirection = new Vector2Int(0, delta.y > 0 ? 1 : -1);
                }

                directionLocked = true;

                // Hedef tile'ı bul
                // Hedef tile'ı bul
                Tile targetTile = GetTileAt(
                    selectedTile.x + dragDirection.x,
                    selectedTile.y + dragDirection.y
                );

                if (targetTile != null)
                {
                    Debug.Log($"[DragDebug] sel=({selectedTile.x},{selectedTile.y}) isBat:{selectedTile.isBat} isBlood:{selectedTile.isBloodDrop} isSpecial:{selectedTile.isSpecial} | tgt=({targetTile.x},{targetTile.y}) isBat:{targetTile.isBat} isBlood:{targetTile.isBloodDrop} isSpecial:{targetTile.isSpecial} type:{targetTile.tileType}");

                    // 1) BloodDrop + BloodDrop
                    if (selectedTile.isBloodDrop && targetTile.isBloodDrop)
                    {
                        StartCoroutine(CombineTwoBloodDrops(selectedTile, targetTile));
                    }
                    // 2) Bat + Bat
                    else if (selectedTile.isBat && targetTile.isBat)
                    {
                        StartCoroutine(CombineTwoBats(selectedTile, targetTile));
                    }
                    // 3) Bat + BloodDrop (her iki yön)
                    else if (selectedTile.isBat && targetTile.isBloodDrop)
                    {
                        StartCoroutine(ActivateBatWithBloodDrop(selectedTile, targetTile));
                    }
                    else if (selectedTile.isBloodDrop && targetTile.isBat)
                    {
                        StartCoroutine(ActivateBatWithBloodDrop(targetTile, selectedTile));
                    }
                    // 4) Bat + Normal Tile (eklenen kısım)
                    else if (selectedTile.isBat)
                    {
                        StartCoroutine(ActivateBatWithTile(selectedTile, targetTile));
                    }
                    else if (targetTile.isBat)
                    {
                        StartCoroutine(ActivateBatWithTile(targetTile, selectedTile));
                    }
                    // 5) BloodDrop + Normal Tile
                    else if (selectedTile.isBloodDrop)
                    {
                        StartCoroutine(ActivateBloodDropWithTile(selectedTile, targetTile));
                    }
                    else if (targetTile.isBloodDrop)
                    {
                        StartCoroutine(ActivateBloodDropWithTile(targetTile, selectedTile));
                    }
                    // 6) Normal swap
                    else
                    {
                        StartCoroutine(TrySwapSafe(selectedTile, targetTile));
                    }
                }
                // Drag'i bitir
                isDragging = false;
                selectedTile = null;
            }
        }

        // Mouse bırakıldı
        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            selectedTile = null;
            directionLocked = false;
        }
    }

    IEnumerator ActivateBatWithBloodDrop(Tile bat, Tile bloodDrop)
    {
        if (bat == null || bloodDrop == null) yield break;
        if (!bat.isBat || !bloodDrop.isBloodDrop) yield break;
        if (boardBusy) yield break;

        boardBusy = true;

        Debug.Log($"[Bat+BloodDrop] Aktifleştiriliyor: Yarasa ({bat.x},{bat.y}) + KanDamla ({bloodDrop.x},{bloodDrop.y})");

        // 1) Hamle azalt
        totalMoves--;
        UpdateUI();

        // 2) Ses & Başlangıç efektleri
        if (batSound != null)
            AudioSource.PlayClipAtPoint(batSound, Camera.main.transform.position, 0.8f);

        if (particleEffectManager != null)
            particleEffectManager.PlayEffect(TileType.Bat, bat.transform.position);

        // 3) Grid'den yarasayı ve kan damlasını kaldır (önce grid'e null koy)
        int batX = bat.x, batY = bat.y;
        int bdX = bloodDrop.x, bdY = bloodDrop.y;

        if (batX >= 0 && batX < width && batY >= 0 && batY < height)
            grid[batX, batY] = null;

        if (bdX >= 0 && bdX < width && bdY >= 0 && bdY < height)
            grid[bdX, bdY] = null;

        // 4) GameObject'leri yok et
        Destroy(bat.gameObject);
        Destroy(bloodDrop.gameObject);

        // Kısa bekleme (animasyon hissi)
        yield return new WaitForSeconds(0.08f);

        // 5) Grid'deki NORMAL tile'ları topla (special / blooddrop / bat olmayanlar)
        List<Tile> normalTiles = new List<Tile>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile t = grid[x, y];
                if (t != null && !t.isSpecial && !t.isBloodDrop && !t.isBat)
                    normalTiles.Add(t);
            }
        }

        // 6) Karıştır (Fisher-Yates)
        for (int i = 0; i < normalTiles.Count; i++)
        {
            int j = Random.Range(i, normalTiles.Count);
            Tile tmp = normalTiles[i];
            normalTiles[i] = normalTiles[j];
            normalTiles[j] = tmp;
        }

        // 7) En fazla 3 tane seçip dönüştür
        int convertCount = Mathf.Min(3, normalTiles.Count);

        for (int i = 0; i < convertCount; i++)
        {
            Tile t = normalTiles[i];
            if (t == null) continue;

            int tx = t.x;
            int ty = t.y;
            Vector3 spawnPos = t.transform.position;
            TileType oldColor = t.tileType;

            // Grid'den kaldır ve obje yok et
            if (tx >= 0 && tx < width && ty >= 0 && ty < height)
                grid[tx, ty] = null;

            Destroy(t.gameObject);

            // Kısa gecikme ederek dönüşüm animasyonu hissi ver
            yield return new WaitForSeconds(0.06f);

            // Yeni Kan Damlası oluştur
            GameObject obj = Instantiate(bloodDropPrefab, spawnPos, Quaternion.identity, transform);
            Tile newBlood = obj.GetComponent<Tile>();
            newBlood.x = tx;
            newBlood.y = ty;
            newBlood.tileType = TileType.BloodDrop;
            newBlood.isSpecial = true;
            newBlood.isBloodDrop = true;
            newBlood.isBat = false;
            newBlood.bloodDropColor = oldColor;
            newBlood.UpdateBloodDropVisual();

            // Grid'e yerleştir
            grid[tx, ty] = newBlood;

            // Ses / partikül
            if (bloodDropSound != null)
                AudioSource.PlayClipAtPoint(bloodDropSound, Camera.main.transform.position, 0.6f);

            if (particleEffectManager != null)
                particleEffectManager.PlayEffect(TileType.BloodDrop, spawnPos);
        }

        // 8) Üstte kalan taşların düşmesini sağla
        yield return StartCoroutine(DropTiles());

        // 9) Boş kalan yerleri doldur
        yield return StartCoroutine(RefillTiles());

        // 10) Yeni match kontrolü ve çözüm
        List<Tile> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
            yield return StartCoroutine(ResolveBoard());

        // 11) Opsiyonel: bir hamle sonucu olarak puan/görev güncellemesi
        OnMoveResolved(1);

        boardBusy = false;
        CheckGameState();
    }


    IEnumerator CombineTwoBats(Tile bat1, Tile bat2)
    {
        if (bat1 == null || bat2 == null || !bat1.isBat || !bat2.isBat) yield break;
        if (boardBusy) yield break;

        boardBusy = true;

        Debug.Log($"[Bat Combo] Başlatılıyor: ({bat1.x},{bat1.y}) + ({bat2.x},{bat2.y})");

        // Hamle sayısı
        totalMoves--;
        UpdateUI();

        // Ses ve efekt (başlangıç)
        if (batSound != null)
            AudioSource.PlayClipAtPoint(batSound, Camera.main.transform.position, 1f);

        Vector3 centerPos = (bat1.transform.position + bat2.transform.position) / 2f;

        // Grid'den iki yarasayı kaldır
        grid[bat1.x, bat1.y] = null;
        grid[bat2.x, bat2.y] = null;

        Destroy(bat1.gameObject);
        Destroy(bat2.gameObject);

        yield return new WaitForSeconds(0.08f);

        // Hedef adaylarını topla (normal, special olmayan tile'lar)
        List<Tile> candidates = new List<Tile>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile t = grid[x, y];
                if (t != null && !t.isSpecial && !t.isBloodDrop && !t.isBat)
                    candidates.Add(t);
            }
        }

        // Karıştır (Fisher-Yates)
        for (int i = 0; i < candidates.Count; i++)
        {
            int j = Random.Range(i, candidates.Count);
            Tile tmp = candidates[i];
            candidates[i] = candidates[j];
            candidates[j] = tmp;
        }

        int spawnCount = Mathf.Min(10, candidates.Count);

        List<Coroutine> moveRoutines = new List<Coroutine>();
        int destroyedCount = 0;

        // Spawn ve gönder
        for (int i = 0; i < spawnCount; i++)
        {
            Tile target = candidates[i];
            if (target == null) continue;

            // Görsel yarasa projesili (sahip tile Component'ı olsa bile grid'e eklemiyoruz)
            GameObject proj = Instantiate(batPrefab, centerPos, Quaternion.identity, transform);

            // Eğer prefab içinde Tile component varsa, etkileşim olmasın diye flag'lerini temizleyin
            Tile projTile = proj.GetComponent<Tile>();
            if (projTile != null)
            {
                projTile.isBat = false;
                projTile.isSpecial = false;
                projTile.isBloodDrop = false;
            }

            // Hedef pozisyon ve süre
            Vector3 targetPos = target.transform.position;
            float duration = 0.25f + Random.Range(0f, 0.18f);

            // Küçük gecikme ile stagger
            yield return new WaitForSeconds(0.03f);

            Coroutine c = StartCoroutine(MoveProjectileAndDestroyTarget(proj, target, targetPos, duration, () => { destroyedCount++; }));
            moveRoutines.Add(c);
        }

        // Büyük combo efekti (orta noktada)
        if (particleEffectManager != null)
            particleEffectManager.PlayEffect(TileType.Bat, centerPos);

        // Bekle tüm projelerin bitmesini
        foreach (Coroutine r in moveRoutines)
            yield return r;

        // Eğer hiç hedef yoksa küçük bir bekleme
        yield return new WaitForSeconds(0.08f);

        // Skor/target güncellemesi
        if (destroyedCount > 0)
        {
            OnMoveResolved(destroyedCount);
        }
        else
        {
            // Hiçbir şey yoksa yine 1 hamle sayısı azalmıştı, OnMoveResolved çağrısı yapabiliriz (isteğe bağlı).
            OnMoveResolved(1);
        }

        // Düşme ve doldurma
        yield return StartCoroutine(DropTiles());
        yield return StartCoroutine(RefillTiles());

        // Yeni maç kontrolü
        List<Tile> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
            yield return StartCoroutine(ResolveBoard());

        boardBusy = false;
        CheckGameState();
    }

    IEnumerator MoveProjectileAndDestroyTarget(GameObject proj, Tile target, Vector3 targetPos, float duration, System.Action onArrive)
    {
        if (proj == null) yield break;

        // Hareket
        yield return StartCoroutine(SmoothMove(proj.transform, targetPos, duration));

        // Hedef var ise yok et ve efekt çal
        if (target != null)
        {
            int tx = target.x;
            int ty = target.y;

            // Grid'den kaldır
            if (tx >= 0 && tx < width && ty >= 0 && ty < height && grid[tx, ty] == target)
                grid[tx, ty] = null;

            // Ses & partikül
            if (matchSound != null)
                AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position, 0.7f);

            if (particleEffectManager != null)
                particleEffectManager.PlayEffect(target.tileType, targetPos);

            Destroy(target.gameObject);
            onArrive?.Invoke();
        }

        // Projeyi temizle
        Destroy(proj);

        yield return null;
    }


    IEnumerator ActivateBatWithTile(Tile bat, Tile targetTile)
    {
        if (bat == null || !bat.isBat || boardBusy) yield break;
        if (targetTile == null) yield break;

        boardBusy = true;

        Debug.Log($"[Bat] Aktifleştiriliyor: Yarasa ({bat.x},{bat.y}) + Hedef Tile ({targetTile.x},{targetTile.y})");

        // 1. Grid'den Yarasa'yı kaldır
        grid[bat.x, bat.y] = null;

        // 2. YARASA SESİ
        if (batSound != null)
        {
            AudioSource.PlayClipAtPoint(batSound, Camera.main.transform.position, 0.8f);
        }

        // 3. YARASA PARTICLE EFEKTİ
        if (particleEffectManager != null)
        {
            particleEffectManager.PlayEffect(TileType.Bat, bat.transform.position);
        }

        // 4. Yarasa'yı yok et
        Destroy(bat.gameObject);

        yield return new WaitForSeconds(0.1f);

        // 5. HEDEF TILE KAN DAMLASINA DÖNÜŞSÜN
        if (targetTile != null && !targetTile.isSpecial)
        {
            int targetX = targetTile.x;
            int targetY = targetTile.y;
            Vector3 targetPos = targetTile.transform.position;
            TileType targetColor = targetTile.tileType;

            // Hedef tile'ı yok et
            grid[targetX, targetY] = null;
            Destroy(targetTile.gameObject);

            // Yeni Kan Damlası oluştur
            GameObject bloodDropObj = Instantiate(bloodDropPrefab, targetPos, Quaternion.identity, transform);
            Tile bloodDrop = bloodDropObj.GetComponent<Tile>();
            bloodDrop.x = targetX;
            bloodDrop.y = targetY;
            bloodDrop.tileType = TileType.BloodDrop;
            bloodDrop.isSpecial = true;
            bloodDrop.isBloodDrop = true;
            bloodDrop.isVampire = false;
            bloodDrop.isBat = false;
            bloodDrop.bloodDropColor = targetColor;

            // Görseli güncelle
            bloodDrop.UpdateBloodDropVisual();

            // Grid'e yerleştir
            grid[targetX, targetY] = bloodDrop;

            Debug.Log($"[Bat] Tile Kan Damlası'na dönüştürüldü");

            // 6. KAN DAMLASI OLUŞUM SESİ
            if (bloodDropSound != null)
            {
                AudioSource.PlayClipAtPoint(bloodDropSound, Camera.main.transform.position, 0.6f);
            }

            // Particle efekti
            if (particleEffectManager != null)
            {
                particleEffectManager.PlayEffect(TileType.BloodDrop, targetPos);
            }
        }

        // 7. DÜŞME VE DOLDURMA
        yield return StartCoroutine(DropTiles());
        yield return StartCoroutine(RefillTiles());

        // 8. YENİ MATCH KONTROLÜ
        List<Tile> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
        {
            yield return StartCoroutine(ResolveBoard());
        }

        // 9. HAMLE SAY
        totalMoves--;
        UpdateUI();

        // 10. SKOR GÜNCELLE
        OnMoveResolved(1);

        boardBusy = false;
        CheckGameState();
    }

    Tile GetTileAt(int x, int y)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
            return grid[x, y];

        return null;
    }

    IEnumerator ActivateBloodDropWithTile(Tile bloodDrop, Tile neighborTile)
    {
        if (bloodDrop == null || !bloodDrop.isBloodDrop || boardBusy) yield break;
        if (neighborTile == null || neighborTile.isSpecial) yield break;

        boardBusy = true;

        Debug.Log($"[BloodDrop+] Aktifleştiriliyor: Kan Damlası ({bloodDrop.x},{bloodDrop.y}) + Tile ({neighborTile.x},{neighborTile.y}) - Renk: {neighborTile.tileType}");

        // 1. Komşu tile'ın rengini al
        TileType colorToClear = neighborTile.tileType;
        bloodDrop.bloodDropColor = colorToClear;

        // Görseli güncelle
        bloodDrop.UpdateBloodDropVisual();

        // 2. Grid'den Kan Damlası'nı kaldır
        grid[bloodDrop.x, bloodDrop.y] = null;

        // 3. KAN DAMLASI ÖZEL SESİ
        if (bloodDropSound != null)
        {
            AudioSource.PlayClipAtPoint(bloodDropSound, Camera.main.transform.position, 0.8f);
        }

        // 4. Efekt
        if (particleEffectManager != null)
        {
            particleEffectManager.PlayEffect(TileType.BloodDrop, bloodDrop.transform.position);
        }

        // 5. Kan Damlası'nı yok et
        Destroy(bloodDrop.gameObject);

        yield return new WaitForSeconds(0.1f);

        // 6. O RENKTEKİ TÜM NORMAL TILE'LARI BUL VE PATLAT
        List<Tile> tilesToDestroy = new List<Tile>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile tile = grid[x, y];

                if (tile != null &&
                    !tile.isSpecial &&  // Normal tile
                    !tile.isBloodDrop && // Kan Damlası değil
                    tile.tileType == colorToClear) // Aynı renk
                {
                    tilesToDestroy.Add(tile);
                }
            }
        }

        Debug.Log($"[BloodDrop+] {tilesToDestroy.Count} adet {colorToClear} rengi tile patlatılacak");

        // 7. PATLAT
        if (tilesToDestroy.Count > 0)
        {
            // Grid'den sil
            foreach (Tile tile in tilesToDestroy)
            {
                if (tile != null)
                {
                    grid[tile.x, tile.y] = null;
                }
            }

            // NORMAL MATCH SESİ
            if (matchSound != null)
            {
                AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position, 0.5f);
            }

            // Efektler
            foreach (Tile tile in tilesToDestroy)
            {
                if (tile != null && particleEffectManager != null)
                {
                    particleEffectManager.PlayEffect(tile.tileType, tile.transform.position);
                }
            }

            yield return new WaitForSeconds(0.15f);

            // Yok et
            foreach (Tile tile in tilesToDestroy)
            {
                if (tile != null)
                {
                    Destroy(tile.gameObject);
                }
            }
        }

        // 8. DÜŞME VE DOLDURMA
        yield return StartCoroutine(DropTiles());
        yield return StartCoroutine(RefillTiles());

        // 9. YENİ MATCH VAR MI KONTROL ET
        List<Tile> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
        {
            yield return StartCoroutine(ResolveBoard());
        }

        // 10. HAMLE SAY
        totalMoves--;
        UpdateUI();

        // 11. SKOR GÜNCELLE
        OnMoveResolved(tilesToDestroy.Count > 0 ? tilesToDestroy.Count : 1);

        boardBusy = false;
        CheckGameState();
    }
    IEnumerator TrySwapSafe(Tile a, Tile b)
    {
        if (gameEnded || isResolvingBoard)
            yield break;

        // === 1. ÖNCE POSITION'LARI KAYDET ===
        Vector3 posA = a.transform.position;
        Vector3 posB = b.transform.position;

        // === 2. GRID INDEX'LERİ KAYDET ===
        int aX = a.x, aY = a.y;
        int bX = b.x, bY = b.y;

        // === 3. SWAP YAP (GRID) ===
        grid[aX, aY] = b;
        grid[bX, bY] = a;

        a.x = bX; a.y = bY;
        b.x = aX; b.y = aY;

        // === 4. ANIMASYON ===
        yield return StartCoroutine(SmoothMove(a.transform, posB, 0.18f));
        yield return StartCoroutine(SmoothMove(b.transform, posA, 0.18f));

        // === 5. MATCH KONTROLÜ ===
        List<Tile> matches = FindAllMatches();

        // === 6. BAŞARISIZ SWAP ===
        if (matches.Count == 0)
        {
            // Görsel geri al
            yield return StartCoroutine(SmoothMove(a.transform, posA, 0.15f));
            yield return StartCoroutine(SmoothMove(b.transform, posB, 0.15f));

            // Grid geri al
            grid[aX, aY] = a;
            grid[bX, bY] = b;

            a.x = aX; a.y = aY;
            b.x = bX; b.y = bY;

            // HAMLEYİ GERİ AL
            totalMoves--;
            UpdateUI();

            yield break;
        }

        // === 7. BAŞARILI SWAP ===
        totalMoves--;
        UpdateUI();

        boardBusy = true;
        yield return StartCoroutine(ResolveBoard());
        boardBusy = false;

        CheckGameState();
    }

    IEnumerator SmoothMove(Transform obj, Vector3 targetPos, float duration)
    {
        if (obj == null) yield break;

        Vector3 startPos = obj.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (obj == null) yield break;

            float t = elapsed / duration;
            // Smooth step için (daha yumuşak hareket)
            t = t * t * (3f - 2f * t);

            obj.position = Vector3.Lerp(startPos, targetPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (obj != null)
            obj.position = targetPos;
    }

    IEnumerator ResolveBoard()
    {
        boardBusy = true;

        List<Tile> matches = FindAllMatches();

        while (matches.Count > 0)
        {
            Debug.Log($"[ResolveBoard] {matches.Count} eşleşme bulundu");

            // 1. EŞLEŞMELERİ PATLAT
            yield return StartCoroutine(HandleMatches(matches));

            // 2. TILE'LARI DÜŞÜR
            yield return StartCoroutine(DropTiles());

            // 3. YENİ TILE'LAR EKLE
            yield return StartCoroutine(RefillTiles());

            // 4. YENİDEN KONTROL ET
            matches = FindAllMatches();
        }

        Debug.Log("[ResolveBoard] Tamamlandı");
        boardBusy = false;
        CheckGameState();
    }
    IEnumerator ActivateBloodDrop(Tile bloodDrop)
    {
        if (bloodDrop == null || !bloodDrop.isBloodDrop || boardBusy) yield break;

        boardBusy = true;

        Debug.Log($"[BloodDrop] Aktif ediliyor: ({bloodDrop.x},{bloodDrop.y})");

        // 1. Kan Damlası'nın rengini al
        TileType colorToClear = GetMostCommonColorAround(bloodDrop);
        bloodDrop.bloodDropColor = colorToClear;

        // 2. Grid'den Kan Damlası'nı kaldır
        grid[bloodDrop.x, bloodDrop.y] = null;

        // 3. Efekt
        if (particleEffectManager != null)
        {
            particleEffectManager.PlayEffect(TileType.BloodDrop, bloodDrop.transform.position);
        }

        // 4. Kan Damlası'nı yok et
        Destroy(bloodDrop.gameObject);

        yield return new WaitForSeconds(0.1f);

        // 5. O RENKTEKİ TÜM NORMAL TILE'LARI BUL
        List<Tile> tilesToDestroy = new List<Tile>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile tile = grid[x, y];

                if (tile != null &&
                    !tile.isSpecial &&
                    !tile.isBloodDrop &&
                    tile.tileType == colorToClear)
                {
                    tilesToDestroy.Add(tile);
                }
            }
        }

        Debug.Log($"[BloodDrop] {tilesToDestroy.Count} adet {colorToClear} rengi tile patlatılacak");

        // 6. PATLAT
        if (tilesToDestroy.Count > 0)
        {
            // Grid'den sil
            foreach (Tile tile in tilesToDestroy)
            {
                if (tile != null)
                {
                    grid[tile.x, tile.y] = null;
                }
            }

            // Ses
            if (matchSound != null)
                AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position);

            // Efektler
            foreach (Tile tile in tilesToDestroy)
            {
                if (tile != null && particleEffectManager != null)
                {
                    particleEffectManager.PlayEffect(tile.tileType, tile.transform.position);
                }
            }

            yield return new WaitForSeconds(0.15f);

            // Yok et
            foreach (Tile tile in tilesToDestroy)
            {
                if (tile != null)
                    Destroy(tile.gameObject);
            }
        }

        // 7. DÜŞME VE DOLDURMA
        yield return StartCoroutine(DropTiles());
        yield return StartCoroutine(RefillTiles());

        // 8. YENİ MATCH VAR MI KONTROL ET
        List<Tile> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
        {
            yield return StartCoroutine(ResolveBoard());
        }

        // 9. HAMLE SAY
        totalMoves--;
        UpdateUI();

        // 10. SKOR GÜNCELLE
        OnMoveResolved(tilesToDestroy.Count > 0 ? tilesToDestroy.Count : 1);

        boardBusy = false;
        CheckGameState();
    }


    TileType GetMostCommonColorAround(Tile bloodDrop)
    {
        // Komşu renklerini say
        Dictionary<TileType, int> colorCount = new Dictionary<TileType, int>();

        // 4 yöndeki komşular
        Vector2Int[] directions = {
        new Vector2Int(-1, 0), // sol
        new Vector2Int(1, 0),  // sağ
        new Vector2Int(0, -1), // alt
        new Vector2Int(0, 1)   // üst
    };

        foreach (Vector2Int dir in directions)
        {
            Tile neighbor = GetTileAt(bloodDrop.x + dir.x, bloodDrop.y + dir.y);

            if (neighbor != null &&
                !neighbor.isSpecial && // Normal tile
                !neighbor.isBloodDrop) // Kan Damlası değil
            {
                TileType color = neighbor.tileType;

                if (colorCount.ContainsKey(color))
                    colorCount[color]++;
                else
                    colorCount[color] = 1;
            }
        }

        if (colorCount.Count > 0)
        {
            TileType mostCommon = TileType.Red;
            int maxCount = 0;

            foreach (var kvp in colorCount)
            {
                if (kvp.Value > maxCount)
                {
                    maxCount = kvp.Value;
                    mostCommon = kvp.Key;
                }
            }

            Debug.Log($"[BloodDrop] Komşulardan renk: {mostCommon}");
            return mostCommon;
        }

        Dictionary<TileType, int> gridCount = new Dictionary<TileType, int>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile tile = grid[x, y];

                if (tile != null &&
                    !tile.isSpecial &&
                    !tile.isBloodDrop)
                {
                    TileType color = tile.tileType;

                    if (gridCount.ContainsKey(color))
                        gridCount[color]++;
                    else
                        gridCount[color] = 1;
                }
            }
        }

        if (gridCount.Count > 0)
        {
            TileType mostCommonGrid = TileType.Red;
            int maxGridCount = 0;

            foreach (var kvp in gridCount)
            {
                if (kvp.Value > maxGridCount)
                {
                    maxGridCount = kvp.Value;
                    mostCommonGrid = kvp.Key;
                }
            }

            Debug.Log($"[BloodDrop] Grid'den renk: {mostCommonGrid}");
            return mostCommonGrid;
        }

        // Hiçbir şey yoksa varsayılan
        Debug.Log("[BloodDrop] Varsayılan renk: Red");
        return TileType.Red;
    }
    IEnumerator HandleMatches(List<Tile> matches)
    {
        if (matches == null || matches.Count == 0)
            yield break;

        // 1. YARASA VAR MI KONTROL ET
        bool hasBat = false;
        foreach (Tile tile in matches)
        {
            if (tile != null && tile.isBat)
            {
                hasBat = true;
                break;
            }
        }

        // 2. SES EFEKTİ
        if (hasBat && batSound != null)
        {
            AudioSource.PlayClipAtPoint(batSound, Camera.main.transform.position, 0.7f);
            Debug.Log("[HandleMatches] Yarasa match sesi");
        }
        else if (matchSound != null)
        {
            AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position);
        }

        // 3. BLOODDROP EXTRA TILE'LARI TOPLA
        List<Tile> extraTiles = new List<Tile>();

        foreach (Tile tile in matches)
        {
            if (tile != null && tile.isBloodDrop)
            {
                Debug.Log($"[BloodDrop] Patlıyor: ({tile.x},{tile.y})");

                // 3x3 alan
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (dx == 0 && dy == 0) continue;

                        Tile neighbor = GetTileAt(tile.x + dx, tile.y + dy);

                        if (neighbor != null &&
                            !matches.Contains(neighbor) &&
                            !extraTiles.Contains(neighbor))
                        {
                            extraTiles.Add(neighbor);
                        }
                    }
                }

                // BloodDrop kendisini de ekle
                if (!matches.Contains(tile))
                    matches.Add(tile);
            }
        }

        // Extra tile'ları ekle
        if (extraTiles.Count > 0)
            matches.AddRange(extraTiles);

        // 4. PARTİKÜL EFEKTLERİ
        foreach (Tile tile in matches)
        {
            if (tile != null && particleEffectManager != null)
            {
                particleEffectManager.PlayEffect(tile.tileType, tile.transform.position);
            }
        }

        yield return new WaitForSeconds(0.15f);

        // 5. GRİD'DEN SİL
        foreach (Tile tile in matches)
        {
            if (tile != null)
                grid[tile.x, tile.y] = null;
        }

        // 6. GAMEOBJECT'LERİ YOK ET
        foreach (Tile tile in matches)
        {
            if (tile != null)
                Destroy(tile.gameObject);
        }

        // 7. HAMLE SONUÇLARI
        OnMoveResolved(matches.Count);

        // 8. ÖZEL TILE OLUŞTUR
        if (shouldCreateSpecialTile)
            yield return StartCoroutine(CreateSpecialTileAfterDelay());

        // 9. YARASA OLUŞTUR
        if (shouldCreateBat)
            yield return StartCoroutine(CreateBatAfterDelay());
    }

    IEnumerator DropTiles()
    {
        List<Coroutine> dropCoroutines = new List<Coroutine>();
        bool anyTileMoved;

        do
        {
            anyTileMoved = false;

            // HER SÜTUN İÇİN
            for (int x = 0; x < width; x++)
            {
                // EN ALT SATIRDAN BAŞLA
                for (int y = 0; y < height; y++)
                {
                    // EĞER BOŞ BİR HÜCRE VARSA
                    if (grid[x, y] == null)
                    {
                        // YUKARIYA TARA
                        for (int yAbove = y + 1; yAbove < height; yAbove++)
                        {
                            Tile tileAbove = grid[x, yAbove];

                            if (tileAbove != null)
                            {
                                // TILE'ı AŞAĞI İNDİR
                                grid[x, yAbove] = null;
                                grid[x, y] = tileAbove;

                                // POZİSYONU GÜNCELLE
                                tileAbove.x = x;
                                tileAbove.y = y;

                                // ANİMASYON BAŞLAT (HEPSİ AYNI ANDA)
                                Vector3 targetPos = GetTileWorldPosition(x, y);

                                // PERFORMANS AYARI KULLAN
                                float duration = enableFastAnimations ? fastDropDuration : normalDropDuration;

                                Coroutine dropRoutine = StartCoroutine(
                                    SmoothMove(tileAbove.transform, targetPos, duration)
                                );
                                dropCoroutines.Add(dropRoutine);

                                anyTileMoved = true;
                                break; // BİR TANE BULDUK, DİĞERİNE GEÇ
                            }
                        }
                    }
                }
            }

            // TÜM DÜŞME ANİMASYONLARI BİTENE KADAR BEKLE
            foreach (Coroutine routine in dropCoroutines)
            {
                yield return routine;
            }

            dropCoroutines.Clear();

        } while (anyTileMoved); // HİÇ TILE HAREKET ETMEYENE KADAR DEVAM ET

        yield return new WaitForSeconds(0.05f); // KISA BEKLEME
    }

    IEnumerator RefillTiles()
    {
        List<Coroutine> fillCoroutines = new List<Coroutine>();

        // HER SÜTUN İÇİN
        for (int x = 0; x < width; x++)
        {
            int emptyCount = 0;

            // BOŞ HÜCRELERİ SAY
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == null)
                {
                    emptyCount++;
                }
            }

            // YUKARIDAN YENİ TILE'LAR EKLE
            for (int i = 0; i < emptyCount; i++)
            {
                int targetY = height - emptyCount + i;

                // YENİ TILE TİPİ BELİRLE
                TileType newType = GetSafeRefillTile(x, targetY);
                GameObject prefab = GetPrefabByType(newType);

                // SPAWN POZİSYONU (YUKARIDA)
                Vector3 spawnPos = GetTileWorldPosition(x, height + i);
                GameObject tileObj = Instantiate(prefab, spawnPos, Quaternion.identity, transform);

                // TILE BİLEŞENİNİ AL
                Tile newTile = tileObj.GetComponent<Tile>();
                newTile.x = x;
                newTile.y = targetY;
                newTile.tileType = newType;

                // GRİDE YERLEŞTİR
                grid[x, targetY] = newTile;

                // ANİMASYON BAŞLAT (HEPSİ AYNI ANDA)
                Vector3 targetPos = GetTileWorldPosition(x, targetY);

                // PERFORMANS AYARI KULLAN
                float duration = enableFastAnimations ? fastDropDuration : normalDropDuration;

                Coroutine fillRoutine = StartCoroutine(
                    SmoothMove(newTile.transform, targetPos, duration)
                );
                fillCoroutines.Add(fillRoutine);
            }
        }

        // TÜM DOLDURMA ANİMASYONLARI BİTENE KADAR BEKLE
        foreach (Coroutine routine in fillCoroutines)
        {
            yield return routine;
        }

        yield return new WaitForSeconds(0.05f);
    }

    // ===================== GRID =====================
    void CreateGrid()
    {
        if (grid != null)
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);
        }

        grid = new Tile[width, height];

        float offsetX = (width - 1) / 2f;
        float offsetY = (height - 1) / 2f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                TileType type = GetSafeStartTile(x, y);
                GameObject prefab = GetPrefabByType(type);

                Vector3 pos = new Vector3(
                    (x - offsetX) * tileSpacing,
                    (y - offsetY) * tileSpacing + gridYOffset,
                    0f
                );

                GameObject obj = Instantiate(prefab, pos, Quaternion.identity, transform);
                Tile tile = obj.GetComponent<Tile>();
                tile.x = x;
                tile.y = y;
                tile.tileType = type;
                grid[x, y] = tile;
            }
        }
    }

    TileType GetRandomNormalTile()
    {
        float rand = Random.value;

        if (rand < 0.25f) return TileType.Red;
        else if (rand < 0.5f) return TileType.Yellow;
        else if (rand < 0.75f) return TileType.Green;
        else return TileType.Blue;
    }

    TileType GetSafeStartTile(int x, int y)
    {
        // TÜM MÜMKÜN TİPLER
        List<TileType> possibleTypes = new List<TileType>
        {
            TileType.Red,
            TileType.Yellow,
            TileType.Green,
            TileType.Blue
        };

        // SOLDA 2 TILE VARSA VE AYNIYSA, O TİPİ ÇIKAR
        if (x >= 2 && grid[x - 1, y] != null && grid[x - 2, y] != null)
        {
            if (grid[x - 1, y].tileType == grid[x - 2, y].tileType)
            {
                possibleTypes.Remove(grid[x - 1, y].tileType);
            }
        }

        // ALTTA 2 TILE VARSA VE AYNIYSA, O TİPİ ÇIKAR
        if (y >= 2 && grid[x, y - 1] != null && grid[x, y - 2] != null)
        {
            if (grid[x, y - 1].tileType == grid[x, y - 2].tileType)
            {
                possibleTypes.Remove(grid[x, y - 1].tileType);
            }
        }

        // MÜMKÜN TİPLERDEN RASTGELE SEÇ
        if (possibleTypes.Count > 0)
        {
            return possibleTypes[Random.Range(0, possibleTypes.Count)];
        }

        // YOKSA NORMAL RASTGELE
        return GetRandomNormalTile();
    }

    TileType GetSafeRefillTile(int x, int y)
    {
        for (int i = 0; i < 10; i++)
        {
            TileType type = GetRandomNormalTile();

            bool h =
                x >= 2 &&
                grid[x - 1, y] != null &&
                grid[x - 2, y] != null &&
                grid[x - 1, y].tileType == type &&
                grid[x - 2, y].tileType == type;

            bool v =
                y >= 2 &&
                grid[x, y - 1] != null &&
                grid[x, y - 2] != null &&
                grid[x, y - 1].tileType == type &&
                grid[x, y - 2].tileType == type;

            if (!h && !v)
                return type;
        }

        return GetRandomNormalTile();
    }

    GameObject GetPrefabByType(TileType type)
    {
        switch (type)
        {
            case TileType.Red: return redPrefab;
            case TileType.Yellow: return yellowPrefab;
            case TileType.Green: return greenPrefab;
            case TileType.Blue: return bluePrefab;
            case TileType.BloodDrop: return bloodDropPrefab;
            case TileType.Bat: return batPrefab;
            default: return redPrefab;
        }
    }

    List<Tile> FindAllMatches()
    {
        HashSet<Tile> result = new HashSet<Tile>();

        // Yatay eşleşmeler
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2; x++)
            {
                CheckLine(x, y, 1, 0, result);
            }
        }

        // Dikey eşleşmeler
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2; y++)
            {
                CheckLine(x, y, 0, 1, result);
            }
        }

        // Kare eşleşmeler (2x2)
        CheckSquares(result);

        return new List<Tile>(result);
    }

    // ===================== MATCH RESOLVE =====================
    public void OnMoveResolved(int destroyedTileCount)
    {
        if (gameEnded) return;

        targetCount -= destroyedTileCount;
        targetCount = Mathf.Max(0, targetCount);

        currentBlood -= destroyedTileCount * bloodDecreasePerMatch;
        currentBlood = Mathf.Clamp01(currentBlood);

        UpdateUI();
        UpdateBloodUI();
        CheckGameState();
    }

    // ===================== UI =====================
    void UpdateUI()
    {
        if (movesText != null)
            movesText.text = totalMoves.ToString();

        if (targetText != null)
            targetText.text = targetCount.ToString();
    }

    void UpdateBloodUI()
    {
        bloodFillImage.fillAmount = currentBlood;

        // Vampir sprite durumu
        if (currentBlood < 0.33f)
        {
            vampyrImage.sprite = vampyrNormal;
        }
        else if (currentBlood < 0.66f)
        {
            vampyrImage.sprite = vampyrMedium;
        }
        else
        {
            vampyrImage.sprite = vampyrHungry;
        }
    }

    // ===================== WIN / LOSE =====================
    void CheckGameState()
    {
        if (targetCount <= 0)
        {
            TriggerWin();
            return;
        }

        if (currentBlood >= 1f)
        {
            TriggerLose();
            return;
        }

        if (totalMoves <= 0)
        {
            TriggerLose();
            return;
        }
    }

    void TriggerWin()
    {
        if (gameEnded) return; // ÇİFT TETİKLEMEYİ ÖNLE
        gameEnded = true;
        winGamePanel.SetActive(true);
        winGameAnimator.SetTrigger("Win");
        if (winSound != null)
        {
            AudioSource.PlayClipAtPoint(winSound, Camera.main.transform.position);
        }
        GameObject winvisual = Instantiate(winVFX, winvfxSpawnPoint.position, Quaternion.identity);
        Destroy(winvisual, 5f);
        Debug.Log("WIN");
        Invoke("ResetGame", 5f);
    }

    void TriggerLose()
    {
        if (gameEnded) return; // ÇİFT TETİKLEMEYİ ÖNLE
        gameEnded = true;
        loseGamePanel.SetActive(true);
        loseGameAnimator.SetTrigger("Lost");
        if (loseSound != null)
        {
            AudioSource.PlayClipAtPoint(loseSound, Camera.main.transform.position);
        }
        GameObject losevisual = Instantiate(loseVFX, losevfxSpawnPoint.position, Quaternion.identity);
        Destroy(losevisual, 3f);
        Debug.Log("LOSE");
        Invoke("ResetGame", 3f);
    }

    void ResetGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        );
    }

    void CheckLine(int startX, int startY, int dirX, int dirY, HashSet<Tile> result)
    {
        Tile startTile = grid[startX, startY];
        if (startTile == null) return;

        List<Tile> match = new List<Tile> { startTile };

        int x = startX + dirX;
        int y = startY + dirY;

        // Aynı renkteki tile'ları topla
        while (x >= 0 && x < width && y >= 0 && y < height)
        {
            Tile next = grid[x, y];

            // NULL KONTROLÜ EKLE
            if (next == null) break;

            // Aynı tipte mi kontrol et
            if (next.tileType == startTile.tileType)
            {
                match.Add(next);
                x += dirX;
                y += dirY;
            }
            else
            {
                break;
            }
        }

        // 3 veya daha fazla eşleşme varsa ekle
        if (match.Count >= 3)
        {
            foreach (Tile t in match)
            {
                result.Add(t);
            }

            // 4+ eşleşmede özel tile oluştur
            if (match.Count >= 4)
            {
                int midIndex = match.Count / 2;
                Tile centerTile = match[midIndex];

                // Özel tile oluşturulacak pozisyonu kaydet
                CreateSpecialTileAfterMatch(centerTile.x, centerTile.y, match.Count);
            }
        }
    }

    void CheckSquares(HashSet<Tile> result)
    {
        for (int x = 0; x < width - 1; x++)
        {
            for (int y = 0; y < height - 1; y++)
            {
                Tile a = grid[x, y];
                Tile b = grid[x + 1, y];
                Tile c = grid[x, y + 1];
                Tile d = grid[x + 1, y + 1];

                if (a != null && b != null && c != null && d != null &&
                    a.tileType == b.tileType &&
                    a.tileType == c.tileType &&
                    a.tileType == d.tileType)
                {
                    result.Add(a);
                    result.Add(b);
                    result.Add(c);
                    result.Add(d);

                    CreateBatAfterSquare(x, y);
                }
            }
        }
    }
    void CreateBatAfterSquare(int squareX, int squareY)
    {
        shouldCreateBat = true;
        batTileX = squareX; // Kare'nin sol alt köşesi
        batTileY = squareY;

        Debug.Log($"[Bat] 2x2 kare bulundu: ({squareX},{squareY}) - Yarasa oluşturulacak");
    }

    IEnumerator CreateBatAfterDelay()
    {
        yield return new WaitForSeconds(0.2f);

        if (!shouldCreateBat)
            yield break;

        // 2x2 karenin ORTA NOKTASINI bul
        int centerX = batTileX + 1;
        int centerY = batTileY + 1;

        // Hücre boş mu kontrol et
        if (grid[centerX, centerY] != null)
        {
            Debug.LogWarning($"[Bat] Pozisyon ({centerX},{centerY}) dolu, yarasa oluşturulamadı");

            // Alternatif pozisyon dene (karenin köşelerinden biri)
            Vector2Int[] possiblePositions = {
            new Vector2Int(batTileX, batTileY),     // sol alt
            new Vector2Int(batTileX + 1, batTileY), // sağ alt
            new Vector2Int(batTileX, batTileY + 1), // sol üst
            new Vector2Int(batTileX + 1, batTileY + 1) // sağ üst
        };

            bool spawned = false;
            foreach (Vector2Int pos in possiblePositions)
            {
                if (grid[pos.x, pos.y] == null)
                {
                    centerX = pos.x;
                    centerY = pos.y;
                    spawned = true;
                    break;
                }
            }

            if (!spawned)
            {
                shouldCreateBat = false;
                yield break;
            }
        }
        if (batSound != null)
        {
            AudioSource.PlayClipAtPoint(batSound, Camera.main.transform.position, 0.5f);
            Debug.Log("[Bat] Yarasa oluşum sesi");
        }

        // Yarasa prefab'ını oluştur
        GameObject prefab = batPrefab;
        Vector3 spawnPos = GetTileWorldPosition(centerX, centerY);
        GameObject batObj = Instantiate(prefab, spawnPos, Quaternion.identity, transform);

        // Tile bileşenini al ve ayarla
        Tile batTile = batObj.GetComponent<Tile>();
        batTile.x = centerX;
        batTile.y = centerY;
        batTile.tileType = TileType.Bat;
        batTile.isSpecial = true;
        batTile.isBat = true;

        // Grid'e yerleştir
        grid[centerX, centerY] = batTile;

        Debug.Log($"[Bat] Yarasa oluşturuldu: ({centerX},{centerY})");

        shouldCreateBat = false;
    }


    Vector3 GetTileWorldPosition(int x, int y)
    {
        float offsetX = (width - 1) / 2f;
        float offsetY = (height - 1) / 2f;

        return new Vector3(
            (x - offsetX) * tileSpacing,
            (y - offsetY) * tileSpacing + gridYOffset,
            0f
        );
    }

    void CreateSpecialTileAfterMatch(int x, int y, int matchLength)
    {
        shouldCreateSpecialTile = true;
        specialTileX = x;
        specialTileY = y;
        matchLengthForSpecial = matchLength;

        Debug.Log($"[Special Tile] Pozisyon: ({x},{y}), Eşleşme uzunluğu: {matchLength}");
    }

    IEnumerator CreateSpecialTileAfterDelay()
    {
        yield return new WaitForSeconds(0.2f);

        if (!shouldCreateSpecialTile)
            yield break;

        if (grid[specialTileX, specialTileY] != null)
        {
            Debug.LogWarning($"[Special Tile] Pozisyon dolu, iptal");
            shouldCreateSpecialTile = false;
            yield break;
        }

        GameObject prefab = bloodDropPrefab;
        Vector3 pos = GetTileWorldPosition(specialTileX, specialTileY);
        GameObject obj = Instantiate(prefab, pos, Quaternion.identity, transform);

        Tile tile = obj.GetComponent<Tile>();
        tile.x = specialTileX;
        tile.y = specialTileY;
        tile.tileType = TileType.BloodDrop;
        tile.isSpecial = true;
        tile.isBloodDrop = true;

        // Başlangıç rengini belirle
        tile.bloodDropColor = GetMostCommonColorAround(tile);

        // Görseli güncelle
        tile.UpdateBloodDropVisual();

        grid[specialTileX, specialTileY] = tile;

        Debug.Log($"[Special Tile] BloodDrop: ({specialTileX},{specialTileY}), Renk: {tile.bloodDropColor}");

        shouldCreateSpecialTile = false;
    }




    IEnumerator CombineTwoBloodDrops(Tile bloodDrop1, Tile bloodDrop2)
    {
        if (bloodDrop1 == null || bloodDrop2 == null || !bloodDrop1.isBloodDrop || !bloodDrop2.isBloodDrop)
            yield break;

        if (boardBusy) yield break;

        boardBusy = true;

        Debug.Log($"[BloodDrop Combo] Başlatılıyor: ({bloodDrop1.x},{bloodDrop1.y}) + ({bloodDrop2.x},{bloodDrop2.y})");

        // 1. HAMLE SAY
        totalMoves--;
        UpdateUI();

        // 2. ANİMASYON - İki Kan Damlası birbirine doğru hareket etsin
        Vector3 middlePos = (bloodDrop1.transform.position + bloodDrop2.transform.position) / 2f;

        yield return StartCoroutine(SmoothMove(bloodDrop1.transform, middlePos, 0.25f));
        yield return StartCoroutine(SmoothMove(bloodDrop2.transform, middlePos, 0.25f));

        // 3. ÖZEL SES (büyük combo sesi)
        if (bloodDropSound != null)
        {
            AudioSource.PlayClipAtPoint(bloodDropSound, Camera.main.transform.position, 1f);
        }

        // 4. BÜYÜK PARTICLE EFEKTİ
        if (particleEffectManager != null)
        {
            // Çift particle efekti
            particleEffectManager.PlayEffect(TileType.BloodDrop, bloodDrop1.transform.position);
            particleEffectManager.PlayEffect(TileType.BloodDrop, bloodDrop2.transform.position);

            // Özel combo efekti (orta noktada)
            particleEffectManager.PlayEffect(TileType.Vampyr, middlePos);
        }

        yield return new WaitForSeconds(0.3f);

        // 5. RASTGELE 2 RENK SEÇ
        List<TileType> allColors = new List<TileType> { TileType.Red, TileType.Yellow, TileType.Green, TileType.Blue };

        // Rastgele 2 farklı renk seç
        TileType color1 = allColors[Random.Range(0, allColors.Count)];
        TileType color2 = color1;

        // Farklı renkler seçene kadar dene
        int attempts = 0;
        while (color2 == color1 && attempts < 10)
        {
            color2 = allColors[Random.Range(0, allColors.Count)];
            attempts++;
        }

        Debug.Log($"[BloodDrop Combo] Seçilen renkler: {color1} ve {color2}");

        // 6. GRİD'DEN KAN DAMLALARINI KALDIR
        grid[bloodDrop1.x, bloodDrop1.y] = null;
        grid[bloodDrop2.x, bloodDrop2.y] = null;

        // 7. KAN DAMLALARINI YOK ET
        Destroy(bloodDrop1.gameObject);
        Destroy(bloodDrop2.gameObject);

        yield return new WaitForSeconds(0.1f);

        // 8. İKİ RENKTEKİ TÜM TILE'LARI BUL
        List<Tile> tilesToDestroy = new List<Tile>();
        int color1Count = 0;
        int color2Count = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile tile = grid[x, y];

                if (tile != null &&
                    !tile.isSpecial &&  // Normal tile
                    !tile.isBloodDrop)   // Kan Damlası değil
                {
                    if (tile.tileType == color1)
                    {
                        tilesToDestroy.Add(tile);
                        color1Count++;
                    }
                    else if (tile.tileType == color2)
                    {
                        tilesToDestroy.Add(tile);
                        color2Count++;
                    }
                }
            }
        }

        Debug.Log($"[BloodDrop Combo] {color1Count} adet {color1}, {color2Count} adet {color2} = Toplam {tilesToDestroy.Count} tile");

        // 9. TILE'LARI PATLAT
        if (tilesToDestroy.Count > 0)
        {
            // Grid'den sil
            foreach (Tile tile in tilesToDestroy)
            {
                if (tile != null)
                {
                    grid[tile.x, tile.y] = null;
                }
            }

            // NORMAL MATCH SESİ
            if (matchSound != null)
            {
                AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position, 0.7f);
            }

            // PARTICLE EFEKTLERİ
            foreach (Tile tile in tilesToDestroy)
            {
                if (tile != null && particleEffectManager != null)
                {
                    particleEffectManager.PlayEffect(tile.tileType, tile.transform.position);
                }
            }

            yield return new WaitForSeconds(0.2f);

            // YOK ET
            foreach (Tile tile in tilesToDestroy)
            {
                if (tile != null)
                {
                    Destroy(tile.gameObject);
                }
            }

            // SKOR GÜNCELLE
            OnMoveResolved(tilesToDestroy.Count);
        }
        else
        {
            Debug.Log("[BloodDrop Combo] Hiç tile patlatılmadı, sadece Kan Damlaları kullanıldı");
        }

        // 10. DÜŞME VE DOLDURMA
        yield return StartCoroutine(DropTiles());
        yield return StartCoroutine(RefillTiles());

        // 11. YENİ MATCH KONTROLÜ
        List<Tile> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
        {
            yield return StartCoroutine(ResolveBoard());
        }

        boardBusy = false;
        CheckGameState();
    }
}