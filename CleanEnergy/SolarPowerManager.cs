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
        private float sunBrightness = 0.0f;

        private Transform sunTransform;
        private const float SUN_RADIUS = 2000.0f; // 4000 at the end of the loop

        private const float CHECK_OCCLUSION_INTERVAL = 1.0f;
        private float checkOcclusionTimer = 0.0f;

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

            modConsole.WriteLine($"Updated solar power generation strength to {solarEfficiency}", MessageType.Success);
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
            float fuelDrainConstant = shipStartFuel / 150.0f;

            float fuelDrainRate = 0.0f;
            if (sunBrightness <= 0.0f) fuelDrainRate = fuelDrainConstant * Time.deltaTime;

            // The ship's battery should take 100 seconds to refuel at Timber Hearth
            float fuelRefillConstant = shipStartFuel / 100.0f;

            // At Timber Hearth's distance from the sun, the solar panels should also take 150 seconds to fully recharge on medium efficiency
            float fuelRefillRate = 0.0f;
            if (sunBrightness > 0.0f) fuelRefillRate = solarPowerEfficiency * fuelRefillConstant * inverseSquareFalloff * Time.deltaTime;

            // Prevent the fuel refill rate from exceeding the max fuel refill rate
            fuelRefillRate = Math.Min(fuelRefillRate, MAX_FUEL_REFILL_RATE);

            // Update the ship's fuel
            shipResourceManager.DrainFuel(fuelDrainRate);

            float currentFuel = shipResourceManager.GetFuel();
            shipResourceManager.SetFuel(currentFuel + fuelRefillRate);

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
            float occlusion = 0.0f;

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

            occlusion += GetOcclusionFromBody(shipPos, caveTwin, caveTwinRadius);
            occlusion += GetOcclusionFromBody(shipPos, towerTwin, towerTwinRadius);
            occlusion += GetOcclusionFromBody(shipPos, timberHearth, timberHearthRadius);
            occlusion += GetOcclusionFromBody(shipPos, attlerock, attlerockRadius);
            occlusion += GetOcclusionFromBody(shipPos, brittleHollow, brittleHollowRadius);
            occlusion += GetOcclusionFromBody(shipPos, volcanicMoon, volcanicMoonRadius);
            occlusion += GetOcclusionFromBody(shipPos, giantsDeep, giantsDeepRadius);
            occlusion += GetOcclusionFromBody(shipPos, darkBramble, darkBrambleRadius);
            occlusion += GetOcclusionFromBody(shipPos, comet, cometRadius);

            modConsole.WriteLine($"Occlusion: {occlusion}", MessageType.Success);

            return occlusion;
        }

        private float GetOcclusionFromBody(Vector3 observerPos, Transform body, float bodyRadius)
        {
            Vector3 toSun = sunTransform.position - observerPos;
            Vector3 toBody = body.position - observerPos;

            float sunDistance = toSun.magnitude;
            float bodyDistance = toBody.magnitude;

            Vector3 sunDir = toSun / sunDistance;
            Vector3 bodyDir = toBody / bodyDistance;

            // If the sun is closer then the body cannot be in front of it
            if (sunDistance < bodyDistance) return 0f;

            // Ignore if body is not roughly in front of the sun
            if (Vector3.Dot(sunDir, bodyDir) < 0.0f) return 0f;

            float sunAngular = GetAngularRadius(SUN_RADIUS, sunDistance);
            float bodyAngular = GetAngularRadius(bodyRadius, bodyDistance);

            float separation = Mathf.Acos(Mathf.Clamp(Vector3.Dot(sunDir, bodyDir), -1f, 1f));

            float overlap = CircleOverlap(sunAngular, bodyAngular, separation);

            float sunArea = Mathf.PI * sunAngular * sunAngular;

            return overlap / sunArea;
        }

        private float GetAngularRadius(float radius, float distance)
        {
            return Mathf.Asin(radius / distance);
        }

        float CircleOverlap(float r1, float r2, float d)
        {
            // No overlap
            if (d >= r1 + r2) return 0f;

            // One fully inside the other
            if (d <= Mathf.Abs(r1 - r2))
            {
                float minR = Mathf.Min(r1, r2);
                return Mathf.PI * minR * minR;
            }

            float r1Sq = r1 * r1;
            float r2Sq = r2 * r2;

            float alpha = Mathf.Acos((d * d + r1Sq - r2Sq) / (2f * d * r1));
            float beta = Mathf.Acos((d * d + r2Sq - r1Sq) / (2f * d * r2));

            float area =
                r1Sq * alpha +
                r2Sq * beta -
                0.5f * Mathf.Sqrt(
                    (-d + r1 + r2) *
                    (d + r1 - r2) *
                    (d - r1 + r2) *
                    (d + r1 + r2)
                );

            return area;
        }

    }
}
