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
        // Batter efficiency determines how quickly the battery loses charge
        private float shipBatteryEfficiency = 1.0f;

        // Player starts off on the dark side of Timber Hearth
        private float sunBrightness = 0.0f;

        private Transform sunTransform;
        private const float SUN_RADIUS = 2000.0f; // 4000 at the end of the loop

        private const float CHECK_OCCLUSION_INTERVAL = 1.0f;
        private float checkOcclusionTimer = 0.0f;

        private float MAX_FUEL_REFILL_RATE = 1000.0f;

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

            // Locate the ship's cabin module
            Transform shipCabin = ship.Find("Module_Cabin");

            if (shipCabin != null)
            {
                // Load and attach the solar panels to the ship's cabin
                GameObject solarPanels = SolarPanelLoader.LoadSolarPanels();

                if (solarPanels != null)
                {
                    solarPanels.transform.SetParent(shipCabin, false);
                    solarPanels.transform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
                    solarPanels.SetActive(true);
                }
                else
                {
                    modHelperConsole.WriteLine("Failed to load solar panel model", MessageType.Error);
                }

            }
            else
            {
                modHelperConsole.WriteLine("Couldn't locate the ship cabin to attach solar panels to", MessageType.Error);
            }

            return solarPowerManager;
        }

        public void Start()
        {
            // Max fuel refill rate is 1% of the starting fuel per second
            MAX_FUEL_REFILL_RATE = shipStartFuel / 10.0f;
        }

        public void UpdateSettings(string solarEfficiency, string batteryEfficiency)
        {
            switch (solarEfficiency)
            {
                case "Low":
                    solarPowerEfficiency = 0.5f;
                    break;
                case "Medium":
                    solarPowerEfficiency = 1.0f;
                    break;
                case "High":
                    solarPowerEfficiency = 1.5f;
                    break;
                case "Ultra":
                    solarPowerEfficiency = 2.0f;
                    break;
                default:
                    modConsole.WriteLine($"Invalid solar power generation strength: {solarEfficiency}", MessageType.Error);
                    return;
            }

            switch (batteryEfficiency)
            {
                case "Low":
                    shipBatteryEfficiency = 0.5f;
                    break;
                case "Medium":
                    shipBatteryEfficiency = 1.0f;
                    break;
                case "High":
                    shipBatteryEfficiency = 1.5f;
                    break;
                case "Ultra":
                    shipBatteryEfficiency = 2.0f;
                    break;
                default:
                    modConsole.WriteLine($"Invalid battery efficiency: {batteryEfficiency}", MessageType.Error);
                    return;
            }

            modConsole.WriteLine($"Updated solar power generation strength to {solarEfficiency} and battery efficiency to {batteryEfficiency}", MessageType.Success);
        }

        public void Update()
        {
            // Sun              3000
            // Twins            360
            // Timber Hearth    450   
            // Brittle Hollow   450
            // Giant's Deep     1100
            // Quantum Moon     150

            //shipResourceManager.GetFuel();
            //shipResourceManager.DrainFuel(0.0f);
            //shipResourceManager.SetFuel(0.0f);

            // Get the distance between the ship and the sun
            sunTransform = Locator.GetSunTransform();
            float sunDistance = Vector3.Distance(transform.position, sunTransform.position);

            // If the sun is visible, then generate fuel based on the distance to the sun and the solar power efficiency
            float sunSurfaceDistance = Math.Max(sunDistance - 3000.0f, 1.0f);
            float sunToTimberHearthDistance = 8593.0f - 3000.0f;

            // Will have value of 1 when at Timber Hearth
            float inverseSquareFalloff = sunToTimberHearthDistance * sunToTimberHearthDistance / (sunSurfaceDistance * sunSurfaceDistance);

            // The ship's battery should last 150 seconds without solar power
            float fuelDrainConstant = shipStartFuel / (150.0f * shipBatteryEfficiency);
            float fuelDrainRate = fuelDrainConstant * Time.deltaTime;

            // The ship's battery should take 75 seconds to refuel at Timber Hearth
            float fuelRefillConstant = shipStartFuel / 75.0f;

            // At Timber Hearth's distance from the sun, the solar panels should take 75 seconds to fully recharge on medium efficiency
            float fuelRefillRate = 0.0f;
            if (sunBrightness > 0.0f) fuelRefillRate = solarPowerEfficiency * fuelRefillConstant * inverseSquareFalloff * Time.deltaTime;

            // Prevent the fuel refill rate from exceeding the max fuel refill rate
            fuelRefillRate = Math.Min(fuelRefillRate, MAX_FUEL_REFILL_RATE);

            // Update the ship's fuel
            float currentFuel = shipResourceManager.GetFuel();
            shipResourceManager.SetFuel(currentFuel + fuelRefillRate - fuelDrainRate);

            // Increase the sun occlusion timer
            checkOcclusionTimer += Time.deltaTime;

            // Update the sun visibility every CHECK_SUN_INTERVAL seconds
            if (checkOcclusionTimer >= CHECK_OCCLUSION_INTERVAL)
            {
                // Reset the timer
                checkOcclusionTimer = 0.0f;

                // Raycast to the sun and get the distance to it and whether it is currently visible from the ship
                /*RaycastHit hit;

                Transform sunTransform = Locator.GetSunTransform();
                Vector3 sunDirection = (sunTransform.position - transform.position).normalized;

                // Prevent the ray from starting inside the ship
                Vector3 rayStartPosition = transform.position + sunDirection * 10.0f;

                isSunVisible = Physics.Raycast(rayStartPosition, sunDirection, out hit) && hit.transform == sunTransform;
                float sunDistance = Vector3.Distance(transform.position, sunTransform.position);*/

                float sunOcclusionFraction = GetSunOcclusionFraction();
                sunBrightness = 1.0f - sunOcclusionFraction;
            }
        }

        private float GetSunOcclusionFraction()
        {
            Vector3 shipPos = transform.position;

            Transform caveTwin = Locator.GetAstroObject(AstroObject.Name.CaveTwin).transform;   // Ember Twin
            float caveTwinRadius = 170.0f;
            Transform towerTwin = Locator.GetAstroObject(AstroObject.Name.TowerTwin).transform; // Ash Twin
            float towerTwinRadius = 169.0f; // Need to add in way to deal with radius change during game

            Transform timberHearth = Locator.GetAstroObject(AstroObject.Name.TimberHearth).transform;
            float timberHearthRadius = 254.0f;
            Transform attlerock = Locator.GetAstroObject(AstroObject.Name.TimberMoon).transform;
            float attlerockRadius = 80.0f;

            Transform brittleHollow = Locator.GetAstroObject(AstroObject.Name.BrittleHollow).transform;
            float brittleHollowRadius = 272.0f;
            Transform volcanicMoon = Locator.GetAstroObject(AstroObject.Name.VolcanicMoon).transform;
            float volcanicMoonRadius = 97.3f;

            Transform giantsDeep = Locator.GetAstroObject(AstroObject.Name.GiantsDeep).transform;
            float giantsDeepRadius = 1100.0f;

            Transform darkBramble = Locator.GetAstroObject(AstroObject.Name.DarkBramble).transform;
            float darkBrambleRadius = 203.3f;

            Transform comet = Locator.GetAstroObject(AstroObject.Name.Comet).transform;
            float cometRadius = 83.0f;

            List<(Vector3 pos, float radius)> planets = new List<(Vector3 pos, float radius)>()
            {
                (caveTwin.position, caveTwinRadius),
                (towerTwin.position, towerTwinRadius),
                (timberHearth.position, timberHearthRadius),
                (attlerock.position, attlerockRadius),
                (brittleHollow.position, brittleHollowRadius),
                (volcanicMoon.position, volcanicMoonRadius),
                (giantsDeep.position, giantsDeepRadius),
                (darkBramble.position, darkBrambleRadius),
                (comet.position, cometRadius)
            };

            List<Vector3> sunSampleDirections = GetSunSampleDirections(sunTransform.position, shipPos);
            int occludedSamples = 0;

            foreach (Vector3 dir in sunSampleDirections) 
            {
                if (IsSampleOccluded(shipPos, dir, planets)) occludedSamples++;
            }

            float occlusion = (float)occludedSamples / sunSampleDirections.Count;

            return occlusion;
        }

        bool IsSampleOccluded(Vector3 origin, Vector3 dir, List<(Vector3 pos, float radius)> planets)
        {
            float rayMaxLengthSqr = (sunTransform.position - origin).sqrMagnitude;

            foreach (var p in planets)
            {
                // Check that the planet is closer to the ship then the sun
                float planetDistanceSqr = (p.pos - origin).sqrMagnitude;
                if (planetDistanceSqr > rayMaxLengthSqr) continue;

                // Check that the planet is roughly between the ship and the sun
                float dot = Vector3.Dot(dir, (p.pos - origin).normalized);
                if (dot < 0.0f) continue;

                // Check if the ray intersects the planet
                if (RayIntersectsSphere(origin, dir, p.pos, p.radius)) return true;
            }

            return false;
        }

        bool RayIntersectsSphere(Vector3 origin, Vector3 rayDir, Vector3 sphereCenter, float sphereRadius)
        {
            Vector3 sphereDir = (sphereCenter - origin).normalized;

            // Get the cosine of the acute angle between the ray direction and sphere direction
            float cosTheta = Mathf.Abs(Vector3.Dot(rayDir, sphereDir));

            // Calculate the sin of the angle squared
            float sinThetaSqr = 1.0f - cosTheta * cosTheta;

            // Calculate the shortest distance squared from the ray to the center of the sphere
            float distanceToRaySqr = sinThetaSqr * (sphereCenter - origin).sqrMagnitude;

            // If the shortest distance is less than or equal to the sphere's radius, then it has intersected the sphere
            return distanceToRaySqr <= sphereRadius * sphereRadius;
        }

        List<Vector3> GetSunSampleDirections(Vector3 sunPos, Vector3 observerPos)
        {
            List<Vector3> rayDirs = new List<Vector3>();

            // Ray directly to sun
            Vector3 sunDir = (sunPos - observerPos).normalized;
            rayDirs.Add(sunDir);

            // Calculate the right and up vectors for the rings around the sun direction
            Vector3 right = Vector3.Cross(sunDir, Vector3.up).normalized;
            // If the sun direction is parallel or close to the up vector, use a different vector to calculate the right vector
            if (right.sqrMagnitude < 0.001f) right = Vector3.Cross(sunDir, Vector3.forward).normalized;

            Vector3 up = Vector3.Cross(right, sunDir).normalized;

            // Take samples for edge and central rings around the sun direction
            int edgeRingSamples = 8;
            int midRingSamples = 4;

            float edgeRingAngleStep = 2 * Mathf.PI / edgeRingSamples;
            float midRingAngleStep = 2 * Mathf.PI / midRingSamples;

            // Edge ring 
            for (int i = 0; i < edgeRingSamples; i++)
            {
                Vector3 edgePoint = Mathf.Cos(i * edgeRingAngleStep) * up + Mathf.Sin(i * edgeRingAngleStep) * right;
                edgePoint.Normalize();

                edgePoint = (edgePoint * SUN_RADIUS) + sunPos;

                Vector3 pointDir = (edgePoint - observerPos).normalized;
                rayDirs.Add(pointDir);
            }

            // Mid ring 
            for (int i = 0; i < midRingSamples; i++)
            {
                Vector3 edgePoint = Mathf.Cos(i * midRingAngleStep) * up + Mathf.Sin(i * midRingAngleStep) * right;
                edgePoint.Normalize();

                edgePoint = (edgePoint * SUN_RADIUS * 0.5f) + sunPos;

                Vector3 pointDir = (edgePoint - observerPos).normalized;
                rayDirs.Add(pointDir);
            }

            return rayDirs;
        }

    }
}
