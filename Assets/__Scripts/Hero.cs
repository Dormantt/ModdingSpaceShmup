using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoundsCheck))]
public class Hero : MonoBehaviour
{
    static public Hero S { get; private set; }

    [Header("Inscribed")]
    public float maxSpeed = 30;
    public float rollDegrees = 25;
    public float rollSmoothing = 20;
    public float boundsPadding = 0.75f;
    public float invincibleTime = 1.0f;

    public GameObject projectilePrefab;
    public float projectileSpeed = 40;
    public Weapon[] weapons;

    [Header("Dynamic")]
    [Range(0, 4)]
    [SerializeField]
    private float _shieldLevel = 1;

    [Tooltip("This field holds a reference to the last triggering GameObject")]
    private GameObject lastTriggerGo = null;

    public delegate void WeaponFireDelegate();
    public event WeaponFireDelegate fireEvent;

    private BoundsCheck bndCheck;
    private float currentRoll;
    private float nextDamageAllowedTime;

    void Awake()
    {
        if (S == null)
        {
            S = this;
        }
        else
        {
            Debug.LogError("Hero.Awake() - Attempted to assign second Hero.S!");
        }

        bndCheck = GetComponent<BoundsCheck>();

        ClearWeapons();
        weapons[0].SetType(eWeaponType.blaster);
    }

    void Update()
    {
        float hAxis = Input.GetAxisRaw("Horizontal");
        float vAxis = Input.GetAxisRaw("Vertical");

        Vector3 inputDir = new Vector3(hAxis, vAxis, 0);
        if (inputDir.magnitude > 1f) inputDir.Normalize();

        Vector3 desiredVelocity = inputDir * maxSpeed;
        Vector3 pos = transform.position + desiredVelocity * Time.deltaTime;
        if (bndCheck != null)
        {
            float xMin = -bndCheck.camWidth + boundsPadding;
            float xMax = bndCheck.camWidth - boundsPadding;
            float yMin = -bndCheck.camHeight + boundsPadding;
            float yMax = bndCheck.camHeight - boundsPadding;
            pos.x = Mathf.Clamp(pos.x, xMin, xMax);
            pos.y = Mathf.Clamp(pos.y, yMin, yMax);
        }
        transform.position = pos;

        float targetRoll = -hAxis * rollDegrees;
        float smoothT = 1f - Mathf.Exp(-rollSmoothing * Time.deltaTime);
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, smoothT);
        transform.rotation = Quaternion.Euler(vAxis * (rollDegrees * 0.5f), currentRoll, 0);

        if (Input.GetAxis("Jump") == 1 && fireEvent != null)
        {
            fireEvent();
        }
    }

    public void TakeDamage()
    {
        if (Time.time < nextDamageAllowedTime) return;
        nextDamageAllowedTime = Time.time + invincibleTime;
        shieldLevel--;
    }

    // Resolve the correct GameObject to check for components.
    // Projectiles are parented to _ProjectileAnchor, so transform.root gives the anchor,
    // not the projectile itself. We detect projectiles first via the actual hit object.
    private GameObject ResolveHitObject(GameObject hitObj, out ProjectileHero proj)
    {
        proj = hitObj.GetComponent<ProjectileHero>();
        if (proj != null)
            return hitObj;               // Use projectile directly
        return hitObj.transform.root.gameObject; // Use root for enemies / powerups
    }

    void OnTriggerEnter(Collider other)
    {
        ProjectileHero p;
        GameObject go = ResolveHitObject(other.gameObject, out p);

        if (go == lastTriggerGo) return;
        lastTriggerGo = go;

        Enemy enemy = go.GetComponent<Enemy>();
        PowerUp pUp = go.GetComponent<PowerUp>();

        if (enemy != null || (p != null && p.isEnemy))
        {
            if (Time.time < nextDamageAllowedTime) return;
            nextDamageAllowedTime = Time.time + invincibleTime;
            shieldLevel--;
            Destroy(go);
        }
        else if (pUp != null)
        {
            AbsorbPowerUp(pUp);
        }
        else
        {
            Debug.LogWarning("Shield trigger hit by non-Enemy: " + go.name);
        }
    }

    void OnCollisionEnter(Collision coll)
    {
        ProjectileHero p;
        GameObject go = ResolveHitObject(coll.gameObject, out p);

        if (go == lastTriggerGo) return;
        lastTriggerGo = go;

        Enemy enemy = go.GetComponent<Enemy>();

        if (enemy != null || (p != null && p.isEnemy))
        {
            if (Time.time < nextDamageAllowedTime) return;
            nextDamageAllowedTime = Time.time + invincibleTime;
            shieldLevel--;
            Destroy(go);
        }
    }

    public float shieldLevel
    {
        get { return _shieldLevel; }
        private set
        {
            _shieldLevel = Mathf.Min(value, 4);
            if (value < 0)
            {
                Destroy(this.gameObject);
                Main.HERO_DIED();
            }
        }
    }

    Weapon GetEmptyWeaponSlot()
    {
        for (int i = 0; i < weapons.Length; i++)
        {
            if (weapons[i].type == eWeaponType.none)
                return weapons[i];
        }
        return null;
    }

    void ClearWeapons()
    {
        foreach (Weapon w in weapons)
            w.SetType(eWeaponType.none);
    }

    public void AbsorbPowerUp(PowerUp pUp)
    {
        Debug.Log("Absorbed PowerUp: " + pUp.type);
        switch (pUp.type)
        {
            case eWeaponType.shield:
                shieldLevel++;
                break;

            default:
                if (pUp.type == weapons[0].type)
                {
                    Weapon weap = GetEmptyWeaponSlot();
                    if (weap != null)
                        weap.SetType(pUp.type);
                }
                else
                {
                    ClearWeapons();
                    weapons[0].SetType(pUp.type);
                }
                break;
        }
        pUp.AbsorbedBy(this.gameObject);
    }
}
