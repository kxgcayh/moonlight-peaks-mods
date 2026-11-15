using System;
using System.Linq;
using Chicken.UI;
using Director.Core;
using Director.Nodes;
using HarmonyLib;
using UnityEngine;

namespace MoonlightPeaks.SkipIntro.Patches
{
    public static class IntroSpeechPatch
    {
        // IDs from the shipped Content_Intro_Director graph, not scene-name guesses.
        private static readonly SerializedGuid DepartureNodeId = SerializedGuid.FromULongs(2768253579706788815UL, 18168476376552781307UL);
        private static readonly SerializedGuid SkipBranchId = SerializedGuid.FromULongs(10734512228647114790UL, 10634028791141932821UL);
        private static bool _showPreTrainPrompt;
        private static GameObject? _nativePrompt;
        private static GameDirectorStateMachine? _pendingDirector;
        private static ExecutionContext? _pendingContext;
        private static UnityEngine.UI.Image? _holdProgressImage;
        private static float _heldSeconds;
        private static float _visualProgress;
        private static bool _requireKeyRelease;

        public static void BeginPreTrainPrompt()
        {
            Reset();
            _showPreTrainPrompt = SkipIntroPlugin.EnableHoldFToSkip.Value;
        }

        private static GameDirectorStateMachine? FindDepartureDirector() =>
            UnityEngine.Object.FindObjectsByType<GameDirectorStateMachine>(FindObjectsSortMode.None)
                .FirstOrDefault(state => state.CurrentNode is WaitForInputNode
                    && state.CurrentNode.SerializedGuid == DepartureNodeId
                    && state.CurrentNode.DirectorAsset.name == "Content_Intro_Director");

        public static void UpdatePreTrainPrompt()
        {
            if (!_showPreTrainPrompt || !SkipIntroPlugin.EnableHoldFToSkip.Value)
            {
                DestroyPrompt();
                return;
            }
            var director = FindDepartureDirector();
            if (director == null)
            {
                DestroyPrompt();
                return;
            }
            EnsureNativePrompt();
            if (_nativePrompt == null) return;
            var held = Application.isFocused && Rewired.ReInput.isReady
                && Rewired.ReInput.controllers.Keyboard.GetKey(KeyCode.F);
            if (!Application.isFocused) _requireKeyRelease = true;
            if (!held || _requireKeyRelease)
            {
                if (!held && Application.isFocused) _requireKeyRelease = false;
                _heldSeconds = 0f;
                SetHoldVisual(Mathf.MoveTowards(_visualProgress, 0f, Time.unscaledDeltaTime / 0.2f));
                return;
            }

            _heldSeconds += Time.unscaledDeltaTime;
            var duration = SkipIntroPlugin.HoldDurationSeconds.Value;
            if (float.IsNaN(duration) || float.IsInfinity(duration)) duration = 0.6f;
            var progress = Mathf.Clamp01(_heldSeconds / Mathf.Max(0.1f, duration));
            SetHoldVisual(progress);
            if (progress < 1f) return;

            var branch = director.CurrentNode.DirectorAsset.Nodes
                .SingleOrDefault(node => node.SerializedGuid == SkipBranchId);
            if (!(branch is SequenceNode))
            {
                SkipIntroPlugin.Logger.LogError("Native intro skip branch missing; departure left unchanged.");
                return;
            }

            // Let the input state close normally, then replace its next graph transition.
            _pendingDirector = director;
            _pendingContext = new ExecutionContext(branch, director.CurrentContext);
            try
            {
                Traverse.Create(InputScreen.Instance).Method("HandleButtonClicked").GetValue();
                if (_pendingContext != null)
                    SkipIntroPlugin.Logger.LogError("Departure did not advance; native intro skip was not applied.");
            }
            catch (Exception ex)
            {
                SkipIntroPlugin.Logger.LogError($"Unable to enter native intro skip branch: {ex}");
            }
            finally
            {
                _pendingDirector = null;
                _pendingContext = null;
                _showPreTrainPrompt = false;
                DestroyPrompt();
            }
        }

