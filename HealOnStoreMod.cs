using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic; // Нужно для List<>
using BepInEx.Logging; // Нужно для ManualLogSource
using System; // Добавь это в начало файла, если еще не добавлено

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

            // Проверяем, является ли текущий уровень Лобби
            if (currentLevelName == targetLobbyName)
            {
                // Используем логгер нового класса
                HealInLobbyMod.Log.LogInfo($"Detected entry into Lobby ('{currentLevelName}'). Attempting to heal players...");

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
                                player.playerHealth.Heal(healAmount, false);
                                // Используем логгер нового класса
                                HealInLobbyMod.Log.LogInfo($"Player '{player.playerName}' healed by {healAmount} ({currentHealth} -> {maximumHealth}) using Heal method.");
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
                     HealInLobbyMod.Log.LogInfo("Player healing process completed for Lobby entry.");
                }
                else
                {
                    // Используем логгер нового класса
                    HealInLobbyMod.Log.LogWarning("Could not get player list (GameDirector.instance or GameDirector.instance.PlayerList is null).");
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