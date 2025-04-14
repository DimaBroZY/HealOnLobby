using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic; // Нужно для List<>
using BepInEx.Logging; // Нужно для ManualLogSource
using System; // Добавь это в начало файла, если еще не добавлено
using Photon.Pun;
using Photon.Realtime; // <--- ДОБАВЬ ЭТО для доступа к ClientState

// Изменяем ID, имя и версию плагина
[BepInPlugin("com.example.healinlobby", "Heal In Lobby Mod", "1.0.0")]
public class HealInLobbyMod : BaseUnityPlugin
{
    // Статический логгер для доступа из других классов
    internal static ManualLogSource Log;

    void Awake()
    {
        // Инициализируем статический логгер
        Log = Logger;

        // Используем новый ID для Harmony
        var harmony = new Harmony("com.example.healinlobby");
        harmony.PatchAll();
        Log.LogInfo("HealInLobbyMod loaded!"); // Обновляем сообщение в логе
    }
}

// Патчим метод ChangeLevel из класса RunManager
[HarmonyPatch(typeof(RunManager), "ChangeLevel")]
public static class RunManager_ChangeLevel_Patch
{
    // Название RPC-метода, который мы БУДЕМ вызывать (и который нужно будет определить)
    private const string HealRpcName = "RPC_HealPlayer";

    // Postfix выполняется ПОСЛЕ оригинального метода ChangeLevel
    static void Postfix(RunManager __instance)
    {
        string currentLevelName = null;
        string targetLobbyName = "Level - Lobby";

        try
        {
            // Получаем имя текущего (нового) уровня
            if (__instance.levelCurrent != null)
            {
                currentLevelName = __instance.levelCurrent.name;
                // Оставляем этот лог, он полезен для отладки смены уровней
                HealInLobbyMod.Log.LogInfo($"RunManager.ChangeLevel Postfix: Current level is '{currentLevelName}'");
            }
            else
            {
                 // Используем логгер нового класса
                 HealInLobbyMod.Log.LogWarning("RunManager.ChangeLevel Postfix: __instance.levelCurrent is null.");
                 return;
            }

            // Логируем состояние Photon ПЕРЕД проверкой
            bool isMaster = PhotonNetwork.IsMasterClient;
            ClientState clientState = PhotonNetwork.NetworkClientState;
            HealInLobbyMod.Log.LogInfo($"Checking conditions: IsMasterClient={isMaster}, NetworkClientState={clientState}");

            // Проверяем, является ли текущий уровень Лобби
            if (currentLevelName == targetLobbyName)
            {
                // Выполняем лечение, если мы Мастер-клиент ИЛИ если клиент Photon только создан (одиночный режим)
                if (isMaster || clientState == ClientState.PeerCreated)
                {
                    // Мы либо хост в мультиплеере, либо в одиночке (судя по состоянию PeerCreated) - выполняем лечение
                    HealInLobbyMod.Log.LogInfo($"Conditions met. Attempting to heal players via RPC...");

                    if (GameDirector.instance != null && GameDirector.instance.PlayerList != null)
                    {
                        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
                        {
                            if (player != null && player.playerHealth != null)
                            {
                                var currentHealth = player.playerHealth.health;
                                var maximumHealth = player.playerHealth.maxHealth;
                                int healAmount = maximumHealth - currentHealth;

                                if (healAmount > 0)
                                {
                                    // Получаем PhotonView из GameObject'а игрока
                                    PhotonView pv = player.GetComponent<PhotonView>();
                                    if (pv != null)
                                    {
                                        HealInLobbyMod.Log.LogInfo($"Attempting to send RPC '{HealRpcName}' for player '{player.playerName}' (ViewID: {pv.ViewID}) with amount {healAmount}.");
                                        // Отправляем RPC всем, включая себя (AllBuffered - сохранится для тех, кто подключится позже)
                                        // RPC будет выполнен на экземпляре скрипта, который наблюдается этим PhotonView
                                        pv.RPC(HealRpcName, RpcTarget.AllBuffered, healAmount);
                                    }
                                    else
                                    {
                                        HealInLobbyMod.Log.LogWarning($"Player '{player.playerName}' has no PhotonView component. Cannot send RPC. Healing might only work locally for host if applicable.");
                                    }
                                }
                                else
                                {
                                    // Используем логгер нового класса
                                    HealInLobbyMod.Log.LogInfo($"Player '{player.playerName}' already at max health ({currentHealth}/{maximumHealth})");
                                }
                            }
                            else
                            {
                                // Используем логгер нового класса
                                HealInLobbyMod.Log.LogWarning("Found player in list, but they are null or have no PlayerHealth component.");
                            }
                        }
                         // Используем логгер нового класса
                         HealInLobbyMod.Log.LogInfo("Player healing RPC dispatch completed by Master Client / Single Player.");
                    }
                    else
                    {
                        // Используем логгер нового класса
                        HealInLobbyMod.Log.LogWarning("Could not get player list (GameDirector.instance or GameDirector.instance.PlayerList is null).");
                    }
                }
                else
                {
                    // Мы клиент в мультиплеере (не хост) - пропускаем лечение
                    HealInLobbyMod.Log.LogInfo($"Conditions NOT met (IsMasterClient: {isMaster}, NetworkClientState: {clientState}). Skipping healing logic on this client.");
                }
            }
            // Этот лог можно убрать, если не нужен
            /* else
            {
                 HealInLobbyMod.Log.LogInfo($"Current level ('{currentLevelName}') is not the target Lobby ('{targetLobbyName}'). No healing performed.");
            }*/
        }
        catch (Exception ex)
        {
            // Используем логгер нового класса
            HealInLobbyMod.Log.LogError($"Exception occurred in RunManager_ChangeLevel_Patch.Postfix: {ex}");
        }
        // Этот лог тоже можно убрать, если не нужен
        // HealInLobbyMod.Log.LogInfo("RunManager.ChangeLevel Postfix finished.");
    }
}

