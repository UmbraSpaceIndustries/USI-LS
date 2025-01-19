using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using KSP.UI.Screens;
using UnityEngine;

namespace LifeSupport
{
    [KSPAddon(KSPAddon.Startup.SpaceCentre, false)]
    public class LifeSupportMonitor_SpaceCenter : MonoBehaviour
    {
        private ApplicationLauncherButton orbLogButton;
        private Rect _windowPosition = new Rect(300, 60, 500, 560);
        private GUIStyle _windowStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _smButtonStyle;
        private GUIStyle _toggleStyle;
        private bool _hasInitStyles = false;
        public static bool renderDisplay = false;

        private LifeSupportConfig config;


        void Awake()
        {
            var texture = new Texture2D(36, 36, TextureFormat.RGBA32, false);
            var textureFile = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                "Assets",
                "Supplies.png");
            print("Loading " + textureFile);
            texture.LoadImage(File.ReadAllBytes(textureFile));
            this.orbLogButton = ApplicationLauncher.Instance.AddModApplication(GuiOn, GuiOff, null, null, null, null,
                ApplicationLauncher.AppScenes.ALWAYS, texture);
        }

        private void GuiOn()
        {
            renderDisplay = true;
            //Load initial values
            config = LifeSupportScenario.Instance.settings.GetSettings();
            supplyTime = string.Format("{0:0.########}", config.SupplyTime);
            ecTime = string.Format("{0:0.########}", config.ECTime);
            evaTime = string.Format("{0:0.########}", config.EVATime);
            ecAmount = string.Format("{0:0.########}", config.ECAmount);
            supplyAmount = string.Format("{0:0.########}", config.SupplyAmount);
            wasteAmount = string.Format("{0:0.########}", config.WasteAmount);
            wearAmount = string.Format("{0:0.########}", config.ReplacementPartAmount);
            supNoVet = config.NoSupplyEffect;
            supVet = config.NoSupplyEffectVets;
            habNoVet = config.NoHomeEffect;
            habVet = config.NoHomeEffectVets;
            ecNoVet = config.NoECEffect;
            ecVet = config.NoECEffectVets;
            evaNoVet = config.EVAEffect;
            evaVet = config.EVAEffectVets;
            habMulti = string.Format("{0:0.########}", config.HabMultiplier);
            enableRecyclers = config.EnableRecyclers;
            scoutHabTime = config.ScoutHabTime.ToString();
            permaHabTime = config.PermaHabTime.ToString();
            habRange = config.HabRange.ToString();
            homeAltitude = config.HomeWorldAltitude.ToString();
            baseHabTime = config.BaseHabTime.ToString();
            recoverySpeed = config.RecoverySpeed.ToString();
            vetNames = config.VetNames;
        }

        public void Start()
        {
            if (!_hasInitStyles)
                InitStyles();
        }

        private void GuiOff()
        {
            renderDisplay = false;
        }

        private void OnGUI()
        {
            if (!renderDisplay)
                return;
            if (HighLogic.LoadedScene != GameScenes.SPACECENTER)
                return;

            if (Event.current.type == EventType.Repaint || Event.current.isMouse)
            {
                //preDrawQueue
            }
            Ondraw();
        }


        private void Ondraw()
        {
            _windowPosition = GUILayout.Window(10, _windowPosition, OnWindow, "Life Support Setup", _windowStyle);
        }

        private void OnWindow(int windowId)
        {
            GenerateWindow();
        }

        private string supplyTime;
        private string ecTime;
        private int ecNoVet;
        private int ecVet;
        private string evaTime;
        private string ecAmount;
        private string supplyAmount;
        private string wasteAmount;
        private string wearAmount;
        private int supNoVet;
        private int supVet;
        private int habNoVet;
        private int habVet;
        private int evaNoVet;
        private int evaVet;
        private string habMulti;
        private bool enableRecyclers;
        private string permaHabTime;
        private string scoutHabTime;
        private string habRange;
        private string homeAltitude;
        private string baseHabTime;
        private string recoverySpeed;
        private string vetNames;

