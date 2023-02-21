﻿using Aki.Reflection.Patching;
using Comfort.Common;
using EFT.InventoryLogic;
using EFT;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Diz.Skinning;
using EFT.CameraControl;
using System.Collections;
using EFT.Interactive;
using EFT.Animations;

namespace RealismMod
{
    public class SetAimingSlowdownPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(GClass1598).GetMethod("SetAimingSlowdown", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPrefix]
        private static bool Prefix(ref GClass1598 __instance, bool isAiming)
        {

            Player player = (Player)AccessTools.Field(typeof(GClass1598), "player_0").GetValue(__instance);
            if (player.IsYourPlayer == true)
            {
                if (isAiming)
                {
                    //slow is hard set to 0.33 when called, 0.4-0.43 feels best.
                    float baseSpeed = PlayerProperties.AimMoveSpeedBase;
                    __instance.AddStateSpeedLimit(Math.Max(baseSpeed * WeaponProperties.AimMoveSpeedModifier, 0.15f), Player.ESpeedLimit.Aiming);

                    return false;
                }
                __instance.RemoveStateSpeedLimit(Player.ESpeedLimit.Aiming);
                return false;
            }
            return true;
        }
    }

    public class UpdateWeaponVariablesPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(EFT.Animations.ProceduralWeaponAnimation).GetMethod("UpdateWeaponVariables", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPostfix]
        private static void PatchPostfix(ref EFT.Animations.ProceduralWeaponAnimation __instance)
        {
            Player.FirearmController firearmController = (Player.FirearmController)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "firearmController_0").GetValue(__instance);
            if (firearmController != null)
            {
                Player player = (Player)AccessTools.Field(typeof(EFT.Player.FirearmController), "_player").GetValue(firearmController);
                if (player.IsYourPlayer == true)
                {
                    //to find float_9 on new client version, look for: public float AimingSpeed { get{ return this.float_9; } }
                    //to finf float_19 again, it's set to ErgnomicWeight in this method.
                    float _aimsSpeed = (float)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_9").GetValue(__instance);
                    SkillsClass.GClass1675 skillsClass = (SkillsClass.GClass1675)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "gclass1675_0").GetValue(__instance);
                    __instance.HandsContainer.Recoil.ReturnSpeed = Plugin.StartingConvergence * __instance.Aiming.RecoilConvergenceMult;
                    __instance.HandsContainer.Recoil.Damping = WeaponProperties.TotalRecoilDamping;
                    __instance.HandsContainer.HandsPosition.Damping = WeaponProperties.TotalRecoilHandDamping;
                    float aimSpeed = _aimsSpeed * (1f + WeaponProperties.AimSpeedModifier) * WeaponProperties.GlobalAimSpeedModifier; //*PlayerProperties.StrengthSkillAimBuff
                    WeaponProperties.AimSpeed = aimSpeed;
                    Logger.LogWarning("base aim speed = " + _aimsSpeed);
                    Logger.LogWarning("total aimSpeed = " + aimSpeed);
                    AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_9").SetValue(__instance, aimSpeed);
                    AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_19").SetValue(__instance, WeaponProperties.ErgonomicWeight * PlayerProperties.StrengthSkillAimBuff); //this is only called once, so can't do injury multi. It's probably uncessary to set the value here anyway, it's more just-in-case.
                }
            }
        }
    }



    public class OverweightPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(EFT.Animations.ProceduralWeaponAnimation).GetMethod("get_Overweight", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPostfix]
        private static void PatchPostfix(ref EFT.Animations.ProceduralWeaponAnimation __instance, ref float __result)
        {

            Player.FirearmController firearmController = (Player.FirearmController)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "firearmController_0").GetValue(__instance);

            if (firearmController != null)
            {
                Player player = (Player)AccessTools.Field(typeof(EFT.Player.FirearmController), "_player").GetValue(firearmController);
                if (player.IsYourPlayer == true)
                {
                    AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_2").SetValue(__instance, 0);
                    AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_10").SetValue(__instance, Mathf.Lerp(1f, Singleton<BackendConfigSettingsClass>.Instance.Stamina.AimingSpeedMultiplier, 0));

                    __result = 0;
                }
            }
        }
    }

    public class InitTransformsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(EFT.Animations.ProceduralWeaponAnimation).GetMethod("InitTransforms", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPostfix]
        private static void PatchPostfix(EFT.Animations.ProceduralWeaponAnimation __instance)
        {

            Logger.LogWarning("InitTransforms");
            __instance.HandsContainer.WeaponRoot.localPosition += new Vector3(Plugin.camX.Value, Plugin.camY.Value, Plugin.camZ.Value);
            Plugin.WeapStartPosition = new Vector3(0.0f, 0.0f, 0.0f);
            Plugin.TargetPosition = Plugin.WeapStartPosition + new Vector3(Plugin.offsetX.Value, Plugin.offsetY.Value, Plugin.offsetZ.Value);

            Logger.LogWarning("Plugin.WeapStartPosition" + Plugin.WeapStartPosition);
        }
    }


    public class ApplyComplexRotationPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(EFT.Animations.ProceduralWeaponAnimation).GetMethod("ApplyComplexRotation", BindingFlags.Instance | BindingFlags.Public);
        }

        public static float changeSpeedMulti = Plugin.changeTimeMult.Value;
        public static float resetSpeedMulti = Plugin.resetTimeMult.Value;


        [PatchPrefix]
        private static bool Postfix(ref EFT.Animations.ProceduralWeaponAnimation __instance, float dt)
        {
            Vector3 targetRotation = new Vector3(Plugin.rotationX.Value, Plugin.rotationY.Value, Plugin.rotationZ.Value);
            //x = up/down, y = tilt, z = pivot out
            Vector3 inverseRotation = new Vector3(5.0f, 25.0f, 5.0f);
            Quaternion targetQuaternion = Quaternion.Euler(targetRotation);
            Quaternion miniTargetQuaternion = Quaternion.Euler(new Vector3(5.0f, -90.0f, -5.0f));
            Quaternion inverseQuaternion = Quaternion.Euler(inverseRotation);
            float aimSpeed = (float)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_9").GetValue(__instance);
            Quaternion currentRotation = (Quaternion)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "quaternion_1").GetValue(__instance);

            //for setting baseline position
            /*            __instance.HandsContainer.WeaponRoot.localPosition = Plugin.WeapStartPosition + new Vector3(Plugin.offsetX.Value, Plugin.offsetY.Value, Plugin.offsetZ.Value);
            */
            if (Plugin.IsPassiveAiming == true)
            {
                currentRotation = Quaternion.Lerp(currentRotation, targetQuaternion, __instance.CameraSmoothTime * aimSpeed * dt * Plugin.rotationMulti.Value);
                AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "quaternion_1").SetValue(__instance, currentRotation);

                if (__instance.HandsContainer.TrackingTransform.localPosition.x > Plugin.TargetPosition.x)
                {
                    currentRotation = Quaternion.Lerp(currentRotation, miniTargetQuaternion, __instance.CameraSmoothTime * aimSpeed * dt * Plugin.rotationMulti.Value * 1.2f);
                    AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "quaternion_1").SetValue(__instance, currentRotation);
                    Logger.LogWarning("changeSpeedMulti = " + changeSpeedMulti);
                    changeSpeedMulti += Plugin.changeTimeIncrease.Value;
                    Vector3 currentPos = __instance.HandsContainer.TrackingTransform.localPosition + new Vector3(-0.01f * changeSpeedMulti * aimSpeed, 0.0f, 0.0f);
                    __instance.HandsContainer.TrackingTransform.localPosition = currentPos;

                }
            }
            else
            {

                if (__instance.HandsContainer.TrackingTransform.localPosition.x != Plugin.WeapStartPosition.x)
                {
                    currentRotation = Quaternion.Lerp(currentRotation, inverseQuaternion, __instance.CameraSmoothTime * aimSpeed * dt * Plugin.rotationMulti.Value * 1.2f);
                    AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "quaternion_1").SetValue(__instance, currentRotation);
                    Logger.LogWarning("reseting");
                    changeSpeedMulti = Plugin.changeTimeMult.Value;
                    resetSpeedMulti += Plugin.restTimeIncrease.Value;
                    Vector3 currentPos = __instance.HandsContainer.TrackingTransform.localPosition + new Vector3(0.01f * resetSpeedMulti * aimSpeed, 0.0f, 0.0f);
                    __instance.HandsContainer.TrackingTransform.localPosition = currentPos;
                }
                if (__instance.HandsContainer.TrackingTransform.localPosition.x > Plugin.WeapStartPosition.x)
                {
                    Logger.LogWarning("final reset");
                    resetSpeedMulti = Plugin.resetTimeMult.Value;
                    __instance.HandsContainer.TrackingTransform.localPosition = new Vector3(0.0f, 0.0f, 0.0f);
                }

            }

            return true;
            /*           Vector3 targetPositiion = new Vector3(Plugin.offsetX.Value, Plugin.offsetY.Value, Plugin.offsetZ.Value);
                       Vector3 targetRotation = new Vector3(Plugin.rotationX.Value, Plugin.rotationY.Value, Plugin.rotationZ.Value);
                       Quaternion targetQuaternion = Quaternion.Euler(targetRotation);

                       *//*                __instance.HandsContainer.WeaponRootAnim.rotation = Quaternion.Euler(targetRotation);
                                       __instance.HandsContainer.WeaponRootAnim.localRotation = Quaternion.Euler(targetRotation);
                                       __instance.HandsContainer.WeaponRootAnim.localPosition = new Vector3(Plugin.offsetX.Value, Plugin.offsetY.Value, Plugin.offsetZ.Value);
                                       __instance.HandsContainer.WeaponRootAnim.position = new Vector3(Plugin.offsetX.Value, Plugin.offsetY.Value, Plugin.offsetZ.Value);*//*


                       float float_21 = (float)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_21").GetValue(__instance);
                       float float_13 = (float)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_13").GetValue(__instance);
                       float float_14 = (float)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_14").GetValue(__instance);
                       float aimSpeed = (float)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_9").GetValue(__instance);
                       Quaternion quaternion_1 = (Quaternion)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "quaternion_1").GetValue(__instance);
                       Quaternion quaternion_2 = (Quaternion)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "quaternion_2").GetValue(__instance);
                       bool bool_1 = (bool)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "bool_1").GetValue(__instance);
                       float Single_3 = (float)AccessTools.Property(typeof(EFT.Animations.ProceduralWeaponAnimation), "Single_3").GetValue(__instance);

                       Vector3 vector = __instance.HandsContainer.HandsRotation.Get();
                       Vector3 value = __instance.HandsContainer.SwaySpring.Value;
                       vector += float_21 * (bool_1 ? __instance.AimingDisplacementStr : 1f) * new Vector3(value.x, 0f, value.z);
                       vector += value;
                       Vector3 position = __instance._shouldMoveWeaponCloser ? __instance.HandsContainer.RotationCenterWoStock : __instance.HandsContainer.RotationCenter;
                       Vector3 worldPivot = __instance.HandsContainer.WeaponRootAnim.TransformPoint(position);

                       AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "vector3_4").SetValue(__instance, __instance.HandsContainer.WeaponRootAnim.position);
                       //these are probably supposed to be base values, setting them to my target doesn;t make ense.
                       AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "quaternion_5").SetValue(__instance, __instance.HandsContainer.WeaponRootAnim.localRotation);
                       AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "quaternion_6").SetValue(__instance, __instance.HandsContainer.WeaponRootAnim.rotation);

                       Quaternion quaternion_6 = (Quaternion)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "quaternion_6").GetValue(__instance);
                       Vector3 vector3_4 = (Vector3)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "vector3_4").GetValue(__instance);
                       Vector3 vector3_6 = (Vector3)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "vector3_6").GetValue(__instance);

                       __instance.DeferredRotateWithCustomOrder(__instance.HandsContainer.WeaponRootAnim, worldPivot, vector);
                       Vector3 vector2 = __instance.HandsContainer.Recoil.Get();
                       if (vector2.magnitude > 1E-45f)
                       {
                           if (float_13 < 1f && __instance.ShotNeedsFovAdjustments)
                           {
                               vector2.x = Mathf.Atan(Mathf.Tan(vector2.x * 0.017453292f) * float_13) * 57.29578f;
                               vector2.z = Mathf.Atan(Mathf.Tan(vector2.z * 0.017453292f) * float_13) * 57.29578f;
                           }
                           Vector3 worldPivot2 = vector3_4 + quaternion_6 * __instance.HandsContainer.RecoilPivot;
                           __instance.DeferredRotate(__instance.HandsContainer.WeaponRootAnim, worldPivot2, quaternion_6 * vector2);
                       }
                       __instance.ApplyAimingAlignment(dt);
                       if (Input.GetKey(KeyCode.U))
                       {
                           Logger.LogWarning("vector3_4" + vector3_4);
                           quaternion_1 = Quaternion.Lerp(quaternion_1, targetQuaternion, __instance.CameraSmoothTime * aimSpeed * dt);
                           AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "quaternion_1").SetValue(__instance, quaternion_1);
                           Quaternion rhs = Quaternion.Euler(float_14 * Single_3 * vector3_6);
                           __instance.HandsContainer.WeaponRootAnim.SetPositionAndRotation(vector3_4, quaternion_6 * rhs * quaternion_1);
                           return false;
                       }
                       else
                       {
                           Logger.LogWarning("else");
                           quaternion_1 = Quaternion.Lerp(quaternion_1, __instance.IsAiming ? quaternion_2 : Quaternion.identity, __instance.CameraSmoothTime * aimSpeed * dt);
                           Quaternion rhs = Quaternion.Euler(float_14 * Single_3 * vector3_6);
                           AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "quaternion_1").SetValue(__instance, quaternion_1);
                           __instance.HandsContainer.WeaponRootAnim.SetPositionAndRotation(vector3_4, quaternion_6 * rhs * quaternion_1);
                       }
                       return true;*/

        }
    }

    public class method_20Patch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(EFT.Animations.ProceduralWeaponAnimation).GetMethod("method_20", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        [PatchPostfix]
        private static void PatchPostfix(ref EFT.Animations.ProceduralWeaponAnimation __instance)
        {
            Player.FirearmController firearmController = (Player.FirearmController)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "firearmController_0").GetValue(__instance);

            if (firearmController != null)
            {
                Player player = (Player)AccessTools.Field(typeof(EFT.Player.FirearmController), "_player").GetValue(firearmController);
                if (player.IsYourPlayer == true)
                {
                    float baseAimSpeed = WeaponProperties.AimSpeed * PlayerProperties.ADSInjuryMulti;
                    Mod currentAimingMod = (player.ProceduralWeaponAnimation.CurrentAimingMod != null) ? player.ProceduralWeaponAnimation.CurrentAimingMod.Item as Mod : null;
                    float sightSpeedModi = (currentAimingMod != null) ? AttachmentProperties.AimSpeed(currentAimingMod) : 1;
                    float newAimSpeed = baseAimSpeed * (1 + (sightSpeedModi / 100f));
                    Logger.LogWarning("aimSpeed = " + newAimSpeed);
                    AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_9").SetValue(__instance, newAimSpeed); //aimspeed

                    float ergoWeight = WeaponProperties.ErgonomicWeight * PlayerProperties.ErgoDeltaInjuryMulti * PlayerProperties.StrengthSkillAimBuff;
                    float ergoWeightFactor = StatCalc.ProceduralIntensityFactorCalc(ergoWeight, 6f);
                    float breathIntensity;
                    float handsIntensity;

                    if (WeaponProperties.HasShoulderContact == false)
                    {
                        breathIntensity = Mathf.Min(0.75f * ergoWeightFactor, 0.9f);
                        handsIntensity = Mathf.Min(0.75f * ergoWeightFactor, 0.95f);
                    }
                    else if (firearmController.Item.WeapClass == "pistol" && WeaponProperties.HasShoulderContact != true)
                    {
                        breathIntensity = Mathf.Min(0.58f * ergoWeightFactor, 0.75f);
                        handsIntensity = Mathf.Min(0.58f * ergoWeightFactor, 0.8f);
                    }
                    else
                    {
                        breathIntensity = Mathf.Min(0.55f * ergoWeightFactor, 0.85f);
                        handsIntensity = Mathf.Min(0.55f * ergoWeightFactor, 0.9f);
                    }

                    breathIntensity *= Plugin.SwayIntensity.Value;
                    handsIntensity *= Plugin.SwayIntensity.Value;

                    __instance.Shootingg.Intensity = Plugin.RecoilIntensity.Value;

                    __instance.Breath.Intensity = breathIntensity * __instance.IntensityByPoseLevel; //both aim sway and up and down breathing
                    __instance.HandsContainer.HandsRotation.InputIntensity = (__instance.HandsContainer.HandsPosition.InputIntensity = handsIntensity * handsIntensity); //also breathing and sway but different, the hands doing sway motion but camera bobbing up and down. 
                }
            }
            else
            {
                if (__instance.PointOfView == EPointOfView.FirstPerson)
                {
                    int AimIndex = (int)AccessTools.Property(typeof(EFT.Animations.ProceduralWeaponAnimation), "AimIndex").GetValue(__instance);
                    if (!__instance.Sprint && AimIndex < __instance.ScopeAimTransforms.Count)
                    {
                        Logger.LogWarning("Range finder sway");
                        __instance.Breath.Intensity = 0.5f * __instance.IntensityByPoseLevel;
                        __instance.HandsContainer.HandsRotation.InputIntensity = (__instance.HandsContainer.HandsPosition.InputIntensity = 0.5f * 0.5f);
                    }
                }
            }
        }
    }

    public class UpdateSwayFactorsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(EFT.Animations.ProceduralWeaponAnimation).GetMethod("UpdateSwayFactors", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPrefix]
        private static bool Prefix(ref EFT.Animations.ProceduralWeaponAnimation __instance)
        {
            Player.FirearmController firearmController = (Player.FirearmController)AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "firearmController_0").GetValue(__instance);

            if (firearmController != null)
            {
                Player player = (Player)AccessTools.Field(typeof(EFT.Player.FirearmController), "_player").GetValue(firearmController);

                if (player.IsYourPlayer == true)
                {
                    float ergoWeight = WeaponProperties.ErgonomicWeight * PlayerProperties.ErgoDeltaInjuryMulti * PlayerProperties.StrengthSkillAimBuff;
                    float weightFactor = StatCalc.ProceduralIntensityFactorCalc(ergoWeight, 6f);
                    float displacementModifier = 0.4f;//lower = less drag
                    float aimIntensity = Plugin.SwayIntensity.Value * 0.4f;

                    if (WeaponProperties.HasShoulderContact == false && firearmController.Item.WeapClass != "pistol")
                    {
                        aimIntensity = Plugin.SwayIntensity.Value * 1.1f;
                    }

                    float swayStrength = EFTHardSettings.Instance.SWAY_STRENGTH_PER_KG.Evaluate(ergoWeight * weightFactor);
                    AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_20").SetValue(__instance, swayStrength);

                    float weapDisplacement = EFTHardSettings.Instance.DISPLACEMENT_STRENGTH_PER_KG.Evaluate(ergoWeight * weightFactor);//delay from moving mouse to the weapon moving to center of screen.
                    AccessTools.Field(typeof(EFT.Animations.ProceduralWeaponAnimation), "float_21").SetValue(__instance, weapDisplacement * weightFactor * displacementModifier);

                    __instance.MotionReact.SwayFactors = new Vector3(swayStrength, __instance.IsAiming ? (swayStrength * 0.3f) : swayStrength, swayStrength) * Mathf.Clamp(aimIntensity * weightFactor, aimIntensity, 1.1f); // the diving/tiling animation as you move weapon side to side.

                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return true;
            }
        }
    }
}
