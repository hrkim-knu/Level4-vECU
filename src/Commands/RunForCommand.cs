//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Core;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Time;
using AntShell.Commands;
using Antmicro.Renode.Logging;

namespace Antmicro.Renode.UserInterface.Commands
{
    public class RunForCommand : AutoLoadCommand
    {

        public override void PrintHelp(ICommandInteraction writer)
        {
            base.PrintHelp(writer);
            writer.WriteLine();
            writer.WriteLine("Usage:");
            writer.WriteLine($"{Name} <period> - runs emulation for the specified period (e.g., 10s, 1m, 500ms)");
            // writer.WriteLine(String.Format("{0} @path - executes the script and starts the emulation", Name));
        }

        // [Runnable]
        // public void Run(ICommandInteraction writer)
        // {
        //     writer.WriteLine("RunFor command executed without parameters.");
        // }
        [Runnable]
        public void Run(ICommandInteraction writer, StringToken period)
        {

            Logger.Log(LogLevel.Info, $"Received period: '{period.Value}'");

            if(string.IsNullOrEmpty(period.Value))
            {
                writer.WriteError("Error: Period is required.");
                return;
            }

            if(!TimeInterval.TryParse(period.Value, out var parsedPeriod))
            {
                writer.WriteError("Error: Invalid period format Please specify a valid time interval (e.g., 10s, 1m, 500ms).");
                return;
            }
    
            writer.WriteLine($"Running emulation for {parsedPeriod}...");

            try
            {
                EmulationManager.Instance.CurrentEmulation.RunFor(parsedPeriod);
                writer.WriteLine("Emulation completed successfully.");
            }
            catch(Exception ex)
            {
                writer.WriteError($"Error: {ex.Message}");
            }

        }


        public RunForCommand(Monitor monitor) :  base(monitor, "runfor", "Runs the emulation for a specified period.", "rf")
        {
            Logger.Log(LogLevel.Info, "RunForCommand initialized");
        }
    }
}

