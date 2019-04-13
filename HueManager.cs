using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using IPA.Config;
using IPA.Logging;
using Logger = IPA.Logging.Logger;
using Q42.HueApi;
using Q42.HueApi.ColorConverters;
using Q42.HueApi.ColorConverters.HSB;
using Q42.HueApi.Streaming;
using Q42.HueApi.Streaming.Extensions;
using Q42.HueApi.Streaming.Models;
using UnityEngine;
using Q42.HueApi.Models.Bridge;

namespace HueSaber
{
    class HueManager
    {
        private readonly Logger log;
        private readonly IModPrefs prefs;

        internal static object currentColor = (object) Color.white;
        internal static object missTime = (object) DateTimeOffset.MinValue;
        internal static object cutTime = (object) DateTimeOffset.MinValue;

        public HueManager(Logger log, IModPrefs prefs)
        {
            this.log = log;
            this.prefs = prefs;
        }

        public bool Ready => prefs.HasKey("HueSaber", "appKey") && prefs.HasKey("HueSaber", "clientKey");

        private async Task<string> FindBridge()
        {
            log.Info("Searching for bridge...");
            var overrideIP = prefs.GetString("HueSaber", "overrideIP");
            if (overrideIP != "")
            {
                log.Info("Using IP address override.");
                return overrideIP;
            } else
            {
                return (await new HttpBridgeLocator().LocateBridgesAsync(TimeSpan.FromSeconds(5))).FirstOrDefault()?.IpAddress;
            }
        }

        public async Task Sync(CancellationToken token)
        {
            string bridge;
            try
            {
                token.ThrowIfCancellationRequested();
                bridge = await FindBridge();
                if (bridge == null)
                {
                    log.Error("No bridge found!");
                    return;
                }
                token.ThrowIfCancellationRequested();
                log.Info("Found bridge, pairing...");
                var keys = await LocalHueClient.RegisterAsync(bridge, "HueSaber", "BeatSaber", true);
                log.Info("Successfully paired!");
                token.ThrowIfCancellationRequested();
                prefs.SetString("HueSaber", "appKey", keys.Username);
                prefs.SetString("HueSaber", "clientKey", keys.StreamingClientKey);
                log.Info("Starting HueSaber...");
            } catch (Exception ex)
            {
                log.Error(ex);
                throw;
            }
            await Run(token, bridge);
        }

        public async Task Run(CancellationToken token, string ip = null)
        {
            try
            {
                if (ip == null)
                {
                    ip = await FindBridge();
                }
                token.ThrowIfCancellationRequested();
                var appKey = prefs.GetString("HueSaber", "appKey");
                var clientKey = prefs.GetString("HueSaber", "clientKey");
                log.Info("Connecting to bridge...");
                var client = new StreamingHueClient(ip, appKey, clientKey);
                token.ThrowIfCancellationRequested();
                log.Info("Connected! Getting entertainment group...");
                var group = (await client.LocalHueClient.GetEntertainmentGroups()).ElementAtOrDefault(prefs.GetInt("HueSaber", "overrideRoom", 0));
                if (group == null)
                {
                    log.Error("Group is missing!");
                    return;
                }
                token.ThrowIfCancellationRequested();
                var entGroup = new StreamingGroup(group.Locations);
                log.Info("Found group! Connecting to lightbulbs...");
                await client.Connect(group.Id);
                token.ThrowIfCancellationRequested();
                log.Info("Connected to bulbs! Tracking background color...");
                _ = client.AutoUpdate(entGroup, token);
                var layer = entGroup.GetNewLayer(true);
                while (!token.IsCancellationRequested)
                {
                    //log.Info($"Color is {currentColor}");
                    var color = (Color) currentColor;
                    var miss = DateTimeOffset.UtcNow - (DateTimeOffset) missTime;
                    var cut = DateTimeOffset.UtcNow - (DateTimeOffset) cutTime;

                    var rgbColor = new RGBColor(color.r, color.g, color.b);
                    var hsbColor = rgbColor.GetHSB();
                    if (hsbColor.Saturation > 35)
                    {
                        hsbColor.Saturation = 200;
                    }

                    var brightness = 0.7;
                    if (miss < TimeSpan.FromMilliseconds(100))
                    {
                        brightness = 0.5;
                    } else if (cut < TimeSpan.FromMilliseconds(100))
                    {
                        brightness = 1;
                    }

                    layer.SetState(token, hsbColor.GetRGB(), brightness, TimeSpan.FromMilliseconds(50));
                    await Task.Delay(TimeSpan.FromMilliseconds(1000.0 / 30.0), token);
                }
            } catch (Exception ex)
            {
                log.Error(ex);
                throw;
            }
        }
    }
}
