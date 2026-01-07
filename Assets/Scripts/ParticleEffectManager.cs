using UnityEngine;
using System.Collections.Generic;

public class ParticleEffectManager : MonoBehaviour
{
    [Header("Particle Prefab")]
    [SerializeField] private ParticleSystem particlePrefab;

    [Header("Color Settings")]
    [SerializeField] private List<ParticleColorData> particleColors;

    Dictionary<TileType, Color> colorLookup;

    void Awake()
    {
        colorLookup = new Dictionary<TileType, Color>();

        foreach (var data in particleColors)
        {
            if (!colorLookup.ContainsKey(data.type))
                colorLookup.Add(data.type, data.color);
        }
    }

    public void PlayEffect(TileType type, Vector3 position)
    {
        if (!colorLookup.ContainsKey(type))
        {
            Debug.LogWarning($"ParticleEffectManager: Color not found for {type}");
            return;
        }

        ParticleSystem ps = Instantiate(particlePrefab, position, Quaternion.identity);

        var main = ps.main;
        main.startColor = colorLookup[type];

        ApplySpecialSettings(ps, type);

        ps.Play();
        Destroy(ps.gameObject, main.startLifetime.constant + 0.2f);
    }

    void ApplySpecialSettings(ParticleSystem ps, TileType type)
    {
        var emission = ps.emission;
        var main = ps.main;

        switch (type)
        {
            case TileType.BloodDrop:
                emission.SetBurst(0, new ParticleSystem.Burst(0, 24));
                main.startSize = 0.35f;
                break;

            case TileType.Bat:
                emission.SetBurst(0, new ParticleSystem.Burst(0, 18));
                main.startSpeed = 3.5f;
                break;

            case TileType.Vampyr:
                emission.SetBurst(0, new ParticleSystem.Burst(0, 30));
                main.startSize = 0.4f;
                break;
        }
    }

    [System.Serializable]
    public class ParticleColorData
    {
        public TileType type;
        public Color color;
    }
}
