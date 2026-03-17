using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum eWeaponType
{
    none,
    blaster,
    spread,
    phaser,
    missile,
    laser,
    shield
}

[System.Serializable]
public class WeaponDefinition
{
    public eWeaponType type = eWeaponType.none;
    [Tooltip("Letter to show on the PowerUp Cube")]
    public string letter;
    [Tooltip("Color of PowerUp Cube")]
    public Color powerUpColor = Color.white;
    [Tooltip("Prefab of Weapon model that is attached to the Player Ship")]
    public GameObject weaponModelPrefab;
    [Tooltip("Prefab of projectile that is fired")]
    public GameObject projectilePrefab;
    [Tooltip("Color of the Projectile that is fired")]
    public Color projectileColor = Color.white;
    [Tooltip("Damage caused when a single Projectile hits an Enemy")]
    public float damageOnHit = 0;
    [Tooltip("Damage caused per second by the Laser [Not Implemented]")]
    public float damagePerSec = 0;
    [Tooltip("Seconds to delay between shots")]
    public float delayBetweenShots = 0;

    [FormerlySerializedAs("velocity")]
    [Tooltip("Velocity of individual Projectiles")]
    public float projectileSpeed = 50;

    [Tooltip("Energy consumed per shot [Not Implemented]")]
    public float energyCost = 0;
    [Tooltip("Volume of shot sound effect [Not Implemented]")]
    public float shotSoundVol = 1;
    [Tooltip("Heat generated per shot [Not Implemented]")]
    public float weaponHeatPerShot = 0;

    [Tooltip("How many projectiles are fired per shot")]
    public int projectilesPerShot = 1;
    [Tooltip("Total spread cone in degrees")]
    public float spreadAngleDegrees = 20;
    [Tooltip("Projectile lifetime in seconds. <=0 means until offscreen")]
    public float lifetimeSeconds = 0;
}

public class Weapon : MonoBehaviour
{
    static public Transform PROJECTILE_ANCHOR;

    [Header("Dynamic")]
    [SerializeField]
    [Tooltip("Setting this manually while playing does not work properly.")]
    private eWeaponType _type = eWeaponType.none;

    public WeaponDefinition def;
    public float nextShotTime;

    private GameObject weaponModel;
    private Transform shotPointTrans;
    private bool isEnemyWeapon = false;

    private LineRenderer lineRenderer;
    private float laserActiveTime;

    void Start()
    {
        if (PROJECTILE_ANCHOR == null)
        {
            GameObject go = new GameObject("_ProjectileAnchor");
            PROJECTILE_ANCHOR = go.transform;
        }

        shotPointTrans = transform.GetChild(0);
        SetType(_type);

        Hero hero = GetComponentInParent<Hero>();
        if (hero != null)
        {
            hero.fireEvent += Fire;
            isEnemyWeapon = false;
        }
        else
        {
            isEnemyWeapon = true;
        }
    }

