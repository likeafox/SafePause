using System;
#if HARMONY_1_2
using Harmony;
#elif HARMONY_2_0
using HarmonyLib;
#endif
using Verse;
using RimWorld;
using System.Diagnostics;
using UnityEngine;

namespace SafePause
{
    public class SafePauseSettings : ModSettings
    {
        private static int _pause_timeout = 1500;
        private static int _input_timeout = 400;
        public static long pause_timeout { get { return _pause_timeout; } }
        public static long input_timeout { get { return _input_timeout; } }
        public static long max_timeout { get { return Math.Max(pause_timeout, input_timeout); } }

        private static string _pause_timeout_buffer;
        private static string _input_timeout_buffer;

        public static void DoSettingsWindowContents(Rect inRect)
        {
            _pause_timeout_buffer = _pause_timeout.ToString();
            _input_timeout_buffer = _input_timeout.ToString();
            var controls = new Listing_Standard();
            controls.Begin(inRect);
            controls.TextFieldNumericLabeled("Pause timeout (in milliseconds): ", ref _pause_timeout, ref _pause_timeout_buffer);
            controls.TextFieldNumericLabeled("Button press timeout (in milliseconds): ", ref _input_timeout, ref _input_timeout_buffer);
            controls.End();
        }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref _pause_timeout, "PauseTimeout", 1500);
            Scribe_Values.Look(ref _input_timeout, "InputTimeout", 400);
        }
    }

    public class SafePause : Verse.Mod
    {
        public static Stopwatch last_pause = Stopwatch.StartNew();
        public static Stopwatch last_unpause_input = Stopwatch.StartNew();
        public static TimeSpeed speed_this_tick;

        public SafePause(ModContentPack content) : base(content)
        {
            GetSettings<SafePauseSettings>();
            string mod_id = "likeafox.rimworld.safepause";
#if HARMONY_1_2
            var harmony = HarmonyInstance.Create(mod_id);
#elif HARMONY_2_0
            var harmony = new Harmony(mod_id);
#endif
            harmony.PatchAll(System.Reflection.Assembly.GetExecutingAssembly());
        }

        public override string SettingsCategory() => "SafePause";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            base.DoSettingsWindowContents(inRect);
            SafePauseSettings.DoSettingsWindowContents(inRect);
        }

        public static long MillisecondsUntilCanUnpause()
        {
            return Math.Max(Math.Max(0, SafePauseSettings.pause_timeout - last_pause.ElapsedMilliseconds),
                SafePauseSettings.input_timeout - last_unpause_input.ElapsedMilliseconds);
        }
    }

    [Verse.StaticConstructorOnStartup]
    [HarmonyPatch(typeof(TimeControls), "DoTimeControlsGUI")]
    class TimeControls_DoTimeControlsGUI_Patch
    {
        private static Texture2D TimeoutTex;
        static TimeControls_DoTimeControlsGUI_Patch()
        {
            TimeoutTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.5f, 0.5f, 0.93f, 1f));
        }

        static void Prefix()
        {
            SafePause.speed_this_tick = Find.TickManager.CurTimeSpeed;
        }

        static void Postfix(Rect timerRect)
        {
            long until_unpause = SafePause.MillisecondsUntilCanUnpause();
            TickManager mgr = Find.TickManager;
            if (SafePause.speed_this_tick == TimeSpeed.Paused
                && mgr.CurTimeSpeed != TimeSpeed.Paused)
            {
                if (until_unpause != 0)
                    mgr.CurTimeSpeed = TimeSpeed.Paused;
                SafePause.last_unpause_input = Stopwatch.StartNew();
            }
            if (until_unpause != 0 && mgr.CurTimeSpeed == TimeSpeed.Paused)
            {
                Vector2 line_start = timerRect.position;
                line_start.x += TimeControls.TimeButSize.x;
                line_start.y += TimeControls.TimeButSize.y / 2f;
                float total_width = TimeControls.TimeButSize.x * 3;
                Widgets.DrawLineHorizontal(line_start.x, line_start.y, total_width);
                float timeout_ratio = 1f - ((float)until_unpause / (float)SafePauseSettings.max_timeout);
                GUI.DrawTexture(new Rect(line_start.x, line_start.y, total_width * timeout_ratio, 1f), TimeoutTex);
            }
        }
    }

    [HarmonyPatch(typeof(TickManager),"TogglePaused")]
    class TickManager_TogglePaused_Patch
    {
        static void Postfix(TickManager __instance)
        {
            if (__instance.Paused)
                SafePause.last_pause = Stopwatch.StartNew();
        }
    }

    [HarmonyPatch(typeof(TimeControls), "PlaySoundOf")]
    class TimeControls_PlaySoundOf_Patch
    {
        static bool Prefix()
        {
            return (SafePause.speed_this_tick != TimeSpeed.Paused
                || SafePause.MillisecondsUntilCanUnpause() == 0);
        }
    }
}
