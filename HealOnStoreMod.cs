using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using System;
using Photon.Pun;
using Photon.Realtime;
using BepInEx.Configuration;

// Declares the main plugin metadata for BepInEx
[BepInPlugin("HealOnLobby", "Heal In Lobby Mod", "1.0.4")]
public class HealInLobbyMod : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    // Configuration settings
    internal static ConfigEntry<bool> HealToMaxEnabled;
    internal static ConfigEntry<bool> HealByPercentEnabled;
    internal static ConfigEntry<int> HealPercentAmount;
    internal static ConfigEntry<int> HealAmount;

    void Awake()
    {
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

        var harmony = new Harmony("HealOnLobby");
        harmony.PatchAll();
        Log.LogInfo("HealInLobbyMod loaded! Configuration loaded.");
    }
}

// Patches the ChangeLevel method from RunManager class to execute logic upon entering the lobby
[HarmonyPatch(typeof(RunManager), "ChangeLevel")]
public static class RunManager_ChangeLevel_Patch
{
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
            HealInLobbyMod.Log.LogInfo($"RunManager.ChangeLevel Postfix: Current level is '{currentLevelName}'");

            if (currentLevelName == targetLobbyName)
            {
                bool isMaster = PhotonNetwork.IsMasterClient;
                ClientState clientState = PhotonNetwork.NetworkClientState;
                HealInLobbyMod.Log.LogInfo($"Checking conditions: IsMasterClient={isMaster}, NetworkClientState={clientState}");

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
                                int healAmountToSend = 0;

                                bool healToMax = HealInLobbyMod.HealToMaxEnabled.Value;
                                bool healByPercent = HealInLobbyMod.HealByPercentEnabled.Value;
                                int percentHealValue = HealInLobbyMod.HealPercentAmount.Value;
                                int specificHealAmount = HealInLobbyMod.HealAmount.Value;

                                if (healToMax)
                                {
                                    healAmountToSend = maximumHealth - currentHealth;
                                    HealInLobbyMod.Log.LogInfo($"Player '{player.playerName}': Priority 1 (HealToMax) enabled. Calculated heal amount: {healAmountToSend}");
                                }
                                else if (healByPercent)
                                {
                                    int healBasedOnPercent = Mathf.CeilToInt(maximumHealth * (percentHealValue / 100.0f));
                                    healAmountToSend = Mathf.Min(healBasedOnPercent, maximumHealth - currentHealth);
                                    HealInLobbyMod.Log.LogInfo($"Player '{player.playerName}': Priority 2 (HealByPercent) enabled ({percentHealValue}%). Calculated heal amount: {healAmountToSend} (Raw percent heal: {healBasedOnPercent})");
                                }
                                else
                                {
                                    healAmountToSend = Mathf.Min(specificHealAmount, maximumHealth - currentHealth);
                                    HealInLobbyMod.Log.LogInfo($"Player '{player.playerName}': Priority 3 (SpecificAmount) enabled. Calculated heal amount: {healAmountToSend} (Configured amount: {specificHealAmount})");
                                }

                                healAmountToSend = Mathf.Max(0, healAmountToSend);

                                if (healAmountToSend > 0)
                                {
                                    PhotonView pv = player.GetComponent<PhotonView>();
                                    if (pv != null)
                                    {
                                        HealInLobbyMod.Log.LogInfo($"Attempting to send RPC '{HealRpcName}' for player '{player.playerName}' (ViewID: {pv.ViewID}) with final amount {healAmountToSend}.");
                                        pv.RPC(HealRpcName, RpcTarget.AllBuffered, healAmountToSend);
                                    }
                                    else
                                    {
                                        HealInLobbyMod.Log.LogWarning($"Player '{player.playerName}' has no PhotonView component. Cannot send RPC.");
                                    }
                                }
                                else
                                {
                                     HealInLobbyMod.Log.LogInfo($"Player '{player.playerName}' needs no healing (Current: {currentHealth}/{maximumHealth}, Calculated Amount: {healAmountToSend}). No RPC sent.");
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

// Patches the Awake method in PlayerAvatar to add our PlayerHealSync component
[HarmonyPatch(typeof(PlayerAvatar), "Awake")]
public static class PlayerAvatar_Awake_Patch
{
    static void Postfix(PlayerAvatar __instance)
    {
        try
        {
            GameObject playerGO = __instance.gameObject;

            // Ensure the PlayerHealSync component exists to receive RPCs
            // PlayerHealSync class is now in PlayerHealSync.cs
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