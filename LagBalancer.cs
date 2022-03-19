using BepInEx;
using HarmonyLib;
using UnboundLib;
using UnityEngine;
using UnboundLib.Utils.UI;
using UnboundLib.GameModes;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using UnboundLib.Networking;
using Photon.Pun;
using LagBalancer.Extensions;

namespace LagBalancer
{
    [BepInDependency("com.willis.rounds.unbound", BepInDependency.DependencyFlags.HardDependency)] // necessary for most modding stuff here
    [BepInPlugin(ModId, ModName, Version)]
    [BepInProcess("Rounds.exe")]
    public class LagBalancer : BaseUnityPlugin
    {
        private const string ModId = "pykess.rounds.plugins.lagcompensator";
        private const string ModName = "Lag Compensator";
        public const string Version = "0.0.0";
        private static string CompatibilityModName => ModName.Replace(" ", "");

        private const float UpdateEvery = 1f;
        private const int MaxLagSimPerUpdate = 20; // at maximum, add 20ms of lag per update, to prevent runaway lag
        private const int MaxLag = 200; // maximum of 200ms of extra lag

        public static LagBalancer instance;

        private Harmony harmony;
        private float updateTimer = 0f;

        internal static string GetCustomPropertyKey(string prop)
        {
            return $"{CompatibilityModName}_{prop}";
        }
        private static string PingKey => GetCustomPropertyKey("Ping");
        private static string PingVarKey => GetCustomPropertyKey("PingVar");

#if DEBUG
        public static readonly bool DEBUG = true;
#else
        public static readonly bool DEBUG = false;
#endif
        internal static void Log(object str)
        {
            if (DEBUG)
            {
                UnityEngine.Debug.Log($"[{ModName}] {str}");
            }
        }
        internal static void LogWarning(object str)
        {
            UnityEngine.Debug.LogError($"[{ModName}] {str}");
        }
        internal static void LogError(object str)
        {
            UnityEngine.Debug.LogError($"[{ModName}] {str}");
        }


        void Awake()
        {
            instance = this;
            
            harmony = new Harmony(ModId);
            harmony.PatchAll();
        }
        void Start()
        {
            // add credits
            Unbound.RegisterCredits(ModName, new string[] { "Pykess" }, new string[] { "github", "Support Pykess" }, new string[] { "REPLACE WITH LINK", "https://ko-fi.com/pykess"});

            // add GUI to modoptions menu
            //Unbound.RegisterMenu(ModName, () => { }, GUI, null, false);

        }
        /// <summary>
        /// Check if two values with associated uncertainties are consistent
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="a_v"></param>
        /// <param name="b_v"></param>
        /// <returns></returns>
        bool Consistent(float a, float b, float a_v, float b_v)
        {
            float diff = Mathf.Abs(a - b);
            float u = Mathf.Abs(a_v) + Mathf.Abs(b_v);

            return diff <= u;
        }
        void Update()
        {
            this.updateTimer -= Time.deltaTime;
            if (this.updateTimer < 0f)
            {
                this.updateTimer = UpdateEvery;
                if (PhotonNetwork.OfflineMode || PhotonNetwork.CurrentRoom is null || PhotonNetwork.CurrentRoom.PlayerCount < 2 || PhotonNetwork.NetworkingClient?.LoadBalancingPeer is null) { return; }
                PhotonNetwork.LocalPlayer.SetProperty(PingKey, PhotonNetwork.GetPing());
                PhotonNetwork.LocalPlayer.SetProperty(PingVarKey, PhotonNetwork.NetworkingClient.LoadBalancingPeer.RoundTripTimeVariance/2);

                // find the playerID with the highest ping - if it's this player then no need to add any simulated lag
                int highPingID = PhotonNetwork.CurrentRoom.Players.OrderByDescending(kv => kv.Value.GetProperty<int>(PingKey)).Select(kv => kv.Key).First();

                // always enable simulation with 0 loss and 0 jitter
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.IsSimulationEnabled = true;
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.NetworkSimulationSettings.IncomingLossPercentage = 0;
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.NetworkSimulationSettings.OutgoingLossPercentage = 0;
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.NetworkSimulationSettings.IncomingJitter = 0;
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.NetworkSimulationSettings.OutgoingJitter = 0;

                int highestPing = PhotonNetwork.CurrentRoom.Players[highPingID].GetProperty<int>(PingKey);

                int currentLag = PhotonNetwork.NetworkingClient.LoadBalancingPeer.NetworkSimulationSettings.IncomingLag;
                int lagToSet = 0;

                // match lag (roughly)
                if (PhotonNetwork.LocalPlayer.ActorNumber == highPingID)
                {
                    lagToSet = Mathf.Clamp(currentLag - MaxLagSimPerUpdate, 0, MaxLag);
                }
                else
                {
                    lagToSet = Mathf.Clamp(currentLag + Mathf.Clamp(highestPing / 2 - currentLag, 0, MaxLagSimPerUpdate), 0, MaxLag);

                    // if this player's lag is statistically consistent with the highest, then do not update
                    if (Consistent(PhotonNetwork.LocalPlayer.GetProperty<int>(PingKey), highestPing, PhotonNetwork.LocalPlayer.GetProperty<int>(PingVarKey), PhotonNetwork.CurrentRoom.Players[highPingID].GetProperty<int>(PingVarKey)))
                    {
                        return;
                    }
                }
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.NetworkSimulationSettings.IncomingLag = lagToSet;
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.NetworkSimulationSettings.OutgoingLag = lagToSet;
            }
        }

        private void OnDestroy()
        {
            harmony.UnpatchAll();
        }

        internal static string GetConfigKey(string key) => $"{LagBalancer.ModName}_{key}";

        private static void GUI(GameObject menu)
        {
            MenuHandler.CreateText(ModName, menu, out TextMeshProUGUI _, 60);
            MenuHandler.CreateText(" ", menu, out TextMeshProUGUI _, 30);
        }
    }
}