//
// Copyright (c) 2010-2024 Antmicro
// Copyright (c) 2011-2015 Realtime Embedded
//
// This file is licensed under the MIT License.
// Full license text is available in 'licenses/MIT.txt'.
//
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using Antmicro.Renode.Time;
using Antmicro.Renode.Exceptions;
using Antmicro.Renode.UserInterface;
using Antmicro.Renode.UserInterface.Commands;  // IncludeFileCommand를 위해 추가
using Antmicro.Renode.UserInterface.Tokenizer; // StringToken을 위해 추가
using AntShell.Commands;

namespace Antmicro.Renode.Core
{
    public class FMIHandler
    {
        private static TcpClient client;
        private static NetworkStream stream;

        // 서버와 연결을 시도하고 연결되면 데이터를 수신할 준비를 합니다.
        public static void ConnectToServer(string serverIp, int port)
        {
            try
            {
                client = new TcpClient();
                client.Connect(serverIp, port);  // 서버에 연결
                stream = client.GetStream();
                Console.WriteLine($"Connected to server at {serverIp}:{port}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not connect to server: {ex.Message}");
            }
        }

        public static void DisconnectFromServer()
        {
            stream?.Close();
            client?.Close();
            Console.WriteLine("Disconnected from server.");
        }

        public static void SendCompletionMessageToServer()
        {
            if (stream != null && stream.CanWrite)
            {
                string completionMessage = "complete";
                byte[] messageBuffer = Encoding.ASCII.GetBytes(completionMessage);
                stream.Write(messageBuffer, 0, messageBuffer.Length);
                Console.WriteLine("Sent completion message to server.");
            }
        }

        // ReceiveCommand waits for incoming data from the vECU Controller.
        public static string ReceiveCommand()
        {
            if (stream == null || !stream.CanRead)
            {
                Console.WriteLine("Stream not available to receive data.");
                return null;
            }

            byte[] buffer = new byte[256]; // Buffer to hold incoming command data
            int bytesRead = stream.Read(buffer, 0, buffer.Length);
            if (bytesRead > 0)
            {
                // Convert bytes to string command
                string command = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                Console.WriteLine($"Received command: {command}");
                return command;
            }
            return null;
        }
        // HandleCommand processes the received command and executes appropriate actions.
        public static void HandleCommand(string command, Antmicro.Renode.UserInterface.Monitor monitor)
        {
            if (string.IsNullOrEmpty(command))
            {
                Console.WriteLine("No command to process.");
                return;
            }

            // Split command into components (command type and parameters)
            var commandParts = command.Split(',');
            var commandType = commandParts[0].Trim(); // first part is command type

            // Handle different command types and parameters
            switch (commandType)
            {
                case "init":
                    if (commandParts.Length > 1)
                    {
                        var rescFilePath = commandParts[1].Trim(); // Second part is the resc file path
                        var includeCommand = $"include @{rescFilePath}";
                        Console.WriteLine($"Initializing simulation with resc file: {rescFilePath}");
                        IncludeRescFile(includeCommand, monitor);
                    }
                    else
                    {
                        Console.WriteLine("No resc file path provided.");
                    }
                    break;

                case "dostep":
                    // Expecting format like: doStep, 100ms or doStep, 1s
                    if (commandParts.Length > 1)
                    {
                        var period = commandParts[1].Trim();  // "100ms", "1s"
                        Console.WriteLine($"Executing runfor {period}");
                        TimeInterval parsedPeriod;

                        // Parsing to TimeInterval and Execute RunFor
                        if (TimeInterval.TryParse(period, out parsedPeriod))
                        {
                            try
                            {
                                EmulationManager.Instance.CurrentEmulation.RunFor(parsedPeriod);
                                Console.WriteLine("Emulation completed successfully.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error during emulation: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Invalid time format: {period}");
                        }
                    }
                    else
                    {
                        Console.WriteLine("No step size provided.");
                    }
                    break;

                // case "CAN":
                //     if (commandParts.Length > 3)
                //     {
                //         var canMessageId = commandParts[1].Trim();
                //         var dlc = commandParts[2].Trim();
                //         var canMessageData = commandParts[3].Trim();

                //         Console.WriteLine($"Processing CAN message with ID: {canMessageId}, DLC: {dlc}, Data: {canMessageData}");
                //         ProcessCANMessage(canMessageId, canMessageData);
                //     }
                //     else
                //     {
                //         Console.WriteLine("Invalid CAN message format.");
                //     }
                //     break;

                default:
                    Console.WriteLine($"Unknown command: {commandType}");
                    break;
            }
        }

        // private static void ProcessCANMessage(string id, string data)
        // {
        //     Console.WriteLine($"Processing CAN message with ID: {id} and Data: {data}");
        //     // Insert code to handle CAN message within the simulation
        // }

        // Method to parse and include resc file without Monitor shell interaction
        private static void IncludeRescFile(string rescFilePath, Antmicro.Renode.UserInterface.Monitor monitor)
        {
            Console.WriteLine($"rescFile : {rescFilePath}");
            try
            {
                // 실제 include 명령을 처리
                var token = new StringToken(rescFilePath);

                Console.WriteLine($"Attempting to include resc file: {rescFilePath}");
                var result = monitor.Parse(rescFilePath);

                if (result)
                {
                    Console.WriteLine($"Successfully included resc file: {rescFilePath}");
                }
                else
                {
                    Console.WriteLine($"Failed to include resc file: {rescFilePath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading resc file: {ex.Message}");
            }
        }
    }
}


