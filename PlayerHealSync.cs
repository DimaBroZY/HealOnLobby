using UnityEngine;
using Photon.Pun;

// Добавляем этот компонент к игроку, чтобы принимать RPC для лечения
public class PlayerHealSync : MonoBehaviourPun
{
    private PlayerAvatar playerAvatar;
    private PlayerHealth playerHealth; // Предполагаем, что PlayerHealth тоже на этом объекте

    void Awake()
    {
        playerAvatar = GetComponent<PlayerAvatar>();
        playerHealth = GetComponent<PlayerHealth>();

        if (playerAvatar == null)
        {
            Debug.LogError("PlayerHealSync: Could not find PlayerAvatar component!");
        }
        if (playerHealth == null)
        {
            Debug.LogError("PlayerHealSync: Could not find PlayerHealth component!");
        }
    }

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

            int actualHealAmount = Mathf.Min(healAmount, maximumHealth - currentHealth);

            if (actualHealAmount > 0)
            {
                playerHealth.Heal(actualHealAmount, false);
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