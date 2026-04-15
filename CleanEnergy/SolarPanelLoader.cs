using OWML.Common;
using OWML.Logging;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace CleanEnergy
{
    internal class SolarPanelLoader
    {
        private static string modFolderPath = "";
        private static IModConsole modConsole;

        public static void SetModDirectoryPath(string dirPath)
        {
            modFolderPath = dirPath;
        }

        public static void SetModConsole(IModConsole console)
        {
            modConsole = console;
        }

        public static GameObject LoadSolarPanels()
        {
            try
            {
                string platformFolder = "";

                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.WindowsEditor:
                        platformFolder = "Windows";
                        break;
                    case RuntimePlatform.LinuxPlayer:
                        platformFolder = "Linux";
                        break;
                    case RuntimePlatform.OSXPlayer:
                        platformFolder = "Mac";
                        break;
                    default:
                        modConsole.WriteLine($"Unsupported platform: {Application.platform}", MessageType.Warning);
                        return null;
                }

                string bundlePath = Path.Combine(modFolderPath, "Assets", platformFolder, "solarpanels");
                AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);

                GameObject prefab = bundle.LoadAsset<GameObject>("Solar Panel Holder");
                GameObject solarPanels = GameObject.Instantiate(prefab);

                solarPanels.transform.name = "Solar Panels";

                return solarPanels;
            }
            catch (Exception e)
            {
                modConsole.WriteLine($"Failed to load the solarpanels asset bundle from Assets/solarpanels: {e}", MessageType.Error);
                return null;
            }
        }
    }
}
