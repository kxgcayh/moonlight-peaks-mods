using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Chicken.UI;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace MoonlightPeaks.GameBridge
{
    public sealed partial class GameBridgePlugin
    {
        private sealed class Subscription
        {
            public object Signal = null!;
            public MethodInfo Remove = null!;
            public Delegate Callback = null!;
        }
        private readonly List<Subscription> subscriptions = new List<Subscription>();
        private Subscription? targetSubscription;
        private object? currentInteractor;
        private string? lastTargetKey;
        private Harmony? observationHarmony;
        private static GameBridgePlugin? observer;
        private const BindingFlags AllInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
        private static readonly string[] ObservedSignals = {
            "OnRoomSwitchTriggered", "OnRoomLoadStarted", "OnRoomPersistenceLoaded", "OnCharacterRoomChanged",
            "OnRoomRevealedOnMap", "OnPlayerDoorEnterStarted", "OnPlayerDoorEnterFinished", "OnPlayerDoorExitStarted", "OnPlayerDoorExitFinished",
            "OnItemPickedUp", "OnItemDropped", "OnItemReceived", "OnPlayerInventoryChanged", "OnEntityInventoryChanged",
            "OnInventoryItemSelected", "OnInventoryItemDeselected", "OnGrabbedItemChanged", "OnItemViewEquipped", "OnItemViewUnequipped",
            "OnPersistenceSave", "OnUnloadGame", "OnStartDirectorStateRequest", "OnCutsceneAboutToStart", "OnCutsceneStarted",
            "OnCutsceneAboutToEnd", "OnCutsceneEnded", "OnCutsceneEndedLate", "OnEntityStartTalking", "OnEntityStopTalking", "OnIsSpeechSeenChanged",
            "OnPlayerStateChanged", "OnPlayerEnergyChanged", "OnPlayerManaChanged", "OnPlayerTeleported", "OnTimeTick", "OnDayChanged",
            "OnWitchCircleActivated", "OnTreeShook", "OnPlantHarvest"
        };

        private void StartObserving()
        {
            observer = this;
            foreach (var name in ObservedSignals)
            {
                try
                {
                    var signal = typeof(EventBus).GetField(name, BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                    if (signal == null) { Logger.LogWarning("Observer signal unavailable: " + name); continue; }
                    var captured = name;
                    subscriptions.Add(Subscribe(signal, values => Emit(captured, values)));
                }
                catch (Exception ex) { Logger.LogWarning("Could not observe " + name + ": " + ex.Message); }
            }
            try
            {
                var signal = typeof(UIScreen).GetField("OnFocusChanged", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (signal != null) subscriptions.Add(Subscribe(signal, _ => Emit("OnUIFocusChanged", Array.Empty<object>())));
            }
            catch (Exception ex) { Logger.LogWarning("Could not observe UI focus: " + ex.Message); }
            try
            {
                observationHarmony = new Harmony("com.moonlightpeaks.gamebridge.observation");
                observationHarmony.PatchAll(typeof(SaveObservation));
                observationHarmony.PatchAll(typeof(SaveAttemptObservation));
            }
            catch (Exception ex) { Logger.LogWarning("Could not observe save writes: " + ex.Message); }
        }

        private static Subscription Subscribe(object signal, Action<object[]> callback)
        {
            var add = signal.GetType().GetMethods().First(m => m.Name == "AddListener" && m.GetParameters().Length == 1);
            var delegateType = add.GetParameters()[0].ParameterType;
            var invoke = delegateType.GetMethod("Invoke") ?? throw new InvalidOperationException("Invalid signal delegate");
            var parameters = invoke.GetParameters().Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();
            var arguments = Expression.NewArrayInit(typeof(object), parameters.Select(p => Expression.Convert(p, typeof(object))));
            var listener = Expression.Lambda(delegateType, Expression.Invoke(Expression.Constant(callback), arguments), parameters).Compile();
            var remove = signal.GetType().GetMethod("RemoveListener", new[] { delegateType }) ?? throw new InvalidOperationException("Missing unsubscribe");
            add.Invoke(signal, new object[] { listener });
            return new Subscription { Signal = signal, Callback = listener, Remove = remove };
        }

        private void RefreshInteractor()
        {
            try
            {
                var view = PlayerView.Exists ? PlayerView.Instance : null;
                var interactor = view != null ? view.Interactor : null;
                if (ReferenceEquals(interactor, currentInteractor)) return;
                if (targetSubscription != null) Unsubscribe(targetSubscription);
                targetSubscription = null;
                lastTargetKey = null;
                if (interactor != null)
                    targetSubscription = Subscribe(interactor.OnTargetChanged, _ =>
                    {
                        try
                        {
                            var target = interactor.TargetInteractable;
                            if (target == null) return;
                            var key = TargetKey(target);
                            if (string.Equals(key, lastTargetKey, StringComparison.Ordinal)) return;
                            lastTargetKey = key;
                            Emit("OnTargetChanged", new object[] { target });
                        }
                        catch (Exception ex) { Logger.LogDebug("Target observation failed: " + ex.Message); }
                    });
                currentInteractor = interactor;
            }
            catch (Exception ex) { Logger.LogDebug("Interactor observation unavailable: " + ex.Message); }
        }

        private static string TargetKey(object target)
        {
            if (target is UnityEngine.Object unityObject && unityObject != null)
                return target.GetType().FullName + "|" + unityObject.name;
            return target.GetType().FullName + "|" + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(target);
        }

        private void Emit(string name, object[] values)
        {
            try
            {
                var entry = new { timestamp = DateTime.UtcNow.ToString("O"), name, arguments = values.Select(Describe).ToArray() };
                Logger.LogInfo("GAME_EVENT " + JsonConvert.SerializeObject(entry));
            }
            catch (Exception ex) { Logger.LogDebug("Event observation failed: " + ex.Message); }
        }

        private static object? Get(object? obj, string name)
        {
            if (obj == null) return null;
            try
            {
                var type = obj.GetType();
                return type.GetProperty(name, AllInstance)?.GetValue(obj) ?? type.GetField(name, AllInstance)?.GetValue(obj);
            }
            catch { return null; }
        }

        private static object? Describe(object? value)
        {
            if (value == null) return null;
            if (value is string || value is bool || value is int || value is long || value is float || value is double) return value;
            if (value.GetType().IsEnum || value is SerializedGuid) return value.ToString();
            var asset = Get(value, "ItemAsset");
            var amount = Get(value, "Amount");
            var label = Get(value, "AssetName") as string;
            if (string.IsNullOrEmpty(label) && value is UnityEngine.Object unityObject && unityObject != null) label = unityObject.name;
            return new { type = value.GetType().Name, name = label, item = asset == null || ReferenceEquals(asset, value) ? null : Describe(asset), amount };
        }

        private static object InventorySummary(Inventory? inventory)
        {
            if (inventory == null) return new { available = false };
            var items = new List<object>();
            var truncated = false;
            foreach (var slot in inventory.Slots ?? new List<InventorySlotPersistence>())
            {
                if (slot == null || slot.IsEmpty) continue;
                if (items.Count == 256) { truncated = true; break; }
                items.Add(new { item = Describe(slot.ItemAsset), amount = slot.Amount, quality = slot.ItemQualityLevel.ToString(), custom_name = slot.CustomName });
            }
            return new { available = true, items, truncated };
        }

        private object GameState()
        {
            if (GamePersistence.Instance == null) return new { ok = false, error = "No save is loaded." };
            var room = GamePersistence.CurrentRoomAsset;
            var inventory = GameInventory.Exists ? GameInventory.Instance : null;
            var player = PlayerView.Exists ? PlayerView.Instance : null;
            var director = UnityEngine.Object.FindFirstObjectByType<GameDirectorStateMachine>();
            var node = director != null ? director.CurrentNode : null;
            var screens = UIScreen.ShowStack?.Where(s => s != null && s.IsShowing).Select(s => s.GetType().Name).ToArray() ?? Array.Empty<string>();
            var speech = screens.Contains("SpeechScreen") ? SpeechScreen.Instance : null;
            var entry = Get(speech, "speechEntry");
            var blockers = typeof(GamePersistence).GetField("saveBlockers", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as IEnumerable;
            var blockerNames = blockers == null ? Array.Empty<string>() : blockers.Cast<object>().Select(x => x?.ToString() ?? "").ToArray();
            return new {
                ok = true, room = Describe(room), screens,
                target = Describe(player != null && player.Interactor != null ? player.Interactor.TargetInteractable : null),
                inventory = new { backpack = InventorySummary(inventory?.Inventory), tools = InventorySummary(inventory?.Tools), wallet = InventorySummary(inventory?.Wallet), quest_items = InventorySummary(inventory?.QuestItems), global = InventorySummary(inventory?.GlobalInventory) },
                save_blockers = blockerNames,
                director = new { active = GameDirectorStateMachine.IsActive, node = Describe(node), graph = Describe(node?.DirectorAsset) },
                dialogue = new { visible = speech != null, speaker = Describe(Get(entry, "SpeakerEntity")), text = Get(entry, "Text"), writing = speech != null && speech.IsWriting, messenger_visible = screens.Contains("MessengerSpeechScreen") }
            };
        }

        private static void Unsubscribe(Subscription subscription)
        {
            try { subscription.Remove.Invoke(subscription.Signal, new object[] { subscription.Callback }); } catch { }
        }

        private void StopObserving()
        {
            if (targetSubscription != null) Unsubscribe(targetSubscription);
            foreach (var subscription in subscriptions) Unsubscribe(subscription);
            subscriptions.Clear();
            observationHarmony?.UnpatchSelf();
            observer = null;
        }

        [HarmonyPatch(typeof(FileSystemPersistenceService), "SaveGamePersistenceAsync")]
        private static class SaveObservation
        {
            [HarmonyPostfix]
            private static void Postfix() => observer?.Emit("SaveWriteReturned", Array.Empty<object>());
            [HarmonyFinalizer]
            private static Exception? Finalizer(Exception? __exception)
            {
                if (__exception != null) observer?.Emit("SaveWriteException", new object[] { __exception.GetType().Name });
                return __exception;
            }
        }

        [HarmonyPatch(typeof(GamePersistence), "SaveAsync")]
        private static class SaveAttemptObservation
        {
            [HarmonyPrefix]
            private static void Prefix(bool force)
            {
                try
                {
                    var blockers = typeof(GamePersistence).GetField("saveBlockers", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as IEnumerable;
                    var reasons = blockers == null ? "" : string.Join(", ", blockers.Cast<object>().Select(x => x?.ToString()));
                    if (!force && reasons.Length > 0) observer?.Emit("SaveAttemptWithBlockers", new object[] { reasons });
                }
                catch (Exception ex) { observer?.Logger.LogDebug("Save observation failed: " + ex.Message); }
            }
        }
    }
}
