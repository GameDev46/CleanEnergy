using Newtonsoft.Json.Linq;
using OWML.Common;
using OWML.Logging;
using OWML.ModHelper;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using UnityEngine;
using UnityEngine.InputSystem.HID;
using UnityEngine.UI;
using static UnityEngine.GraphicsBuffer;
using UnityEngine.SceneManagement;

namespace CleanEnergy
{
    public class SolarPowerManager : MonoBehaviour
    {
        public IModConsole modConsole;

        private ShipResources shipResourceManager;
        public float shipStartFuel = 10000.0f;

        // Default to medium solar power strength
        private float solarPowerEfficiency = 0.5f;

        // Player starts off on the dark side of Timber Hearth
        private bool isSunVisible = false;

        private const float CHECK_SUN_INTERVAL = 1.0f;
        private float checkSunTimer = 0.0f;

        private const float FUEL_DRAIN_RATE = 0.1f;
        private float MAX_FUEL_REFILL_RATE = 100.0f;

        public static SolarPowerManager Create(Transform ship, IModConsole modHelperConsole)
        {
            ShipResources shipResources = ship.GetComponent<ShipResources>();

            // Check if the ShipResources component was found successfully
            if (shipResources == null)
            {
                modHelperConsole.WriteLine("Couldn't locate the ShipResources component on ship transform", MessageType.Error);
                return null;
            }

            // Create the solar power manager and attach it to the ship's body
            var solarPowerManager = ship.gameObject.AddComponent<SolarPowerManager>();
            solarPowerManager.modConsole = modHelperConsole;
            solarPowerManager.shipResourceManager = shipResources;
            solarPowerManager.shipStartFuel = shipResources.GetFuel();

            return solarPowerManager;
        }

        public void Start()
        {
            // Max fuel refill rate is 1% of the starting fuel per second
            MAX_FUEL_REFILL_RATE = shipStartFuel / 100.0f;
        }

        public void UpdateSettings(string solarEfficiency)
        {
            switch (solarEfficiency)
            {
                case "Low":
                    solarPowerEfficiency = 0.25f;
                    break;
                case "Medium":
                    solarPowerEfficiency = 0.5f;
                    break;
                case "High":
                    solarPowerEfficiency = 0.75f;
                    break;
                case "Ultra":
                    solarPowerEfficiency = 1.0f;
                    break;
                default:
                    modConsole.WriteLine($"Invalid solar power generation strength: {solarEfficiency}", MessageType.Error);
                    return;
            }

            modConsole.WriteLine($"Updated solar power generation strength to {solarEfficiency}", MessageType.Success);
        }

        public void Update()
        {
            // Sun              3000
            // Twins            
            // Timber Hearth    450   
            // Brittle Hollow   450
            // Giant's Deep     1100
            // Quantum Moon     150

            //shipResourceManager.GetFuel();
            //shipResourceManager.DrainFuel(0.0f);
            //shipResourceManager.SetFuel(0.0f);

            checkSunTimer += Time.deltaTime;

            if (checkSunTimer >= CHECK_SUN_INTERVAL)
            {
                // Reset the timer
                checkSunTimer = 0.0f;

                // Raycast to the sun and get the distance to it and whether it is currently visible from the ship
                RaycastHit hit;

                Transform sunTransform = Locator.GetSunTransform();
                Vector3 sunDirection = (sunTransform.position - transform.position).normalized;

                isSunVisible = Physics.Raycast(transform.position, sunDirection, out hit) && hit.transform == sunTransform;
                float sunDistance = Vector3.Distance(transform.position, sunTransform.position);

                // If the sun is visible, then generate fuel based on the distance to the sun and the solar power efficiency
                float sunSurfaceDistance = Math.Max(sunDistance - 3000.0f, 1.0f);
                float sunToTimberHearthDistance = 8593.0f;

                // Will have value of 1 when the Timber Hearth
                float inverseSquareFalloff = (sunToTimberHearthDistance - 3000.0f) / (sunSurfaceDistance * sunSurfaceDistance);

                float fuelDrainRate = FUEL_DRAIN_RATE * CHECK_SUN_INTERVAL;
                if (isSunVisible) fuelDrainRate = -solarPowerEfficiency * CHECK_SUN_INTERVAL * inverseSquareFalloff;

                // Prevent the fuel refill rate from exceeding the max fuel refill rate
                fuelDrainRate = Math.Max(fuelDrainRate, -MAX_FUEL_REFILL_RATE);

                // Update the ship's fuel
                shipResourceManager.DrainFuel(fuelDrainRate);
            }
        }

    }
}