        private static void EnsureNativePrompt()
        {
            if (_nativePrompt != null) return;
            var screen = InputScreen.Instance;
            if (screen == null || !screen.IsShowing) return;
            var screenFields = Traverse.Create(screen);
            var original = screenFields.Field<AnimatedButton>("holdButton").Value;
            const string labelField = "holdButtonTextField";
            if (original == null) return;

            _nativePrompt = UnityEngine.Object.Instantiate(original.gameObject, original.transform.parent);
            _nativePrompt.name = "SkipIntroNativePrompt";
            var rect = (RectTransform)_nativePrompt.transform;
            var layout = _nativePrompt.GetComponent<UnityEngine.UI.LayoutElement>()
                ?? _nativePrompt.AddComponent<UnityEngine.UI.LayoutElement>();
            layout.ignoreLayout = true;
            rect.anchoredPosition += new Vector2(0f, 85f);
            foreach (var button in _nativePrompt.GetComponentsInChildren<AnimatedButton>(true))
                button.enabled = false;
            var sourceLabel = Traverse.Create(screen).Field<LocalizedTextField>(labelField).Value;
            var labelPath = GetChildPath(original.transform, sourceLabel.transform);
            var label = _nativePrompt.transform.Find(labelPath).GetComponent<LocalizedTextField>();
            label.Text = "Hold to Skip Intro";
            var holdButton = _nativePrompt.GetComponent<HoldButton>();
            _holdProgressImage = Traverse.Create(holdButton)
                .Field<UnityEngine.UI.Image>("holdProgressImage").Value;
            _requireKeyRelease = true;
            _heldSeconds = 0f;
            SetHoldVisual(0f);
            foreach (var icon in _nativePrompt.GetComponentsInChildren<InputIconWidget>(true))
            {
                icon.enabled = false;
                var keyText = Traverse.Create(icon).Field<LocalizedTextField>("text").Value;
                keyText.gameObject.SetActive(true);
                keyText.Text = "F";
                var image = Traverse.Create(icon).Field<Component>("iconImage").Value;
                if (image != null) image.gameObject.SetActive(false);
            }
            _nativePrompt.SetActive(true);
            foreach (var widget in _nativePrompt.GetComponentsInChildren<AnimatedWidget>(true))
                widget.Show(true);
            foreach (var group in _nativePrompt.GetComponentsInChildren<CanvasGroup>(true))
            {
                group.alpha = 1f;
                group.interactable = false;
                group.blocksRaycasts = false;
            }
            SkipIntroPlugin.Logger.LogInfo($"Created native F prompt from {original.name} ({labelField}).");
        }

        private static string GetChildPath(Transform root, Transform child)
        {
            var path = child.name;
            while (child.parent != root && child.parent != null)
            {
                child = child.parent;
                path = child.name + "/" + path;
            }
            return path;
        }

        private static void SetHoldVisual(float progress)
        {
            _visualProgress = progress;
            if (_holdProgressImage == null) return;
            var rect = _holdProgressImage.rectTransform;
            rect.anchorMax = new Vector2(Mathf.Lerp(0.1f, 1f, progress), rect.anchorMax.y);
            var color = _holdProgressImage.color;
            color.a = Mathf.Pow(Mathf.Clamp01(progress / 0.1f), 3f);
            _holdProgressImage.color = color;
        }

        private static void DestroyPrompt()
        {
            if (_nativePrompt != null) UnityEngine.Object.Destroy(_nativePrompt);
            _nativePrompt = null;
            _holdProgressImage = null;
            _heldSeconds = 0f;
            _visualProgress = 0f;
            _requireKeyRelease = true;
        }

        public static void Reset()
        {
            DestroyPrompt();
            _showPreTrainPrompt = false;
            _pendingDirector = null;
            _pendingContext = null;
        }

        [HarmonyPatch(typeof(GameDirectorStateMachine), "GoToNextNode")]
        private static class NativeSkipTransitionPatch
        {
            [HarmonyPrefix]
            private static void Prefix(GameDirectorStateMachine __instance, ref bool __0)
            {
                if (__instance != _pendingDirector || _pendingContext == null) return;
                var context = _pendingContext;
                _pendingContext = null;
                _pendingDirector = null;
                Traverse.Create(__instance).Property("CurrentContext").SetValue(context);
                __0 = false;
                SkipIntroPlugin.Logger.LogInfo("F hold confirmed: entering built-in intro skip branch; train cinematic bypassed.");
            }
        }

        [HarmonyPatch(typeof(MenuCharacterCreatorStateMachine), "FinishCustomizing")]
        private static class CharacterCreatorFinishedPatch
        {
            [HarmonyPostfix]
            private static void Postfix() => BeginPreTrainPrompt();
        }
    }
}
