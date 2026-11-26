using System.Collections;
using UnityEngine;

// Удар мечем: окремий компонент, PlayerController про нього не знає.
// Урон іде через той самий контракт IDamageable, що й у ворогів.
public class PlayerAttack : MonoBehaviour
{
    [Header("Удар")]
    [SerializeField] private SpriteRenderer body;        // спрайт гравця: з flipX беремо напрямок
    [SerializeField] private Transform attackPoint;      // центр зони удару перед гравцем
    [SerializeField, Range(0.2f, 2f)] private float radius = 1f;
    [SerializeField] private LayerMask enemyLayer;
    [SerializeField, Range(1, 50)] private int damage = 15;
    [SerializeField, Range(0.1f, 1f)] private float cooldown = 0.35f;
    [SerializeField, Range(0f, 0.1f)] private float hitStopTime = 0.03f;

    [Header("Ефект")]
    [SerializeField] private SpriteRenderer slash;       // дуга змаху
    [SerializeField] private Sprite[] slashFrames;
    [SerializeField, Range(0.02f, 0.1f)] private float frameTime = 0.04f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip swingSfx;

    private PlayerControls controls;
    private HitStop hitStop;
    private float readyAt;
    private float pointX, slashX;
    private Coroutine slashRoutine;

    private void Awake()
    {
        controls = new PlayerControls();
        hitStop = FindAnyObjectByType<HitStop>();

        // Гравець дзеркалиться через flipX, а не масштабом, тому дочірні
        // точки самі не переїжджають — запам'ятовуємо їхній X і віддзеркалюємо вручну
        pointX = attackPoint.localPosition.x;
        slashX = slash.transform.localPosition.x;
        slash.enabled = false;
    }

    private void OnEnable() => controls.Player.Enable();
    private void OnDisable() => controls.Player.Disable();
    private void OnDestroy() => controls.Dispose();

    private int Facing => body.flipX ? -1 : 1;

    private void Update()
    {
        if (Time.time < readyAt) return;
        if (!controls.Player.Attack.WasPressedThisFrame()) return;

        readyAt = Time.time + cooldown;
        Swing();
    }

    private void Swing()
    {
        int dir = Facing;
        attackPoint.localPosition = new Vector3(pointX * dir, attackPoint.localPosition.y, 0f);
        slash.transform.localPosition = new Vector3(slashX * dir, slash.transform.localPosition.y, 0f);

        if (swingSfx != null) audioSource.PlayOneShot(swingSfx);
        if (slashRoutine != null) StopCoroutine(slashRoutine);
        slashRoutine = StartCoroutine(PlaySlash(dir));

        // Усі вороги в зоні: удар може зачепити двох одразу
        bool hitSomething = false;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(attackPoint.position, radius, enemyLayer))
        {
            if (hit.TryGetComponent(out IDamageable target))
            {
                target.TakeDamage(damage, transform.position);
                hitSomething = true;
            }
        }

        // Мікропауза лише при влучанні — удар «відчувається»
        if (hitSomething && hitStop != null && hitStopTime > 0f)
            hitStop.Stop(hitStopTime);
    }

    private IEnumerator PlaySlash(int dir)
    {
        slash.flipX = dir < 0;
        slash.enabled = true;
        foreach (Sprite frame in slashFrames)
        {
            slash.sprite = frame;
            yield return new WaitForSeconds(frameTime);
        }
        slash.enabled = false;
        slashRoutine = null;
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null) Gizmos.DrawWireSphere(attackPoint.position, radius);
    }
}
