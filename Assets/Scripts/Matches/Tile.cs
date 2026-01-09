using UnityEngine;

public class Tile : MonoBehaviour
{
    public int x;
    public int y;
    public TileType tileType;
    public bool isSpecial = false;

    public bool isBloodDrop = false;
    public bool isVampire = false;
    public bool isBat = false;
    public bool isRandomDestroyer = false;

    public TileType bloodDropColor = TileType.Red;
    public SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // KAN DAMLALARI HER ZAMAN 0.5 SCALE
        if (isBloodDrop)
        {
            transform.localScale = new Vector3(0.5f, 0.5f, 1f);
        }

        // Z-POSITION AYARLA
        SetCorrectZPosition();

        UpdateVisual();
    }

    // BU FONKSİYONU EKLEYİN!
    public void SetCorrectZPosition()
    {
        // Kan damlaları ve özel tile'lar çok önde olsun
        if (isBloodDrop || isBat || isVampire)
        {
            Vector3 pos = transform.position;
            transform.position = new Vector3(pos.x, pos.y, -10f); // Çok önde!

            // Sorting order da yüksek olsun
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = 100;
            }
        }
        else
        {
            // Normal tile'lar
            Vector3 pos = transform.position;
            transform.position = new Vector3(pos.x, pos.y, -y * 0.01f); // Y'ye göre
        }
    }

    public void UpdateVisual()
    {
        if (spriteRenderer == null) return;

        // KAN DAMLASI BOYUT KONTROLÜ
        if (isBloodDrop)
        {
            transform.localScale = new Vector3(0.5f, 0.5f, 1f);
            UpdateBloodDropVisual();
        }
        else if (isBat)
        {
            spriteRenderer.color = new Color(0.3f, 0.1f, 0.5f, 1f);
        }
        else if (isVampire)
        {
            spriteRenderer.color = Color.magenta;
        }
        else
        {
            switch (tileType)
            {
                case TileType.Red: spriteRenderer.color = Color.red; break;
                case TileType.Yellow: spriteRenderer.color = Color.yellow; break;
                case TileType.Green: spriteRenderer.color = Color.green; break;
                case TileType.Blue: spriteRenderer.color = Color.blue; break;
            }
        }
    }

    public void UpdateBloodDropVisual()
    {
        if (!isBloodDrop || spriteRenderer == null) return;

        // BOYUTU KESİN OLARAK 0.5 YAP
        transform.localScale = new Vector3(0.5f, 0.5f, 1f);

        // Z-POSITION AYARLA
        SetCorrectZPosition();

        Color color = Color.white;

        switch (bloodDropColor)
        {
            case TileType.Red: color = new Color(1f, 0.5f, 0.5f, 1f); break;
            case TileType.Yellow: color = new Color(1f, 1f, 0.5f, 1f); break;
            case TileType.Green: color = new Color(0.5f, 1f, 0.5f, 1f); break;
            case TileType.Blue: color = new Color(0.5f, 0.5f, 1f, 1f); break;
        }

        spriteRenderer.color = color;

        // SPRITE RENDERER AYARLARI
        if (spriteRenderer.sortingOrder < 100)
        {
            spriteRenderer.sortingOrder = 100;
        }
    }
}