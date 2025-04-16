using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using System;
using Photon.Pun;
using Photon.Realtime;
using BepInEx.Configuration;

// Объявляет основные метаданные плагина для BepInEx
[BepInPlugin("HealOnLobby", "Heal In Lobby Mod", "1.0.2")]
public class HealInLobbyMod : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    internal static ConfigEntry<bool> HealToMaxEnabled;
    internal static ConfigEntry<int> HealAmount;

    void Awake()
    {
        Log = Logger;

        HealToMaxEnabled = Config.Bind(
            "1. General",
            "Heal To Max Health",
            true,
            "If true, players will be healed to their maximum health in the lobby, ignoring the 'Heal Amount' setting. If false, the specific 'Heal Amount' will be used."
        );

        HealAmount = Config.Bind(
            "1. General",
            "Heal Amount",
            100,
            "The amount of health to restore when entering the lobby. Only used if 'Heal To Max Health' is set to false. Healing will not exceed the player's maximum health."
        );

        var harmony = new Harmony("HealOnLobby"); // Используем новый GUID
        harmony.PatchAll();
        Log.LogInfo("HealInLobbyMod loaded! Configuration loaded.");
    }
}

// Патчит метод ChangeLevel из класса RunManager для выполнения логики при входе в лобби
[HarmonyPatch(typeof(RunManager), "ChangeLevel")]
public static class RunManager_ChangeLevel_Patch
{
    // Название RPC-метода для синхронизации лечения
    private const string HealRpcName = "RPC_HealPlayer";

    static void Postfix(RunManager __instance)
    {
        string currentLevelName = null;
        string targetLobbyName = "Level - Lobby";

        try
        {
            if (__instance.levelCurrent == null)
            {
                 HealInLobbyMod.Log.LogWarning("RunManager.ChangeLevel Postfix: __instance.levelCurrent is null.");
                 return;
            }
            currentLevelName = __instance.levelCurrent.name;
            // Этот лог полезен для отладки смены уровней
            HealInLobbyMod.Log.LogInfo($"RunManager.ChangeLevel Postfix: Current level is '{currentLevelName}'");


            if (currentLevelName == targetLobbyName)
            {
                bool isMaster = PhotonNetwork.IsMasterClient;
                ClientState clientState = PhotonNetwork.NetworkClientState;
                HealInLobbyMod.Log.LogInfo($"Checking conditions: IsMasterClient={isMaster}, NetworkClientState={clientState}");

                // Выполняем лечение, если мы Мастер-клиент (хост) ИЛИ если клиент Photon только создан (вероятно, одиночный режим)
                if (isMaster || clientState == ClientState.PeerCreated)
                {
                    HealInLobbyMod.Log.LogInfo($"Conditions met. Processing healing based on config...");

                    if (GameDirector.instance != null && GameDirector.instance.PlayerList != null)
                    {
                        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
                        {
                            if (player != null && player.playerHealth != null)
                            {
                                var currentHealth = player.playerHealth.health;
                                var maximumHealth = player.playerHealth.maxHealth;
                                int desiredHealAmount = 0;

                                bool healToMax = HealInLobbyMod.HealToMaxEnabled.Value;
                                int specificHealAmount = HealInLobbyMod.HealAmount.Value;

                                if (healToMax)
                                {
                                    desiredHealAmount = maximumHealth - currentHealth;
                                    HealInLobbyMod.Log.LogInfo($"Player '{player.playerName}': HealToMax is enabled. Calculated heal amount: {desiredHealAmount}");
                                }
                                else
                                {
                                    desiredHealAmount = specificHealAmount;
                                     HealInLobbyMod.Log.LogInfo($"Player '{player.playerName}': HealToMax is disabled. Using configured heal amount: {desiredHealAmount}");
                                }

                                if (desiredHealAmount > 0 && currentHealth < maximumHealth)
                                {
                                    PhotonView pv = player.GetComponent<PhotonView>();
                                    if (pv != null)
                                    {
                                        // Логика ограничения до макс. ХП будет на стороне получателя (в PlayerHealSync)
                                        HealInLobbyMod.Log.LogInfo($"Attempting to send RPC '{HealRpcName}' for player '{player.playerName}' (ViewID: {pv.ViewID}) with desired amount {desiredHealAmount}.");
                                        pv.RPC(HealRpcName, RpcTarget.AllBuffered, desiredHealAmount);
                                    }
                                    else
                                    {
                                        HealInLobbyMod.Log.LogWarning($"Player '{player.playerName}' has no PhotonView component. Cannot send RPC.");
                                    }
                                }
                                else if (currentHealth >= maximumHealth)
                                {
                                     HealInLobbyMod.Log.LogInfo($"Player '{player.playerName}' already at or above max health ({currentHealth}/{maximumHealth}). No heal needed.");
                                }
                                else
                                {
                                     HealInLobbyMod.Log.LogInfo($"Player '{player.playerName}' calculated heal amount is zero or negative ({desiredHealAmount}). No heal needed.");
                                }
                            }
                            else
                            {
                                HealInLobbyMod.Log.LogWarning("Found player in list, but they are null or have no PlayerHealth component.");
                            }
                        }
                         HealInLobbyMod.Log.LogInfo("Player healing RPC dispatch processing completed.");
                    }
                    else
                    {
                        HealInLobbyMod.Log.LogWarning("Could not get player list (GameDirector.instance or GameDirector.instance.PlayerList is null).");
                    }
                }
                else
                {
                    HealInLobbyMod.Log.LogInfo($"Conditions NOT met (IsMasterClient: {isMaster}, NetworkClientState: {clientState}). Skipping healing logic on this client.");
                }
            }
        }
        catch (Exception ex)
        {
            HealInLobbyMod.Log.LogError($"Exception occurred in RunManager_ChangeLevel_Patch.Postfix: {ex}");
        }
    }
}

// Патчит метод Awake в PlayerAvatar, чтобы добавить наш компонент PlayerHealSync и зарегистрировать его в PhotonView
[HarmonyPatch(typeof(PlayerAvatar), "Awake")] // Или "Start", если Awake вызывает проблемы с инициализацией
public static class PlayerAvatar_Awake_Patch
{
    static void Postfix(PlayerAvatar __instance)
    {
        try
        {
            GameObject playerGO = __instance.gameObject;

            PlayerHealSync healSync = playerGO.GetComponent<PlayerHealSync>();
            if (healSync == null)
            {
                healSync = playerGO.AddComponent<PlayerHealSync>();
                HealInLobbyMod.Log.LogInfo($"Added PlayerHealSync component to {playerGO.name}");
            }
        }
        catch (Exception ex)
        {
            HealInLobbyMod.Log.LogError($"Exception in PlayerAvatar_Awake_Patch.Postfix: {ex}");
        }
    }
}