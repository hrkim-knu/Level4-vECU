//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using Antmicro.Renode.Logging;
using Antmicro.Renode.Logging.Backends;
using Antmicro.Renode.Core;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.UserInterface.Tokenizer;
using Antmicro.Renode.Backends.Terminals;
using AntShell;
using AntShell.Terminal;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.Utilities;
using Antmicro.Renode.Peripherals.UART;
using System.Linq;
using Antmicro.OptionsParser;
using System.IO;
using System.Diagnostics;
using Antmicro.Renode.Analyzers;
using Antmicro.Renode.Extensions.Analyzers.Video;
using Antmicro.Renode.Backends.Video;
// hrkim add
using System.Threading;

namespace Antmicro.Renode.UI
{
    public static class CommandLineInterface
    {
        public static void Run(Options options, Action<ObjectCreator.Context> beforeRun = null)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => CrashHandler.HandleCrash((Exception)e.ExceptionObject);

            if(options.KeepTemporaryFiles)
            {
                EmulationManager.DisableEmulationFilesCleanup = true;
            }

            if(!options.HideLog)
            {
                Logger.AddBackend(ConsoleBackend.Instance, "console");
                if(options.Plain)
                {
                    //This is set in Program.cs already, but we leave it here in case CommandLineInterface is reused,
                    //to prevent hard to trace bugs
                    ConsoleBackend.Instance.PlainMode = true;
                }
            }
            else
            {
                Logger.AddBackend(new DummyLoggerBackend(), "dummy");
            }

            Logger.AddBackend(new MemoryBackend(), "memory");
            Emulator.ShowAnalyzers = !options.HideAnalyzers;
            XwtProvider xwt = null;
            if(options.PidFile != null)
            {
                var pid = Process.GetCurrentProcess().Id;
                File.WriteAllText(options.PidFile, pid.ToString());
            }

            if(!options.DisableXwt || options.RobotDebug)
            {
                xwt = XwtProvider.Create(new WindowedUserInterfaceProvider());
            }

            if(xwt == null && options.RobotFrameworkRemoteServerPort == -1 && !options.Console)
            {
                if(options.Port == -1)
                {
                    options.Port = 1234;
                }

                if(!options.DisableXwt)
                {
                    Logger.Log(LogLevel.Warning, "Couldn't start UI - falling back to console mode");
                    options.Console = true;
                }
            }

            using(var context = ObjectCreator.Instance.OpenContext())
            {
                var monitor = new Antmicro.Renode.UserInterface.Monitor();
                context.RegisterSurrogate(typeof(Antmicro.Renode.UserInterface.Monitor), monitor);

                // we must initialize plugins AFTER registering monitor surrogate
                // as some plugins might need it for construction
                TypeManager.Instance.PluginManager.Init("CLI");

                EmulationManager.Instance.ProgressMonitor.Handler = new CLIProgressMonitor();

                var uartAnalyzerType = (xwt == null || options.RobotDebug) ? typeof(LoggingUartAnalyzer) : typeof(ConsoleWindowBackendAnalyzer);
                var videoAnalyzerType = (xwt == null || options.RobotDebug) ? typeof(DummyVideoAnalyzer) : typeof(VideoAnalyzer);

                EmulationManager.Instance.CurrentEmulation.BackendManager.SetPreferredAnalyzer(typeof(UARTBackend), uartAnalyzerType);
                EmulationManager.Instance.CurrentEmulation.BackendManager.SetPreferredAnalyzer(typeof(VideoBackend), videoAnalyzerType);
                EmulationManager.Instance.EmulationChanged += () =>
                {
                    EmulationManager.Instance.CurrentEmulation.BackendManager.SetPreferredAnalyzer(typeof(UARTBackend), uartAnalyzerType);
                    EmulationManager.Instance.CurrentEmulation.BackendManager.SetPreferredAnalyzer(typeof(VideoBackend), videoAnalyzerType);
                };

                // hrkim add
                if (options.FMI)
                {
                    FMIHandler.ConnectToServer("localhost", 7789); // 서버 IP와 포트를 입력하세요
                    monitor.Interaction = new DummyCommandInteraction();

                    var communicationThread = new System.Threading.Thread(new ThreadStart(() =>
                    {
                        try
                        {
                            while(true)
                            {
                                // Receive Command from vECU Controller.fmu
                                // 1. Receive Command
                                // 2. Execute vECU according to Commands (init, doStep ...)
                                // 3. If there is an event like CAN, return output to vECU Controller.fmu
                                var command = FMIHandler.ReceiveCommand();
                                
                                if(command != null)
                                {
                                    FMIHandler.HandleCommand(command, monitor);
                                    FMIHandler.SendCompletionMessageToServer();
                                }

                            }
                        }
                        finally
                        {
                            FMIHandler.DisconnectFromServer();
                        }
                    }));

                    communicationThread.Start();

                    // Exit 처리 추가
                    Emulator.BeforeExit += () =>
                    {
                        FMIHandler.DisconnectFromServer();
                        Emulator.DisposeAll();
                        xwt?.Dispose();
                        xwt = null;
                    };
                }
                else
                {
                var shell = PrepareShell(options, monitor);
                new System.Threading.Thread(x => shell.Start(true))
                {
                    IsBackground = true,
                    Name = "Shell thread"
                }.Start();
            

                Emulator.BeforeExit += () =>
                {
                    Emulator.DisposeAll();
                    xwt?.Dispose();
                    xwt = null;
                };

                if(beforeRun != null)
                {
                    beforeRun(context);
                }

                if(options.RobotDebug)
                {
                    ConsoleWindowBackendAnalyzer terminal = null;

                    Emulator.EnableGUI += () =>
                    {
                        Logger.AddBackend(ConsoleBackend.Instance, "console", true);
                        terminal = new ConsoleWindowBackendAnalyzer(true);
                        terminal.Show();
                        shell.Terminal = new NavigableTerminalEmulator(terminal.IO);
                        shell.Terminal.PlainMode = options.Plain;

                        new System.Threading.Thread(x => shell.Start(true))
                        {
                            IsBackground = true,
                            Name = "Shell thread"
                        }.Start();
                    };

                    Emulator.DisableGUI += () =>
                    {
                        if(options.HideLog)
                        {
                            Logger.RemoveBackend(ConsoleBackend.Instance);
                        }
                        terminal?.Hide();
                        terminal = null;
                    };
                }
                }

                Emulator.WaitForExit();
            }
        }

