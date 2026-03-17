using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy_4 : Enemy
{
    private enum State { Entering, Attacking, Fleeing }

    private State currentState;
    private Vector3 targetPos;
    private float stateTime;

    [Header("Enemy_4 Settings")]
    public eWeaponType weaponType  = eWeaponType.blaster;
    [Tooltip("Units per second while entering the screen")]
    public float enterSpeed        = 8f;
    [Tooltip("Units per second of horizontal tracking while attacking")]
    public float trackSpeed        = 2.5f;
    [Tooltip("Seconds between shots")]
    public float shotDelay         = 1.5f;
    [Tooltip("Max projectile speed regardless of weapon definition")]
    public float shotSpeed         = 12f;

    private float nextFireTime;

    void Start()
    {
        currentState = State.Entering;
        nextFireTime = 0f;

        if (bndCheck != null)
        {
            float xInset = Mathf.Abs(bndCheck.radius) + 1f;
            targetPos = new Vector3(
                Random.Range(-bndCheck.camWidth  + xInset, bndCheck.camWidth  - xInset),
                Random.Range( bndCheck.camHeight * 0.2f,  bndCheck.camHeight - xInset),
                0f);
        }
        else
        {
            targetPos = new Vector3(0f, 5f, 0f);
        }
    }

    public override void Move()
    {
        stateTime += Time.deltaTime;

        switch (currentState)
        {
            case State.Entering:
                pos = Vector3.MoveTowards(pos, targetPos, enterSpeed * Time.deltaTime);
                if (pos == targetPos)
                {
                    currentState = State.Attacking;
                    stateTime    = 0f;
                    nextFireTime = 0f;
                }
                break;

            case State.Attacking:
                // Track hero horizontally — guarded, but SEPARATE from shooting
                if (Hero.S != null)
                {
                    Vector3 newPos = pos;
                    newPos.x = Mathf.MoveTowards(newPos.x,
                                                  Hero.S.transform.position.x,
                                                  trackSpeed * Time.deltaTime);
                    pos = newPos;
                }

                // Always try to fire, regardless of hero tracking
                FireWeapon();

                if (stateTime > 5f)
                {
                    currentState = State.Fleeing;
                    targetPos    = pos + new Vector3(Random.Range(-5f, 5f), -25f, 0f);
                }
                break;

            case State.Fleeing:
                pos = Vector3.MoveTowards(pos, targetPos, speed * 2f * Time.deltaTime);
                break;
        }
    }

    private void FireWeapon()
    {
        if (Time.time < nextFireTime) return;

        WeaponDefinition def = Main.GET_WEAPON_DEFINITION(weaponType);
        if (def.projectilePrefab == null)
        {
            // Delay retry so we don't spam the check every frame
            nextFireTime = Time.time + shotDelay;
            return;
        }

        nextFireTime = Time.time + shotDelay;

        if (Weapon.PROJECTILE_ANCHOR == null)
            Weapon.PROJECTILE_ANCHOR = new GameObject("_ProjectileAnchor").transform;

        GameObject go = Instantiate<GameObject>(def.projectilePrefab, Weapon.PROJECTILE_ANCHOR);
        go.transform.position = new Vector3(
            transform.position.x,
            transform.position.y - 1.5f,
            0f);

        ProjectileHero p = go.GetComponent<ProjectileHero>();
        if (p != null)
        {
            p.type    = weaponType;
            p.isEnemy = true;
            p.vel     = Vector3.down * Mathf.Min(def.projectileSpeed, shotSpeed);
        }

        // Ignore collisions between this enemy and its own projectile
        Collider[] enemyCols = GetComponentsInChildren<Collider>();
        Collider   projCol   = go.GetComponent<Collider>();
        if (projCol != null)
        {
            foreach (Collider c in enemyCols)
                Physics.IgnoreCollision(c, projCol);
        }
    }
}
