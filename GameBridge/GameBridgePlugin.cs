using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BepInEx;
using BepInEx.Bootstrap;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MoonlightPeaks.GameBridge
{
    [BepInPlugin("com.moonlightpeaks.gamebridge", "Moonlight Peaks Game Bridge", "1.1.0")]
    public sealed partial class GameBridgePlugin : BaseUnityPlugin
    {
        [DllImport("libc", SetLastError = true)] private static extern int chmod(string path, uint mode);
        [DllImport("libc")] private static extern uint getuid();
        private sealed class Request
        {
            public string Action = "";
            public TaskCompletionSource<string> Result = new TaskCompletionSource<string>();
        }
        private readonly ConcurrentQueue<Request> queue = new ConcurrentQueue<Request>();
        private Socket? listener;
        private string? socketPath;
        private volatile bool stopping;
        private int activeRequests;
        private int queuedRequests;

        private void Awake()
        {
            try
            {
                StartObserving();
                var dir = Path.Combine("/tmp", "moonlight-gamebridge-" + getuid());
                Directory.CreateDirectory(dir);
                if (chmod(dir, 448) != 0) throw new IOException("Cannot secure socket directory.");
                socketPath = Path.Combine(dir, "bridge.sock");
                if (File.Exists(socketPath))
                {
                    // Only replace a stale socket; never disconnect a live game.
                    try
                    {
                        using var probe = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                        probe.Connect(new UnixDomainSocketEndPoint(socketPath));
                        throw new IOException("Another game bridge is already listening.");
                    }
                    catch (SocketException) { File.Delete(socketPath); }
                }
                listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                listener.Bind(new UnixDomainSocketEndPoint(socketPath));
                if (chmod(socketPath, 384) != 0) throw new IOException("Cannot secure socket.");
                listener.Listen(8);
                _ = Task.Run(AcceptLoop);
                Logger.LogInfo("Game bridge ready at " + socketPath);
            }
            catch (Exception ex)
            {
                Logger.LogError("Game bridge startup failed: " + ex.Message);
                listener?.Dispose();
                listener = null;
            }
        }

        private void AcceptLoop()
        {
            while (!stopping && listener != null)
            {
                try
                {
                    var client = listener.Accept();
                    if (Interlocked.Increment(ref activeRequests) > 8)
                    {
                        Interlocked.Decrement(ref activeRequests);
                        client.Dispose();
                        continue;
                    }
                    _ = Task.Run(() => Handle(client));
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { if (stopping) break; }
            }
        }

        private void Handle(Socket client)
        {
            try
            {
                client.ReceiveTimeout = 5000;
                client.SendTimeout = 10000;
                using var stream = new NetworkStream(client, ownsSocket: true);
                using var reader = new StreamReader(stream, new UTF8Encoding(false), false, 1024, true);
                using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
                var chars = new StringBuilder();
                for (var n = 0; n < 32; n++)
                {
                    var c = reader.Read();
                    if (c < 0 || c == '\n') break;
                    chars.Append((char)c);
                }
                var action = chars.ToString();
                if (action != "status" && action != "state" && action != "screenshot")
                {
                    writer.WriteLine(JsonConvert.SerializeObject(new { ok = false, error = "Unknown request." }));
                    return;
                }
                if (Interlocked.Increment(ref queuedRequests) > 8)
                {
                    Interlocked.Decrement(ref queuedRequests);
                    writer.WriteLine(JsonConvert.SerializeObject(new { ok = false, error = "Game bridge is busy." }));
                    return;
                }
                var request = new Request { Action = action };
                queue.Enqueue(request);
                if (request.Result.Task.Wait(TimeSpan.FromSeconds(8))) writer.WriteLine(request.Result.Task.Result);
                else
                {
                    request.Result.TrySetCanceled();
                    writer.WriteLine(JsonConvert.SerializeObject(new { ok = false, error = "Game did not respond before timeout." }));
                }
            }
            catch (Exception) { /* Client disconnected or timed out. */ }
            finally { client.Dispose(); Interlocked.Decrement(ref activeRequests); }
        }

        private void Update()
        {
            RefreshInteractor();
            for (var n = 0; n < 8 && queue.TryDequeue(out var request); n++)
            {
                Interlocked.Decrement(ref queuedRequests);
                if (request.Result.Task.IsCanceled) continue;
                try
                {
                    if (request.Action == "screenshot") StartCoroutine(Capture(request));
                    else if (request.Action == "state") request.Result.TrySetResult(JsonConvert.SerializeObject(GameState()));
                    else request.Result.TrySetResult(JsonConvert.SerializeObject(Snapshot()));
                }
                catch (Exception ex) { request.Result.TrySetResult(JsonConvert.SerializeObject(new { ok = false, error = ex.Message })); }
            }
        }

        private object Snapshot()
        {
            var scenes = new List<string>();
            for (int i = 0; i < SceneManager.sceneCount; i++) scenes.Add(SceneManager.GetSceneAt(i).name);
            var plugins = Chainloader.PluginInfos.Values.Select(p => new {
                id = p.Metadata.GUID, name = p.Metadata.Name, version = p.Metadata.Version.ToString()
            }).OrderBy(p => p.name).ToArray();
            return new {
                ok = true, game = Application.productName, game_version = Application.version,
                active_scene = SceneManager.GetActiveScene().name, scenes,
                save_state_available = GamePersistence.Instance != null,
                gameplay_initialized = AppGameStateMachine.Instance != null && AppGameStateMachine.Instance.IsFullyInitialized,
                focused = Application.isFocused, width = Screen.width, height = Screen.height, plugins
            };
        }

        private IEnumerator Capture(Request request)
        {
            yield return new WaitForEndOfFrame();
            if (request.Result.Task.IsCanceled) yield break;
            Texture2D? texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null) throw new InvalidOperationException("Screenshot capture returned no image.");
                var bytes = ImageConversion.EncodeToPNG(texture);
                request.Result.TrySetResult(JsonConvert.SerializeObject(new { ok = true, png = Convert.ToBase64String(bytes) }));
            }
            catch (Exception ex) { request.Result.TrySetResult(JsonConvert.SerializeObject(new { ok = false, error = ex.Message })); }
            finally { if (texture != null) Destroy(texture); }
        }

        private void OnDestroy()
        {
            StopObserving();
            stopping = true;
            listener?.Dispose();
            if (socketPath != null && listener != null) { try { File.Delete(socketPath); } catch (IOException) { } }
        }
    }
}
