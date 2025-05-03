using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using System;
using Photon.Pun;
using Photon.Realtime;
using BepInEx.Configuration;
using Random = UnityEngine.Random;

// Убираем PlayerData
// public struct PlayerData { ... }

[BepInPlugin("HealOnLobby", "Heal On Lobby Mod", "1.0.8")] // Direct Heal for SP
public class HealOnLobbyMod : BaseUnityPlugin
{
    internal static ManualLogSource Log;
    public static HealOnLobbyMod Instance { get; private set; }

    // Configuration settings
    internal static ConfigEntry<bool> HealToMaxEnabled;
    internal static ConfigEntry<bool> HealByPercentEnabled;
    internal static ConfigEntry<int> HealPercentAmount;
    internal static ConfigEntry<int> HealAmount;
    internal static ConfigEntry<int> HealChance;

    void Awake()
    {
        Instance = this;
        Log = Logger;

        // 1. Heal to max health
        HealToMaxEnabled = Config.Bind(
            "1. General",
            "1. Heal To Max Health",
            true,
            "Heal players to full health in the lobby. Overrides other settings if enabled."
        );

        // 2. Heal by percentage
        HealByPercentEnabled = Config.Bind(
            "1. General",
            "2. Heal By Percent Enabled",
            false,
            "Heal players by a percentage of their max health (if 'Heal To Max Health' is off)."
        );
        HealPercentAmount = Config.Bind(
            "1. General",
            "3. Heal Percent Amount",
            50,
            new ConfigDescription(
                "The percentage (0-100) of max health to restore (used if 'Heal By Percent Enabled' is on).",
                new AcceptableValueRange<int>(0, 100)
            )
        );

        // 3. Heal by static amount
        HealAmount = Config.Bind(
            "1. General",
            "4. Heal Amount",
            100,
            "Fixed amount of health to restore (used if both 'Heal To Max Health' and 'Heal By Percent Enabled' are off)."
        );

        // 2. Heal chance
        HealChance = Config.Bind(
            "2. Heal Chance",
            "Heal Chance",
            100,
            new ConfigDescription(
                "The chance (0-100) that healing will occur when entering the lobby. 100 means healing always happens (if conditions are met).",
                new AcceptableValueRange<int>(0, 100)
            )
        );

        var harmony = new Harmony("HealOnLobby");
        harmony.PatchAll();
        Log.LogInfo("HealOnLobbyMod loaded! All configurations loaded.");
    }
}

// Патч на RunManager.ChangeLevel для немедленного действия
[HarmonyPatch(typeof(RunManager), "ChangeLevel")]
public static class RunManager_ChangeLevel_Patch
{
    // Имя RPC из PlayerHealSync.cs v1.0.6
    private const string HealRpcName = "RPC_HealPlayer";