        private void GenerateWindow()
        {
            if (!renderDisplay)
                return;

            if (string.IsNullOrEmpty(supplyTime)) //Guard clause for nulls
                GuiOn();
            try
            {
                var effectStrings = new[] {"none", "grouchy", "mutiny", "return", "M.I.A.", "K.I.A."};
                GUILayout.BeginVertical();
                // Colors
                string textColor = "FFFFFF";

                // column widths
                const int c1 = 100;
                const int c2 = 100;
                const int c3 = 350;
                const int c4 = 40;
                const int c5 = 150;
                const int c6 = 20;
                const int c7 = 70;
                // LABELS
                GUILayout.BeginHorizontal();
                GUILayout.Label(CTag("See Wiki for config documentation", textColor), _labelStyle, GUILayout.Width(c3));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Supply Time:", _labelStyle, GUILayout.Width(c1));
                supplyTime = GUILayout.TextField(supplyTime, 10, GUILayout.Width(c2));
                GUILayout.Label("", _labelStyle, GUILayout.Width(c4));
                GUILayout.Label("EC Time:", _labelStyle, GUILayout.Width(c1));
                ecTime = GUILayout.TextField(ecTime, 10, GUILayout.Width(c2));
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("EC Amount:", _labelStyle, GUILayout.Width(c1));
                ecAmount = GUILayout.TextField(ecAmount, 10, GUILayout.Width(c2));
                GUILayout.Label("", _labelStyle, GUILayout.Width(c4));
                GUILayout.Label("Supply Amount:", _labelStyle, GUILayout.Width(c1));
                supplyAmount = GUILayout.TextField(supplyAmount, 10, GUILayout.Width(c2));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("EVA Time:", _labelStyle, GUILayout.Width(c1));
                evaTime = GUILayout.TextField(evaTime, 10, GUILayout.Width(c2));
                GUILayout.EndHorizontal();


                GUILayout.BeginHorizontal();
                GUILayout.Label("Waste Amount:", _labelStyle, GUILayout.Width(c1));
                wasteAmount = GUILayout.TextField(wasteAmount, 10, GUILayout.Width(c2));
                GUILayout.Label("", _labelStyle, GUILayout.Width(c4));
                GUILayout.Label("Wear Amount:", _labelStyle, GUILayout.Width(c1));
                wearAmount = GUILayout.TextField(wearAmount, 10, GUILayout.Width(c2));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Supply Effect (Non-Vet):", _labelStyle, GUILayout.Width(c5));
                supNoVet = GUILayout.SelectionGrid(supNoVet, effectStrings, 6, _smButtonStyle);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Supply Effect (Vet):", _labelStyle, GUILayout.Width(c5));
                supVet = GUILayout.SelectionGrid(supVet, effectStrings, 6, _smButtonStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("EC Effect (Non-Vet):", _labelStyle, GUILayout.Width(c5));
                ecNoVet = GUILayout.SelectionGrid(ecNoVet, effectStrings, 6, _smButtonStyle);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("EC Effect (Vet):", _labelStyle, GUILayout.Width(c5));
                ecVet = GUILayout.SelectionGrid(ecVet, effectStrings, 6, _smButtonStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("EVA Effect (Non-Vet):", _labelStyle, GUILayout.Width(c5));
                evaNoVet = GUILayout.SelectionGrid(evaNoVet, effectStrings, 6, _smButtonStyle);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("EVA Effect (Vet):", _labelStyle, GUILayout.Width(c5));
                evaVet = GUILayout.SelectionGrid(evaVet, effectStrings, 6, _smButtonStyle);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Hab Effect (Non-Vet):", _labelStyle, GUILayout.Width(c5));
                habNoVet = GUILayout.SelectionGrid(habNoVet, effectStrings, 6, _smButtonStyle);
                GUILayout.EndHorizontal();
                GUILayout.BeginHorizontal();
                GUILayout.Label("Hab Effect (Vet):", _labelStyle, GUILayout.Width(c5));
                habVet = GUILayout.SelectionGrid(habVet, effectStrings, 6, _smButtonStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Hab Multiplier:", _labelStyle, GUILayout.Width(c1));
                habMulti = GUILayout.TextField(habMulti, 3, GUILayout.Width(c4));
                GUILayout.Label("", _labelStyle, GUILayout.Width(c6));
                GUILayout.Label("Cabin Months:", _labelStyle, GUILayout.Width(90));
                baseHabTime = GUILayout.TextField(baseHabTime, 10, GUILayout.Width(c4));
                GUILayout.Label("", _labelStyle, GUILayout.Width(c6));
                GUILayout.Label("Hab Range:", _labelStyle, GUILayout.Width(c7));
                habRange = GUILayout.TextField(habRange, 4, GUILayout.Width(c4));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Recovery Speed:", _labelStyle, GUILayout.Width(c1));
                recoverySpeed = GUILayout.TextField(recoverySpeed, 4, GUILayout.Width(c4));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Homeworld Altitude:", _labelStyle, GUILayout.Width(130));
                homeAltitude = GUILayout.TextField(homeAltitude, 8, GUILayout.Width(c7));
                enableRecyclers = GUILayout.Toggle(enableRecyclers, "Enable Recyclers", _toggleStyle);
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Scout Hab Time:", _labelStyle, GUILayout.Width(110));
                scoutHabTime = GUILayout.TextField(scoutHabTime, 12, GUILayout.Width(100));
                GUILayout.Label("Perma-Hab Time:", _labelStyle, GUILayout.Width(110));
                permaHabTime = GUILayout.TextField(permaHabTime, 12, GUILayout.Width(100));
                GUILayout.EndHorizontal();

                GUILayout.Label("Vet Names (separate with commas, first name only):", _labelStyle, GUILayout.Width(c3));
                vetNames = GUILayout.TextField(vetNames, 100, GUILayout.Width(c3));
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Save"))
                    SaveSettings(config);
                if (GUILayout.Button("Cancel"))
                    GuiOff();
                GUILayout.EndHorizontal();
            }
            catch (Exception ex)
            {
                Debug.Log("Error rendering USI-LS window: " + ex.ToString());
            }
            finally
            {
                GUILayout.EndVertical();
                GUI.DragWindow();
            }
        }

        private void SaveSettings(LifeSupportConfig config)
        {
            config.SupplyTime = SaveFloat(config.SupplyTime, supplyTime);
            config.ECTime = SaveFloat(config.ECTime, ecTime);
            config.EVATime = SaveFloat(config.EVATime, evaTime);
            config.ECAmount = SaveFloat(config.ECAmount, ecAmount);
            config.SupplyAmount = SaveFloat(config.SupplyAmount, supplyAmount);
            config.WasteAmount = SaveFloat(config.WasteAmount, wasteAmount);
            config.ReplacementPartAmount = SaveFloat(config.ReplacementPartAmount, wearAmount);
            config.EVAEffect = evaNoVet;
            config.EVAEffectVets = evaVet;
            config.NoHomeEffect = habNoVet;
            config.NoHomeEffectVets = habVet;
            config.NoSupplyEffect = supNoVet;
            config.NoSupplyEffectVets = supVet;

            config.NoECEffect = ecNoVet;
            config.NoECEffectVets = ecVet;
            config.ScoutHabTime = SaveDouble(config.ScoutHabTime, scoutHabTime);
            config.PermaHabTime = SaveDouble(config.PermaHabTime, permaHabTime);

            config.HabMultiplier = SaveInt(config.HabMultiplier,habMulti);
            config.EnableRecyclers = enableRecyclers;
            config.HabRange = SaveDouble(config.HabRange,habRange);
            config.HomeWorldAltitude = SaveInt(config.HomeWorldAltitude,homeAltitude);
            config.BaseHabTime = SaveDouble(config.BaseHabTime,baseHabTime);
            config.RecoverySpeed = SaveFloat(config.RecoverySpeed,recoverySpeed);
            config.VetNames = vetNames;
            LifeSupportScenario.Instance.settings.SaveConfig(config);
            GuiOff();
        }

        private double SaveDouble(double curVal, string strVal)
        {
            var newVal = curVal;
            double.TryParse(strVal, out newVal);
            return newVal;
        }

        private int SaveInt(int curVal, string strVal)
        {
            var newVal = curVal;
            int.TryParse(strVal, out newVal);
            return newVal;
        }

        private float SaveFloat(float curVal, string strVal)
        {
            var newVal = curVal;
            float.TryParse(strVal, out newVal);
            return newVal;
        }


        internal void OnDestroy()
        {
            if (orbLogButton == null)
                return;
            ApplicationLauncher.Instance.RemoveModApplication(orbLogButton);
            orbLogButton = null;
        }

        private void InitStyles()
        {
            _windowStyle = new GUIStyle(HighLogic.Skin.window);
            _windowStyle.fixedWidth = _windowPosition.width;
            _windowStyle.fixedHeight = _windowPosition.height;
            _labelStyle = new GUIStyle(HighLogic.Skin.label);
            _toggleStyle = new GUIStyle(HighLogic.Skin.toggle);
            _smButtonStyle = new GUIStyle(HighLogic.Skin.button);
            _smButtonStyle.fontSize = 10;
            _hasInitStyles = true;
        }

        private string CTag(string text, string colorHex)
        {
            return String.Format("<color=#{0}>{1}</color>", colorHex, text);
        }
    }
}
