using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class Coin : MonoBehaviour
{
    [SerializeField] private int value = 1;
    [SerializeField] private IntEventSO coinsChanged;

    [Header("Відгук")]
    [SerializeField] private ParticleSystem pickupEffect; // ПРЕФАБ, не об'єкт зі сцени

    private bool collected; // монета підбирається рівно один раз

    // Унікальний ключ душі: номер сцени + ім'я об'єкта (Coin_01, Coin_02…)
    private string Id => $"{gameObject.scene.buildIndex}:{name}";

    private void Start()
    {
        // Душа, зібрана до збереження, після «Продовжити» не з'являється вдруге
        if (GameProgress.IsCollected(Id)) Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Два колайдери гравця можуть увійти в тригер одного кадру —
        // без цього прапорця монета зарахується двічі
        if (collected) return;

        // Знову не тег, а наявність компонента — як на контрольній точці
        if (!other.TryGetComponent(out PlayerController _)) return;

        collected = true;
        GameProgress.AddCoins(value);
        GameProgress.MarkCollected(Id);
        coinsChanged.Raise(GameProgress.Coins); // монета сама нікого не малює

        // Ефект породжуємо ОКРЕМИМ об'єктом. Якби він був дочірнім
        // до монети, то зник би разом із нею тієї ж миті
        if (pickupEffect != null)
            Instantiate(pickupEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}