    static void Postfix(RunManager __instance)
    {
        string currentLevelName = null;
        string targetLobbyName = "Level - Lobby";

        try
        {
            if (__instance.levelCurrent == null)
            {
                 HealOnLobbyMod.Log.LogWarning("RunManager.ChangeLevel Postfix: __instance.levelCurrent is null.");
                 return;
            }
            currentLevelName = __instance.levelCurrent.name;
            HealOnLobbyMod.Log.LogInfo($"RunManager.ChangeLevel Postfix: Current level is '{currentLevelName}'");

            if (currentLevelName == targetLobbyName)
            {
                bool isMaster = PhotonNetwork.IsMasterClient;
                ClientState clientState = PhotonNetwork.NetworkClientState;
                HealOnLobbyMod.Log.LogInfo($"Checking conditions: IsMasterClient={isMaster}, NetworkClientState={clientState}");

                // Инициатор: Хост или Одиночка (PeerCreated / Disconnected)
                bool isSinglePlayerLike = clientState == ClientState.Disconnected || clientState == ClientState.PeerCreated;
                bool shouldInitiateHealing = isMaster || isSinglePlayerLike;

                if (shouldInitiateHealing)
                {
                    string initiator = isMaster ? "Host" : (isSinglePlayerLike ? "Single-player" : "Unknown");
                    HealOnLobbyMod.Log.LogInfo($"Conditions met ({initiator}, State: {clientState}). Processing healing IMMEDIATELY...");

                    try
                    {
                        int healChanceValue = HealOnLobbyMod.HealChance.Value;

                        if (GameDirector.instance != null && GameDirector.instance.PlayerList != null)
                        {
                             HealOnLobbyMod.Log.LogInfo($"Found {GameDirector.instance.PlayerList.Count} player(s) in list.");

                             foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
                             {
                                 if (player != null && player.playerHealth != null)
                                 {
                                     string playerName = player.playerName ?? "NULL_NAME";
                                     PhotonView pv = player.GetComponent<PhotonView>();

                                     // PhotonView нужен только для отправки RPC хостом
                                     if (pv == null && isMaster) {
                                         HealOnLobbyMod.Log.LogWarning($"Player '{playerName}' has no PhotonView. Skipping RPC.");
                                         continue;
                                     }
                                     // Для одиночки pv может быть null, это не страшно

                                     var currentHealth = player.playerHealth.health;
                                     var maximumHealth = player.playerHealth.maxHealth;
                                     HealOnLobbyMod.Log.LogInfo($"--- Player '{playerName}' (ViewID: {pv?.ViewID ?? 0}): Health READ as {currentHealth}/{maximumHealth} ---");

                                     // --- Расчет healAmountToSend с учетом ВСЕХ конфигов ---
                                     int healAmountToSend = 0;
                                     bool healToMax = HealOnLobbyMod.HealToMaxEnabled.Value;
                                     bool healByPercent = HealOnLobbyMod.HealByPercentEnabled.Value;
                                     int percentHealValue = HealOnLobbyMod.HealPercentAmount.Value;
                                     int specificHealAmount = HealOnLobbyMod.HealAmount.Value;

                                     if (healToMax) { healAmountToSend = maximumHealth - currentHealth; }
                                     else if (healByPercent) { int h = Mathf.CeilToInt(maximumHealth * (percentHealValue / 100.0f)); healAmountToSend = Mathf.Min(h, maximumHealth - currentHealth); }
                                     else { healAmountToSend = Mathf.Min(specificHealAmount, maximumHealth - currentHealth); }
                                     healAmountToSend = Mathf.Max(0, healAmountToSend);
                                     // --- Конец расчета ---

                                     if (healAmountToSend > 0)
                                     {
                                         HealOnLobbyMod.Log.LogInfo($"Player '{playerName}': Calculated HEAL AMOUNT: {healAmountToSend}");

                                         int randomRoll = Random.Range(1, 101);
                                         bool healAllowedByChance = randomRoll <= healChanceValue;
                                         HealOnLobbyMod.Log.LogInfo($"Player '{playerName}': Heal chance check: Rolled {randomRoll} vs Chance {healChanceValue}. Allowed: {healAllowedByChance}");

                                         if (healAllowedByChance)
                                         {
                                             // --- РАЗДЕЛЬНАЯ ЛОГИКА ---
                                             if (isMaster) // Мультиплеер - Хост отправляет RPC
                                             {
                                                 if (pv != null) // Перепроверка pv
                                                 {
                                                     HealOnLobbyMod.Log.LogInfo($"MP Host: Sending RPC '{HealRpcName}' for player '{playerName}' (ViewID: {pv.ViewID}) with HEAL AMOUNT {healAmountToSend}.");
                                                     pv.RPC(HealRpcName, RpcTarget.AllBuffered, healAmountToSend);
                                                 } else {
                                                      HealOnLobbyMod.Log.LogWarning($"MP Host: PhotonView became null for player '{playerName}' before sending RPC.");
                                                 }
                                             }
                                             else if (isSinglePlayerLike) // Одиночная игра - ПРЯМОЙ ВЫЗОВ Heal()
                                             {
                                                 // В одиночной игре лечим напрямую, используя playerHealth.Heal
                                                 // Рассчитываем фактическое количество (как в PlayerHealSync v1.0.6)
                                                 int actualHeal = Mathf.Min(healAmountToSend, maximumHealth - currentHealth);
                                                 actualHeal = Mathf.Max(0, actualHeal);

                                                 if(actualHeal > 0) {
                                                     HealOnLobbyMod.Log.LogInfo($"SP (State: {clientState}): Calling playerHealth.Heal directly for player '{playerName}' (ViewID: {pv?.ViewID ?? 0}) with ACTUAL amount {actualHeal}.");
                                                     player.playerHealth.Heal(actualHeal, false); // Используем стандартный Heal игры
                                                 } else {
                                                      HealOnLobbyMod.Log.LogInfo($"SP: Calculated actual heal is 0 for player '{playerName}'. Skipping direct call.");
                                                 }
                                             }
                                         }
                                         else { HealOnLobbyMod.Log.LogInfo($"Player '{playerName}' healing skipped due to chance ({randomRoll} > {healChanceValue})."); }
                                     }
                                     else
                                     {
                                         HealOnLobbyMod.Log.LogInfo($"Player '{playerName}' needs no healing ({currentHealth}/{maximumHealth}). No action taken.");
                                     }
                                 }
                                 else { HealOnLobbyMod.Log.LogWarning($"Player entry is null or has no PlayerHealth component."); }
                             } // Конец foreach
                             HealOnLobbyMod.Log.LogInfo("Immediate healing processing loop completed.");
                        }
                        else { HealOnLobbyMod.Log.LogWarning("GameDirector or PlayerList is null."); }
                    }
                    catch (Exception ex)
                    {
                        HealOnLobbyMod.Log.LogError($"Exception occurred during immediate healing processing: {ex}");
                    }
                }
                else // Логика для клиентов (не хостов)
                {
                     HealOnLobbyMod.Log.LogInfo($"Client role detected (IsMasterClient: {isMaster}, NetworkClientState: {clientState}). Waiting for host RPC...");
                }
            }
        }
        catch (Exception ex)
        {
            HealOnLobbyMod.Log.LogError($"Exception occurred in RunManager_ChangeLevel_Patch.Postfix: {ex}");
        }
    }
} // Конец RunManager_ChangeLevel_Patch

// --- PlayerAvatar_Awake_Patch остается ---
[HarmonyPatch(typeof(PlayerAvatar), "Awake")]
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
                HealOnLobbyMod.Log.LogInfo($"Added PlayerHealSync component to {playerGO.name}");
            }
        }
        catch (Exception ex)
        {
            HealOnLobbyMod.Log.LogError($"Exception in PlayerAvatar_Awake_Patch.Postfix: {ex}");
        }
    }
}