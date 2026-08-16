using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HideBetaRework
{
    [BepInPlugin("redlaser42.HideBetaRework", "HideBetaRework", "4.1.2")]
    public class Plugin : BaseUnityPlugin
    {
        private static readonly Harmony Harmony = new Harmony("redlaser42.HideBetaRework");

        private void Awake()
        {
            Logger.LogInfo("HideBetaRework 4.1.2 loaded.");
            PatchMenuScreen();
            PatchPreloaderUI();
        }

        private void Update()
        {
            HideLowerLeftVersionText();
        }

        private void PatchMenuScreen()
        {
            var menuScreenType = Type.GetType("EFT.UI.MenuScreen, Assembly-CSharp");
            if (menuScreenType == null)
            {
                Logger.LogWarning("MenuScreen type not found.");
                return;
            }

            var method = menuScreenType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .FirstOrDefault(m => m.Name == "method_3" || m.Name == "method_4" || m.Name.Contains("Show") || m.Name.Contains("CG_Show"));

            if (method == null)
            {
                Logger.LogWarning("MenuScreen patch target not found.");
                return;
            }

            Harmony.Patch(method, postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(MenuPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
        }

        private void PatchPreloaderUI()
        {
            var preloaderType = Type.GetType("EFT.UI.PreloaderUI, Assembly-CSharp");
            if (preloaderType == null)
            {
                Logger.LogWarning("PreloaderUI type not found.");
                return;
            }

            var method = preloaderType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .FirstOrDefault(m => m.Name == "method_3" || m.Name == "method_4" || m.Name.Contains("Show") || m.Name.Contains("CG_Show"));

            if (method == null)
            {
                Logger.LogWarning("PreloaderUI patch target not found.");
                return;
            }

            Harmony.Patch(method, postfix: new HarmonyMethod(typeof(Plugin).GetMethod(nameof(PreloaderPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
        }

        private static void MenuPostfix(object __instance)
        {
            if (__instance == null)
            {
                return;
            }

            HideField(__instance, "_alphaWarningGameObject");
            HideField(__instance, "_warningGameObject");
            HideField(__instance, "_toggleGameModeButton");
            HideVersionRelatedObjects(__instance);
        }

        private static void PreloaderPostfix(object __instance)
        {
            if (__instance == null)
            {
                return;
            }

            HideField(__instance, "_alphaVersionLabel");
            HideVersionRelatedObjects(__instance);
        }

        private static void HideVersionRelatedObjects(object instance)
        {
            if (instance == null)
            {
                return;
            }

            var root = (instance as Component)?.gameObject ?? GetGameObjectFromInstance(instance);
            if (root == null)
            {
                return;
            }

            foreach (var field in instance.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.Name.IndexOf("Version", StringComparison.OrdinalIgnoreCase) >= 0
                    || field.Name.IndexOf("Build", StringComparison.OrdinalIgnoreCase) >= 0
                    || field.Name.IndexOf("GameVersion", StringComparison.OrdinalIgnoreCase) >= 0
                    || field.Name.IndexOf("ClientVersion", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    HideField(instance, field.Name);
                }
            }
        }

        private static void HideLowerLeftVersionText()
        {
            foreach (var rect in UnityEngine.Object.FindObjectsOfType<RectTransform>())
            {
                if (rect == null || rect.gameObject == null)
                {
                    continue;
                }

                if (!IsLowerLeftVersionAnchor(rect))
                {
                    continue;
                }

                var text = GetTextFromGameObject(rect.gameObject);
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (!LooksLikeVersionText(text) && rect.name.IndexOf("version", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                rect.gameObject.SetActive(false);
            }
        }

        private static bool IsLowerLeftVersionAnchor(RectTransform rect)
        {
            if (rect == null)
            {
                return false;
            }

            var anchorMin = rect.anchorMin;
            var anchorMax = rect.anchorMax;
            var anchoredLeft = anchorMin.x <= 0.22f && anchorMax.x <= 0.35f;
            var anchoredBottom = anchorMin.y <= 0.18f && anchorMax.y <= 0.28f;
            return anchoredLeft && anchoredBottom;
        }

        private static bool LooksLikeVersionText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            return text.IndexOf("SPT", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("LOCAL", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("PVE", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("PvE", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("version", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("build", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("game version", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetTextFromGameObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            foreach (var component in gameObject.GetComponents<Component>())
            {
                if (component == null)
                {
                    continue;
                }

                var property = component.GetType().GetProperty("text", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var text = property?.GetValue(component) as string;
                if (!string.IsNullOrEmpty(text))
                {
                    return text;
                }
            }

            return null;
        }

        private static GameObject GetGameObjectFromInstance(object instance)
        {
            var property = instance.GetType().GetProperty("gameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return property?.GetValue(instance) as GameObject;
        }

        private static void HideField(object instance, string fieldName)
        {
            var type = instance.GetType();
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                return;
            }

            var value = field.GetValue(instance);
            if (value == null)
            {
                return;
            }

            var go = value as GameObject;
            if (go != null)
            {
                go.SetActive(false);
                return;
            }

            var gameObjectProperty = value.GetType().GetProperty("gameObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var gameObject = gameObjectProperty?.GetValue(value) as GameObject;
            if (gameObject != null)
            {
                gameObject.SetActive(false);
            }
        }
    }
}
