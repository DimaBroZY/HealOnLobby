using UnityEngine;
using Photon.Pun;

// Добавляем этот компонент к игроку, чтобы принимать RPC для лечения
public class PlayerHealSync : MonoBehaviourPun
{
    // private PlayerAvatar playerAvatar; // Можно убрать, если не используется
    private PlayerHealth playerHealth;

    void Awake()
    {
        // playerAvatar = GetComponent<PlayerAvatar>();
        playerHealth = GetComponent<PlayerHealth>();
        // photonView = GetComponent<PhotonView>(); // УДАЛЕНО

        // if (playerAvatar == null) { Debug.LogError("..."); }
        if (playerHealth == null) { Debug.LogError("PlayerHealSync: Could not find PlayerHealth component!"); }
        // Проверка photonView не нужна, так как MonoBehaviourPun должен его предоставлять
        // if (photonView == null) { Debug.LogError("..."); }
    }

    // Возвращаем старый RPC
    [PunRPC]
    public void RPC_HealPlayer(int healAmount, PhotonMessageInfo info) // PhotonMessageInfo содержит инфо об отправителе
    {
        // Проверка безопасности: Убедимся, что RPC пришел от Мастер-клиента
        if (!info.Sender.IsMasterClient)
        {
            Debug.LogWarning($"PlayerHealSync (ViewID: {this.photonView?.ViewID ?? 0}): Received RPC_HealPlayer from non-MasterClient {info.Sender?.NickName ?? "N/A"}. Ignoring.");
            return;
        }
         Debug.Log($"PlayerHealSync (ViewID: {this.photonView?.ViewID ?? 0}): RPC_HealPlayer received from {info.Sender?.NickName ?? "N/A"} with amount {healAmount}.");


        if (playerHealth != null)
        {
            // Пересчитываем актуальное количество на основе ТЕКУЩЕГО здоровья получателя
            var currentHealth = playerHealth.health;
            var maximumHealth = playerHealth.maxHealth;
            int actualHealAmount = Mathf.Min(healAmount, maximumHealth - currentHealth);
            actualHealAmount = Mathf.Max(0, actualHealAmount); // Убедимся, что не отрицательное

             Debug.Log($"PlayerHealSync (ViewID: {this.photonView?.ViewID ?? 0}): Current health is {currentHealth}/{maximumHealth}. Calculated actual heal amount: {actualHealAmount}.");

            if (actualHealAmount > 0)
            {
                playerHealth.Heal(actualHealAmount, false); // Используем стандартный метод
                Debug.Log($"PlayerHealSync (ViewID: {this.photonView?.ViewID ?? 0}): Called playerHealth.Heal({actualHealAmount}). New health should be {Mathf.Min(currentHealth + actualHealAmount, maximumHealth)}.");
            }
            else
            {
                 Debug.Log($"PlayerHealSync (ViewID: {this.photonView?.ViewID ?? 0}): Actual heal amount is zero. No heal needed.");
            }
        }
        else
        {
            Debug.LogError($"PlayerHealSync (ViewID: {this.photonView?.ViewID ?? 0}): Received RPC_HealPlayer but PlayerHealth component is missing!");
        }
    }

    // Старые методы можно удалить или закомментировать
    /*
    public void HealLocally(int amountToHeal) { ... }
    [PunRPC]
    public void RPC_HealPlayer(int amountToHeal) { ... }
    */
}