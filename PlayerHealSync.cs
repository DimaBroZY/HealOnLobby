using UnityEngine;
using Photon.Pun; // Нужно для PunRPC и PhotonMessageInfo

// Добавляем этот компонент к игроку, чтобы принимать RPC для лечения
public class PlayerHealSync : MonoBehaviourPun // Наследуем от MonoBehaviourPun для удобства (хотя для RPC достаточно MonoBehaviour)
{
    private PlayerAvatar playerAvatar; // Ссылка на основной скрипт аватара
    private PlayerHealth playerHealth; // Ссылка на компонент здоровья

    void Awake()
    {
        // Получаем ссылки на нужные компоненты на этом же GameObject
        playerAvatar = GetComponent<PlayerAvatar>();
        playerHealth = GetComponent<PlayerHealth>(); // Предполагаем, что PlayerHealth тоже на этом объекте

        if (playerAvatar == null)
        {
            Debug.LogError("PlayerHealSync: Could not find PlayerAvatar component!");
        }
        if (playerHealth == null)
        {
            Debug.LogError("PlayerHealSync: Could not find PlayerHealth component!");
        }
    }

    // Этот метод будет вызван через RPC
    [PunRPC]
    public void RPC_HealPlayer(int healAmount, PhotonMessageInfo info) // PhotonMessageInfo содержит инфо об отправителе
    {
        // Проверка безопасности: Убедимся, что RPC пришел от Мастер-клиента
        // Это важно, чтобы клиенты не могли сами себе отправлять команды на лечение
        if (!info.Sender.IsMasterClient)
        {
            Debug.LogWarning($"PlayerHealSync: Received RPC_HealPlayer from non-MasterClient {info.Sender.NickName}. Ignoring.");
            return;
        }

        if (playerHealth != null)
        {
            var currentHealth = playerHealth.health;
            var maximumHealth = playerHealth.maxHealth;
            // Убедимся, что лечим не больше максимума (на всякий случай)
            int actualHealAmount = Mathf.Min(healAmount, maximumHealth - currentHealth);

            if (actualHealAmount > 0)
            {
                playerHealth.Heal(actualHealAmount, false); // Выполняем реальное лечение локально
                Debug.Log($"PlayerHealSync: Player '{playerAvatar?.playerName ?? gameObject.name}' received RPC and healed by {actualHealAmount}. Sender: {info.Sender.NickName}");
            }
            else
            {
                 Debug.Log($"PlayerHealSync: Player '{playerAvatar?.playerName ?? gameObject.name}' received RPC, but already at max health or heal amount is zero.");
            }
        }
        else
        {
            Debug.LogError($"PlayerHealSync: Received RPC_HealPlayer but PlayerHealth component is missing!");
        }
    }
}