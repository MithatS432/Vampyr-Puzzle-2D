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
    public GameObject vampirePrefab;

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

    private bool shouldCreateVampire = false;
    private int vampireX = 0;
    private int vampireY = 0;

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
        targetCount = Random.Range(150, 251);
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
                    if (selectedTile.isVampire && targetTile.isVampire)
                    {
                        StartCoroutine(CombineTwoVampires(selectedTile, targetTile));
                    }


                    if (selectedTile.isVampire && targetTile.isBloodDrop)
                    {
                        StartCoroutine(ActivateVampireWithBloodDrop(selectedTile, targetTile));
                    }
                    else if (selectedTile.isBloodDrop && targetTile.isVampire)
                    {
                        StartCoroutine(ActivateVampireWithBloodDrop(targetTile, selectedTile));
                    }
                    else if (selectedTile.isVampire && targetTile.isBat)
                    {
                        StartCoroutine(CombineVampireWithBat(selectedTile, targetTile));
                    }
                    else if (selectedTile.isBat && targetTile.isVampire)
                    {
                        StartCoroutine(CombineVampireWithBat(targetTile, selectedTile));
                    }
                    else if (selectedTile.isVampire && !targetTile.isSpecial)
                    {
                        StartCoroutine(ActivateVampireWithNormalTile(selectedTile, targetTile));
                    }
                    else if (targetTile.isVampire && !selectedTile.isSpecial)
                    {
                        StartCoroutine(ActivateVampireWithNormalTile(targetTile, selectedTile));
                    }

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
            float duration = 0.45f + Random.Range(0.15f, 0.35f);

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
        // 🔒 KRİTİK KORUMA
        if (a == null || b == null)
            yield break;

        // Destroy edilmiş objeler için Unity özel null kontrolü
        if (!a || !b)
            yield break;

        if (boardBusy)
            yield break;

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

    IEnumerator SmoothMove(Transform obj, Vector3 target, float duration)
    {
        Vector3 start = obj.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            obj.position = Vector3.Lerp(start, target, smoothT);
            yield return null;
        }

        obj.position = target;
    }

    IEnumerator ResolveBoard()
    {
        boardBusy = true;

        List<Tile> matches = FindAllMatches();

        while (matches.Count > 0)
        {
            // 1. EŞLEŞMELERİ PATLAT
            yield return StartCoroutine(HandleMatches(matches));

            // 2. TILE'LARI DÜŞÜR
            yield return StartCoroutine(DropTiles());

            // 3. YENİ TILE'LAR EKLE
            yield return StartCoroutine(RefillTiles());

            if (shouldCreateVampire)
                yield return StartCoroutine(CreateVampireAfterDelay());


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

    IEnumerator CreateVampireAfterDelay()
    {
        // küçük gecikme varsa bırakın; yoksa doğrudan devam edebiliriz
        yield return new WaitForSeconds(0.12f);

        if (!shouldCreateVampire)
            yield break;

        // bounds kontrolü
        if (vampireX < 0 || vampireX >= width || vampireY < 0 || vampireY >= height)
        {
            shouldCreateVampire = false;
            yield break;
        }

        // hedef pozisyonu değişkene al
        int tx = vampireX;
        int ty = vampireY;

        // Eğer hedef hücre boş ise doğrudan oluştur
        if (grid[tx, ty] == null)
        {
            InstantiateVampireAt(tx, ty);
            shouldCreateVampire = false;
            yield break;
        }

        // Eğer dolu ama dolu olan normal bir tile ise onu kaldırıp vampiri oluştur
        Tile existing = grid[tx, ty];
        if (existing != null && !existing.isSpecial)
        {
            grid[tx, ty] = null;
            Destroy(existing.gameObject);
            yield return new WaitForSeconds(0.05f); // küçük bekleme his vermek için
            InstantiateVampireAt(tx, ty);
            shouldCreateVampire = false;
            yield break;
        }

        // Eğer dolu ve özelse, yakın bir boş hücre yeğle (manhattan radius 1..2)
        bool placed = false;
        for (int r = 1; r <= 2 && !placed; r++)
        {
            for (int ox = -r; ox <= r; ox++)
            {
                for (int oy = -r; oy <= r; oy++)
                {
                    if (Mathf.Abs(ox) + Mathf.Abs(oy) > r) continue; // manhattan sınırlı
                    int nx = tx + ox;
                    int ny = ty + oy;
                    if (nx < 0 || nx >= width || ny < 0 || ny >= height) continue;
                    if (grid[nx, ny] == null)
                    {
                        InstantiateVampireAt(nx, ny);
                        placed = true;
                        break;
                    }
                }
                if (placed) break;
            }
        }
        shouldCreateVampire = false;
    }


    void InstantiateVampireAt(int x, int y)
    {
        Vector3 pos = GetTileWorldPosition(x, y);
        GameObject prefab = vampirePrefab != null ? vampirePrefab : redPrefab;
        GameObject obj = Instantiate(prefab, pos, Quaternion.identity, transform);

        Tile tile = obj.GetComponent<Tile>();
        if (tile == null)
        {
            Debug.LogWarning("[Vampire] Oluşturulan prefab Tile component içermiyor!");
            Destroy(obj);
            return;
        }

        tile.x = x;
        tile.y = y;

        // Vampir için benzersiz davranış: isSpecial=true yapıyoruz
        tile.isSpecial = true;
        tile.isVampire = true;
        tile.isBat = false;
        tile.isBloodDrop = false;

        // Eğer Tile.UpdateVisual() isVampire'ı magenta yapıyorsa yeterli; yoksa tile.tileType ayarlayın
        tile.UpdateVisual();

        // grid'e yerleştir
        grid[x, y] = tile;

        if (particleEffectManager != null)
            particleEffectManager.PlayEffect(TileType.Vampyr, pos);

        if (matchSound != null)
            AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position, 0.6f);

        Debug.Log($"[Vampire] Oluşturuldu: ({x},{y})");
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
        CheckTAndLShapes(result);


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
        if (startTile.isSpecial) return;


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
     !a.isSpecial && !b.isSpecial && !c.isSpecial && !d.isSpecial &&
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




    void CheckTAndLShapes(HashSet<Tile> result)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile center = grid[x, y];
                if (center == null) continue;

                TileType color = center.tileType;

                // --- T / + detection (center intersection) ---
                int left = CountRunFrom(x - 1, y, -1, 0, color);
                int right = CountRunFrom(x + 1, y, 1, 0, color);
                int horizLen = left + 1 + right;

                int down = CountRunFrom(x, y - 1, 0, -1, color);
                int up = CountRunFrom(x, y + 1, 0, 1, color);
                int vertLen = down + 1 + up;

                if (horizLen >= 3 && vertLen >= 3)
                {
                    // horizontal
                    for (int hx = x - left; hx <= x + right; hx++)
                    {
                        Tile t = grid[hx, y];
                        if (t != null) result.Add(t);
                    }
                    // vertical
                    for (int vy = y - down; vy <= y + up; vy++)
                    {
                        Tile t = grid[x, vy];
                        if (t != null) result.Add(t);
                    }

                    CreateVampireAfterMatch(x, y);
                    continue;
                }

                // --- L-shape detection (corner based) ---
                // up + right (corner at x,y)
                int vertUpFromCorner = CountRunFrom(x, y, 0, 1, color);
                int horizRightFromCorner = CountRunFrom(x, y, 1, 0, color);
                if (vertUpFromCorner >= 3 && horizRightFromCorner >= 3)
                {
                    for (int vy = y; vy <= y + vertUpFromCorner - 1; vy++)
                        if (grid[x, vy] != null) result.Add(grid[x, vy]);
                    for (int hx = x; hx <= x + horizRightFromCorner - 1; hx++)
                        if (grid[hx, y] != null) result.Add(grid[hx, y]);

                    CreateVampireAfterMatch(x, y);
                    continue;
                }

                // up + left
                int vertUpFromCorner2 = CountRunFrom(x, y, 0, 1, color);
                int horizLeftFromCorner = CountRunFrom(x, y, -1, 0, color);
                if (vertUpFromCorner2 >= 3 && horizLeftFromCorner >= 3)
                {
                    for (int vy = y; vy <= y + vertUpFromCorner2 - 1; vy++)
                        if (grid[x, vy] != null) result.Add(grid[x, vy]);
                    for (int hx = x; hx >= x - (horizLeftFromCorner - 1); hx--)
                        if (grid[hx, y] != null) result.Add(grid[hx, y]);

                    CreateVampireAfterMatch(x, y);
                    continue;
                }

                // down + right
                int vertDownFromCorner = CountRunFrom(x, y, 0, -1, color);
                int horizRightFromCorner2 = CountRunFrom(x, y, 1, 0, color);
                if (vertDownFromCorner >= 3 && horizRightFromCorner2 >= 3)
                {
                    for (int vy = y; vy >= y - (vertDownFromCorner - 1); vy--)
                        if (grid[x, vy] != null) result.Add(grid[x, vy]);
                    for (int hx = x; hx <= x + horizRightFromCorner2 - 1; hx++)
                        if (grid[hx, y] != null) result.Add(grid[hx, y]);

                    CreateVampireAfterMatch(x, y);
                    continue;
                }

                // down + left
                int vertDownFromCorner2 = CountRunFrom(x, y, 0, -1, color);
                int horizLeftFromCorner2 = CountRunFrom(x, y, -1, 0, color);
                if (vertDownFromCorner2 >= 3 && horizLeftFromCorner2 >= 3)
                {
                    for (int vy = y; vy >= y - (vertDownFromCorner2 - 1); vy--)
                        if (grid[x, vy] != null) result.Add(grid[x, vy]);
                    for (int hx = x; hx >= x - (horizLeftFromCorner2 - 1); hx--)
                        if (grid[hx, y] != null) result.Add(grid[hx, y]);

                    CreateVampireAfterMatch(x, y);
                    continue;
                }
            }
        }
    }


    void CreateVampireAfterMatch(int x, int y)
    {
        shouldCreateVampire = true;
        vampireX = x;
        vampireY = y;
        Debug.Log($"[Vampire] İşaretlendi: ({x},{y})");
    }
    private int CountRunFrom(int sx, int sy, int dx, int dy, TileType color)
    {
        int cnt = 0;
        int cx = sx, cy = sy;
        while (cx >= 0 && cx < width && cy >= 0 && cy < height)
        {
            Tile t = grid[cx, cy];
            if (t != null && t.tileType == color) { cnt++; cx += dx; cy += dy; }
            else break;
        }
        return cnt;
    }


    IEnumerator ActivateVampireWithBloodDrop(Tile vampire, Tile bloodDrop)
    {
        if (vampire == null || bloodDrop == null) yield break;
        if (!vampire.isVampire || !bloodDrop.isBloodDrop) yield break;
        if (boardBusy) yield break;

        boardBusy = true;

        Debug.Log($"[Vampire+BloodDrop] Başlatılıyor: Vampir ({vampire.x},{vampire.y}) + KanDamla ({bloodDrop.x},{bloodDrop.y})");

        // 1) Hamle azalt (isteğe göre)
        totalMoves--;
        UpdateUI();

        // 2) Ses/efekt (isteğe bağlı)
        if (matchSound != null)
            AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position, 0.8f);

        if (particleEffectManager != null)
            particleEffectManager.PlayEffect(TileType.Vampyr, vampire.transform.position);

        // 3) Grid'den vampir ve kan damlasını kaldır (önce grid, sonra yok et)
        int vx = vampire.x, vy = vampire.y;
        int bx = bloodDrop.x, by = bloodDrop.y;

        if (vx >= 0 && vx < width && vy >= 0 && vy < height)
            grid[vx, vy] = null;
        if (bx >= 0 && bx < width && by >= 0 && by < height)
            grid[bx, by] = null;

        Destroy(vampire.gameObject);
        Destroy(bloodDrop.gameObject);

        // Kısa bekleme animasyon hissi için
        yield return new WaitForSeconds(0.08f);

        // 4) 3x3 alanı hesapla (kan damlası merkezli)
        int startX = Mathf.Max(0, bx - 1);
        int endX = Mathf.Min(width - 1, bx + 1);
        int startY = Mathf.Max(0, by - 1);
        int endY = Mathf.Min(height - 1, by + 1);

        // 5) Yok edilecek tile'ları topla
        List<Tile> tilesToDestroy = new List<Tile>();
        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                Tile t = grid[x, y];
                if (t != null)
                {
                    tilesToDestroy.Add(t);
                }
            }
        }

        Debug.Log($"[Vampire+BloodDrop] 3x3 içinde {tilesToDestroy.Count} tile yok edilecek.");

        // 6) Grid'den kaldır ve efekt çal
        if (tilesToDestroy.Count > 0)
        {
            foreach (Tile t in tilesToDestroy)
            {
                if (t == null) continue;
                grid[t.x, t.y] = null;
            }

            // Ses / partikül
            if (matchSound != null)
                AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position, 0.7f);

            foreach (Tile t in tilesToDestroy)
            {
                if (t != null && particleEffectManager != null)
                    particleEffectManager.PlayEffect(t.tileType, t.transform.position);
            }

            yield return new WaitForSeconds(0.12f);

            foreach (Tile t in tilesToDestroy)
            {
                if (t != null)
                    Destroy(t.gameObject);
            }
        }

        // 7) Skor / hedef güncelle (yok edilen sayıya göre)
        int destroyedCount = tilesToDestroy.Count;
        if (destroyedCount == 0)
            destroyedCount = 1; // en az 1 olarak saymak isterseniz
        OnMoveResolved(destroyedCount);

        // 8) Drop ve refill
        yield return StartCoroutine(DropTiles());
        yield return StartCoroutine(RefillTiles());

        // 9) Yeni match kontrolü
        List<Tile> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
            yield return StartCoroutine(ResolveBoard());

        boardBusy = false;
        CheckGameState();
    }



    IEnumerator CombineVampireWithBat(Tile vampire, Tile bat)
    {
        if (vampire == null || bat == null) yield break;
        if (!vampire.isVampire || !bat.isBat) yield break;
        if (boardBusy) yield break;

        boardBusy = true;

        // 1) Hamle azalt (isteğe göre)
        totalMoves--;
        UpdateUI();

        // 2) Ses & başlangıç efektleri
        if (batSound != null)
            AudioSource.PlayClipAtPoint(batSound, Camera.main.transform.position, 0.9f);
        if (particleEffectManager != null)
            particleEffectManager.PlayEffect(TileType.Bat, vampire.transform.position);

        // 3) Vampire ve yarasayı grid'den kaldır (önce grid, sonra Destroy)
        int vx = vampire.x, vy = vampire.y;
        int bx = bat.x, by = bat.y;

        if (vx >= 0 && vx < width && vy >= 0 && vy < height) grid[vx, vy] = null;
        if (bx >= 0 && bx < width && by >= 0 && by < height) grid[bx, by] = null;

        Destroy(vampire.gameObject);
        Destroy(bat.gameObject);

        yield return new WaitForSeconds(0.08f);

        // 4) Hedef adaylarını topla (normal tile'lar: special/blooddrop/bat olmayanlar)
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

        int spawnCount = Mathf.Min(20, candidates.Count);
        Vector3 centerPos = ((vampire != null) ? vampire.transform.position : GetTileWorldPosition(bx, by)); // vampire destroyed - fallback center

        List<Coroutine> moveRoutines = new List<Coroutine>();
        int destroyedCount = 0;

        // 5) Spawn ve gönder (stagger ile)
        for (int i = 0; i < spawnCount; i++)
        {
            Tile target = candidates[i];
            if (target == null) continue;

            // Proje görseli: batPrefab kullanıyoruz (aynı görsel)
            GameObject proj = Instantiate(batPrefab, centerPos, Quaternion.identity, transform);

            // Eğer prefab içinde Tile component varsa, etkileşimi kapat
            Tile projTile = proj.GetComponent<Tile>();
            if (projTile != null)
            {
                projTile.isBat = false;
                projTile.isSpecial = false;
                projTile.isBloodDrop = false;
            }

            Vector3 targetPos = target.transform.position;
            float duration = 0.45f + Random.Range(0.15f, 0.35f);

            // küçük stagger
            yield return new WaitForSeconds(0.02f);

            Coroutine c = StartCoroutine(MoveProjectileAndDestroyTargetVamp(proj, target, targetPos, duration, () => { destroyedCount++; }));
            moveRoutines.Add(c);
        }

        // 6) Büyük efekt ortada (opsiyonel)
        if (particleEffectManager != null)
            particleEffectManager.PlayEffect(TileType.Vampyr, centerPos);

        // 7) Tüm projelerin bitmesini bekle
        foreach (Coroutine r in moveRoutines)
            yield return r;

        // 8) Skor / hedef güncellemesi
        if (destroyedCount > 0)
        {
            OnMoveResolved(destroyedCount);
        }
        else
        {
            // Hiç yok edilmediyse yine en az 1 hamle sayısı tüketilmişti; isteğe bağlı:
            OnMoveResolved(1);
        }

        // 9) Drop & refill & resolve
        yield return StartCoroutine(DropTiles());
        yield return StartCoroutine(RefillTiles());

        List<Tile> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
            yield return StartCoroutine(ResolveBoard());

        boardBusy = false;
        CheckGameState();
    }



    IEnumerator MoveProjectileAndDestroyTargetVamp(GameObject proj, Tile target, Vector3 targetPos, float duration, System.Action onArrive)
    {
        if (proj == null) yield break;

        // Hareket
        yield return StartCoroutine(SmoothMove(proj.transform, targetPos, duration));

        // Hedef var ise yok et
        if (target != null)
        {
            int tx = target.x;
            int ty = target.y;

            if (tx >= 0 && tx < width && ty >= 0 && ty < height && grid[tx, ty] == target)
                grid[tx, ty] = null;

            // Ses & efekt
            if (matchSound != null)
                AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position, 0.7f);

            if (particleEffectManager != null)
                particleEffectManager.PlayEffect(target.tileType, targetPos);

            Destroy(target.gameObject);
            onArrive?.Invoke();
        }

        Destroy(proj);
        yield return null;
    }


    IEnumerator CombineTwoVampires(Tile vamp1, Tile vamp2)
    {
        if (vamp1 == null || vamp2 == null) yield break;
        if (!vamp1.isVampire || !vamp2.isVampire) yield break;
        if (boardBusy) yield break;

        boardBusy = true;

        Debug.Log($"[Vampire Combo] Başlatılıyor: Vampir1 ({vamp1.x},{vamp1.y}) + Vampir2 ({vamp2.x},{vamp2.y})");

        // 1) Hamle azalt
        totalMoves--;
        UpdateUI();

        // 2) Başlangıç ses/efekt
        if (matchSound != null)
            AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position, 1f);
        if (particleEffectManager != null)
        {
            // Ortada efekt oynatmak için ortalama pozisyon
            Vector3 centerPos = (vamp1.transform.position + vamp2.transform.position) * 0.5f;
            particleEffectManager.PlayEffect(TileType.Vampyr, centerPos);
        }

        // 3) Önce grid'den vampirleri kaldır
        int v1x = vamp1.x, v1y = vamp1.y;
        int v2x = vamp2.x, v2y = vamp2.y;

        if (v1x >= 0 && v1x < width && v1y >= 0 && v1y < height) grid[v1x, v1y] = null;
        if (v2x >= 0 && v2x < width && v2y >= 0 && v2y < height) grid[v2x, v2y] = null;

        // 4) Destroy vampir GameObject'leri
        Destroy(vamp1.gameObject);
        Destroy(vamp2.gameObject);

        // Küçük bekleme efekt hissi
        yield return new WaitForSeconds(0.08f);

        // 5) Tüm grid'i tarayıp yok edilecek tile'ları topla
        List<Tile> tilesToDestroy = new List<Tile>();
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile t = grid[x, y];
                if (t != null)
                {
                    // Eğer special'ları KORUMAK isterseniz buraya !t.isSpecial koşulu ekleyin.
                    tilesToDestroy.Add(t);
                }
            }
        }

        int destroyedCount = tilesToDestroy.Count;
        Debug.Log($"[Vampire Combo] Tüm grid temizleniyor. Yok edilecek tile sayısı: {destroyedCount}");

        // 6) Grid'den kaldır ve efektleri tetikle
        if (tilesToDestroy.Count > 0)
        {
            // Grid'den null'la
            foreach (Tile t in tilesToDestroy)
            {
                if (t != null)
                    grid[t.x, t.y] = null;
            }

            // Tek seferde ses çal (zorluk olmaması için)
            if (matchSound != null)
                AudioSource.PlayClipAtPoint(matchSound, Camera.main.transform.position, 1f);

            // Her hedef için partikül (isteğe göre tümüne uygulansın)
            foreach (Tile t in tilesToDestroy)
            {
                if (t != null && particleEffectManager != null)
                    particleEffectManager.PlayEffect(t.tileType, t.transform.position);
            }

            // Kısa bekleme, efektlerin görünmesi için
            yield return new WaitForSeconds(0.15f);

            // Destroy gameObjects
            foreach (Tile t in tilesToDestroy)
            {
                if (t != null)
                    Destroy(t.gameObject);
            }
        }

        // 7) Skor/target güncellemesi
        if (destroyedCount > 0)
        {
            OnMoveResolved(destroyedCount);
        }
        else
        {
            // Hiç yok edilmediyse yine 1 işlem sayısı azaldıysa OnMoveResolved(1) çağrılabilir
            OnMoveResolved(1);
        }

        // 8) Drop & refill
        yield return StartCoroutine(DropTiles());
        yield return StartCoroutine(RefillTiles());

        // 9) Yeni match kontrolü ve çözüm
        List<Tile> newMatches = FindAllMatches();
        if (newMatches.Count > 0)
            yield return StartCoroutine(ResolveBoard());

        boardBusy = false;
        CheckGameState();

        Debug.Log("[Vampire Combo] Tamamlandı");
    }



    IEnumerator ActivateVampireWithNormalTile(Tile vampire, Tile normalTile)
    {
        if (boardBusy) yield break;
        if (vampire == null || normalTile == null) yield break;
        if (!vampire.isVampire || normalTile.isSpecial) yield break;

        boardBusy = true;

        // 1️⃣ Grid’den kaldır
        grid[vampire.x, vampire.y] = null;
        grid[normalTile.x, normalTile.y] = null;

        Vector3 vPos = vampire.transform.position;
        Vector3 nPos = normalTile.transform.position;

        // 2️⃣ Yarasa çıkış noktası
        Vector3 spawnOrigin = (vPos + nPos) * 0.5f;

        // 3️⃣ Efekt
        if (particleEffectManager != null)
        {
            particleEffectManager.PlayEffect(TileType.Vampyr, vPos);
        }

        // 4️⃣ Yok et
        Destroy(vampire.gameObject);
        Destroy(normalTile.gameObject);

        yield return new WaitForSeconds(0.15f);

        // 5️⃣ Normal tile’ları topla
        List<Tile> normalTiles = new List<Tile>();

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Tile t = grid[x, y];
                if (t != null && !t.isSpecial)
                {
                    normalTiles.Add(t);
                }
            }
        }

        // 6️⃣ Rastgele 3 tanesini seç
        int spawnCount = Mathf.Min(3, normalTiles.Count);

        for (int i = 0; i < spawnCount; i++)
        {
            Tile target = normalTiles[Random.Range(0, normalTiles.Count)];
            normalTiles.Remove(target);

            int tx = target.x;
            int ty = target.y;

            // 🔒 Hedef pozisyonu ÖNCE al
            Vector3 targetWorldPos = target.transform.position;

            // 🔒 Grid temizle
            grid[tx, ty] = null;

            // 🔒 Tile yok et
            Destroy(target.gameObject);

            yield return new WaitForSeconds(0.12f);

            // 7️⃣ Yarasa animasyonla gitsin
            yield return StartCoroutine(
                AnimateBatToTarget(spawnOrigin, tx, ty, targetWorldPos)
            );
        }

        // 8️⃣ Düşme & doldurma
        yield return StartCoroutine(DropTiles());
        yield return StartCoroutine(RefillTiles());

        // 9️⃣ Match kontrol
        List<Tile> matches = FindAllMatches();
        if (matches.Count > 0)
            yield return StartCoroutine(ResolveBoard());

        // 🔟 Hamle
        totalMoves--;
        UpdateUI();

        boardBusy = false;
        CheckGameState();
    }



    IEnumerator AnimateBatToTarget(
     Vector3 startPos,
     int targetX,
     int targetY,
     Vector3 targetWorldPos
 )
    {
        GameObject batObj =
            Instantiate(batPrefab, startPos, Quaternion.identity, transform);

        Tile bat = batObj.GetComponent<Tile>();

        float duration = 0.6f + Random.Range(0.15f, 0.3f);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // ease-out (sona doğru yavaşlar)
            float smoothT = 1f - Mathf.Pow(1f - t, 3f);

            batObj.transform.position =
                Vector3.Lerp(startPos, targetWorldPos, smoothT);

            yield return null;
        }

        // Tam hedefe kilitle
        batObj.transform.position = targetWorldPos;

        // Tile bilgileri
        bat.x = targetX;
        bat.y = targetY;
        bat.tileType = TileType.Bat;
        bat.isSpecial = true;
        bat.isBat = true;

        grid[targetX, targetY] = bat;

        if (particleEffectManager != null)
        {
            particleEffectManager.PlayEffect(TileType.Bat, targetWorldPos);
        }
    }


}