using Epic.OnlineServices;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using OWML.Utils;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace CleanEnergy
{
    public class CleanEnergy : ModBehaviour
    {
        public static CleanEnergy Instance;

        private SolarPowerManager solarPowerManager;

        public void Awake()
        {
            Instance = this;
            // You won't be able to access OWML's mod helper in Awake.
            // So you probably don't want to do anything here.
            // Use Start() instead.
        }

        public void Start()
        {
            // Starting here, you'll have access to OWML's mod helper.
            ModHelper.Console.WriteLine($"{nameof(CleanEnergy)} has loaded!", MessageType.Success);

            new Harmony("GameDev46.CleanEnergy").PatchAll(Assembly.GetExecutingAssembly());

            //OnCompleteSceneLoad(OWScene.TitleScreen, OWScene.TitleScreen); // We start on title screen
            LoadManager.OnCompleteSceneLoad += OnCompleteSceneLoad;
        }

        public void OnCompleteSceneLoad(OWScene previousScene, OWScene newScene)
        {
            if (newScene != OWScene.SolarSystem) return;
            StartCoroutine(WaitToSetupSolarPanels());
        }

        // Called by OWML; once at the start and upon each config setting change.
        public override void Configure(IModConfig config)
        {
            if (solarPowerManager == null) return;

            string solarPowerEfficiency = config.GetSettingsValue<string>("solarPowerEfficiency");
            string shipBatteryEfficiency = config.GetSettingsValue<string>("shipBatteryEfficiency");

            solarPowerManager.UpdateSettings(solarPowerEfficiency, shipBatteryEfficiency);
        }

        IEnumerator WaitToSetupSolarPanels()
        {
            // Wait a moment to ensure the ship's cockpit has been fully loaded into the scene
            yield return new WaitForSeconds(2.0f);
            SetupSolarPanels();
        }

        private void SetupSolarPanels()
        {
            // Get the ship's cockpit module
            Transform shipTransform = Locator.GetShipTransform();

            // Check that the ship transform exists
            if (shipTransform == null)
            {
                ModHelper.Console.WriteLine("Couldn't locate the ship's transform", MessageType.Error);
                return;
            }

            // Update the solar panel loader
            SolarPanelLoader.SetModDirectoryPath(ModHelper.Manifest.ModFolderPath);
            SolarPanelLoader.SetModConsole(ModHelper.Console);

            // Create the solar power manager and attach it to the ship's cockpit
            solarPowerManager = SolarPowerManager.Create(shipTransform, ModHelper.Console);

            // Apply the current config settings
            StartCoroutine(WaitToUpdateSettings());
        }

        IEnumerator WaitToUpdateSettings()
        {
            yield return new WaitForSeconds(0.5f);

            // Apply the current config settings
            string solarPowerEfficiency = ModHelper.Config.GetSettingsValue<string>("solarPowerEfficiency");
            string shipBatteryEfficiency = ModHelper.Config.GetSettingsValue<string>("shipBatteryEfficiency");

            solarPowerManager.UpdateSettings(solarPowerEfficiency, shipBatteryEfficiency);
        }

    }

}
