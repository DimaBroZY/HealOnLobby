using UnityEngine;
using Photon.Pun;

// Добавляем этот компонент к игроку, чтобы принимать RPC для лечения
public class PlayerHealSync : MonoBehaviourPun
{
    private PlayerAvatar playerAvatar;
    private PlayerHealth playerHealth; 

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

    // Новый метод для прямого вызова лечения (в одиночном режиме)
    public void HealLocally(int amountToHeal)
    {
        if (playerHealth != null)
        {
            int currentHealth = playerHealth.health;
            int maxHealth = playerHealth.maxHealth;
            // Ограничиваем лечение максимальным здоровьем
            int actualHeal = Mathf.Min(amountToHeal, maxHealth - currentHealth);

            if (actualHeal > 0)
            {
                playerHealth.Heal(actualHeal); // Вызываем метод лечения из игры
                // Log using Debug.Log or your mod's logger if accessible
                 Debug.Log($"PlayerHealSync: Healed locally by {actualHeal} (Requested: {amountToHeal}). New health: {playerHealth.health}/{maxHealth}");
                 // HealOnLobbyMod.Log?.LogInfo($"PlayerHealSync: Healed locally by {actualHeal} (Requested: {amountToHeal}). New health: {playerHealth.health}/{maxHealth}");

            }
            else
            {
                 Debug.Log($"PlayerHealSync: Local heal called, but no actual healing needed (Requested: {amountToHeal}, Current: {currentHealth}/{maxHealth}).");
                 // HealOnLobbyMod.Log?.LogInfo($"PlayerHealSync: Local heal called, but no actual healing needed (Requested: {amountToHeal}, Current: {currentHealth}/{maxHealth}).");
            }
        }
        else
        {
            Debug.LogError($"PlayerHealSync: HealLocally called, but PlayerHealth component is missing!");
             // HealOnLobbyMod.Log?.LogError($"PlayerHealSync: HealLocally called, but PlayerHealth component is missing!");
        }
    }

    // RPC-метод для получения команды от хоста (в мультиплеере)
    [PunRPC]
    public void RPC_HealPlayer(int amountToHeal /* Убери PhotonMessageInfo, если он был, т.к. он не нужен при прямом вызове */)
    {
        // Просто вызываем локальную логику
        // Проверка на MasterClient здесь больше не нужна, она делается перед отправкой RPC
         Debug.Log($"PlayerHealSync: RPC_HealPlayer received with amount {amountToHeal}. Calling HealLocally.");
         // HealOnLobbyMod.Log?.LogInfo($"PlayerHealSync: RPC_HealPlayer received with amount {amountToHeal}. Calling HealLocally.");
        HealLocally(amountToHeal);
    }
}