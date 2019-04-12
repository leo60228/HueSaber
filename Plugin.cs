using System;
using UnityEngine.SceneManagement;
using UnityEngine;
using System.Reflection;
using System.Linq;
using IPA;
using Logger = IPA.Logging.Logger;
using System.Collections.Generic;
using IPA.Config;
using System.Threading;
using System.Threading.Tasks;

namespace HueSaber
{
    public class Plugin : IBeatSaberPlugin
    {
        public string Name => "HueSaber";
        public string Version => "0.0.1";

        protected static Logger Log = null;
        protected static IModPrefs Prefs = null;

        private static HueManager hueManager = null;
        private static CancellationTokenSource hueCts = new CancellationTokenSource();

        public void Init(Logger logger, IModPrefs prefs)
        {
            Log = logger;
            Prefs = prefs;
            hueManager = new HueManager(Log, Prefs);
            if (hueManager.Ready)
            {
                Log.Info("Found existing Hue connection...");
                Task.Run(() => hueManager.Run(hueCts.Token), hueCts.Token);
            }
        }

        public static void SyncHue()
        {
            try
            {
                hueCts.Cancel();
                hueCts = new CancellationTokenSource();
            }
            catch (Exception) { } // can't cancel unused token
            Task.Run(() => hueManager.Sync(hueCts.Token), hueCts.Token);
        }

        private static Texture2D colorTex = null;
        private static RenderTexture colorRenderTex = null;

        public static Color GetBloomColor()
        {
            var tex = Shader.GetGlobalTexture("_BloomPrePassTexture");
            if (tex == null)
            {
                return Color.white;
            }

            if (colorTex == null || colorRenderTex == null || colorTex.width
                                                              != tex.width || colorTex.height != tex.height || colorRenderTex.width != tex.width || colorRenderTex.height != tex.height)
            {
                colorTex = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                colorRenderTex = new RenderTexture(tex.width, tex.height, 32);
            }

            var currentRT = RenderTexture.active;

            Graphics.Blit(tex, colorRenderTex);

            RenderTexture.active = colorRenderTex;
            colorTex.ReadPixels(new Rect(0, 0, colorRenderTex.width, colorRenderTex.height), 0, 0);
            colorTex.Apply();

            var color1 = colorTex.GetPixel(colorTex.width / 3, colorTex.height - 1);
            var color2 = colorTex.GetPixel(colorTex.width / 3 * 2, colorTex.height - 1);
            var color3 = colorTex.GetPixel(colorTex.width / 3, 0);
            var color4 = colorTex.GetPixel(colorTex.width / 3 * 2, 0);
            var color5 = colorTex.GetPixel(colorTex.width / 3, colorTex.height / 2);
            var color6 = colorTex.GetPixel(colorTex.width / 3 * 2, colorTex.height / 2);

            var colors = new Tuple<Color, float>[]
            {
                Tuple.Create(color1, color1.grayscale),
                Tuple.Create(color2, color2.grayscale),
                Tuple.Create(color3, color3.grayscale),
                Tuple.Create(color4, color4.grayscale),
                Tuple.Create(color5, color5.grayscale),
                Tuple.Create(color6, color6.grayscale)
            };

            var color = colors.OrderBy(t => t.Item2).ElementAt(1).Item1;

            RenderTexture.active = currentRT;

            return color;
        }

        public void OnSceneLoaded(Scene scene, LoadSceneMode sceneMode)
        {
            if (scene.name == "MenuCore")
                HueUI.CreateUI();

            var spawner = Resources.FindObjectsOfTypeAll<BeatmapObjectSpawnController>().FirstOrDefault();

            if (spawner != null)
            {
                spawner.noteWasCutEvent += Spawner_noteWasCutEvent;
                spawner.noteWasMissedEvent += Spawner_noteWasMissedEvent;
            }
        }

        private void Spawner_noteWasMissedEvent(BeatmapObjectSpawnController spawner, NoteController note)
        {
            if (note.noteData.noteType != NoteType.Bomb)
            {
                HueManager.missTime = (object) DateTimeOffset.UtcNow;
            }
        }

        private void Spawner_noteWasCutEvent(BeatmapObjectSpawnController spawner, NoteController note, NoteCutInfo info)
        {
            if (info.allIsOK)
            {
                HueManager.cutTime = (object) DateTimeOffset.UtcNow;
            } else
            {
                HueManager.missTime = (object) DateTimeOffset.UtcNow;
            }
        }

        public void OnSceneUnloaded(Scene scene)
        {
            
        }

        public void OnActiveSceneChanged(Scene prevScene, Scene nextScene)
        {
            
        }

        public void OnApplicationStart()
        {
            
        }

        public void OnApplicationQuit()
        {
            
        }

        public void OnUpdate()
        {
            
        }

        public void OnFixedUpdate()
        {
            try
            {
                HueManager.currentColor = (object) GetBloomColor();
            }
            catch { } // may fail for any reason
        }

#pragma warning disable IDE0051 // Remove unused private members
        private void PullInSystemNetHttp()
#pragma warning restore IDE0051 // Remove unused private members
        {
            Console.WriteLine(typeof(System.Net.Http.HttpClient).FullName);
        }
    }
}
