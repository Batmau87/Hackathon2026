using Fusion;
using Fusion.Menu;
using Fusion.Photon.Realtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

#pragma warning disable 1998
#pragma warning disable 4014

namespace SimpleFPS
{
    public class MenuConnectionBehaviour : FusionMenuConnectionBehaviour
    {
        public NetworkRunner RunnerPrefab;
        public MenuUIController UIController;

        private NetworkRunner _runner;

        // --- ACTUALIZADO: Implementación de las propiedades obligatorias ---
        public override bool IsConnected => _runner != null && _runner.IsConnectedToServer;
        public override string SessionName => _runner != null && _runner.IsRunning ? _runner.SessionInfo.Name : string.Empty;
        public override int MaxPlayerCount => _runner != null && _runner.IsRunning ? _runner.SessionInfo.MaxPlayers : 0;
        public override string Region => _runner != null && _runner.IsRunning ? _runner.SessionInfo.Region : string.Empty;
        public override int Ping => _runner != null && _runner.IsRunning ? Mathf.RoundToInt((float)(_runner.GetPlayerRtt(PlayerRef.None) * 1000.0)) : 0;
        public override string AppVersion => PhotonAppSettings.Global.AppSettings.AppVersion;
        public override List<string> Usernames => new List<string>(); // Lista vacía por defecto

        // --- ACTUALIZADO: Implementación de los métodos obligatorios ---

        public override async Task<List<FusionMenuOnlineRegion>> RequestAvailableOnlineRegionsAsync(FusionMenuConnectArgs connectArgs)
        {
            List<FusionMenuOnlineRegion> regions = new List<FusionMenuOnlineRegion>();
            if (UIController != null && UIController.Config != null)
            {
                foreach (var region in UIController.Config.AvailableRegions)
                {
                    regions.Add(new FusionMenuOnlineRegion { Code = region, Ping = 0 });
                }
            }
            return regions;
        }

        protected override async Task<ConnectResult> ConnectAsyncInternal(FusionMenuConnectArgs connectionArgs)
        {
            if (string.IsNullOrEmpty(PhotonAppSettings.Global.AppSettings.AppIdFusion))
            {
                await UIController.PopupAsync("The Fusion AppId is missing in PhotonAppSettings. Please follow setup instructions before running the game.", "Game not configured");
                UIController.Show<FusionMenuUIMain>();
                return new ConnectResult { FailReason = ConnectFailReason.UserRequest };
            }

            _runner = CreateRunner();

            var appSettings = PhotonAppSettings.Global.AppSettings.GetCopy();
            appSettings.FixedRegion = connectionArgs.Region;

            var startGameArgs = new StartGameArgs()
            {
                SessionName = connectionArgs.Session,
                PlayerCount = connectionArgs.MaxPlayerCount,
                GameMode = GetGameMode(connectionArgs),
                CustomPhotonAppSettings = appSettings
            };

            if (connectionArgs.Creating == false && string.IsNullOrEmpty(connectionArgs.Session))
            {
                startGameArgs.EnableClientSessionCreation = false;

                var randomJoinResult = await StartRunner(startGameArgs);
                if (randomJoinResult.Success)
                    return await StartGame(connectionArgs.Scene.SceneName);

                if (randomJoinResult.FailReason == ConnectFailReason.UserRequest)
                    return new ConnectResult { FailReason = randomJoinResult.FailReason };

                connectionArgs.Creating = true;
                _runner = CreateRunner();

                startGameArgs.EnableClientSessionCreation = true;
                startGameArgs.SessionName = UIController.Config.CodeGenerator.Create();
                startGameArgs.GameMode = GetGameMode(connectionArgs);
            }

            var result = await StartRunner(startGameArgs);
            if (result.Success)
                return await StartGame(connectionArgs.Scene.SceneName);

            await DisconnectAsyncInternal(result.FailReason);
            return new ConnectResult { FailReason = result.FailReason };
        }

