using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace PingDisplay;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin {
    static TMP_Text _pingText;
    static float _lastUpdateTime;
    
    static ConfigEntry<bool> _showPingConfig;
    static ConfigEntry<DisplayPosition> _displayPositionConfig;

    const float UpdateInterval = 0.5f;
    const float Margin = 0.03f;
    
    void Awake() {
        SetupConfig(_showPingConfig = Config.Bind(
            "General",
            "ShowPing",
            true,
            "Show your latency to the host in the top left corner of the screen"
        ), value => _pingText.enabled = value);
        
        SetupConfig(_displayPositionConfig = Config.Bind(
            "General",
            "DisplayPosition",
            DisplayPosition.TopLeft,
            "Where on the HUD to display your latency"
        ), PositionDisplay);
        
        Harmony.CreateAndPatchAll(typeof(Plugin));
        
        Logger.LogInfo("Plugin loaded!");
    }
    
    enum DisplayPosition {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight
    }
    
    static void PositionDisplay(DisplayPosition position) {
        if (_pingText == null) return;
        
        var rect = _pingText.rectTransform;
        var pivot = position switch {
            DisplayPosition.TopLeft => new Vector2(0f, 1f),
            DisplayPosition.TopRight => new Vector2(1f, 1f),
            DisplayPosition.BottomLeft => new Vector2(0f, 0f),
            DisplayPosition.BottomRight => new Vector2(1f, 0f),
            _ => throw new ArgumentOutOfRangeException()
        };
        
        rect.pivot = pivot;
        AddMargin(ref pivot.x);
        AddMargin(ref pivot.y);
        
        rect.anchorMin = rect.anchorMax = pivot;
        rect.anchoredPosition = Vector2.zero;
        
        _pingText.alignment = position switch {
            DisplayPosition.TopLeft => TextAlignmentOptions.TopLeft,
            DisplayPosition.TopRight => TextAlignmentOptions.TopRight,
            DisplayPosition.BottomLeft => TextAlignmentOptions.BottomLeft,
            DisplayPosition.BottomRight => TextAlignmentOptions.BottomRight,
            _ => throw new ArgumentOutOfRangeException()
        };
        
        return;

        void AddMargin(ref float value) {
            if (value == 0) value += Margin;
            else value -= Margin;
        }
    }

    static void SetupConfig<T>(ConfigEntry<T> config, Action<T> changedHandler) {
        config.SettingChanged += (_, _) => changedHandler(config.Value);
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(HUDManager), "Awake")]
    static void HUDManager_Start_Postfix(HUDManager __instance) {
        var textGameObject = new GameObject("PingDisplay", typeof(RectTransform));
        textGameObject.transform.SetParent(__instance.HUDContainer.transform, false);
        
        _pingText = textGameObject.AddComponent<TextMeshProUGUI>();
        _pingText.faceColor = new Color32(255, 255, 255, 40);
        _pingText.font = __instance.weightCounter.font;
        _pingText.fontSize = 12;
        
        PositionDisplay(_displayPositionConfig.Value);
        _pingText.enabled = _showPingConfig.Value;
        
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
        if (_pingText == null || !_pingText.enabled) return; 
        
        if (Time.time - _lastUpdateTime < UpdateInterval) return;
        _lastUpdateTime = Time.time;

        if (NetworkManager.Singleton.IsHost) {
            _pingText.text = "Ping: ---";
        } else {
            var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
            var rtt = transport.GetCurrentRtt(NetworkManager.ServerClientId);
            _pingText.text = $"Ping: {rtt}ms";
        }
    }
}