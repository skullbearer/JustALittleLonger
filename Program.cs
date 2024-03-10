using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        #region mdk preserve
        // The following are the default values that will be installed into connectors.
        // Change these to change what goes into the CustomData, but beware...
        // Altering the header names or HOTname will break support of existing
        // configurations from this script.

        // These are the names of the configurable variables. DO NOT ALTER THE LET SIDE OF THIS ="
        string iniHeader = "JALLSTART";
        string TOSheader = "TimerOnConnect";
        string TOEheader = "TimerAtDisconnect";
        string Dheader = "UseThisDisplay";
        string HOTname = "HoldOnTimeInMinutes";

        // These are the DEFAULT values for the configurable variables.
        // These values will be installed ONLY if there is no configuration already present.
        string TOSname = "[JALLC]";
        string TOEname = "[JALLE]";
        string Dname = "[JALLD]";

        //This is in minutes and it's a float. You can math to seconds or god forbid, hours if need be.
        float HOTvalue = 30f;
        #endregion

        string JALLversion = "Just A Little Longer v0.2/nAuthored by Skullbearer/nInspired by Aerghabaegeck's whining.";

        List<ManagedConnector> ManagedConnectors = new List<ManagedConnector>();
        Dictionary<string, string> iniNames = new Dictionary<string, string>();
        Dictionary<IMyTextSurfaceProvider, List<ManagedConnector>> displayDictionary = new Dictionary<IMyTextSurfaceProvider, List<ManagedConnector>>();

        List<IMyTextSurfaceProvider> Displays = new List<IMyTextSurfaceProvider>();

        MyCommandLine commandLine = new MyCommandLine();

        MyIni ini = new MyIni();

        int tickCount;

        public Program()
        {
            UpdateBlocks(true); //Pull everything totally from scratch.
            InstallDictionary(ref iniNames); //Setup our minimum MyIni string variables Dictionary

            //If whatever is in the CustomData of the PB isn't compatible with MyIni, we'll wipe it.
            if (!ReadMeIniStorage())
            {
                if (!ReadMeIni()) UpdateIni(true);
                //If it IS compatible with MyIni, we'll just add anything missing for OUR script and preserve other script
                //configuration data so if the player is swapping scripts they don't lose settings for other MyIni based scripts.
                else {Me.CustomData = ini.ToString(); Storage = ini.ToString();}
            }
            else {Me.CustomData = ini.ToString(); Storage = ini.ToString();}

            tickCount = 0; //Reset our tick counter since we just recompiled or compiled.
        }

        public void Save()
        {
            Storage = ini.ToString();
        }

        public void Main(string argument, UpdateType updateSource)
        {
            if (commandLine.TryParse(argument))
            {
                if (commandLine.Argument(0) == "start")
                    InitiateScript();
                else if (commandLine.Argument(0) == "stop")
                    StopScript();
                else
                    RunScript();
            }
        }

        public void StopScript()
        {
            ManagedConnectors.Clear();
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }

        public void InitiateScript()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once;
            UpdateBlocks(true);
            tickCount = 0;
        }

        public void RunScript()
        {
            Runtime.UpdateFrequency = UpdateFrequency.Once;
            tickCount++;
            if (tickCount % 10 == 0)
            {
                ManagedConnector manCon;
                StringBuilder displayText = new StringBuilder();
                displayText.AppendLine(JALLversion);
                displayText.AppendLine("");
                for (int i = ManagedConnectors.Count - 1; i > -1; i--)
                {
                    manCon = ManagedConnectors[i];
                    if (manCon.IsDead())
                        ManagedConnectors.RemoveAtFast(i);
                    else manCon.CheckConnection(10);
                }
                if (tickCount < 0) //overflow situation
                    tickCount -= int.MinValue;
            }
            else if (tickCount % 601 == 0)
            {
                UpdateBlocks(false);
                Save();
            }
            if (tickCount % 60 == 0)
            {
                UpdateDisplays();
            }
        }

        public void UpdateBlocks(bool _totalRefresh)
        {
            List<IMyTerminalBlock> blocks = new List<IMyTerminalBlock>();
            GridTerminalSystem.GetBlocksOfType(blocks, b => b.CubeGrid == Me.CubeGrid);
            if (_totalRefresh)
            {
                ManagedConnectors.Clear();

                foreach (var b in blocks)
                {
                    if (b is IMyShipConnector)
                    {
                        ManagedConnector manCon = new ManagedConnector((IMyShipConnector)b, ini, iniHeader, iniNames);
                        ManagedConnectors.Add(manCon);
                        GrabGroupsForManagedConnector(ref manCon);
                    }
                }
            }
            else
            {
                for (int i = ManagedConnectors.Count - 1; i > -1; i--)
                {
                    ManagedConnector manCon = ManagedConnectors[i];
                    if (manCon.IsDead()) ManagedConnectors.RemoveAtFast(i);
                    else
                    {
                        manCon.Refresh();
                        GrabGroupsForManagedConnector(ref manCon);
                    }
                }
                if (!ReadMeIni())
                {
                    if (!ReadMeIniStorage())
                        UpdateIni(true);
                    Me.CustomData = ini.ToString();
                }
                else Storage = ini.ToString();
            }
            GridTerminalSystem.GetBlockGroupWithName(Dname).GetBlocksOfType(Displays);
        }
        public void UpdateDisplay(IMyTextSurfaceProvider _sp, string _showThis)
        {

            IMyTextSurface surf = _sp.GetSurface(0);
            if (surf.ContentType != ContentType.TEXT_AND_IMAGE)
                surf.ContentType = ContentType.TEXT_AND_IMAGE;
            surf.WriteText(_showThis);
        }

        public void UpdateDisplays()
        {
            StringBuilder dtxt = new StringBuilder();
            displayDictionary.Clear();
            foreach (var d in displayDictionary.Keys)
            {
                foreach (var manCon in ManagedConnectors)
                {
                    if (manCon.Displays.Contains(d)) displayDictionary[d].Add(manCon);
                    if (Displays.Count > 0)
                    {
                        foreach (var d2 in Displays)
                        {
                            if (!displayDictionary[d2].Contains(manCon)) displayDictionary[d2].Add(manCon);
                        }
                    }
                }
                if (displayDictionary[d].Count > 0)
                {
                    dtxt.AppendLine(JALLversion);
                    foreach (var manCon in displayDictionary[d])
                    {
                        dtxt.AppendLine(manCon.GetDisplayText());
                    }
                    UpdateDisplay(d, dtxt.ToString());
                }
            }
        }
        public void GrabGroupsForManagedConnector(ref ManagedConnector _manCon)
        {
            GridTerminalSystem.GetBlockGroupWithName(_manCon.IniNames[TOSname]).GetBlocksOfType(_manCon.TriggerOnStart);
            GridTerminalSystem.GetBlockGroupWithName(_manCon.IniNames[TOEname]).GetBlocksOfType(_manCon.TriggerOnEnd);
            GridTerminalSystem.GetBlockGroupWithName(_manCon.IniNames[Dname]).GetBlocksOfType(_manCon.Displays);
        }

        public void InstallDictionary(ref Dictionary<string,string> dic)
        {
            dic.Add(TOSheader, TOSname);
            dic.Add(TOEheader, TOEname);
            dic.Add(Dheader, Dname);
        }

        public void UpdateIni(bool _freshInstall = false)
        {

            if (_freshInstall)
            {
                ini.Clear();
                Me.CustomData = "";
                ini.AddSection(iniHeader);
                ini.Set(iniHeader, HOTname, HOTvalue);
                if (iniNames.Keys.Count > 0)
                {
                    foreach (var key in iniNames.Keys)
                    {
                        ini.Set(iniHeader, key, iniNames[key]);
                    }
                }
            }
            else ReadMeIni(true);
        }

        /// <summary>
        /// ReadMiIni(bool _allowThrow = false)
        /// Attempts to read the PB's CustomData for compatible configuration files.
        /// If MyIni format is present, it will pull any values that match our
        /// script configuration fields.
        /// </summary>
        /// <param name="_allowThrow"></param>
        /// <returns>Returns false if it could not read a MyIni format, true if it could.
        /// _allowThrow = true will allow an exception to be thrown if the
        /// CustomData is not MyIni compatible format.</returns>
        /// <exception cref="Exception">The Exception is produced by MyIni.TryParse()</exception>
        public bool ReadMeIni(bool _allowThrow = false)
        {
            ini.Clear();
            MyIniParseResult result = new MyIniParseResult();
            if (!ini.TryParse(Me.CustomData, out result))
            {
                if (_allowThrow) throw new Exception(result.ToString());
                return false;
            }
            HOTvalue = ini.Get(iniHeader, HOTname).ToSingle(-1f);
            foreach (var key in iniNames.Keys)
            {
                iniNames[key] = ini.Get(iniHeader, key).ToString();
            }
            return true;
        }
        /// <summary>
        /// ReadMiIniStorage()
        /// Attempts to read the PB's Storage string for compatible configuration files.
        /// If MyIni format is present, it will pull any values that match our
        /// script configuration fields.
        /// </summary>
        /// <param name="_allowThrow"></param>
        /// <returns>Returns false if it could not read a MyIni format, true if it could.
        /// _allowThrow = true will allow an exception to be thrown if the
        /// CustomData is not MyIni compatible format.</returns>
        public bool ReadMeIniStorage()
        {
            ini.Clear();
            MyIniParseResult result = new MyIniParseResult();
            if (!ini.TryParse(Storage, out result))
            {// If we can't read the Storage, it's not from our script, just junk it.
                if (result.ToString().Length > 0) Storage = "";
                return false; //We got nothing from Storage.
            }
            HOTvalue = ini.Get(iniHeader, HOTname).ToSingle(-1f);
            foreach (var key in iniNames.Keys)
            {
                iniNames[key] = ini.Get(iniHeader, key).ToString();
            }
            return true; //Storage was in MyIni format, doesn't mean we got anything for ourselves.
        }

        /// <summary>
        /// Contains various properties and methods for managing connectors.
        /// Includes the primary logic for when to reconnect, disconnect, and trigger timers.
        /// </summary>
        public class ManagedConnector
        {
            public IMyShipConnector Connector;

            public float HoldOnTime = -1f;
            public string HOTname = "HoldOnTime";
            private int ticksElapsed;
            private int ticksSinceTradeDisconnect;
            private const int TICKSDELAYRECONNECT = 10 * 6;
            public float TimeElapsed;
            public MyIni Ini = new MyIni();
            private string section;
            private bool isConnected;
            private const float TICKPERMIN = 60f*60f;

            public List<IMyTimerBlock> TriggerOnStart = new List<IMyTimerBlock>();
            public List<IMyTimerBlock> TriggerOnEnd = new List<IMyTimerBlock>();
            public List<IMyTextSurfaceProvider> Displays = new List<IMyTextSurfaceProvider>();

            public Dictionary<string, string> IniNames = new Dictionary<string, string>();

            /// <summary>
            /// Constructor for the ManagedConnectors class.
            /// </summary>
            /// <param name="_connector">The connector which is being managed.</param>
            /// <param name="_ini">The core, default MyIni from the main script.</param>
            /// <param name="_section">The unified section header for the MyIni.</param>
            /// <param name="_iniNames">The string field names for the MyIni.</param>
            public ManagedConnector(IMyShipConnector _connector, MyIni _ini, string _section, Dictionary<string, string> _iniNames)
            {
                Connector = _connector;
                Ini = _ini;
                section = _section;
                IniNames = _iniNames;
                InstallIni();
                isConnected = false;
            }
            /// <summary>
            /// Performs the primary logic of the ManagedConnector,
            /// such as when to reconnect, when to disconect, when to trigger timers.
            /// </summary>
            /// <param name="_ticksElapsed">The amount of game ticks since last called.</param>
            /// <returns>Returns true if the connector is connected AND attempting to maintain the connection. False if it is not connected or maintaining a connection.</returns>
            public bool CheckConnection(int _ticksElapsed)
            {
                if (Connector.IsConnected) 
                {
                    if (!isConnected)
                        OnConnect();
                    isConnected = true;
                    UpdateTime(_ticksElapsed);
                    if (TimeElapsed > HoldOnTime)
                    {
                        Disconnect();
                        return false;
                    }
                    return true;
                }
                else 
                { 
                    if (ticksElapsed > 0 && isConnected)
                    {
                        ticksSinceTradeDisconnect += ticksElapsed;
                        UpdateTime(_ticksElapsed);
                        if (TimeElapsed < HoldOnTime && !Connector.Closed && Connector.IsWorking && Connector.Status == MyShipConnectorStatus.Connectable && ticksSinceTradeDisconnect > TICKSDELAYRECONNECT)
                            Connector.Connect();
                        else
                        {
                            isConnected = false;
                            Disconnect();
                        }
                            
                    }
                    if (isConnected) return true;
                    ticksElapsed = 0;
                    TimeElapsed = 0f;
                    ticksSinceTradeDisconnect = 0;
                    return false;
                }
            }

            private void UpdateTime(int _ticksElapsed)
            {
                ticksElapsed += _ticksElapsed;
                TimeElapsed = ticksElapsed / TICKPERMIN;
            }
            /// <summary>
            /// Disconnects the connector and triggers the TriggerOnDisconnect timer group.
            /// </summary>
            public void Disconnect()
            {
                if (Connector.IsConnected) Connector.Disconnect();
                if(TriggerOnEnd.Count > 0)
                {
                    foreach (var tim in TriggerOnEnd)
                        tim.Trigger();
                }
                Reset();
                Refresh();
            }
            /// <summary>
            /// Triggers the TriggerOnConnect timer group.
            /// </summary>
            public void OnConnect()
            {
                if (TriggerOnStart.Count > 0)
                {
                    foreach (var tim in TriggerOnStart)
                        tim.Trigger();
                }
            }
            /// <summary>
            /// Resets the connector time counter and the stored MyIni configuration
            /// </summary>
            public void Reset()
            {
                isConnected = false;
                ticksElapsed = 0;
                TimeElapsed = 0f;
                ticksSinceTradeDisconnect = 0;
                Ini.Clear();
            }
            
            private void InstallIni()
            {
                MyIniParseResult result = new MyIniParseResult();
                if(!Ini.TryParse(Connector.CustomData, out result))
                    throw new Exception(result.ToString());

                List<string> sections = new List<string>();
                Ini.GetSections(sections);
                if (!sections.Contains(section))
                {
                    Ini.AddSection(section);
                    Ini.Set(section, HOTname, HoldOnTime);
                    foreach (var key in IniNames.Keys)
                    {
                        Ini.Set(section, key, IniNames[key]);
                    }
                }
                HoldOnTime = Ini.Get(section, HOTname).ToSingle(-1f);
                CustomDataInterface(true);
            }

            /// <summary>
            /// Attempts to read or write the configuration data to the CustomData of the connector.
            /// </summary>
            /// <param name="_write">If true, will write to the CustomData, if false, will read from it.</param>
            /// <exception cref="Exception"></exception>
            public void CustomDataInterface(bool _write = false)
            {
                if (_write) Connector.CustomData = Ini.ToString();
                else
                {
                    Ini.Clear();
                    MyIniParseResult result = new MyIniParseResult();
                    if (!Ini.TryParse(Connector.CustomData, out result))
                        throw new Exception(result.ToString());
                    HoldOnTime = Ini.Get(section, HOTname).ToSingle(-1f);
                    foreach (var key in IniNames.Keys)
                    {
                        IniNames[key] = Ini.Get(section, key).ToString();
                    }
                }
            }
            /// <summary>
            /// Checks if the connector can be used or not.
            /// </summary>
            /// <returns>Returns true if it is fully functional. Returns false if it is null, Closed (no longer exists in the grid), or isn't working.</returns>
            public bool IsDead()
            {
                if (Connector == null || Connector.Closed || !Connector.IsWorking) return true;
                return false;
            }
            /// <summary>
            /// Pulls the configuaration from CustomData and empties the timer and display lists for population.
            /// </summary>
            public void Refresh()
            {
                CustomDataInterface();
                TriggerOnEnd.Clear();
                TriggerOnStart.Clear();
                Displays.Clear();
            }
            /// <summary>
            /// Produces the text snippet for displaying on a surface.
            /// </summary>
            /// <returns></returns>
            public string GetDisplayText()
            {
                int timeLeft = (int)(HoldOnTime*60*60) - ticksElapsed;
                int days = timeLeft / (60 * 60 * 60 * 24);
                int hours = timeLeft / (60 * 60 * 60);
                int mins = timeLeft / (60 * 60);
                int secs = timeLeft / 60;
                StringBuilder blah = new StringBuilder();
                blah.AppendLine($"{Connector.CustomName}");
                blah.AppendLine($"Disconnect in: )");
                if (days > 0) blah.Append($"{days}d:");
                if (hours > 0) blah.Append($"{hours}h:");
                blah.AppendLine($"{mins}m:{secs}s");
                return blah.ToString();
            }
        }
    }
}
