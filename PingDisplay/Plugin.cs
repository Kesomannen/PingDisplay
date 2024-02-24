using System;
using BepInEx;
using HarmonyLib;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace PingDisplay;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    static TMP_Text _pingText;
    static float _lastUpdateTime;
    static bool _showPing;

    const float UpdateInterval = 0.5f;
    
    void Awake() {
        var showPingConfig = Config.Bind(
            "General",
            "ShowPing",
            true,
            "Show your latency to the host in the top left corner of the screen"
        );
        
        _showPing = showPingConfig.Value;
        showPingConfig.SettingChanged += (_, _) => _showPing = showPingConfig.Value;
        
        Harmony.CreateAndPatchAll(typeof(Plugin));
        
        Logger.LogInfo("Plugin loaded!");
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HUDManager), "Awake")]
    static void HUDManager_Start_Postfix(HUDManager __instance) {
        var textGameObject = new GameObject("PingDisplay", typeof(RectTransform));
        textGameObject.transform.SetParent(__instance.HUDContainer.transform, false);
        
        _pingText = textGameObject.AddComponent<TextMeshProUGUI>();
        _pingText.faceColor = new Color32(255, 255, 255, 40);
        _pingText.font = __instance.weightCounter.font;
        _pingText.alignment = TextAlignmentOptions.TopLeft;
        _pingText.fontSize = 12;
        
        var rect = _pingText.rectTransform;

        rect.pivot = new Vector2(0f, 1f);
        rect.anchorMin = rect.anchorMax = new Vector2(0.03f, 0.97f);
        rect.anchoredPosition = new Vector2(0f, 0f);
        
        var canvasGroup = textGameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 1f;
        
        var hudElement = new HUDElement {
            canvasGroup = canvasGroup,
            targetAlpha = 1f
        };
        
        var hudElements = (HUDElement[]) AccessTools.Field(typeof(HUDManager), "HUDElements").GetValue(__instance);
        Array.Resize(ref hudElements, hudElements.Length + 1);
        hudElements[^1] = hudElement;
    }
    
    [HarmonyPostfix]
    [HarmonyPatch(typeof(HUDManager), "Update")]
    static void HUDManager_Update_Postfix(HUDManager __instance) {
        if (_pingText == null) return;

        if (Time.time - _lastUpdateTime < UpdateInterval) return;
        _lastUpdateTime = Time.time;
        
        if (!_showPing) {
            _pingText.text = "";
            return;
        }

        if (NetworkManager.Singleton.IsHost) {
            _pingText.text = "Ping: ---";
        } else {
            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            var rtt = transport.GetCurrentRtt(NetworkManager.ServerClientId);
            _pingText.text = $"Ping: {rtt}ms";
        }
    }
}