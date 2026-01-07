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
    private Vector2 mouseStartPos;
    private bool isDragging = false;



    void Start()
    {
        targetCount = Random.Range(20, 101);
        totalMoves = 15;

        currentBlood = 0f;
        gameEnded = false;

        CreateGrid();
        UpdateUI();
        UpdateBloodUI();
        StartCoroutine(ClearInitialMatches());

    }
    void Update()
    {
        if (gameEnded) return;

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

    void HandleMouseDrag()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // Başlangıç pozisyonu
            Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Collider2D hit = Physics2D.OverlapPoint(worldPos);

            if (hit != null)
            {
                Tile tile = hit.GetComponent<Tile>();
                if (tile != null)
                {
                    selectedTile = tile;
                    mouseStartPos = worldPos;
                    isDragging = true;
                }
            }
        }

        if (Input.GetMouseButtonUp(0) && selectedTile != null)
        {
            // Bırakma ile drag bitiyor
            isDragging = false;
            selectedTile = null;
        }

        if (isDragging && selectedTile != null)
        {
            Vector2 currentPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 delta = currentPos - mouseStartPos;

            // Yatay veya dikey hareket kontrolü
            if (delta.magnitude >= 0.3f) // minimum sürükleme mesafesi
            {
                Tile swapTile = null;

                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                {
                    // Yatay sürükleme
                    int dir = delta.x > 0 ? 1 : -1;
                    swapTile = GetTileAt(selectedTile.x + dir, selectedTile.y);
                }
                else
                {
                    // Dikey sürükleme
                    int dir = delta.y > 0 ? 1 : -1;
                    swapTile = GetTileAt(selectedTile.x, selectedTile.y + dir);
                }

                if (swapTile != null)
                {
                    StartCoroutine(TrySwapSafe(selectedTile, swapTile));
                    isDragging = false;
                    selectedTile = null;
                }
            }
        }
    }
    Tile GetTileAt(int x, int y)
    {
        if (x >= 0 && x < width && y >= 0 && y < height)
            return grid[x, y];
        return null;
    }

    IEnumerator TrySwapSafe(Tile a, Tile b)
    {
        if (a == null || b == null) yield break;

        Vector3 posA = a.transform.position;
        Vector3 posB = b.transform.position;

        // Swap görsel ve grid
        a.transform.position = posB;
        b.transform.position = posA;

        grid[a.x, a.y] = b;
        grid[b.x, b.y] = a;

        // tempX ve tempY zaten tanımlı üstte, tekrar int yazma
        int tempX = a.x;
        int tempY = a.y;
        a.x = b.x; a.y = b.y;
        b.x = tempX; b.y = tempY;

        // Kısa bekleme swap animasyonu için
        yield return new WaitForSeconds(0.1f);

        List<Tile> matches = FindAllMatches();

        if (matches.Count > 0)
        {
            // Match varsa yok et ve düşür
            yield return StartCoroutine(HandleMatches(matches));
        }
        else
        {
            // Match yoksa swap geri al ve hamleyi düşür
            yield return StartCoroutine(MoveTile(a, posA, 0.25f));
            yield return StartCoroutine(MoveTile(b, posB, 0.25f));

            grid[a.x, a.y] = a;
            grid[b.x, b.y] = b;

            // Sadece atama yap, tekrar int yazma
            tempX = a.x; tempY = a.y;
            a.x = b.x; a.y = b.y;
            b.x = tempX; b.y = tempY;

            totalMoves--;
            UpdateUI();

            // DropAndRefill fonksiyonunu çağırırken aynı isim kullan
            yield return StartCoroutine(DropAndRefill());

            CheckGameState();
        }
    }
    IEnumerator DropAndRefill()
    {
        List<Tile> allMatches = FindAllMatches();

        while (allMatches.Count > 0)
        {
            // Match varsa, targetCount ve blood azalt
            int destroyedCount = allMatches.Count;
            targetCount -= destroyedCount;
            targetCount = Mathf.Max(0, targetCount);

            currentBlood -= destroyedCount * bloodDecreasePerMatch;
            currentBlood = Mathf.Clamp01(currentBlood);

            UpdateUI();
            UpdateBloodUI();

            // Efektler ve grid’den kaldır
            foreach (Tile tile in allMatches)
            {
                if (tile != null && particleEffectManager != null)
                    particleEffectManager.PlayEffect(tile.tileType, tile.transform.position);

                if (tile != null)
                    grid[tile.x, tile.y] = null;

                if (tile != null)
                    Destroy(tile.gameObject);
            }

            yield return new WaitForSeconds(0.1f);

            // Taşları aşağı düşür
            for (int x = 0; x < width; x++)
            {
                for (int y = 1; y < height; y++)
                {
                    if (grid[x, y] == null) continue;

                    int fallDistance = 0;
                    for (int ny = y - 1; ny >= 0; ny--)
                    {
                        if (grid[x, ny] == null)
                            fallDistance++;
                        else break;
                    }

                    if (fallDistance > 0)
                    {
                        Tile tile = grid[x, y];
                        grid[x, y] = null;
                        grid[x, y - fallDistance] = tile;

                        StartCoroutine(MoveTile(tile, GetTileWorldPosition(x, y - fallDistance), 0.2f));
                    }
                }
            }

            yield return new WaitForSeconds(0.25f);

            // Yukarıdan yeni taş ekle
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    if (grid[x, y] == null)
                    {
                        TileType type = GetRandomNormalTile();
                        GameObject prefab = GetPrefabByType(type);
                        Vector3 spawnPos = GetTileWorldPosition(x, y + height);
                        GameObject tileObj = Instantiate(prefab, spawnPos, Quaternion.identity, transform);
                        Tile tile = tileObj.GetComponent<Tile>();
                        tile.x = x;
                        tile.y = y;
                        tile.tileType = type;
                        grid[x, y] = tile;

                        StartCoroutine(MoveTile(tile, GetTileWorldPosition(x, y), 0.3f));
                    }
                }
            }

            yield return new WaitForSeconds(0.35f);

            // Bir sonraki match kontrolü
            allMatches = FindAllMatches();
        }

        // Hamle sonrası kontroller
        CheckGameState();
    }

    IEnumerator HandleMatches(List<Tile> matches)
    {
        if (matches == null || matches.Count == 0) yield break;

        // 1️⃣ Efektleri oynat
        foreach (Tile tile in matches)
        {
            if (tile != null && particleEffectManager != null)
                particleEffectManager.PlayEffect(tile.tileType, tile.transform.position);
        }

        yield return new WaitForSeconds(0.15f);

        // 2️⃣ Grid’den kaldır (Destroy öncesi)
        foreach (Tile tile in matches)
        {
            if (tile != null)
                grid[tile.x, tile.y] = null;
        }

        yield return new WaitForSeconds(0.05f);

        // 3️⃣ Objeleri yok et
        foreach (Tile tile in matches)
        {
            if (tile != null)
                Destroy(tile.gameObject);
        }

        // 4️⃣ Taşları düşür ve boşlukları doldur
        yield return StartCoroutine(DropTiles());

        // 5️⃣ Yukarıdan taş ekle
        yield return StartCoroutine(RefillTiles());

        // 6️⃣ Patlayan taş sayısına göre target ve blood güncelle
        OnMoveResolved(matches.Count);
    }


    IEnumerator DropTiles()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 1; y < height; y++) // 0’dan başlama, çünkü alt satır boş olamaz
            {
                if (grid[x, y] == null) continue;

                int fallDistance = 0;
                for (int ny = y - 1; ny >= 0; ny--)
                {
                    if (grid[x, ny] == null) fallDistance++;
                    else break;
                }

                if (fallDistance > 0)
                {
                    Tile tile = grid[x, y];
                    grid[x, y] = null;
                    grid[x, y - fallDistance] = tile;

                    Vector3 targetPos = GetTileWorldPosition(x, y - fallDistance);
                    StartCoroutine(MoveTile(tile, targetPos, 0.2f));
                }
            }
        }

        yield return new WaitForSeconds(0.25f); // düşme animasyonu bitene kadar bekle
    }
    IEnumerator RefillTiles()
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (grid[x, y] == null)
                {
                    TileType type = GetRandomNormalTile();
                    GameObject prefab = GetPrefabByType(type);

                    // Yukardan düşecek şekilde spawn
                    Vector3 spawnPos = GetTileWorldPosition(x, y + height);
                    GameObject tileObj = Instantiate(prefab, spawnPos, Quaternion.identity, transform);

                    Tile tile = tileObj.GetComponent<Tile>();
                    tile.x = x;
                    tile.y = y;
                    tile.tileType = type;
                    grid[x, y] = tile;

                    // Hedef pozisyona animasyon
                    Vector3 targetPos = GetTileWorldPosition(x, y);
                    StartCoroutine(MoveTile(tile, targetPos, 0.3f));
                }
            }
        }

        yield return new WaitForSeconds(0.35f); // animasyon bitene kadar bekle
    }





    IEnumerator MoveTile(Tile tile, Vector3 targetPos, float duration)
    {
        if (tile == null) yield break;

        Vector3 startPos = tile.transform.position;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (tile == null) yield break;
            tile.transform.position = Vector3.Lerp(startPos, targetPos, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (tile != null)
            tile.transform.position = targetPos;
    }




    // ===================== GRID =====================
    void CreateGrid()
    {
        if (grid != null)
        {
            foreach (Transform child in transform)
            {
                Destroy(child.gameObject);
            }
        }

        grid = new Tile[width, height];

        float offsetX = (width - 1) / 2f;
        float offsetY = (height - 1) / 2f;

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                TileType type = GetRandomNormalTile();
                GameObject prefab = GetPrefabByType(type);

                Vector3 spawnPos = new Vector3(
     (x - offsetX) * tileSpacing,
     (y - offsetY) * tileSpacing + gridYOffset,
     0f
 );


                GameObject tileObj = Instantiate(prefab, spawnPos, Quaternion.identity, transform);

                Tile tile = tileObj.GetComponent<Tile>();
                tile.x = x;
                tile.y = y;
                tile.tileType = type;

                grid[x, y] = tile;
            }
        }
    }

    TileType GetRandomNormalTile()
    {
        TileType[] normalTiles =
        {
            TileType.Red,
            TileType.Yellow,
            TileType.Green,
            TileType.Blue
        };

        return normalTiles[Random.Range(0, normalTiles.Length)];
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
            default: return redPrefab;
        }
    }

    IEnumerator ClearInitialMatches()
    {
        yield return new WaitForSeconds(0.1f);

        bool foundMatch = true;

        while (foundMatch)
        {
            List<Tile> matches = FindAllMatches();

            if (matches.Count > 0)
            {
                yield return StartCoroutine(HandleMatches(matches));
            }
            else
            {
                foundMatch = false;
            }
        }
    }

    List<Tile> FindAllMatches()
    {
        HashSet<Tile> result = new HashSet<Tile>();

        // Yatay
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2; x++)
            {
                CheckLine(x, y, 1, 0, result);
            }
        }

        // Dikey
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2; y++)
            {
                CheckLine(x, y, 0, 1, result);
            }
        }

        CheckSquares(result);

        return new List<Tile>(result);
    }



    // ===================== MATCH RESOLVE =====================
    /// <summary>
    /// HER HAMLE BİTTİĞİNDE çağrılır
    /// destroyedTileCount = patlayan toplam tile
    /// </summary>
    public void OnMoveResolved(int destroyedTileCount)
    {
        if (gameEnded) return;

        totalMoves--;

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
        // WIN
        if (targetCount <= 0)
        {
            TriggerWin();
            return;
        }

        // LOSE – hamle bitti
        if (totalMoves <= 0)
        {
            TriggerLose();
            return;
        }

        // LOSE – kan full
        if (currentBlood >= 1f)
        {
            TriggerLose();
            return;
        }
    }

    void TriggerWin()
    {
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

        while (x >= 0 && x < width && y >= 0 && y < height)
        {
            Tile next = grid[x, y];
            if (next != null && next.tileType == startTile.tileType)
            {
                match.Add(next);
                x += dirX;
                y += dirY;
            }
            else break;
        }

        if (match.Count >= 3)
        {
            foreach (var t in match)
                result.Add(t);

            if (match.Count >= 4)
            {
                CreateBloodDrop(match);
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

                if (a && b && c && d &&
                    a.tileType == b.tileType &&
                    a.tileType == c.tileType &&
                    a.tileType == d.tileType)
                {
                    result.Add(a);
                    result.Add(b);
                    result.Add(c);
                    result.Add(d);
                }
            }
        }
    }
    IEnumerator DestroyMatchesCoroutine(List<Tile> matches)
    {
        // Efektleri oynat
        foreach (Tile tile in matches)
        {
            if (tile != null && particleEffectManager != null)
                particleEffectManager.PlayEffect(tile.tileType, tile.transform.position);
        }

        // Kısa bekleme, efekt görünsün
        yield return new WaitForSeconds(0.1f);

        // Grid’den kaldır, objeleri daha sonra yok et
        foreach (Tile tile in matches)
        {
            if (tile != null)
            {
                grid[tile.x, tile.y] = null;
            }
        }

        yield return new WaitForSeconds(0.1f);

        foreach (Tile tile in matches)
        {
            if (tile != null)
                Destroy(tile.gameObject);
        }
    }


    Vector3 GetTileWorldPosition(int x, int y)
    {
        return new Vector3(
            (x - (width - 1) / 2f) * tileSpacing,
            (y - (height - 1) / 2f) * tileSpacing + gridYOffset,
            0f
        );
    }

    void CreateBloodDrop(List<Tile> match)
    {
        // Ortadaki tile özel taş olacak
        int midIndex = match.Count / 2;
        Tile specialTile = match[midIndex];

        // Mevcut tile'ı yok et
        Destroy(specialTile.gameObject);

        // BloodDrop prefabını seç
        GameObject prefab = GetPrefabByType(TileType.BloodDrop);

        // Spawn
        GameObject tileObj = Instantiate(prefab, specialTile.transform.position, Quaternion.identity, transform);
        Tile newTile = tileObj.GetComponent<Tile>();
        newTile.x = specialTile.x;
        newTile.y = specialTile.y;
        newTile.tileType = TileType.BloodDrop;

        // Grid'i güncelle
        grid[newTile.x, newTile.y] = newTile;
    }


    void CreateSpecialTile(List<Tile> match, TileType specialType)
    {
        // Örnek: Match’in ortasındaki tile özel taş olacak
        int midIndex = match.Count / 2;
        Tile specialTile = match[midIndex];

        // Mevcut tile'ı yok et
        Destroy(specialTile.gameObject);

        // Prefab seçimi
        GameObject prefab = GetPrefabByType(specialType); // Özel prefab ekle

        // Spawn
        GameObject tileObj = Instantiate(prefab, specialTile.transform.position, Quaternion.identity, transform);
        Tile newTile = tileObj.GetComponent<Tile>();
        newTile.x = specialTile.x;
        newTile.y = specialTile.y;
        newTile.tileType = specialType;

        grid[newTile.x, newTile.y] = newTile;
    }


}