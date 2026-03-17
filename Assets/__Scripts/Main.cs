using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(BoundsCheck))]
public class Main : MonoBehaviour
{
    static private Main S;
    static private Dictionary<eWeaponType, WeaponDefinition> WEAP_DICT;

    [Header("Inscribed")]
    public bool spawnEnemies = true;
    public GameObject[] prefabEnemies;
    public float enemyInsetDefault = 1.5f;
    public float gameRestartDelay = 4.0f;
    public GameObject prefabPowerUp;
    public WeaponDefinition[] weaponDefinitions;
    public eWeaponType[] powerUpFrequency = new eWeaponType[] {
        eWeaponType.blaster, eWeaponType.blaster,
        eWeaponType.spread, eWeaponType.phaser,
        eWeaponType.missile, eWeaponType.shield };

    [Header("Difficulty Scaling")]
    [Tooltip("Enemies spawned per second at game start")]
    public float spawnRateStart = 0.3f;
    [Tooltip("Added to spawn rate every 60 seconds")]
    public float spawnRateIncreasePerMinute = 0.3f;
    [Tooltip("Maximum enemies per second (cap)")]
    public float spawnRateMax = 3.0f;
    [Range(0, 1)] public float burstSpawnChance = 0.35f;
    public int burstSpawnMin = 2;
    public int burstSpawnMax = 3;

    // Current spawn rate — modified over time by difficulty scaling
    [HideInInspector] public float enemySpawnPerSecond;

    private BoundsCheck bndCheck;
    private float _gameTime = 0f;

    void Awake()
    {
        S = this;
        bndCheck = GetComponent<BoundsCheck>();

        // Start at the lower spawn rate
        enemySpawnPerSecond = spawnRateStart;

        WEAP_DICT = new Dictionary<eWeaponType, WeaponDefinition>();
        foreach (WeaponDefinition def in weaponDefinitions)
        {
            WEAP_DICT[def.type] = def;
        }

        Invoke(nameof(SpawnEnemy), 1f / enemySpawnPerSecond);
    }

    void Update()
    {
        if (!spawnEnemies) return;

        _gameTime += Time.deltaTime;

        // Increase spawn rate by spawnRateIncreasePerMinute every 60 seconds
        float minutes = _gameTime / 60f;
        enemySpawnPerSecond = Mathf.Min(
            spawnRateStart + minutes * spawnRateIncreasePerMinute,
            spawnRateMax
        );
    }

    public void SpawnEnemy()
    {
        if (!spawnEnemies)
        {
            Invoke(nameof(SpawnEnemy), 1f / enemySpawnPerSecond);
            return;
        }

        int spawnCount = 1;
        if (Random.value < burstSpawnChance)
            spawnCount = Random.Range(burstSpawnMin, burstSpawnMax + 1);

        for (int i = 0; i < spawnCount; i++)
            SpawnSingleEnemy();

        Invoke(nameof(SpawnEnemy), 1f / enemySpawnPerSecond);
    }

    private void SpawnSingleEnemy()
    {
        int ndx = Random.Range(0, prefabEnemies.Length);
        GameObject go = Instantiate<GameObject>(prefabEnemies[ndx]);

        float enemyInset = enemyInsetDefault;
        BoundsCheck enemyBnd = go.GetComponent<BoundsCheck>();
        if (enemyBnd != null) enemyInset = Mathf.Abs(enemyBnd.radius);

        Vector3 pos = Vector3.zero;
        float xMin = -bndCheck.camWidth + enemyInset;
        float xMax = bndCheck.camWidth - enemyInset;
        pos.x = Random.Range(xMin, xMax);
        pos.y = bndCheck.camHeight + enemyInset;
        go.transform.position = pos;
    }

    void DelayedRestart()
    {
        Invoke(nameof(Restart), gameRestartDelay);
    }

    void Restart()
    {
        SceneManager.LoadScene("__Scene_0");
    }

    static public void HERO_DIED()
    {
        if (UIManager.S != null) UIManager.S.ShowGameOver();
        S.DelayedRestart();
    }

    static public WeaponDefinition GET_WEAPON_DEFINITION(eWeaponType wt)
    {
        if (WEAP_DICT.ContainsKey(wt))
            return WEAP_DICT[wt];
        return new WeaponDefinition();
    }

    static public void SHIP_DESTROYED(Enemy e)
    {
        // Always award score when an enemy is destroyed
        if (UIManager.S != null) UIManager.S.AddScore(e.score);

        // Potentially spawn a PowerUp
        if (Random.value <= e.powerUpDropChance)
        {
            int ndx = Random.Range(0, S.powerUpFrequency.Length);
            eWeaponType pUpType = S.powerUpFrequency[ndx];

            GameObject go = Instantiate<GameObject>(S.prefabPowerUp);
            PowerUp pUp = go.GetComponent<PowerUp>();
            pUp.SetType(pUpType);
            pUp.transform.position = e.transform.position;
        }
    }
}
