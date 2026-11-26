using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class Health : MonoBehaviour, IDamageable
{
    [SerializeField] private int maxHealth = 30;
    [SerializeField, Range(0f, 2f)] private float invulnerableTime = 0.6f;
    [SerializeField, Range(0f, 20f)] private float knockbackForce = 8f;

    protected int current;
    private float invulnerableUntil;
    private Rigidbody2D body;

    public int Current => current;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        ResetHealth();
    }

    public void ResetHealth() => current = maxHealth;

    // Ворогу максимум приходить з ассета — викликає EnemyController
    public void Configure(int newMax)
    {
        maxHealth = newMax;
        ResetHealth();
    }

    public void TakeDamage(int amount, Vector2 sourcePosition)
    {
        // Кадри невразливості: без них дотик знімає здоров'я ЩОКАДРУ
        if (Time.time < invulnerableUntil) return;
        invulnerableUntil = Time.time + invulnerableTime;

        current = Mathf.Max(0, current - amount);

        if (current == 0) { Die(); return; }

        // Спершу стан «поранений», ПОТІМ поштовх: вихід зі стану погоні
        // обнуляє швидкість і з'їв би відкидання, застосоване раніше
        OnDamaged();

        // Відкидання — і зворотний зв'язок, і механіка: дає час відійти.
        // Напрямок — від джерела по горизонталі, з невеликим підкиданням угору
        float side = Mathf.Sign(transform.position.x - sourcePosition.x);
        Vector2 dir = new Vector2(side, KnockbackLift).normalized;
        body.linearVelocity = Vector2.zero;
        body.AddForce(dir * knockbackForce, ForceMode2D.Impulse);
    }

    // Частка вертикалі в напрямку відкидання: тіло трохи відривається від землі
    private const float KnockbackLift = 0.4f;

    // Два гачки для спадкоємців — єдине, що відрізняє ворога від гравця
    protected virtual void OnDamaged() { }
    protected virtual void Die() => gameObject.SetActive(false);
}
