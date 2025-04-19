using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using BepInEx.Logging;
using System;
using Photon.Pun;
using Photon.Realtime;
using BepInEx.Configuration;
using Random = UnityEngine.Random;

// Declares the main plugin metadata for BepInEx
[BepInPlugin("HealOnLobby", "Heal On Lobby Mod", "1.0.6")]
public class HealOnLobbyMod : BaseUnityPlugin
{
    internal static ManualLogSource Log;

    // Configuration settings
    internal static ConfigEntry<bool> HealToMaxEnabled;
    internal static ConfigEntry<bool> HealByPercentEnabled;
    internal static ConfigEntry<int> HealPercentAmount;
    internal static ConfigEntry<int> HealAmount;
    internal static ConfigEntry<int> HealChance;

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
        Log.LogInfo("HealOnLobbyMod loaded! Configuration loaded.");
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

                if (isMaster || clientState == ClientState.PeerCreated)
                {
                    HealOnLobbyMod.Log.LogInfo($"Conditions met. Processing healing based on config...");
                    int healChanceValue = HealOnLobbyMod.HealChance.Value;

                    if (GameDirector.instance != null && GameDirector.instance.PlayerList != null)
                    {
                        foreach (PlayerAvatar player in GameDirector.instance.PlayerList)
                        {
                            if (player != null && player.playerHealth != null)
                            {
                                var currentHealth = player.playerHealth.health;
                                var maximumHealth = player.playerHealth.maxHealth;
                                int healAmountToSend = 0;

                                bool healToMax = HealOnLobbyMod.HealToMaxEnabled.Value;
                                bool healByPercent = HealOnLobbyMod.HealByPercentEnabled.Value;
                                int percentHealValue = HealOnLobbyMod.HealPercentAmount.Value;
                                int specificHealAmount = HealOnLobbyMod.HealAmount.Value;

                                if (healToMax)
                                {
                                    healAmountToSend = maximumHealth - currentHealth;
                                    HealOnLobbyMod.Log.LogInfo($"Player '{player.playerName}': Priority 1 (HealToMax) enabled. Calculated heal amount: {healAmountToSend}");
                                }
                                else if (healByPercent)
                                {
                                    int healBasedOnPercent = Mathf.CeilToInt(maximumHealth * (percentHealValue / 100.0f));
                                    healAmountToSend = Mathf.Min(healBasedOnPercent, maximumHealth - currentHealth);
                                    HealOnLobbyMod.Log.LogInfo($"Player '{player.playerName}': Priority 2 (HealByPercent) enabled ({percentHealValue}%). Calculated heal amount: {healAmountToSend} (Raw percent heal: {healBasedOnPercent})");
                                }
                                else
                                {
                                    healAmountToSend = Mathf.Min(specificHealAmount, maximumHealth - currentHealth);
                                    HealOnLobbyMod.Log.LogInfo($"Player '{player.playerName}': Priority 3 (SpecificAmount) enabled. Calculated heal amount: {healAmountToSend} (Configured amount: {specificHealAmount})");
                                }

                                healAmountToSend = Mathf.Max(0, healAmountToSend);

                                if (healAmountToSend > 0)
                                {
                                    int randomRoll = Random.Range(1, 101);
                                    bool healAllowedByChance = randomRoll <= healChanceValue;
                                    HealOnLobbyMod.Log.LogInfo($"Player '{player.playerName}': Heal chance check: Rolled {randomRoll} vs Chance {healChanceValue}. Allowed: {healAllowedByChance}");

                                    if (healAllowedByChance)
                                    {
                                        if (isMaster)
                                        {
                                            PhotonView pv = player.GetComponent<PhotonView>();
                                            if (pv != null)
                                            {
                                                HealOnLobbyMod.Log.LogInfo($"Sending RPC '{HealRpcName}' for player '{player.playerName}' (ViewID: {pv.ViewID}) with final amount {healAmountToSend}.");
                                                pv.RPC(HealRpcName, RpcTarget.AllBuffered, healAmountToSend);
                                            }
                                            else
                                            {
                                                HealOnLobbyMod.Log.LogWarning($"Player '{player.playerName}' has no PhotonView component. Cannot send RPC.");
                                            }
                                        }
                                        else if (clientState == ClientState.PeerCreated)
                                        {
                                            PlayerHealSync healSync = player.GetComponent<PlayerHealSync>();
                                            if (healSync != null)
                                            {
                                                HealOnLobbyMod.Log.LogInfo($"Calling HealLocally directly for player '{player.playerName}' with amount {healAmountToSend}.");
                                                healSync.HealLocally(healAmountToSend);
                                            }
                                            else
                                            {
                                                HealOnLobbyMod.Log.LogWarning($"Player '{player.playerName}' has no PlayerHealSync component. Cannot heal locally.");
                                            }
                                        }
                                    }
                                    else
                                    {
                                        HealOnLobbyMod.Log.LogInfo($"Player '{player.playerName}' healing skipped due to chance ({randomRoll} > {healChanceValue}).");
                                    }
                                }
                                else
                                {
                                     HealOnLobbyMod.Log.LogInfo($"Player '{player.playerName}' needs no healing (Current: {currentHealth}/{maximumHealth}, Calculated Amount: {healAmountToSend}). No action taken.");
                                }
                            }
                            else
                            {
                                HealOnLobbyMod.Log.LogWarning("Found player in list, but they are null or have no PlayerHealth component.");
                            }
                        }
                         HealOnLobbyMod.Log.LogInfo("Player healing processing completed.");
                    }
                    else
                    {
                        HealOnLobbyMod.Log.LogWarning("Could not get player list (GameDirector.instance or GameDirector.instance.PlayerList is null).");
                    }
                }
                else
                {
                    HealOnLobbyMod.Log.LogInfo($"Conditions NOT met (IsMasterClient: {isMaster}, NetworkClientState: {clientState}). Skipping healing logic on this client.");
                }
            }
        }
        catch (Exception ex)
        {
            HealOnLobbyMod.Log.LogError($"Exception occurred in RunManager_ChangeLevel_Patch.Postfix: {ex}");
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
                HealOnLobbyMod.Log.LogInfo($"Added PlayerHealSync component to {playerGO.name}");
            }
        }
        catch (Exception ex)
        {
            HealOnLobbyMod.Log.LogError($"Exception in PlayerAvatar_Awake_Patch.Postfix: {ex}");
        }
    }
}