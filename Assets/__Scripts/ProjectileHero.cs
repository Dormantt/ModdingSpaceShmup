using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoundsCheck))]
public class ProjectileHero : MonoBehaviour
{
    private BoundsCheck bndCheck;
    private Renderer rend;

    [Header("Dynamic")]
    public Rigidbody rigid;
    [SerializeField]
    private eWeaponType _type;

    public bool isEnemy = false;

    [Tooltip("Seconds before auto-destroy. <=0 means no timer.")]
    public float lifeTime = 0;
    private float birthTime;

    // Internal velocity stored for transform-based movement fallback
    // and for homing missile tracking
    private Vector3 _vel;

    // Phaser sine wave
    private Vector3 _birthPos;
    public float phaserPhaseOffset = 0f;
    private const float PHASER_FREQUENCY = 2f;
    private const float PHASER_AMPLITUDE = 1.5f;

    // Missile homing
    private Enemy _homingTarget;
    private const float MISSILE_SPEED = 60f;
    private const float MISSILE_TURN_RATE = 8f; // higher = tighter turning

    public eWeaponType type
    {
        get { return _type; }
        set { SetType(value); }
    }

    void Awake()
    {
        bndCheck = GetComponent<BoundsCheck>();
        rend = GetComponent<Renderer>();
        rigid = GetComponent<Rigidbody>();
        if (rigid != null) rigid.useGravity = false;
        birthTime = Time.time;
    }

    void Update()
    {
        if (lifeTime > 0 && Time.time > birthTime + lifeTime)
        {
            Destroy(gameObject);
            return;
        }

        // --- Movement ---
        if (_type == eWeaponType.phaser)
        {
            // Sine wave oscillation on X, constant speed on Y
            float age = Time.time - birthTime;
            float theta = Mathf.PI * 2f * age / PHASER_FREQUENCY + phaserPhaseOffset;
            float sineX = Mathf.Sin(theta) * PHASER_AMPLITUDE;
            Vector3 newPos = _birthPos;
            newPos.x += sineX;
            newPos.y += _vel.y * age;
            transform.position = newPos;
        }
        else if (_type == eWeaponType.missile && !isEnemy)
        {
            // Missile handles its own movement inside HomingUpdate (Rigidbody or transform)
            HomingUpdate();
        }
        else if (rigid == null || rigid.isKinematic)
        {
            // Fallback: move via transform when no usable Rigidbody
            transform.position += _vel * Time.deltaTime;
        }

        // --- Bounds destruction ---
        if (bndCheck.LocIs(BoundsCheck.eScreenLocs.offUp) && !isEnemy)
        {
            Destroy(gameObject);
        }
        else if (bndCheck.LocIs(BoundsCheck.eScreenLocs.offDown) && isEnemy)
        {
            Destroy(gameObject);
        }
    }

    private void HomingUpdate()
    {
        // Reacquire target if lost (destroyed or never set)
        if (_homingTarget == null)
        {
            Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);
            if (enemies.Length == 0) return; // no targets — keep going straight

            Enemy nearest = null;
            float minDist = float.MaxValue;
            foreach (Enemy e in enemies)
            {
                float d = Vector3.Distance(transform.position, e.transform.position);
                if (d < minDist) { minDist = d; nearest = e; }
            }
            _homingTarget = nearest;
        }

        if (_homingTarget == null) return;

        // Steer toward target at fixed speed (never slows down)
        Vector3 dir = (_homingTarget.transform.position - transform.position).normalized;
        Vector3 targetVel = dir * MISSILE_SPEED;
        // Lerp direction only, then force constant magnitude
        _vel = Vector3.Lerp(_vel.normalized, targetVel.normalized, MISSILE_TURN_RATE * Time.deltaTime)
               .normalized * MISSILE_SPEED;

        if (rigid != null && !rigid.isKinematic)
            rigid.linearVelocity = _vel;
        else
            transform.position += _vel * Time.deltaTime;
    }

    public void SetType(eWeaponType eType)
    {
        _type = eType;
        WeaponDefinition def = Main.GET_WEAPON_DEFINITION(_type);
        if (rend != null) rend.material.color = def.projectileColor;

        // Store birth position AFTER the caller sets transform.position
        // (MakeProjectile sets position before calling p.type = ...)
        _birthPos = transform.position;
        birthTime = Time.time;

        // Phaser uses transform-based movement, disable physics
        if (eType == eWeaponType.phaser && rigid != null)
        {
            rigid.isKinematic = true;
        }
    }

    public Vector3 vel
    {
        get { return _vel; }
        set
        {
            _vel = value;
            // Apply to Rigidbody for physics-based types
            if (rigid != null && !rigid.isKinematic)
            {
                rigid.linearVelocity = value;
            }
        }
    }
}
