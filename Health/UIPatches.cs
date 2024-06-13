using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Aki.Reflection.Patching;
using EFT;
using EFT.UI;
using UnityEngine;

namespace RealismMod
{
    public class HealthEffectIconsPatch : ModulePatch
    {
        protected override MethodBase GetTargetMethod()
        {
            return typeof(StaticIcons.EffectSprites).GetMethod("UnityEngine.ISerializationCallbackReceiver.OnAfterDeserialize", BindingFlags.Instance | BindingFlags.Public);
        }

        [PatchPostfix]
        private static void Postfix(StaticIcons.EffectSprites __instance)
        {
            __instance.EffectIcons.Add(typeof(IHealthRegen), __instance.StimulatorBuff);
        }
    }
}
