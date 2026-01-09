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
                Tile targetTile = GetTileAt(
                    selectedTile.x + dragDirection.x,
                    selectedTile.y + dragDirection.y
                );

                if (targetTile != null)
                {
                    // ✅ ÖNCELİK SIRASI ÖNEMLİ!

                    // 1. İKİ YARASA BİRLEŞTİRME
                    if (selectedTile.isBat && targetTile.isBat)
                    {
                        StartCoroutine(CombineTwoBats(selectedTile, targetTile));
                    }
                    // 2. İKİ KAN DAMLASI BİRLEŞTİRME
                    else if (selectedTile.isBloodDrop && targetTile.isBloodDrop)
                    {
                        StartCoroutine(CombineTwoBloodDrops(selectedTile, targetTile));
                    }
                    // 3. YARASA + KAN DAMLASI (YENİ EKLENDİ) 🆕
                    else if ((selectedTile.isBat && targetTile.isBloodDrop) ||
                             (selectedTile.isBloodDrop && targetTile.isBat))
                    {
                        // Hangisi yarasa, hangisi kan damlası bul
                        Tile batTile = selectedTile.isBat ? selectedTile : targetTile;
                        Tile bloodDropTile = selectedTile.isBloodDrop ? selectedTile : targetTile;

                        StartCoroutine(CombineBatWithBloodDrop(batTile, bloodDropTile));
                    }
                    // 4. YARASA + NORMAL TILE
                    else if (selectedTile.isBat)
                    {
                        StartCoroutine(ActivateBatWithTile(selectedTile, targetTile));
                    }
                    // 5. YARASA + NORMAL TILE (YARASA SAĞDA)
                    else if (targetTile.isBat)
                    {
                        StartCoroutine(ActivateBatWithTile(targetTile, selectedTile));
                    }
                    // 6. KAN DAMLASI + NORMAL TILE
                    else if (selectedTile.isBloodDrop)
                    {
                        StartCoroutine(ActivateBloodDropWithTile(selectedTile, targetTile));
                    }
                    // 7. KAN DAMLASI + NORMAL TILE (KAN DAMLASI SAĞDA)
                    else if (targetTile.isBloodDrop)
                    {
                        StartCoroutine(ActivateBloodDropWithTile(targetTile, selectedTile));
                    }
                    // 8. NORMAL SWAP
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

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
            selectedTile = null;
            directionLocked = false;
        }
    }

    IEnumerator ActivateBatWithTile(Tile bat, Tile targetTile)
    {
        if (bat == null || !bat.isBat || boardBusy) yield break;
        if (targetTile == null) yield break;

        if (targetTile.isSpecial || targetTile.isBloodDrop || targetTile.isBat || targetTile.isVampire)
        {
            Debug.LogWarning($"[Bat] Hedef tile özel, işlem iptal: {targetTile.tileType}");
            boardBusy = false;
            yield break;
        }

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

            // Yeni Kan Damlası oluştur (0.5 SCALE İLE)
            Tile bloodDrop = CreateBloodDropAtPosition(targetX, targetY, targetColor, targetPos);

            // Grid'e yerleştir
            grid[targetX, targetY] = bloodDrop;

            Debug.Log($"[Bat] Tile Kan Damlası'na dönüştürüldü (0.5 scale)");

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
    void FixAllBloodDropZPositions()
    {
        int fixedCount = 0;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile tile = grid[x, y];
                if (tile != null && tile.isBloodDrop)
                {
                    // Tile.cs'deki SetCorrectZPosition fonksiyonunu çağır
                    tile.SetCorrectZPosition();
                    fixedCount++;
                }
            }
        }

        if (fixedCount > 0)
        {
            Debug.Log($"[FixAllBloodDropZPositions] {fixedCount} kan damlasının Z pozisyonu düzeltildi");
        }
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

            // 4. TÜM KAN DAMLALARININ Z-POSITION'UNU DÜZELT (YENİ EKLENDİ!)
            FixAllBloodDropZPositions();

            // 5. YENİDEN KONTROL ET
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

        // ÖZEL TİLE'LARI FİLTRELE
        List<Tile> filteredMatches = new List<Tile>();
        foreach (Tile tile in matches)
        {
            if (tile != null &&
                !tile.isSpecial &&
                !tile.isBloodDrop &&
                !tile.isBat &&
                !tile.isVampire)
            {
                filteredMatches.Add(tile);
            }
            else
            {
                Debug.LogWarning($"[HandleMatches] Özel tile filtrelendi: {tile?.tileType} ({tile?.x},{tile?.y})");
            }
        }

        if (filteredMatches.Count == 0)
            yield break;

        // 1. SES EFEKTİ
        if (matchSound != null)
        {
            AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position);
        }

        // 2. PARTİKÜL EFEKTLERİ
        foreach (Tile tile in filteredMatches)
        {
            if (tile != null && particleEffectManager != null)
            {
                particleEffectManager.PlayEffect(tile.tileType, tile.transform.position);
            }
        }

        yield return new WaitForSeconds(0.15f);

        // 3. ÖZEL TİLE OLUŞTURULACAK POZİSYONLARI KAYDET (AMA HENÜZ OLUŞTURMA!)
        List<Vector2Int> specialTilePositions = new List<Vector2Int>();
        List<int> specialTileLengths = new List<int>();

        // 4+ eşleşmeler için kan damlası pozisyonlarını kaydet
        foreach (Tile tile in filteredMatches)
        {
            // Burada 4+ match kontrolü yapılıyorsa, pozisyonu kaydet
            // Ama HENÜZ OLUŞTURMA!
        }

        // 4. GRİD'DEN SİL
        foreach (Tile tile in filteredMatches)
        {
            if (tile != null)
                grid[tile.x, tile.y] = null;
        }

        // 5. GAMEOBJECT'LERİ YOK ET
        foreach (Tile tile in filteredMatches)
        {
            if (tile != null)
                Destroy(tile.gameObject);
        }

        // 6. HAMLE SONUÇLARI
        OnMoveResolved(filteredMatches.Count);

        // 7. DÜŞME İŞLEMİNİ YAP (ÖNCE DÜŞSÜN)
        yield return StartCoroutine(DropTiles());

        // 8. ŞİMDİ ÖZEL TİLE'LARI OLUŞTUR (DÜŞTÜKTEN SONRA!)
        if (shouldCreateSpecialTile)
        {
            yield return StartCoroutine(CreateSpecialTileAfterDelay());
        }

        if (shouldCreateBat)
        {
            yield return StartCoroutine(CreateBatAfterDelay());
        }

        // 9. DOLDURMA İŞLEMİ
        yield return StartCoroutine(RefillTiles());

        Debug.Log("[HandleMatches] Tüm işlemler tamamlandı");



    }
    // ===================== ÖZEL TİLE KORUMA =====================
    void RemoveSpecialTilesFromMatches(ref HashSet<Tile> matches)
    {
        if (matches == null || matches.Count == 0) return;

        List<Tile> toRemove = new List<Tile>();

        foreach (Tile tile in matches)
        {
            if (tile == null) continue;

            // Kan Damlası, Yarasa, Vampir gibi özel tile'ları çıkar
            if (tile.isSpecial || tile.isBloodDrop || tile.isBat || tile.isVampire)
            {
                toRemove.Add(tile);
            }
        }

        foreach (Tile tile in toRemove)
        {
            matches.Remove(tile);
        }

        if (toRemove.Count > 0)
        {
            Debug.Log($"[SpecialTileProtection] {toRemove.Count} özel tile korundu");
        }
    }


    IEnumerator DropTiles()
    {
        List<Coroutine> dropCoroutines = new List<Coroutine>();
        bool anyTileMoved;

        do
        {
            anyTileMoved = false;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (grid[x, y] == null)
                    {
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

                                // ANİMASYON BAŞLAT
                                Vector3 targetPos = GetTileWorldPosition(x, y);
                                float duration = enableFastAnimations ? fastDropDuration : normalDropDuration;

                                Coroutine dropRoutine = StartCoroutine(
                                    SmoothMove(tileAbove.transform, targetPos, duration)
                                );
                                dropCoroutines.Add(dropRoutine);

                                anyTileMoved = true;
                                break;
                            }
                        }
                    }
                }
            }

            foreach (Coroutine routine in dropCoroutines)
            {
                yield return routine;
            }

            dropCoroutines.Clear();

        } while (anyTileMoved);

        yield return new WaitForSeconds(0.05f);
    }
    IEnumerator RefillTiles()
    {
        List<Coroutine> fillCoroutines = new List<Coroutine>();

        for (int x = 0; x < width; x++)
        {
            int emptyCount = 0;

            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == null)
                {
                    emptyCount++;
                }
            }

            for (int i = 0; i < emptyCount; i++)
            {
                int targetY = height - emptyCount + i;
                TileType newType = GetSafeRefillTile(x, targetY);
                GameObject prefab = GetPrefabByType(newType);

                // SPAWN POZİSYONU (YUKARIDA, ARKADA)
                Vector3 spawnPos = new Vector3(
                    GetTileWorldPosition(x, height + i).x,
                    GetTileWorldPosition(x, height + i).y,
                    0f // Normal tile arkada
                );

                GameObject tileObj = Instantiate(prefab, spawnPos, Quaternion.identity, transform);
                Tile newTile = tileObj.GetComponent<Tile>();
                newTile.x = x;
                newTile.y = targetY;
                newTile.tileType = newType;

                // Sprite Renderer ayarı
                SpriteRenderer sr = tileObj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingOrder = 0; // Normal tile
                }

                grid[x, targetY] = newTile;

                // HEDEF POZİSYON (ARKADA)
                Vector3 targetPos = new Vector3(
                    GetTileWorldPosition(x, targetY).x,
                    GetTileWorldPosition(x, targetY).y,
                    0f
                );

                float duration = enableFastAnimations ? fastDropDuration : normalDropDuration;
                Coroutine fillRoutine = StartCoroutine(SmoothMove(newTile.transform, targetPos, duration));
                fillCoroutines.Add(fillRoutine);
            }
        }

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
                    0f // Normal tile'lar arkada
                );

                GameObject obj = Instantiate(prefab, pos, Quaternion.identity, transform);
                Tile tile = obj.GetComponent<Tile>();
                tile.x = x;
                tile.y = y;
                tile.tileType = type;

                // Sprite Renderer ayarı
                SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.sortingOrder = 0; // Normal tile'lar en arkada
                }

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

        // ÖZEL TILE'LARI SONUÇTAN ÇIKAR (YENİ EKLENDİ)
        RemoveSpecialTilesFromMatches(ref result);

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

        // ÖZEL TİLE'LARI HİÇBİR ZAMAN NORMAL MATCH'E DAHİL ETME
        if (startTile.isSpecial || startTile.isBloodDrop || startTile.isBat || startTile.isVampire)
        {
            return; // Özel tile'lar normal eşleşme yapamaz
        }

        List<Tile> match = new List<Tile> { startTile };
        int x = startX + dirX;
        int y = startY + dirY;

        // Aynı renkteki tile'ları topla
        while (x >= 0 && x < width && y >= 0 && y < height)
        {
            Tile next = grid[x, y];

            if (next == null) break;

            // ÖZEL TİLE'LARI DAHİL ETME
            if (next.isSpecial || next.isBloodDrop || next.isBat || next.isVampire) break;

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

        // 3 veya daha fazla eşleşme varsa ekle (SADECE NORMAL TILE'LAR)
        if (match.Count >= 3)
        {
            // TÜM TILE'LARIN NORMAL OLDUĞUNDAN EMİN OL
            bool allNormal = true;
            foreach (Tile t in match)
            {
                if (t.isSpecial || t.isBloodDrop || t.isBat || t.isVampire)
                {
                    allNormal = false;
                    break;
                }
            }

            if (allNormal)
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

                // TÜM TILE'LARIN NORMAL OLDUĞUNDAN EMİN OL
                if (a != null && b != null && c != null && d != null &&
                    !a.isSpecial && !a.isBloodDrop && !a.isBat && !a.isVampire &&
                    !b.isSpecial && !b.isBloodDrop && !b.isBat && !b.isVampire &&
                    !c.isSpecial && !c.isBloodDrop && !c.isBat && !c.isVampire &&
                    !d.isSpecial && !d.isBloodDrop && !d.isBat && !d.isVampire &&
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


    Vector3 GetTileWorldPosition(int x, int y, bool isSpecialTile = false)
    {
        float offsetX = (width - 1) / 2f;
        float offsetY = (height - 1) / 2f;

        float zPos = isSpecialTile ? -1f : 0f; // Özel tile'lar önde

        return new Vector3(
            (x - offsetX) * tileSpacing,
            (y - offsetY) * tileSpacing + gridYOffset,
            zPos
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

        Vector3 pos = GetTileWorldPosition(specialTileX, specialTileY);

        // CreateBloodDropAtPosition KULLAN (zaten SetCorrectZPosition çağırıyor)
        Tile tile = CreateBloodDropAtPosition(specialTileX, specialTileY, TileType.Red, pos);

        // Başlangıç rengini belirle
        tile.bloodDropColor = GetMostCommonColorAround(tile);
        tile.UpdateBloodDropVisual();

        Debug.Log($"[Special Tile] BloodDrop: ({specialTileX},{specialTileY}), Z={tile.transform.position.z}");

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




    // ===================== İKİ YARASA BİRLEŞTİRME =====================
    IEnumerator CombineTwoBats(Tile bat1, Tile bat2)
    {
        if (bat1 == null || bat2 == null || !bat1.isBat || !bat2.isBat)
            yield break;

        if (boardBusy) yield break;

        boardBusy = true;

        Debug.Log($"[Bat Combo] İki Yarasa Birleşiyor: ({bat1.x},{bat1.y}) + ({bat2.x},{bat2.y})");

        // 1. HAMLE SAY
        totalMoves--;
        UpdateUI();

        // 2. ANİMASYON - İki yarasa birbirine doğru hareket etsin
        Vector3 middlePos = (bat1.transform.position + bat2.transform.position) / 2f;

        yield return StartCoroutine(SmoothMove(bat1.transform, middlePos, 0.25f));
        yield return StartCoroutine(SmoothMove(bat2.transform, middlePos, 0.25f));

        // 3. ÖZEL YARASA BİRLEŞME SESİ
        if (batSound != null)
        {
            AudioSource.PlayClipAtPoint(batSound, Camera.main.transform.position, 1.2f);
        }

        // 4. BÜYÜK PARTICLE EFEKTİ
        if (particleEffectManager != null)
        {
            particleEffectManager.PlayEffect(TileType.Bat, bat1.transform.position);
            particleEffectManager.PlayEffect(TileType.Bat, bat2.transform.position);

            // Patlama efekti
            for (int i = 0; i < 3; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f), 0);
                particleEffectManager.PlayEffect(TileType.BloodDrop, middlePos + offset);
            }
        }

        yield return new WaitForSeconds(0.3f);

        // 5. GRİD'DEN YARASALARI KALDIR
        grid[bat1.x, bat1.y] = null;
        grid[bat2.x, bat2.y] = null;

        // 6. YARASALARI YOK ET
        Destroy(bat1.gameObject);
        Destroy(bat2.gameObject);

        yield return new WaitForSeconds(0.1f);

        // 7. ORTADA 10 YARASA OLUŞTUR
        yield return StartCoroutine(CreateTenBatsInCenter(middlePos));

        boardBusy = false;
        CheckGameState();
    }
    // ===================== YARASA ANİMASYON FONKSİYONLARI =====================

    // Yarasa büyüme efekti
    IEnumerator GrowBat(Transform batTransform, float duration)
    {
        float elapsed = 0f;
        Vector3 startScale = Vector3.zero;
        Vector3 endScale = Vector3.one * 0.8f; // Biraz küçük

        while (elapsed < duration)
        {
            if (batTransform == null) yield break;

            float t = elapsed / duration;
            // Bounce efekti
            t = Mathf.Sin(t * Mathf.PI * 0.5f);

            batTransform.localScale = Vector3.Lerp(startScale, endScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (batTransform != null)
            batTransform.localScale = endScale;
    }

    // Yarasa hedefe uçuş animasyonu
    IEnumerator FlyBatToTarget(Tile bat, Vector3 targetPos, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (bat == null || bat.transform == null) yield break;

        Vector3 startPos = bat.transform.position;
        float duration = 0.4f;
        float elapsed = 0f;

        // Uçuş rotasyonu
        float startRotation = Random.Range(-180f, 180f);
        float endRotation = startRotation + 360f;

        while (elapsed < duration)
        {
            if (bat.transform == null) yield break;

            float t = elapsed / duration;
            // Hızlanma efekti
            t = t * t * (3f - 2f * t);

            // Pozisyon
            bat.transform.position = Vector3.Lerp(startPos, targetPos, t);

            // Rotasyon (dönme efekti)
            float rotation = Mathf.Lerp(startRotation, endRotation, t);
            bat.transform.rotation = Quaternion.Euler(0, 0, rotation);

            // Scale (uçarken küçülme)
            float scale = Mathf.Lerp(0.8f, 0.4f, t);
            bat.transform.localScale = new Vector3(scale, scale, 1f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (bat.transform != null)
        {
            bat.transform.position = targetPos;

            // Hedefe ulaşınca küçük patlama efekti
            if (particleEffectManager != null)
            {
                particleEffectManager.PlayEffect(TileType.Bat, targetPos);
            }
        }
    }

    // Yarasa küçülme ve yok olma efekti
    IEnumerator ShrinkAndDestroy(Transform batTransform, float duration)
    {
        if (batTransform == null) yield break;

        Vector3 startScale = batTransform.localScale;
        Vector3 endScale = Vector3.zero;
        float elapsed = 0f;

        // Renk değişimi (mor -> kırmızı)
        SpriteRenderer sprite = batTransform.GetComponent<SpriteRenderer>();
        Color startColor = sprite != null ? sprite.color : Color.white;
        Color endColor = new Color(1f, 0.3f, 0.3f, 0.5f);

        while (elapsed < duration)
        {
            if (batTransform == null) yield break;

            float t = elapsed / duration;

            // Scale küçültme
            batTransform.localScale = Vector3.Lerp(startScale, endScale, t);

            // Renk değişimi
            if (sprite != null)
            {
                sprite.color = Color.Lerp(startColor, endColor, t);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (batTransform != null)
        {
            Destroy(batTransform.gameObject);
        }
    }

    // ===================== PARÇACIK EFENKTİ FONKSİYONU (OPSİYONEL) =====================
    IEnumerator PlayBatSwarmEffect(Vector3 centerPosition, int batCount)
    {
        for (int i = 0; i < batCount; i++)
        {
            float angle = Random.Range(0, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(0.5f, 2f);

            Vector3 pos = centerPosition + new Vector3(
                Mathf.Cos(angle) * distance,
                Mathf.Sin(angle) * distance,
                0
            );

            if (particleEffectManager != null)
            {
                particleEffectManager.PlayEffect(TileType.Bat, pos);
            }

            yield return new WaitForSeconds(0.05f);
        }
    }

    // ===================== 10 YARASA OLUŞTURMA =====================
    // ===================== 10 YARASA OLUŞTURMA (ANİMASYONLU) =====================
    IEnumerator CreateTenBatsInCenter(Vector3 centerPosition)
    {
        Debug.Log("[Bat Combo] 10 Yarasa oluşturuluyor...");

        List<Tile> spawnedBats = new List<Tile>();
        List<Tile> targetsToDestroy = new List<Tile>();

        // 1. ÖNCE HEDEF TILE'LARI BELİRLE
        // Grid'deki tüm normal tile'ları topla
        List<Tile> allNormalTiles = new List<Tile>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile tile = grid[x, y];
                if (tile != null &&
                    !tile.isSpecial &&
                    !tile.isBloodDrop &&
                    !tile.isBat &&
                    !tile.isVampire)
                {
                    allNormalTiles.Add(tile);
                }
            }
        }

        // 10 (veya daha az) hedef tile seç
        int targetCount = Mathf.Min(10, allNormalTiles.Count);
        List<Tile> selectedTargets = new List<Tile>();
        List<int> selectedIndices = new List<int>();

        for (int i = 0; i < targetCount; i++)
        {
            int randomIndex;
            do
            {
                randomIndex = Random.Range(0, allNormalTiles.Count);
            } while (selectedIndices.Contains(randomIndex));

            selectedIndices.Add(randomIndex);
            selectedTargets.Add(allNormalTiles[randomIndex]);
            targetsToDestroy.Add(allNormalTiles[randomIndex]);
        }

        Debug.Log($"[Bat Combo] {targetCount} hedef tile seçildi");

        // 2. 10 YARASA OLUŞTUR (DAİRE ŞEKLİNDE)
        float radius = 1.5f;
        for (int i = 0; i < 10; i++)
        {
            // Daire üzerinde pozisyon hesapla
            float angle = i * (360f / 10) * Mathf.Deg2Rad;
            Vector3 spawnPos = centerPosition + new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0
            );

            GameObject batObj = Instantiate(batPrefab, spawnPos, Quaternion.identity, transform);
            Tile bat = batObj.GetComponent<Tile>();
            bat.tileType = TileType.Bat;
            bat.isSpecial = true;
            bat.isBat = true;
            bat.isRandomDestroyer = true;

            spawnedBats.Add(bat);

            // Yarasa görselini küçük başlat (büyüme efekti)
            bat.transform.localScale = Vector3.zero;
            StartCoroutine(GrowBat(bat.transform, 0.3f));
        }

        // Ses efekti - Yarasa sürüsü sesi
        if (batSound != null)
        {
            AudioSource.PlayClipAtPoint(batSound, Camera.main.transform.position, 0.8f);
        }

        yield return new WaitForSeconds(0.5f);

        // 3. YARASALARI HEDEFLERE UÇUR
        if (selectedTargets.Count > 0)
        {
            Debug.Log($"[Bat Combo] Yarasa sürüsü saldırıyor!");

            List<Coroutine> flyCoroutines = new List<Coroutine>();

            // Her yarasayı bir hedefe yönlendir
            for (int i = 0; i < Mathf.Min(spawnedBats.Count, selectedTargets.Count); i++)
            {
                if (spawnedBats[i] != null && selectedTargets[i] != null)
                {
                    Coroutine flyRoutine = StartCoroutine(FlyBatToTarget(
                        spawnedBats[i],
                        selectedTargets[i].transform.position,
                        i * 0.05f // Kademeli başlatma
                    ));
                    flyCoroutines.Add(flyRoutine);
                }
            }

            // Tüm uçuş animasyonlarının bitmesini bekle
            foreach (Coroutine routine in flyCoroutines)
            {
                yield return routine;
            }

            yield return new WaitForSeconds(0.2f);

            // 4. HEDEF TILE'LARI PATLAT
            Debug.Log($"[Bat Combo] Hedef tile'lar patlatılıyor: {targetsToDestroy.Count} adet");

            // Grid'den sil
            foreach (Tile tile in targetsToDestroy)
            {
                if (tile != null)
                {
                    grid[tile.x, tile.y] = null;
                }
            }

            // PATLAMA SESİ
            if (matchSound != null)
            {
                AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position, 0.7f);
            }

            // PATLAMA PARTİKÜLLERİ
            foreach (Tile tile in targetsToDestroy)
            {
                if (tile != null && particleEffectManager != null)
                {
                    particleEffectManager.PlayEffect(tile.tileType, tile.transform.position);
                }
            }

            yield return new WaitForSeconds(0.15f);

            // HEDEF TILE'LARI YOK ET
            foreach (Tile tile in targetsToDestroy)
            {
                if (tile != null)
                {
                    Destroy(tile.gameObject);
                }
            }

            // SKOR GÜNCELLE
            OnMoveResolved(targetsToDestroy.Count);
        }
        else
        {
            Debug.Log("[Bat Combo] Yok edilecek hedef bulunamadı!");
        }

        // 5. YARASALARI YOK ET (KÜÇÜLME EFEKTİ)
        Debug.Log($"[Bat Combo] Yarasa sürüsü dağılıyor...");

        foreach (Tile bat in spawnedBats)
        {
            if (bat != null)
            {
                // Küçülme efekti
                StartCoroutine(ShrinkAndDestroy(bat.transform, 0.2f));

                // Partikül efekti
                if (particleEffectManager != null)
                {
                    particleEffectManager.PlayEffect(TileType.Bat, bat.transform.position);
                }
            }
        }

        yield return new WaitForSeconds(0.3f);

        // 6. DÜŞME VE DOLDURMA
        yield return StartCoroutine(DropTiles());
        yield return StartCoroutine(RefillTiles());

        // 7. YENİ MATCH KONTROLÜ
        List<Tile> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
        {
            yield return StartCoroutine(ResolveBoard());
        }
    }



    // ===================== YARASA + KAN DAMLASI BİRLEŞTİRME =====================
    IEnumerator CombineBatWithBloodDrop(Tile bat, Tile bloodDrop)
    {
        if (bat == null || !bat.isBat || bloodDrop == null || !bloodDrop.isBloodDrop || boardBusy)
            yield break;

        boardBusy = true;

        Debug.Log($"[Vampirik Dönüşüm] Yarasa ({bat.x},{bat.y}) + Kan Damlası ({bloodDrop.x},{bloodDrop.y})");

        // 1. HAMLE SAY
        totalMoves--;
        UpdateUI();

        // 2. ANİMASYON - Yarasa kan damlasına doğru uçsun
        Vector3 middlePos = (bat.transform.position + bloodDrop.transform.position) / 2f;

        // Yarasa uçuş animasyonu
        yield return StartCoroutine(FlyToPosition(bat.transform, middlePos, 0.3f, true));

        // Kan damlası titreşim efekti
        yield return StartCoroutine(PulseEffect(bloodDrop.transform, 0.2f, 1.5f));

        // 3. ÖZEL SES EFEKTİ
        if (batSound != null)
        {
            AudioSource.PlayClipAtPoint(batSound, Camera.main.transform.position, 1f);
        }
        if (bloodDropSound != null)
        {
            AudioSource.PlayClipAtPoint(bloodDropSound, Camera.main.transform.position, 0.8f);
        }

        // 4. BÜYÜK PARTİKÜL EFEKTİ (VAMPİR DÖNÜŞÜMÜ)
        if (particleEffectManager != null)
        {
            // Kan efekti
            particleEffectManager.PlayEffect(TileType.BloodDrop, bloodDrop.transform.position);

            // Yarasa efekti
            particleEffectManager.PlayEffect(TileType.Bat, bat.transform.position);

            // Vampirik enerji efekti
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = new Vector3(
                    Random.Range(-0.8f, 0.8f),
                    Random.Range(-0.8f, 0.8f),
                    0
                );
                particleEffectManager.PlayEffect(TileType.Vampyr, middlePos + offset);
            }
        }

        yield return new WaitForSeconds(0.4f);

        // 5. GRİD'DEN İKİSİNİ DE KALDIR
        grid[bat.x, bat.y] = null;
        grid[bloodDrop.x, bloodDrop.y] = null;

        // 6. İKİSİNİ DE YOK ET
        Destroy(bat.gameObject);
        Destroy(bloodDrop.gameObject);

        yield return new WaitForSeconds(0.1f);

        // 7. KAN DAMLASININ RENGİNİ AL
        TileType bloodColor = bloodDrop.bloodDropColor;
        Debug.Log($"[Vampirik Dönüşüm] Kan rengi: {bloodColor}");

        // 8. RASTGELE 4 TILE'ı KAN DAMLASINA DÖNÜŞTÜR
        yield return StartCoroutine(TransformRandomTilesToBloodDrops(bloodColor, 4, middlePos));

        boardBusy = false;
        CheckGameState();
    }


    // ===================== ANİMASYON FONKSİYONLARI =====================

    // Yarasa uçuş animasyonu
    IEnumerator FlyToPosition(Transform batTransform, Vector3 targetPos, float duration, bool withRotation)
    {
        if (batTransform == null) yield break;

        Vector3 startPos = batTransform.position;
        float elapsed = 0f;

        float startRotation = batTransform.rotation.eulerAngles.z;
        float endRotation = startRotation + 720f; // 2 tam tur

        while (elapsed < duration)
        {
            if (batTransform == null) yield break;

            float t = elapsed / duration;
            // Yumuşak başlangıç ve bitiş
            t = t * t * (3f - 2f * t);

            // Pozisyon
            batTransform.position = Vector3.Lerp(startPos, targetPos, t);

            // Rotasyon (isteğe bağlı)
            if (withRotation)
            {
                float rotation = Mathf.Lerp(startRotation, endRotation, t);
                batTransform.rotation = Quaternion.Euler(0, 0, rotation);
            }

            // Scale (uçarken hafif küçülme)
            float scale = Mathf.Lerp(0.8f, 0.6f, t);
            batTransform.localScale = new Vector3(scale, scale, 1f);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (batTransform != null)
            batTransform.position = targetPos;
    }

    // Titreşim efekti
    IEnumerator PulseEffect(Transform targetTransform, float duration, float maxScale)
    {
        if (targetTransform == null) yield break;

        Vector3 originalScale = targetTransform.localScale;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (targetTransform == null) yield break;

            float t = elapsed / duration;
            // Sinüs dalgası ile titreşim
            float pulse = 1f + (Mathf.Sin(t * Mathf.PI * 4f) * (maxScale - 1f));

            targetTransform.localScale = originalScale * pulse;

            // Renk değişimi (kırmızı parlama)
            SpriteRenderer sr = targetTransform.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                float redValue = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.3f;
                sr.color = new Color(redValue, sr.color.g, sr.color.b, sr.color.a);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (targetTransform != null)
            targetTransform.localScale = originalScale;
    }



    // ===================== RASTGELE TILE'LARI KAN DAMLASINA DÖNÜŞTÜR =====================
    IEnumerator TransformRandomTilesToBloodDrops(TileType bloodColor, int count, Vector3 epicenter)
    {
        Debug.Log($"[Vampirik Dönüşüm] {count} tile Kan Damlası'na dönüştürülüyor...");

        // 1. TÜM NORMAL TILE'LARI TOPLA
        List<Tile> allNormalTiles = new List<Tile>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile tile = grid[x, y];
                if (tile != null &&
                    !tile.isSpecial &&
                    !tile.isBloodDrop &&
                    !tile.isBat &&
                    !tile.isVampire)
                {
                    allNormalTiles.Add(tile);
                }
            }
        }

        // 2. RASTGELE TILE'LARI SEÇ (EPİCENTER'A YAKIN OLANLARA ÖNCELİK)
        List<Tile> tilesToTransform = new List<Tile>();

        if (allNormalTiles.Count > 0)
        {
            // Önce epicenter'a yakın olanları sırala
            allNormalTiles.Sort((a, b) =>
                Vector3.Distance(a.transform.position, epicenter).CompareTo(
                Vector3.Distance(b.transform.position, epicenter)));

            // İlk 'count' kadar tile'ı seç (veya daha az)
            int transformCount = Mathf.Min(count, allNormalTiles.Count);
            for (int i = 0; i < transformCount; i++)
            {
                tilesToTransform.Add(allNormalTiles[i]);
            }
        }

        if (tilesToTransform.Count == 0)
        {
            Debug.Log("[Vampirik Dönüşüm] Dönüştürülecek tile bulunamadı!");
            yield break;
        }

        Debug.Log($"[Vampirik Dönüşüm] {tilesToTransform.Count} tile dönüştürülecek");

        // 3. DÖNÜŞÜM ANİMASYONU
        List<Coroutine> transformCoroutines = new List<Coroutine>();

        foreach (Tile tile in tilesToTransform)
        {
            Coroutine transformRoutine = StartCoroutine(
                TransformTileToBloodDrop(tile, bloodColor, epicenter)
            );
            transformCoroutines.Add(transformRoutine);

            // Kademeli başlat
            yield return new WaitForSeconds(0.1f);
        }

        // Tüm dönüşümler bitene kadar bekle
        foreach (Coroutine routine in transformCoroutines)
        {
            yield return routine;
        }

        yield return new WaitForSeconds(0.3f);

        // 4. YENİ MATCH KONTROLÜ
        List<Tile> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
        {
            yield return StartCoroutine(ResolveBoard());
        }
    }

    // ===================== TİLE'DAN KAN DAMLASINA DÖNÜŞÜM =====================
    // ===================== TİLE'DAN KAN DAMLASINA DÖNÜŞÜM =====================
    IEnumerator TransformTileToBloodDrop(Tile originalTile, TileType bloodColor, Vector3 epicenter)
    {
        if (originalTile == null) yield break;

        int x = originalTile.x;
        int y = originalTile.y;
        Vector3 tilePos = originalTile.transform.position;

        // 1. ÖNCE MEVCUT TİLE'IN ÜZERİNDE EFEKT
        if (particleEffectManager != null)
        {
            particleEffectManager.PlayEffect(originalTile.tileType, tilePos);
        }

        // 2. TİLE'ı YOK ET
        grid[x, y] = null;
        Destroy(originalTile.gameObject);

        yield return new WaitForSeconds(0.1f);

        // 3. KAN DALGASI ANİMASYONU
        yield return StartCoroutine(PlayBloodWaveEffect(epicenter, tilePos, 0.3f));

        // 4. YENİ KAN DAMLASI OLUŞTUR (ÖNDE!)
        Vector3 bloodDropPos = new Vector3(tilePos.x, tilePos.y, -1f); // Z = -1 (ön plan)
        Tile bloodDrop = CreateBloodDropAtPosition(x, y, bloodColor, bloodDropPos);

        // 5. KISA BÜYÜME ANİMASYONU
        float growDuration = 0.3f;
        float elapsed = 0f;
        Vector3 startScale = new Vector3(0.1f, 0.1f, 1f);
        Vector3 endScale = new Vector3(0.5f, 0.5f, 1f);

        bloodDrop.transform.localScale = startScale;

        while (elapsed < growDuration)
        {
            if (bloodDrop == null) yield break;

            float t = elapsed / growDuration;
            t = Mathf.Sin(t * Mathf.PI * 0.5f);

            bloodDrop.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (bloodDrop != null)
            bloodDrop.transform.localScale = endScale;

        // 6. Grid'e yerleştir
        grid[x, y] = bloodDrop;

        // 7. SES EFEKTİ
        if (bloodDropSound != null)
        {
            AudioSource.PlayClipAtPoint(bloodDropSound, Camera.main.transform.position, 0.4f);
        }
    }

    // Elastic out easing fonksiyonu
    float ElasticOut(float t)
    {
        if (t <= 0) return 0;
        if (t >= 1) return 1;

        float p = 0.3f;
        return Mathf.Pow(2, -10 * t) * Mathf.Sin((t - p / 4) * (2 * Mathf.PI) / p) + 1;
    }

    // Bitirme titreşim efekti
    IEnumerator FinishPulse(Transform targetTransform, Vector3 normalScale, float duration)
    {
        if (targetTransform == null) yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (targetTransform == null) yield break;

            float t = elapsed / duration;
            float pulse = 1f + Mathf.Sin(t * Mathf.PI * 4f) * 0.05f; // Çok hafif titreşim

            targetTransform.localScale = normalScale * pulse;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (targetTransform != null)
            targetTransform.localScale = normalScale;
    }



    // ===================== KAN DALGASI EFEKTİ =====================
    IEnumerator PlayBloodWaveEffect(Vector3 fromPos, Vector3 toPos, float duration)
    {
        // Bu efekt için bir GameObject veya partikül sistemi kullanabilirsiniz
        // Basit versiyon:
        if (particleEffectManager != null)
        {
            // Aradaki noktalarda partikül oluştur
            int steps = 10;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 pos = Vector3.Lerp(fromPos, toPos, t);
                particleEffectManager.PlayEffect(TileType.BloodDrop, pos);
                yield return new WaitForSeconds(duration / steps);
            }
        }
    }

    // ===================== KAN DAMLASI OLUŞTURMA (SABİT 0.5 BOYUT) =====================
    // ===================== KAN DAMLASI OLUŞTURMA (SABİT 0.5 BOYUT) =====================
    Tile CreateBloodDropAtPosition(int x, int y, TileType bloodColor, Vector3 position)
    {
        GameObject bloodDropObj = Instantiate(bloodDropPrefab, position, Quaternion.identity, transform);
        Tile bloodDrop = bloodDropObj.GetComponent<Tile>();

        bloodDrop.x = x;
        bloodDrop.y = y;
        bloodDrop.tileType = TileType.BloodDrop;
        bloodDrop.isSpecial = true;
        bloodDrop.isBloodDrop = true;
        bloodDrop.isVampire = false;
        bloodDrop.isBat = false;
        bloodDrop.bloodDropColor = bloodColor;

        // BOYUTU 0.5 YAP
        bloodDrop.transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        // SADECE BU SATIRI EKLEYİN:
        bloodDrop.transform.position = new Vector3(position.x, position.y, -100f); // ÇOK ÖNDE!

        // Görseli güncelle
        bloodDrop.UpdateBloodDropVisual();

        return bloodDrop;
    }
    IEnumerator HandleMatchesWithoutSpecialTiles(List<Tile> matches)
    {
        if (matches == null || matches.Count == 0)
            yield break;

        List<Tile> filteredMatches = new List<Tile>();
        foreach (Tile tile in matches)
        {
            if (tile != null &&
                !tile.isSpecial &&
                !tile.isBloodDrop &&
                !tile.isBat &&
                !tile.isVampire)
            {
                filteredMatches.Add(tile);
            }
        }

        if (filteredMatches.Count == 0)
            yield break;

        // SES VE EFEKTLER
        if (matchSound != null)
        {
            AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position);
        }

        foreach (Tile tile in filteredMatches)
        {
            if (tile != null && particleEffectManager != null)
            {
                particleEffectManager.PlayEffect(tile.tileType, tile.transform.position);
            }
        }

        yield return new WaitForSeconds(0.15f);

        // GRİD'DEN SİL VE YOK ET
        foreach (Tile tile in filteredMatches)
        {
            if (tile != null)
            {
                grid[tile.x, tile.y] = null;
                Destroy(tile.gameObject);
            }
        }

        // SKOR GÜNCELLE
        OnMoveResolved(filteredMatches.Count);
    }
    IEnumerator CreateAllSpecialTiles()
    {
        // ÖNCE KAN DAMLALARI
        if (shouldCreateSpecialTile)
        {
            yield return StartCoroutine(CreateSpecialTileAfterDelay());
        }

        // SONRA YARASALAR
        if (shouldCreateBat)
        {
            yield return StartCoroutine(CreateBatAfterDelay());
        }
    }
}