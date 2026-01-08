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

    public TileType bloodDropColor = TileType.Red;

    public SpriteRenderer spriteRenderer;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        UpdateVisual();
    }

    public void UpdateVisual()
    {
        if (spriteRenderer == null) return;

        if (isBloodDrop)
        {
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

        Color color = Color.white;

        switch (bloodDropColor)
        {
            case TileType.Red: color = new Color(1f, 0.5f, 0.5f, 1f); break;
            case TileType.Yellow: color = new Color(1f, 1f, 0.5f, 1f); break;
            case TileType.Green: color = new Color(0.5f, 1f, 0.5f, 1f); break;
            case TileType.Blue: color = new Color(0.5f, 0.5f, 1f, 1f); break;
        }

        spriteRenderer.color = color;
    }
}