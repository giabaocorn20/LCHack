using GameNetcodeStuff;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using static LCHack.Plugin;

namespace LCHack
{
    [HarmonyPatch]
    //Unlimited Scan Range
    public static class MeetsScanNodeRequirementsPatch
    {
        [HarmonyPatch(typeof(HUDManager), "MeetsScanNodeRequirements")]
        [HarmonyPatch(new Type[] { typeof(ScanNodeProperties), typeof(PlayerControllerB) })]

        // The prefix method
        public static bool Prefix(ref bool __result)
        {
            if (Hacks.Instance.isUnlimitedScanRangeEnabled)
            {
                __result = true;
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch]
    //Unlimited Scan Range
    public static class AssignNewNodesPatch
    {
        [HarmonyPatch(typeof(HUDManager), "AssignNewNodes")]
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_R4 && (float)codes[i].operand == 20f)
                {
                    if (Hacks.Instance.isUnlimitedScanRangeEnabled)
                        codes[i].operand = 500f;
                    else
                        codes[i].operand = 20f;
                }
            }
            return codes;
        }
    }


    [HarmonyPatch]
    //Unlimited battery
    public static class SyncBatteryServerRpcPatch
    {
        [HarmonyPatch(typeof(GrabbableObject), "SyncBatteryServerRpc")]
        public static bool Prefix(GrabbableObject __instance, ref int charge)
        {
            if (Hacks.Instance.isUnlimitedItemPowerEnabled)
            {
                if (__instance.itemProperties.requiresBattery)
                {
                    __instance.insertedBattery.empty = false;
                    __instance.insertedBattery.charge = 1f;
                    charge = 100;
                }

            }

            return true;
        }
    }

    [HarmonyPatch]
    //God Mode
    public static class DamagePlayerPatch
    {
        [HarmonyPatch(typeof(PlayerControllerB), "DamagePlayer")]
        public static bool Prefix(PlayerControllerB __instance)
        {
            if (__instance.actualClientId == GameNetworkManager.Instance.localPlayerController.actualClientId)
            {
                if (Hacks.Instance.isGodModeEnabled)
                {
                    return false;
                }
            }

            return true;
        }
    }


    [HarmonyPatch]
    //Infinite Sprint
    public static class InfiniteSprintPatch
    {
        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        public static void Postfix(PlayerControllerB __instance)
        {
            // Infinite Sprint
            if (!Hacks.Instance.isInfiniteSprintEnabled)
                return;

            if (GameNetworkManager.Instance.localPlayerController.actualClientId != __instance.actualClientId)
                return;

            __instance.sprintMeter = 1f;

            if (__instance.sprintMeterUI != null)
            {
                __instance.sprintMeterUI.fillAmount = 1f;
            }
        }
    }


    [HarmonyPatch]
    //High Jump
    public static class HighJumpPatch
    {
        private static float originalJumpForce;

        [HarmonyPatch(typeof(PlayerControllerB), "Start")]
        [HarmonyPrefix]
        public static void getOGJumpForce(PlayerControllerB __instance)
        {
            originalJumpForce = __instance.jumpForce;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "Update")]
        public static void Postfix(PlayerControllerB __instance)
        {
            // High Jump
            if (!Hacks.Instance.isHighJumpEnabled)
            {
                __instance.jumpForce = originalJumpForce;
                return;
            }

            if (GameNetworkManager.Instance.localPlayerController.actualClientId != __instance.actualClientId)
                return;

            __instance.jumpForce = 30f; 
        }
    }

    [HarmonyPatch]
    //High Night Vision
    public static class NightVisionPatch
    {
        private static float nightVisionIntensity;
        private static Color nightVisionColor;
        private static float nightVisionRange;

        [HarmonyPatch(typeof(PlayerControllerB), "Start")]
        [HarmonyPrefix]
        static void getNightVision(ref PlayerControllerB __instance)
        {
            // store nightvision values
            nightVisionIntensity = __instance.nightVision.intensity;
            nightVisionColor = __instance.nightVision.color;
            nightVisionRange = __instance.nightVision.range;
        }

        [HarmonyPatch(typeof(PlayerControllerB), "SetNightVisionEnabled")]
        [HarmonyPostfix]
        static void updateNightVision(PlayerControllerB __instance)
        {
            //instead of enabling/disabling nightvision, set the variables
            if (!Hacks.Instance.isNightVisionEnabled)
            {
                __instance.nightVision.color = nightVisionColor;
                __instance.nightVision.intensity = nightVisionIntensity;
                __instance.nightVision.range = nightVisionRange;
                return;
            }

            if (GameNetworkManager.Instance.localPlayerController.actualClientId != __instance.actualClientId)
                return;

            
                __instance.nightVision.color = UnityEngine.Color.white;
                __instance.nightVision.intensity = 1000f;
                __instance.nightVision.range = 1000f;
   
            // should always be on
            __instance.nightVision.enabled = true;
        }
    }




        [HarmonyPatch]
    //High Scrap Value
    public static class SyncScrapValuesClientRpcPatch
    {
        [HarmonyPatch(typeof(RoundManager), "SyncScrapValuesClientRpc")]
        public static bool Prefix(NetworkObjectReference[] spawnedScrap, ref int[] allScrapValue)
        {
            if (!Hacks.Instance.isHighScrapValueEnabled)
                return true;

            if (spawnedScrap != null)
            {
                for (int i = 0; i < spawnedScrap.Length; i++)
                {
                    NetworkObject networkObject;
                    if (spawnedScrap[i].TryGet(out networkObject, null))
                    {
                        GrabbableObject component = networkObject.GetComponent<GrabbableObject>();
                        if (component != null)
                        {
                            allScrapValue[i] = 420;
                        }
                    }
                }
            }
            return true;
        }
    }

    [HarmonyPatch]
    public static class isEnemySpawnablePatch
    {
        [HarmonyPatch(typeof(RoundManager), "EnemyCannotBeSpawned")]
        public static bool Prefix(ref bool __result)
        {
            if (Hacks.Instance.isEnemySpawnableEnabled)
            {
                __result = true;
                return false;
            }

            return true;
        }
    }
   
}