        private static Shell PrepareShell(Options options, Antmicro.Renode.UserInterface.Monitor monitor)
        {
            Shell shell = null;
            if(options.Console)
            {
            #if NET && !PLATFORM_WINDOWS
                // On dotnet "collapse-repeated-log-entries" configuration is not supported in "--console" mode on unix-like systems 
                // https://github.com/dotnet/runtime/issues/49301#issuecomment-800048609
                Logger.Log(LogLevel.Warning, "On dotnet configuration option \"collapse-repeated-log-entries\" is not supported in console mode on unix-like system. It will be set to false.");
                ConsoleBackend.Instance.ReportRepeatingLines = true;
                // Override config for the current session to keep it consistent, in case it is read from other place in code.
                ConfigurationManager.Instance.SetNonPersistent("general", "collapse-repeated-log-entries", false);
            #endif
                var io = new IOProvider()
                {
                    Backend = new ConsoleIOSource()
                };
                shell = ShellProvider.GenerateShell(monitor, true);
                shell.Terminal = new NavigableTerminalEmulator(io, true);
            }
            else if(options.Port >= 0)
            {
                var io = new IOProvider()
                {
                    Backend = new SocketIOSource(options.Port)
                };
                shell = ShellProvider.GenerateShell(monitor, true);
                shell.Terminal = new NavigableTerminalEmulator(io, true);

                Logger.Log(LogLevel.Info, "Monitor available in telnet mode on port {0}", options.Port);
            }
            else
            {
                ConsoleWindowBackendAnalyzer terminal = null;
                IOProvider io;
                if(options.HideMonitor)
                {
                    io = new IOProvider { Backend = new DummyIOSource() };
                }
                else
                {
                    terminal = new ConsoleWindowBackendAnalyzer(true);
                    io = terminal.IO;
                }

                // forcing vcursor is necessary, because calibrating will never end if the window is not shown
                shell = ShellProvider.GenerateShell(monitor, forceVCursor: options.HideMonitor);
                shell.Terminal = new NavigableTerminalEmulator(io, options.HideMonitor);

                if(terminal != null)
                {
                    try
                    {
                        Emulator.BeforeExit += shell.Stop;
                        terminal.Quitted += Emulator.Exit;
                        terminal.Show();
                    }
                    catch(InvalidOperationException ex)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.Error.WriteLine(ex.Message);
                        Emulator.Exit();
                    }
                }
            }
            monitor.Quitted += shell.Stop;
            shell.Quitted += Emulator.Exit;

            monitor.Interaction = shell.Writer;
            monitor.MachineChanged += emu => shell.SetPrompt(emu != null ? new Prompt(string.Format("({0}) ", emu), ConsoleColor.DarkYellow) : null);

            if(!string.IsNullOrEmpty(options.FilePath))
            {
                var filePath = string.Format("{0}{1}",
                    Uri.IsWellFormedUriString(options.FilePath, UriKind.Absolute) || Path.IsPathRooted(options.FilePath) ? "@" : "$CWD/",
                    options.FilePath);
                String commandToInject;
                switch(Path.GetExtension(new PathToken(filePath).Value))
                {
                    case ".save":
                    case ".gz":
                        commandToInject = string.Format("Load {0}\n", filePath);
                        break;
                    default:
                        commandToInject = string.Format("i {0}\n", filePath);
                        break;
                }
                shell.Started += s => s.InjectInput(commandToInject);
            }
            if(options.Execute != null)
            {
                shell.Started += s => s.InjectInput(string.Format("{0}\n", string.Join("; ", options.Execute)));
            }

            shell.Terminal.PlainMode = options.Plain;
            Logger.Log(LogLevel.Info, "hrkim : Monitor available in normal mode");

            return shell;
        }
    }
}
