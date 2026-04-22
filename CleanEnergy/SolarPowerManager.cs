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
        private float sunlight = 0.0f;
        private SunController[] sunControllers;

        private bool shouldUseSolarPanels = true;

        private const float CHECK_OCCLUSION_INTERVAL = 1.0f;
        private float checkOcclusionTimer = 0.0f;

        private float MAX_FUEL_REFILL_RATE = 200.0f;

        private static GameObject solarPanels;

        private bool isInOWUniverse = true;

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
                solarPanels = SolarPanelLoader.LoadSolarPanels();

                if (solarPanels != null)
                {
                    solarPanels.transform.SetParent(shipCabin, false);
                    solarPanels.transform.localPosition = new Vector3(0.0f, -3.63f, 0.1f);
                    solarPanels.transform.localRotation = Quaternion.Euler(0.0f, 270.0f, 0.0f);
                    solarPanels.transform.localScale = new Vector3(2.3f, 2.3f, 2.3f);
                    solarPanels.SetActive(true);

                    // Front Panels ----
                    Transform frontPanels = solarPanels.transform.Find("Front Panels");
                    frontPanels.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
                    frontPanels.localRotation = Quaternion.Euler(270.0f, 0.0f, 0.0f);
                    frontPanels.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

                    Transform aboveCockpitPanel = frontPanels.Find("Solar Panel Right");
                    aboveCockpitPanel.localPosition = new Vector3(1.55f, 0.0f, 3.3f);
                    aboveCockpitPanel.localRotation = Quaternion.Euler(5.0f, 0.0f, 270.0f);
                    aboveCockpitPanel.transform.localScale = new Vector3(0.2288f, 0.2288f, 0.2288f);

                    Transform solarPanelFrontLeft = frontPanels.Find("Solar Panel Left");
                    solarPanelFrontLeft.gameObject.SetActive(false);

                    // Back Panels -----
                    Transform backPanels = solarPanels.transform.Find("Back Panels");
                    backPanels.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
                    backPanels.localRotation = Quaternion.Euler(270.0f, 0.0f, 0.0f);
                    backPanels.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);

                    Transform backRightPanel = backPanels.Find("Solar Panel Right");
                    backRightPanel.localPosition = new Vector3(-0.4f, 1.48f, 3.4f);
                    backRightPanel.localRotation = Quaternion.Euler(345.0f, 340.0f, 0.0f);
                    backRightPanel.transform.localScale = new Vector3(0.2288f, 0.2288f, 0.2288f);

                    Transform backLeftPanel = backPanels.Find("Solar Panel Left");
                    backLeftPanel.localPosition = new Vector3(-0.43f, -1.48f, 3.4f);
                    backLeftPanel.localRotation = Quaternion.Euler(15.0f, 335.0f, 180.0f);
                    backLeftPanel.transform.localScale = new Vector3(0.2288f, 0.2288f, 0.2288f);
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
            MAX_FUEL_REFILL_RATE = shipStartFuel / 50.0f;

            // Get all the active suns in the scene
            sunControllers = GameObject.FindObjectsOfType<SunController>();
        }

        public void UpdateSettings(bool solarPanelsUsed, string solarEfficiency, string batteryEfficiency, bool solarPanelsEnabled, string solarPanelLayout)
        {
            shouldUseSolarPanels = solarPanelsUsed;

            switch (solarEfficiency)
            {
                case "Very Low":
                    solarPowerEfficiency = 0.25f;
                    break;
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
                case "Very Low":
                    shipBatteryEfficiency = 0.25f;
                    break;
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

            // Enable / Disable solar panels
            solarPanels.SetActive(solarPanelsEnabled);

            // Get the solar panel transforms for updating the layout
            Transform frontPanels = solarPanels.transform.Find("Front Panels");
            Transform backPanels = solarPanels.transform.Find("Back Panels");

            Transform aboveCockpitPanel = frontPanels.Find("Solar Panel Right");
            //Transform solarPanelFrontLeft = frontPanels.Find("Solar Panel Left"); // Front left panel is always disabled
            Transform backRightPanel = backPanels.Find("Solar Panel Right");
            Transform backLeftPanel = backPanels.Find("Solar Panel Left");

            switch (solarPanelLayout)
            {
                case "R Only":
                    aboveCockpitPanel.gameObject.SetActive(false);
                    backRightPanel.gameObject.SetActive(true);
                    backLeftPanel.gameObject.SetActive(false);
                    break;
                case "L Only":
                    aboveCockpitPanel.gameObject.SetActive(false);
                    backRightPanel.gameObject.SetActive(false);
                    backLeftPanel.gameObject.SetActive(true);
                    break;
                case "Cockpit Only":
                    aboveCockpitPanel.gameObject.SetActive(true);
                    backRightPanel.gameObject.SetActive(false);
                    backLeftPanel.gameObject.SetActive(false);
                    break;
                case "R, L":
                    aboveCockpitPanel.gameObject.SetActive(false);
                    backRightPanel.gameObject.SetActive(true);
                    backLeftPanel.gameObject.SetActive(true);
                    break;
                case "R, Cockpit":
                    aboveCockpitPanel.gameObject.SetActive(true);
                    backRightPanel.gameObject.SetActive(true);
                    backLeftPanel.gameObject.SetActive(false);
                    break;
                case "L, Cockpit":
                    aboveCockpitPanel.gameObject.SetActive(true);
                    backRightPanel.gameObject.SetActive(false);
                    backLeftPanel.gameObject.SetActive(true);
                    break;
                case "R, L, Cockpit":
                    aboveCockpitPanel.gameObject.SetActive(true);
                    backRightPanel.gameObject.SetActive(true);
                    backLeftPanel.gameObject.SetActive(true);
                    break;
                default:
                    modConsole.WriteLine($"Invalid solar panel layout: {solarPanelLayout}", MessageType.Error);
                    return;
            }

            modConsole.WriteLine($"Updated settings: Generation strength: {solarEfficiency}, Battery Efficiency: {batteryEfficiency}, Solar Panels: {solarPanelsEnabled}, Solar Panel Layout: {solarPanelLayout}", MessageType.Success);
        }

        public void Update()
        {
            // If solar panels are not being used, then skip all the solar power calculations
            if (!shouldUseSolarPanels) return;

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
            Transform sunTransform = Locator.GetSunTransform();
            float sunDistance = Vector3.Distance(transform.position, sunTransform.position);

            // If the sun is visible, then generate fuel based on the distance to the sun and the solar power efficiency
            float sunSurfaceDistance = Math.Max(sunDistance - 3000.0f, 1.0f);
            float sunToGiantsDeepDistance = 13000.0f - 3000.0f;

            // Will have value of 1 when at Giant's Deep
            float inverseSquareFalloff = sunToGiantsDeepDistance * sunToGiantsDeepDistance / (sunSurfaceDistance * sunSurfaceDistance);

            // The ship's battery should last 150 seconds without solar power
            float fuelDrainConstant = shipStartFuel / (150.0f * shipBatteryEfficiency);
            float fuelDrainRate = fuelDrainConstant * Time.deltaTime;

            // At Giant's Deep's distance from the sun, the solar panels should roughly have an equal recharge and drain rate
            float fuelRefillConstant = shipStartFuel / 100.0f;
            float fuelRefillRate = solarPowerEfficiency * fuelRefillConstant * inverseSquareFalloff * Time.deltaTime;

            // Prevent the fuel refill rate from exceeding the max fuel refill rate
            fuelRefillRate = Math.Min(fuelRefillRate, MAX_FUEL_REFILL_RATE);

            // Sun brightness is directly proportional to the fuel refill rate
            fuelRefillRate *= sunlight;
                
            // Update the ship's fuel
            float currentFuel = shipResourceManager.GetFuel();
            if (isInOWUniverse) shipResourceManager.SetFuel(currentFuel + fuelRefillRate - fuelDrainRate);

            // Increase the sun occlusion timer
            checkOcclusionTimer += Time.deltaTime;

            // Update the sun visibility every CHECK_SUN_INTERVAL seconds
            if (checkOcclusionTimer >= CHECK_OCCLUSION_INTERVAL)
            {
                // Reset the timer
                checkOcclusionTimer = 0.0f;

                sunlight = GetSunBrightnessFraction(sunTransform);
            }
        }

        private float GetSunBrightnessFraction(Transform sunTransform)
        {
            Vector3 shipPos = transform.position;

            Transform caveTwin = Locator.GetAstroObject(AstroObject.Name.CaveTwin).transform;   // Ember Twin
            float caveTwinRadius = 170.0f;
            Transform towerTwin = Locator.GetAstroObject(AstroObject.Name.TowerTwin).transform; // Ash Twin
            float towerTwinRadius = Locator.GetAstroObject(AstroObject.Name.TowerTwin)._sandLevelController?.GetRadius() ?? 169.0f;

            Transform timberHearth = Locator.GetAstroObject(AstroObject.Name.TimberHearth).transform;
            float timberHearthRadius = 254.0f;
            Transform attlerock = Locator.GetAstroObject(AstroObject.Name.TimberHearth)._moon.transform;
            float attlerockRadius = 80.0f;

            Transform brittleHollow = Locator.GetAstroObject(AstroObject.Name.BrittleHollow).transform;
            float brittleHollowRadius = 272.0f;
            Transform volcanicMoon = Locator.GetAstroObject(AstroObject.Name.BrittleHollow)._moon.transform;
            float volcanicMoonRadius = 97.3f;

            Transform giantsDeep = Locator.GetAstroObject(AstroObject.Name.GiantsDeep).transform;
            float giantsDeepRadius = 1100.0f;

            Transform darkBramble = Locator.GetAstroObject(AstroObject.Name.DarkBramble).transform;
            float darkBrambleRadius = 203.3f;

            Transform comet = Locator.GetAstroObject(AstroObject.Name.Comet).transform;
            float cometRadius = 83.0f;

            // Check if Timber Hearth is enabled, if not then disable solar panels and return a brightness of 1
            if (!timberHearth.gameObject.activeInHierarchy)
            {
                isInOWUniverse = false;
                return 1.0f;
            }

            isInOWUniverse = true;

            List<(Vector3 pos, float radius, int id)> planets = new List<(Vector3 pos, float radius, int id)>()
            {
                (caveTwin.position, caveTwinRadius, 1),
                (towerTwin.position, towerTwinRadius, 2),
                (timberHearth.position, timberHearthRadius, 3),
                (attlerock.position, attlerockRadius, 4),
                (brittleHollow.position, brittleHollowRadius, 5),
                (volcanicMoon.position, volcanicMoonRadius, 6),
                (giantsDeep.position, giantsDeepRadius, 7),
                (darkBramble.position, darkBrambleRadius, 8),
                (comet.position, cometRadius, 9)
            };

            List<Vector3> sunSampleDirections = GetSunSampleDirections(sunTransform.position, shipPos);
            int occludedSamples = 0;

            foreach (Vector3 dir in sunSampleDirections) 
            {
                if (IsSampleOccluded(sunTransform.position, shipPos, dir, planets)) occludedSamples++;
            }

            float occlusion = (float)occludedSamples / sunSampleDirections.Count;
            float brightness = 1.0f - occlusion;

            foreach (var p in planets)
            {
                // If the player is inside the planet sphere then aproximate brightness based on the planet and depth
                float normalisedDistance = (p.pos - shipPos).sqrMagnitude / (p.radius * p.radius);
                if (normalisedDistance <= 1.0f) brightness *= GetPlanetDepthBasedBrightness(normalisedDistance, p.id);
            }

            return brightness;
        }

        float GetPlanetDepthBasedBrightness(float normDist, int planetId)
        {
            switch (planetId) 
            {
                case 1: return normDist;                        // Cave Twin (Ember Twin)
                case 2: return 1.0f;                            // Tower Twin (Ash Twin)
                case 3: return normDist;                        // Timber Hearth
                case 4: return normDist;                        // Attlerock
                case 5: return normDist > 0.8f ? 1.0f : 0.0f;   // Brittle Hollow
                case 6: return 1.0f;                            // Volcanic Moon
                case 7: return normDist > 0.95f ? 1.0f : 0.05f; // Giant's Deep
                case 8: return 1.0f;                            // Dark Bramble
                case 9: return 1.0f;                            // Comet
                default: return 1.0f;
            }
        }

        bool IsSampleOccluded(Vector3 sunPos, Vector3 origin, Vector3 dir, List<(Vector3 pos, float radius, int id)> planets)
        {
            float rayMaxLengthSqr = (sunPos - origin).sqrMagnitude;

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

            // Get the current radius of the sun
            float sunRadius = Locator.GetSunController()?.GetSurfaceRadius() ?? 2000.0f;

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

                edgePoint = (edgePoint * sunRadius) + sunPos;

                Vector3 pointDir = (edgePoint - observerPos).normalized;
                rayDirs.Add(pointDir);
            }

            // Mid ring 
            for (int i = 0; i < midRingSamples; i++)
            {
                Vector3 edgePoint = Mathf.Cos(i * midRingAngleStep) * up + Mathf.Sin(i * midRingAngleStep) * right;
                edgePoint.Normalize();

                edgePoint = (edgePoint * sunRadius * 0.5f) + sunPos;

                Vector3 pointDir = (edgePoint - observerPos).normalized;
                rayDirs.Add(pointDir);
            }

            return rayDirs;
        }

    }
}