        protected override async Task DisconnectAsyncInternal(int reason)
        {
            var runner = _runner;
            _runner = null;

            if (runner != null)
            {
                Scene sceneToUnload = default;

                if (runner.IsSceneAuthority && runner.TryGetSceneInfo(out NetworkSceneInfo sceneInfo))
                {
                    foreach (var sceneRef in sceneInfo.Scenes)
                    {
                        await runner.UnloadScene(sceneRef);
                    }
                }
                else
                {
                    if (runner.SceneManager != null)
                        sceneToUnload = runner.SceneManager.MainRunnerScene;
                }

                await runner.Shutdown();

                if (sceneToUnload.IsValid() && sceneToUnload.isLoaded && sceneToUnload != gameObject.scene)
                {
                    SceneManager.SetActiveScene(gameObject.scene);
                    SceneManager.UnloadSceneAsync(sceneToUnload);
                }
            }

            if (reason != ConnectFailReason.UserRequest)
            {
                await UIController.PopupAsync(reason.ToString(), "Disconnected");
            }

            UIController.OnGameStopped();
        }

        private GameMode GetGameMode(FusionMenuConnectArgs connectionArgs)
        {
            if (UIController.SelectedGameMode == GameMode.AutoHostOrClient)
                return connectionArgs.Creating ? GameMode.Host : GameMode.Client;

            return UIController.SelectedGameMode;
        }

        private NetworkRunner CreateRunner()
        {
            var runner = Instantiate(RunnerPrefab);
            runner.ProvideInput = true;
            return runner;
        }

        private async Task<ConnectResult> StartRunner(StartGameArgs args)
        {
            var result = await _runner.StartGame(args);
            return new ConnectResult() { Success = _runner.IsRunning, FailReason = ConnectFailReason.Disconnect };
        }

        private async Task<ConnectResult> StartGame(string sceneName)
        {
            try
            {
                _runner.AddCallbacks(new MenuConnectionCallbacks(UIController, sceneName));
                if (_runner.IsSceneAuthority)
                {
                    await _runner.LoadScene(sceneName, LoadSceneMode.Additive, LocalPhysicsMode.None, true);
                }
                UIController.OnGameStarted();
                return new ConnectResult() { Success = true };
            }
            catch (ArgumentException e)
            {
                UnityEngine.Debug.LogError($"Failed to load scene. {e}.");
                await DisconnectAsyncInternal(ConnectFailReason.Disconnect);
                return new ConnectResult() { FailReason = ConnectFailReason.Disconnect };
            }
        }

        // --- Callbacks internos ---
        private class MenuConnectionCallbacks : INetworkRunnerCallbacks
        {
            public readonly MenuUIController Controller;
            public readonly string SceneName;

            public MenuConnectionCallbacks(MenuUIController controller, string sceneName)
            {
                Controller = controller;
                SceneName = sceneName;
            }

            public async void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
            {
                if (shutdownReason == ShutdownReason.DisconnectedByPluginLogic)
                {
                    Controller.OnGameStopped();
                    Controller.Show<FusionMenuUIMain>();
                    Controller.PopupAsync("Disconnected from the server.", "Disconnected");

                    if (runner.SceneManager != null && runner.SceneManager.MainRunnerScene.IsValid())
                    {
                        SceneRef sceneRef = runner.SceneManager.GetSceneRef(runner.SceneManager.MainRunnerScene.name);
                        runner.SceneManager.UnloadScene(sceneRef);
                    }
                }
            }

            // Callbacks vacíos obligatorios
            public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
            public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
            public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }
            public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
            public void OnInput(NetworkRunner runner, NetworkInput input) { }
            public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
            public void OnConnectedToServer(NetworkRunner runner) { }
            public void OnDisconnectedFromServer(NetworkRunner runner, Fusion.Sockets.NetDisconnectReason reason) { }
            public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
            public void OnConnectFailed(NetworkRunner runner, Fusion.Sockets.NetAddress remoteAddress, Fusion.Sockets.NetConnectFailedReason reason) { }
            public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
            public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
            public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
            public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
            public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, Fusion.Sockets.ReliableKey key, ArraySegment<byte> data) { }
            public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, Fusion.Sockets.ReliableKey key, float progress) { }
            public void OnSceneLoadStart(NetworkRunner runner) { }
            public void OnSceneLoadDone(NetworkRunner runner) { }
        }
    }
}