// Патчим метод Awake (или Start) в PlayerAvatar, чтобы добавить наш компонент и зарегистрировать его
[HarmonyPatch(typeof(PlayerAvatar), "Awake")] // Или "Start", если Awake вызывает проблемы
public static class PlayerAvatar_Awake_Patch
{
    static void Postfix(PlayerAvatar __instance) // Выполняем ПОСЛЕ оригинального Awake
    {
        try
        {
            GameObject playerGO = __instance.gameObject; // Получаем GameObject аватара

            // 1. Добавляем наш компонент синхронизации, если его еще нет
            PlayerHealSync healSync = playerGO.GetComponent<PlayerHealSync>();
            if (healSync == null)
            {
                healSync = playerGO.AddComponent<PlayerHealSync>();
                HealInLobbyMod.Log.LogInfo($"Added PlayerHealSync component to {playerGO.name}");
            }

            // 2. Находим PhotonView
            PhotonView photonView = playerGO.GetComponent<PhotonView>();
            if (photonView != null)
            {
                // 3. Проверяем и добавляем наш компонент в список наблюдаемых PhotonView
                if (photonView.ObservedComponents == null)
                {
                    // Если список null, создаем новый (маловероятно, но на всякий случай)
                     photonView.ObservedComponents = new List<Component>();
                     HealInLobbyMod.Log.LogWarning($"PhotonView on {playerGO.name} had null ObservedComponents. Initialized new list.");
                }

                // Проверяем, есть ли уже наш компонент в списке
                bool alreadyObserved = false;
                foreach (var observed in photonView.ObservedComponents)
                {
                    if (observed is PlayerHealSync)
                    {
                        alreadyObserved = true;
                        break;
                    }
                }

                // Если еще не наблюдается, добавляем
                if (!alreadyObserved)
                {
                    // ВАЖНО: PhotonView.ObservedComponents ожидает список Component,
                    // но часто наблюдаемые скрипты должны быть MonoBehaviour.
                    // Убедимся, что PlayerHealSync наследуется от MonoBehaviour (он наследуется).
                    photonView.ObservedComponents.Add(healSync);
                    HealInLobbyMod.Log.LogInfo($"Added PlayerHealSync to PhotonView ObservedComponents on {playerGO.name}");
                }
                else
                {
                     // HealInLobbyMod.Log.LogInfo($"PlayerHealSync is already observed by PhotonView on {playerGO.name}");
                }
            }
            else
            {
                HealInLobbyMod.Log.LogWarning($"Could not find PhotonView on {playerGO.name} during PlayerAvatar.Awake patch.");
            }
        }
        catch (Exception ex)
        {
            HealInLobbyMod.Log.LogError($"Exception in PlayerAvatar_Awake_Patch.Postfix: {ex}");
        }
    }
}