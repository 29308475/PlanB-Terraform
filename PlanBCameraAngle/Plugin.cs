using System.Collections.Generic;
using System.Reflection.Emit;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace PlanBCameraAngle
{
    [BepInPlugin("tony.planb.cameraangle", "Plan B Camera Angle", "0.1.5")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        internal ConfigEntry<float> Sensitivity;
        internal float Yaw, Tilt;
        internal float NativeTilt;
        private bool dragging;
        private Vector3 lastMouse;
        private Harmony harmony;

        private void Awake()
        {
            Instance = this;
            Sensitivity = Config.Bind("Camera", "Sensitivity", 0.25f,
                new ConfigDescription("Degrees per mouse pixel.", new AcceptableValueRange<float>(0.01f, 2f)));
            harmony = new Harmony("tony.planb.cameraangle");
            harmony.PatchAll();
            Logger.LogInfo("Camera Angle loaded: hold middle mouse and drag; Home resets view.");
        }

        internal static bool CanRotate()
        {
            return GCamera.camera != null && SMain.Inst != null && !SMain.Inst.IsPauseInput()
                && !SMain.Inst.IsPausedInMenu() && !GGame.isAnimalMode
                && SSceneSingleton<SSceneCinematic>.Inst != null
                && !SSceneSingleton<SSceneCinematic>.Inst.IsCameraOverrided()
                && !SSceneSingleton<SSceneCinematic>.Inst.CameraFullControlTaken();
        }

        internal void ReadMouse()
        {
            if (!CanRotate()) { dragging = false; return; }
            if (Input.GetKeyDown(KeyCode.Home)) { Yaw = Tilt = 0f; }
            if (Input.GetMouseButtonDown(2))
            {
                dragging = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
                lastMouse = Input.mousePosition;
            }
            if (!Input.GetMouseButton(2)) { dragging = false; return; }
            if (!dragging) return;
            Vector3 delta = Input.mousePosition - lastMouse;
            lastMouse = Input.mousePosition;
            Yaw = Mathf.Repeat(Yaw + delta.x * Sensitivity.Value + 180f, 360f) - 180f;
            // Discard overshoot before applying the next delta, so reversing at
            // either visible limit responds immediately, including after zooming.
            Tilt = ClampTilt(Tilt, NativeTilt);
            Tilt = ClampTilt(Tilt - delta.y * Sensitivity.Value, NativeTilt);
        }

        internal static float ClampTilt(float offset, float nativeTilt)
        {
            return Mathf.Clamp(offset, 5f - nativeTilt, 80f - nativeTilt);
        }

        private void OnDestroy()
        {
            harmony?.UnpatchSelf();
            if (Instance == this) Instance = null;
        }
    }

    [HarmonyPatch(typeof(SCamera), "Update")]
    internal static class ReadInputPatch
    {
        static void Prefix() { Plugin.Instance?.ReadMouse(); }

        // Rotate the smoothed keyboard vector before the native speed and spherical
        // coordinate conversion. The game's acceleration, Shift and Ctrl still apply.
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var scroll = AccessTools.Field(typeof(SCamera), "_scrollKeysValue");
            var speed = AccessTools.Field(typeof(GCamera), "scrollSpeed");
            var code = new List<CodeInstruction>(instructions);
            int matches = 0;
            for (int i = 0; i < code.Count; i++)
            {
                var instruction = code[i];
                yield return instruction;
                if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, scroll)
                    && i + 1 < code.Count && code[i + 1].opcode == OpCodes.Ldsfld
                    && Equals(code[i + 1].operand, speed))
                {
                    matches++;
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(ReadInputPatch), nameof(RotateMovement)));
                }
            }
            if (matches != 1)
                throw new System.InvalidOperationException("Camera keyboard movement layout changed: " + matches);
        }

        internal static Vector2 RotateMovement(Vector2 movement)
        {
            if (Plugin.Instance == null || !Plugin.CanRotate()) return movement;
            float radians = Plugin.Instance.Yaw * Mathf.Deg2Rad;
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            return new Vector2(cos * movement.x + sin * movement.y,
                -sin * movement.x + cos * movement.y);
        }
    }

    [HarmonyPatch(typeof(SMouse), "Update")]
    internal static class MouseMovementPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var division = AccessTools.Method(typeof(Vector3), "op_Division", new[] { typeof(Vector3), typeof(float) });
            var conversion = AccessTools.Method(typeof(Vector2), "op_Implicit", new[] { typeof(Vector3) });
            var height = AccessTools.Method(typeof(Screen), "get_height");
            var angle = AccessTools.Field(typeof(GCamera), "camAngleCurrent");
            int matches = 0;
            for (int i = 0; i < code.Count; i++)
            {
                yield return code[i];
                // Only the normalized mouse drag delta, leaving world picking and zoom untouched.
                if (i >= 3 && code[i].Calls(conversion) && code[i - 1].Calls(division)
                    && code[i - 3].Calls(height))
                {
                    matches++;
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(ReadInputPatch), nameof(ReadInputPatch.RotateMovement)));
                }
                if (code[i].opcode == OpCodes.Ldsfld && Equals(code[i].operand, angle))
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(MouseMovementPatch), nameof(EffectiveTilt)));
            }
            if (matches != 1)
                throw new System.InvalidOperationException("Mouse drag layout changed: " + matches);
        }

        static float EffectiveTilt(float nativeTilt)
        {
            if (Plugin.Instance == null || !Plugin.CanRotate()) return nativeTilt;
            return Mathf.Clamp(nativeTilt + Plugin.Instance.Tilt, 5f, 80f);
        }
    }

    [HarmonyPatch(typeof(SCamera), "Reset")]
    internal static class ResetPatch
    {
        static void Postfix() { if (Plugin.Instance != null) Plugin.Instance.Yaw = Plugin.Instance.Tilt = 0f; }
    }

    [HarmonyPatch(typeof(SCamera), "SetCameraOnFocus")]
    internal static class OrbitPatch
    {
        static void Postfix(angles forcedFocus)
        {
            var plugin = Plugin.Instance;
            if (plugin == null || !Plugin.CanRotate()) return;
            var focus = forcedFocus != default(angles) ? forcedFocus : GCamera.focusPosition;
            Vector3 pivot = focus.GetPos();
            Vector3 up = focus.GetPosNormalized();
            Transform camera = GCamera.camTransform;
            // Orbit the native camera around the focus, preserving its distance and zoom.
            float baseTilt = Vector3.Angle(camera.position - pivot, up);
            plugin.NativeTilt = baseTilt;
            if (plugin.Yaw == 0f && plugin.Tilt == 0f) return;
            plugin.Tilt = Plugin.ClampTilt(plugin.Tilt, baseTilt);
            // Compose both orbits before touching the Transform. RotateAround
            // writes position and rotation separately for each axis.
            Quaternion orbit = Quaternion.AngleAxis(plugin.Yaw, up)
                * Quaternion.AngleAxis(plugin.Tilt, camera.right);
            camera.SetPositionAndRotation(pivot + orbit * (camera.position - pivot),
                orbit * camera.rotation);
        }
    }

}
