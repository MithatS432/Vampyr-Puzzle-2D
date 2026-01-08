using UnityEngine;
using System.Collections.Generic;

public class ParticleEffectManager : MonoBehaviour
{
    [Header("Particle Prefab")]
    [SerializeField] private ParticleSystem particlePrefab;

    [Header("Color Settings")]
    [SerializeField] private List<ParticleColorData> particleColors;

    [Header("Settings")]
    [SerializeField] private bool useScreenSpace = true; // ✅ YENİ: Ekran boşluğunda oynat
    [SerializeField] private float particleScale = 1f;   // ✅ YENİ: Particle boyutu

    private Dictionary<TileType, Color> colorLookup;
    private Camera mainCamera;

    void Awake()
    {
        colorLookup = new Dictionary<TileType, Color>();
        mainCamera = Camera.main; // ✅ Kamerayı cache'le

        foreach (var data in particleColors)
        {
            if (!colorLookup.ContainsKey(data.type))
                colorLookup.Add(data.type, data.color);
        }
    }

    public void PlayEffect(TileType type, Vector3 worldPosition)
    {
        Debug.Log($"PARTICLE PLAY: {type} at {worldPosition}");

        if (!colorLookup.ContainsKey(type))
        {
            Debug.LogWarning($"COLOR YOK: {type}");
            return;
        }

        // ✅ POZİSYON AYARI
        Vector3 spawnPosition = worldPosition;

        if (useScreenSpace && mainCamera != null)
        {
            // Dünya pozisyonunu ekran pozisyonuna çevir
            Vector3 screenPos = mainCamera.WorldToScreenPoint(worldPosition);

            // Eğer pozisyon kameranın arkasındaysa, önüne al
            if (screenPos.z < 0)
            {
                screenPos.z = 10f; // Kameranın önüne koy
            }

            // Ekran pozisyonunu tekrar dünya pozisyonuna çevir
            spawnPosition = mainCamera.ScreenToWorldPoint(screenPos);
            spawnPosition.z = 0; // 2D oyun için Z=0
        }

        // ✅ PARTICLE OLUŞTUR
        ParticleSystem ps = Instantiate(particlePrefab, spawnPosition, Quaternion.identity);

        // ✅ RENK AYARI
        var main = ps.main;
        main.startColor = colorLookup[type];

        // ✅ BOYUT AYARI
        if (particleScale != 1f)
        {
            main.startSizeMultiplier *= particleScale;
        }

        // ✅ ÖZEL AYARLAR (Kan Damlası, Yarasa, Vampir için)
        ApplySpecialSettings(ps, type);

        // ✅ PARENT AYARI (Particle'ları ParticleManager'a bağla)
        ps.transform.SetParent(transform, true);

        // ✅ PARTICLE'LARI KAMERA'YA BAKACAK ŞEKİLDE AYARLA
        if (mainCamera != null)
        {
            ps.transform.LookAt(mainCamera.transform);
            ps.transform.rotation = Quaternion.LookRotation(mainCamera.transform.forward);
        }

        // ✅ OYNAT
        ps.Play();

        // ✅ YOK ET
        float destroyTime = main.startLifetime.constant + 0.2f;
        Destroy(ps.gameObject, destroyTime);
    }

    void ApplySpecialSettings(ParticleSystem ps, TileType type)
    {
        var emission = ps.emission;
        var main = ps.main;
        var shape = ps.shape;

        switch (type)
        {
            case TileType.BloodDrop:
                // Kan Damlası - patlama efekti
                emission.rateOverTime = 0;
                emission.SetBursts(new ParticleSystem.Burst[] {
                    new ParticleSystem.Burst(0f, 24, 30, 1, 0.1f)
                });
                main.startSize = 0.35f;
                main.startSpeed = 3f;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = 0.5f;
                break;

            case TileType.Bat:
                // Yarasa - küçük patlama
                emission.SetBurst(0, new ParticleSystem.Burst(0, 18));
                main.startSpeed = 3.5f;
                main.startSize = 0.25f;
                break;

            case TileType.Vampyr:
                // Vampir - büyük patlama
                emission.SetBurst(0, new ParticleSystem.Burst(0, 30));
                main.startSize = 0.4f;
                main.startSpeed = 4f;
                break;

            default:
                // Normal tile'lar için
                emission.rateOverTime = 0;
                emission.SetBursts(new ParticleSystem.Burst[] {
                    new ParticleSystem.Burst(0f, 15, 20, 1, 0.05f)
                });
                main.startSize = 0.3f;
                main.startSpeed = 2f;
                shape.shapeType = ParticleSystemShapeType.Sphere;
                shape.radius = 0.3f;
                break;
        }
    }

    // ✅ YENİ: EKRANIN MERKEZİNDE PARTICLE OYNAT (Debug için)
    public void PlayEffectAtScreenCenter(TileType type)
    {
        if (mainCamera == null) return;

        Vector3 screenCenter = new Vector3(Screen.width / 2, Screen.height / 2, 10f);
        Vector3 worldPos = mainCamera.ScreenToWorldPoint(screenCenter);

        PlayEffect(type, worldPos);
    }

    // ✅ YENİ: TÜM PARTICLE'LARI TEMİZLE
    public void ClearAllParticles()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }
    }

    // ParticleEffectManager'a yeni fonksiyon ekle:
    public void PlayBloodDropComboEffect(Vector3 position)
    {
        if (particlePrefab == null) return;

        // Büyük particle efekti
        ParticleSystem ps = Instantiate(particlePrefab, position, Quaternion.identity);
        var main = ps.main;
        main.startSize = 0.5f;
        main.startSpeed = 5f;
        main.maxParticles = 50;

        // Çok renkli efekt
        var colorModule = ps.colorOverLifetime;
        colorModule.enabled = true;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] {
            new GradientColorKey(Color.red, 0.0f),
            new GradientColorKey(Color.yellow, 0.33f),
            new GradientColorKey(Color.green, 0.66f),
            new GradientColorKey(Color.blue, 1.0f)
            },
            new GradientAlphaKey[] {
            new GradientAlphaKey(1.0f, 0.0f),
            new GradientAlphaKey(0.0f, 1.0f)
            }
        );
        colorModule.color = gradient;

        // Burst ayarı
        var emission = ps.emission;
        emission.SetBursts(new ParticleSystem.Burst[] {
        new ParticleSystem.Burst(0f, 40)
    });

        ps.Play();
        Destroy(ps.gameObject, 2f);
    }

    [System.Serializable]
    public class ParticleColorData
    {
        public TileType type;
        public Color color = Color.white;
    }
}