    void Update()
    {
        if (type == eWeaponType.laser)
        {
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
                lineRenderer.startWidth = 0.2f;
                lineRenderer.endWidth = 0.05f;
                // Try shaders in order of preference for URP / Standard pipelines
                Shader laserShader = Shader.Find("Sprites/Default")
                    ?? Shader.Find("Universal Render Pipeline/Particles/Lit")
                    ?? Shader.Find("Unlit/Color");
                lineRenderer.material = new Material(laserShader);
                lineRenderer.material.color = def.projectileColor;
                lineRenderer.positionCount = 2;
                lineRenderer.useWorldSpace = true;
            }

            if (Time.time < laserActiveTime)
            {
                lineRenderer.enabled = true;
                Vector3 startPos = shotPointTrans.position;
                Vector3 dir = isEnemyWeapon ? Vector3.down : Vector3.up;
                
                Vector3 endPos = startPos + dir * 80f;

                RaycastHit hit;
                if (Physics.Raycast(startPos, dir, out hit, 80f))
                {
                    endPos = hit.point;
                    if (isEnemyWeapon)
                    {
                        Hero h = hit.collider.GetComponentInParent<Hero>();
                        if (h != null)
                        {
                            h.TakeDamage();
                        }
                    }
                    else
                    {
                        Enemy e = hit.collider.GetComponentInParent<Enemy>();
                        if (e != null)
                        {
                            float dps = def.damagePerSec > 0 ? def.damagePerSec : 60f;
                            e.TakeDamage(dps * Time.deltaTime);
                        }
                    }
                }

                lineRenderer.SetPosition(0, startPos);
                lineRenderer.SetPosition(1, endPos);
            }
            else
            {
                if (lineRenderer != null)
                {
                    lineRenderer.enabled = false;
                }
            }
        }
        else if (lineRenderer != null)
        {
            lineRenderer.enabled = false;
        }
    }

    public eWeaponType type
    {
        get { return _type; }
        set { SetType(value); }
    }

    public void SetType(eWeaponType wt)
    {
        _type = wt;
        if (type == eWeaponType.none)
        {
            this.gameObject.SetActive(false);
            return;
        }

        this.gameObject.SetActive(true);
        def = Main.GET_WEAPON_DEFINITION(_type);

        if (weaponModel != null)
        {
            Destroy(weaponModel);
            weaponModel = null;
        }

        // weaponModelPrefab is optional — laser/phaser/missile may not have one
        if (def.weaponModelPrefab != null)
        {
            weaponModel = Instantiate<GameObject>(def.weaponModelPrefab, transform);
            weaponModel.transform.localPosition = Vector3.zero;
            weaponModel.transform.localScale = Vector3.one;
        }

        nextShotTime = 0;
    }

    public void Fire()
    {
        if (!gameObject.activeInHierarchy)
        {
            return;
        }

        if (type == eWeaponType.laser)
        {
            laserActiveTime = Time.time + 0.1f;
            return;
        }

        if (Time.time < nextShotTime)
        {
            return;
        }

        Vector3 baseVel = (isEnemyWeapon ? Vector3.down : Vector3.up) * def.projectileSpeed;

        switch (type)
        {
            case eWeaponType.blaster:
                FirePattern(Mathf.Max(1, def.projectilesPerShot), def.spreadAngleDegrees, baseVel);
                break;

            case eWeaponType.spread:
                int spreadCount = Mathf.Max(1, def.projectilesPerShot);
                if (spreadCount == 1) spreadCount = 5;
                float spreadDegrees = def.spreadAngleDegrees > 0 ? def.spreadAngleDegrees : 30f;
                FirePattern(spreadCount, spreadDegrees, baseVel);
                break;

            case eWeaponType.phaser:
            {
                int phaserCount = Mathf.Max(2, def.projectilesPerShot);
                for (int i = 0; i < phaserCount; i++)
                {
                    ProjectileHero p = MakeProjectile();
                    if (p == null) continue;
                    p.vel = baseVel;
                    p.phaserPhaseOffset = i * Mathf.PI;
                }
                break;
            }

            case eWeaponType.missile:
            {
                ProjectileHero missile = MakeProjectile();
                if (missile != null) missile.vel = baseVel;
                break;
            }
        }

        nextShotTime = Time.time + def.delayBetweenShots;
    }

    private void FirePattern(int shotCount, float totalSpread, Vector3 baseVel)
    {
        if (shotCount <= 1)
        {
            ProjectileHero p = MakeProjectile();
            if (p != null) p.vel = baseVel;
            return;
        }

        float start = -totalSpread * 0.5f;
        float step = totalSpread / (shotCount - 1);

        for (int i = 0; i < shotCount; i++)
        {
            float angle = start + step * i;
            ProjectileHero p = MakeProjectile();
            if (p == null) continue;
            p.transform.rotation = Quaternion.AngleAxis(angle, Vector3.back);
            p.vel = p.transform.rotation * baseVel;
        }
    }

    private ProjectileHero MakeProjectile()
    {
        if (def.projectilePrefab == null)
        {
            Debug.LogWarning("Weapon.MakeProjectile() - no projectilePrefab set for " + def.type);
            return null;
        }
        GameObject go = Instantiate<GameObject>(def.projectilePrefab, PROJECTILE_ANCHOR);
        ProjectileHero p = go.GetComponent<ProjectileHero>();

        Vector3 pos = shotPointTrans.position;
        pos.z = 0;
        p.transform.position = pos;

        p.type = type;
        p.lifeTime = def.lifetimeSeconds;
        p.isEnemy = isEnemyWeapon;
        return p;
    }
}
