﻿using System;
using System.Linq;

namespace PowerPing 
{
    /// <summary>
    /// Responsible parsing all commandline arguments and working out ping operation and attributes
    /// </summary>
    class CommandLine 
    {
        /// <summary>
        /// Pases command line arguments and store properties in attributes object
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <param name="attributes">PingAttributes object to store propterties from arguments</param>
        /// <returns>bool based on if parsing was successful or not</returns>
        public static bool Parse(string[] args, ref PingAttributes attributes)
        {
            int curArg = 0;

            // Loop through arguments
            try {
                checked {
                    for (int count = 0; count < args.Length; count++) {
                        curArg = count;

                        switch (args[count]) {
                            case "/version":
                            case "-version":
                            case "--version":
                            case "/v":
                            case "-v":
                            case "--v":
                                Display.Version(true);
                                Helper.CheckRecentVersion();
                                Helper.WaitForUserInput();
                                Environment.Exit(0);
                                break;
                            case "/beep":
                            case "-beep":
                            case "--beep":
                            case "/b":
                            case "-b":
                            case "--b":
                                int level = Convert.ToInt32(args[count + 1]);
                                if (level > 2) {
                                    Display.Message("Invalid beep level, please use a number between 0 & 2");
                                    throw new ArgumentFormatException();
                                }
                                attributes.BeepLevel = level;
                                break;
                            case "/count":
                            case "-count":
                            case "--count":
                            case "/c":
                            case "-c":
                            case "--c": // Ping count
                                attributes.Count = Convert.ToInt32(args[count + 1]);
                                break;
                            case "/infinite":
                            case "-infinite":
                            case "--infinite":
                            case "/t":
                            case "-t":
                            case "--t": // Infinitely send
                                attributes.Continous = true;
                                break;
                            case "/timeout":
                            case "-timeout":
                            case "--timeout":
                            case "/w":
                            case "-w":
                            case "--w": // Timeout
                                attributes.Timeout = Convert.ToInt32(args[count + 1]);
                                break;
                            case "/message":
                            case "-message":
                            case "--message":
                            case "/m":
                            case "-m":
                            case "--m": // Message
                                if (args[count + 1].Contains("--") || args[count + 1].Contains("//") || args[count + 1].Contains("-")) {
                                    throw new ArgumentFormatException();
                                }
                                attributes.Message = args[count + 1];
                                break;
                            case "/ttl":
                            case "-ttl":
                            case "--ttl":
                            case "/i":
                            case "-i":
                            case "--i": // Time To Live
                                int ttl = Convert.ToInt16(args[count + 1]);
                                if (ttl > 255) {
                                    Display.Message("TTL has to be between 0 and 255");
                                    throw new ArgumentFormatException();
                                }
                                attributes.Ttl = ttl;
                                break;
                            case "/interval":
                            case "-interval":
                            case "--interval":
                            case "/in":
                            case "-in":
                            case "--in": // Interval
                                attributes.Interval = Convert.ToInt32(args[count + 1]);
                                if (attributes.Interval < 1) {
                                    Display.Message("Ping interval cannot be less than 1ms");
                                    throw new ArgumentFormatException();
                                }
                                break;
                            case "/type":
                            case "-type":
                            case "--type":
                            case "/pt":
                            case "-pt":
                            case "--pt": // Ping type
                                var type = Convert.ToByte(args[count + 1]);
                                if (type > 255) {
                                    throw new ArgumentFormatException();
                                }
                                attributes.Type = type;
                                break;
                            case "/code":
                            case "-code":
                            case "--code":
                            case "/pc":
                            case "-pc":
                            case "--pc": // Ping code
                                attributes.Code = Convert.ToByte(args[count + 1]);
                                break;
                            case "/displaymsg":
                            case "-displaymsg":
                            case "--displaymsg":
                            case "/dm":
                            case "-dm":
                            case "--dm": // Display packet message
                                Display.ShowMessages = true;
                                break;
                            case "/ipv4":
                            case "-ipv4":
                            case "--ipv4":
                            case "/4":
                            case "-4":
                            case "--4": // Force ping with IPv4
                                if (attributes.ForceV6) {
                                    // Reset IPv6 force if already set
                                    attributes.ForceV6 = false;
                                }
                                attributes.ForceV4 = true;
                                break;
                            case "/ipv6":
                            case "-ipv6":
                            case "--ipv6":
                            case "/6":
                            case "-6":
                            case "--6": // Force ping with IPv6
                                if (attributes.ForceV4) {
                                    // Reset IPv4 force if already set
                                    attributes.ForceV4 = false;
                                }
                                attributes.ForceV6 = true;
                                break;
                            case "/help":
                            case "-help":
                            case "--help":
                            case "/?":
                            case "-?":
                            case "--?": // Display help message
                                Display.Help();
                                Helper.WaitForUserInput();
                                Environment.Exit(0);
                                break;
                            case "/examples":
                            case "-examples":
                            case "--examples":
                            case "/ex":
                            case "-ex":
                            case "--ex": // Displays examples
                                Display.Examples();
                                Environment.Exit(0); // Exit after displaying examples
                                break;
                            case "/shorthand":
                            case "-shorthand":
                            case "--shorthand":
                            case "/sh":
                            case "-sh":
                            case "--sh": // Use short hand messages
                                Display.Short = true;
                                break;
                            case "/nocolor":
                            case "-nocolor":
                            case "--nocolor":
                            case "/nc":
                            case "-nc":
                            case "--nc": // No color mode
                                Display.NoColor = true;
                                break;
                            case "/ni":
                            case "-ni":
                            case "--ni":
                            case "/noinput":
                            case "-noinput":
                            case "--noinput":// No input mode
                                Display.NoInput = true;
                                Properties.Settings.Default.RequireInput = !Properties.Settings.Default.RequireInput;
                                Properties.Settings.Default.Save();
                                Display.Message(
                                    "(RequireInput is now " + 
                                    (Properties.Settings.Default.RequireInput ? 
                                    "ON, from now on you will be prompted for user input when PowerPing is finished)" 
                                    : "OFF, you will no longer be prompted for user input when PowerPing is finished)"), 
                                    ConsoleColor.Cyan);
                                break;
                            case "/decimals":
                            case "-decimals":
                            case "--decimals":
                            case "/dp":
                            case "-dp":
                            case "--dp": // Decimal places
                                if (Convert.ToInt32(args[count + 1]) > 3 || Convert.ToInt32(args[count + 1]) < 0) {
                                    throw new ArgumentFormatException();
                                }
                                Display.DecimalPlaces = Convert.ToInt32(args[count + 1]);
                                break;
                            case "/symbols":
                            case "-symbols":
                            case "--symbols":
                            case "/sym":
                            case "-sym":
                            case "--sym":
                                Display.UseSymbols = true;
                                Display.SetAsciiReplySymbolsTheme(0);

                                // Change symbols theme if an argument is present
                                if (args.Length < count + 1) {
                                    count++;
                                    continue;
                                }
                                if ((args[count + 1].Contains("--") 
                                    || args[count + 1].Contains("//") 
                                    || args[count + 1].Contains("-") 
                                    || args[count + 1].Contains("."))) {
                                    count++;
                                    continue;
                                }
                                int theme = Convert.ToInt32(args[count + 1]);
                                Display.SetAsciiReplySymbolsTheme(theme);
                                break;
                            case "/random":
                            case "-random":
                            case "--random":
                            case "/rng":
                            case "-rng":
                            case "--rng":
                                attributes.RandomMsg = true;
                                break;
                            case "/limit":
                            case "-limit":
                            case "--limit":
                            case "/l":
                            case "-l":
                            case "--l":
                                if (Convert.ToInt32(args[count + 1]) == 1) {
                                    Display.ShowSummary = false;
                                    Display.ShowIntro = false;
                                }
                                else if (Convert.ToInt32(args[count + 1]) == 2) {
                                    Display.ShowSummary = false;
                                    Display.ShowIntro = false;
                                    Display.ShowReplies = false;
                                    Display.ShowRequests = true;
                                }
                                else if (Convert.ToInt32(args[count + 1]) == 3) {
                                    Display.ShowReplies = false;
                                    Display.ShowRequests = false;
                                }
                                else {
                                    throw new ArgumentFormatException();
                                }
                                break;
                            case "/notimeout":
                            case "-notimeout":
                            case "--notimeout":
                            case "/nt":
                            case "-nt":
                            case "--nt":
                                Display.ShowTimeouts = false;
                                break;
                            case "/timestamp":
                            case "-timestamp":
                            case "--timestamp":
                            case "/ts":
                            case "-ts":
                            case "--ts": // Display timestamp
                                if (args[count + 1].ToLower() == "utc") {
                                    Display.ShowtimeStampUTC = true;
                                } else {
                                    Display.ShowTimeStamp = true;
                                }
                                break;
                            case "/fulltimestamp":
                            case "-fulltimestamp":
                            case "--fulltimestamp":
                            case "/fts":
                            case "-fts":
                            case "--fts": // Display timestamp with date
                                if (args[count + 1].ToLower() == "utc") {
                                    Display.ShowFullTimeStampUTC = true;
                                } else {
                                    Display.ShowFullTimeStamp = true;
                                }
                                break;
                            case "/timing":
                            case "-timing":
                            case "--timing":
                            case "/ti":
                            case "-ti":
                            case "--ti": // Timing option
                                switch (args[count + 1].ToLowerInvariant()) {
                                    case "0":
                                    case "paranoid":
                                        attributes.Timeout = 10000;
                                        attributes.Interval = 300000;
                                        break;
                                    case "1":
                                    case "sneaky":
                                        attributes.Timeout = 5000;
                                        attributes.Interval = 120000;
                                        break;
                                    case "2":
                                    case "quiet":
                                        attributes.Timeout = 5000;
                                        attributes.Interval = 30000;
                                        break;
                                    case "3":
                                    case "polite":
                                        attributes.Timeout = 3000;
                                        attributes.Interval = 3000;
                                        break;
                                    case "4":
                                    case "nimble":
                                        attributes.Timeout = 2000;
                                        attributes.Interval = 750;
                                        break;
                                    case "5":
                                    case "speedy":
                                        attributes.Timeout = 1500;
                                        attributes.Interval = 500;
                                        break;
                                    case "6":
                                    case "insane":
                                        attributes.Timeout = 750;
                                        attributes.Interval = 100;
                                        break;
                                    case "7":
                                    case "random":
                                        attributes.RandomTiming = true;
                                        attributes.RandomMsg = true;
                                        attributes.Interval = Helper.RandomInt(5000, 100000);
                                        attributes.Timeout = 15000;
                                        break;
                                    default: // Unknown timing type
                                        throw new ArgumentFormatException();
                                }
                                break;
                            case "/request":
                            case "-request":
                            case "--request":
                            case "/requests":
                            case "-requests":
                            case "--requests":
                            case "/r":
                            case "-r":
                            case "--r":
                                Display.ShowRequests = true;
                                break;
                            case "/quiet":
                            case "-quiet":
                            case "--quiet":
                            case "/q":
                            case "-q":
                            case "--q":
                                Display.ShowOutput = false;
                                break;
                            case "/resolve":
                            case "-resolve":
                            case "--resolve":
                            case "/res":
                            case "-res":
                            case "--res":
                                Display.UseResolvedAddress = true;
                                break;
                            case "/inputaddr":
                            case "-inputaddr":
                            case "--inputaddr":
                            case "/ia":
                            case "-ia":
                            case "--ia":
                                Display.UseInputtedAddress = true;
                                break;
                            case "/buffer":
                            case "-buffer":
                            case "--buffer":
                            case "/rb":
                            case "-rb":
                            case "--rb":
                                int recvbuff = Convert.ToInt32(args[count + 1]);
                                if (recvbuff < 65000) {
                                    attributes.RecieveBufferSize = recvbuff;
                                } else {
                                    throw new ArgumentFormatException();
                                }
                                break;
                            case "/checksum":
                            case "-checksum":
                            case "--checksum":
                            case "/chk":
                            case "-chk":
                            case "--chk":
                                Display.ShowChecksum = true;
                                break;
                            case "/dontfrag":
                            case "-dontfrag":
                            case "--dontfrag":
                            case "/df":
                            case "-df":
                            case "--df":
                                attributes.DontFragment = true;
                                break;
                            case "/size":
                            case "-size":
                            case "--size":
                            case "/s":
                            case "-s":
                            case "--s":
                                int size = Convert.ToInt32(args[count + 1]);
                                if (size < 100000) {
                                    attributes.Size = size;
                                } else {
                                    throw new ArgumentFormatException();
                                }
                                break;
                            case "/whois":
                            case "-whois":
                            case "--whois": // Whois lookup
                                attributes.Operation = PingOperation.Whois;
                                break;
                            case "/whoami":
                            case "-whoami":
                            case "--whoami": // Current computer location
                                attributes.Operation = PingOperation.Whoami;
                                break;
                            case "/location":
                            case "-location":
                            case "--location":
                            case "/loc":
                            case "-loc":
                            case "--loc": // Location lookup
                                attributes.Operation = PingOperation.Location;
                                break;
                            case "/listen":
                            case "-listen":
                            case "--listen":
                            case "/li":
                            case "-li":
                            case "--li": // Listen for ICMP packets
                                attributes.Operation = PingOperation.Listen;
                                break;
                            case "/graph":
                            case "-graph":
                            case "--graph":
                            case "/g":
                            case "-g":
                            case "--g": // Graph view
                                attributes.Operation = PingOperation.Graph;
                                break;
                            case "/compact":
                            case "-compact":
                            case "--compact":
                            case "/cg":
                            case "-cg":
                            case "--cg": // Compact graph view
                                attributes.Operation = PingOperation.CompactGraph;
                                break;
                            case "/flood":
                            case "-flood":
                            case "--flood":
                            case "/fl":
                            case "-fl":
                            case "--fl": // Flood
                                attributes.Operation = PingOperation.Flood;
                                break;
                            case "/scan":
                            case "-scan":
                            case "--scan":
                            case "/sc":
                            case "-sc":
                            case "--sc": // Scan
                                attributes.Operation = PingOperation.Scan;
                                break;
                            default:
                                // Check for invalid argument 
                                if ((args[count].Contains("--") || args[count].Contains("/") || args[count].Contains("-"))
                                    && attributes.Operation != PingOperation.Scan // (ignore if scanning) // TODO: Change this
                                    && (!Helper.IsURL(args[count]) && !Helper.IsIPv4Address(args[count]))) { 
                                    throw new InvalidArgumentException();
                                }
                                break;
                        }
                    }
                }
            }
            catch (IndexOutOfRangeException) {
                Display.Error($"Missing argument parameter @ \"PowerPing >>>{args[curArg]}<<<\"");
                return false;
            }
            catch (OverflowException) {
                Display.Error($"Overflow while converting @ \"PowerPing {args[curArg]} >>>{args[curArg + 1]}<<<\"");
                return false;
            }
            catch (InvalidArgumentException) {
                Display.Error($"Invalid argument @ \"PowerPing >>>{args[curArg]}<<<\"");
                return false;
            }
            catch (ArgumentFormatException) {
                Display.Error($"Incorrect parameter for [{args[curArg]}] @ \"PowerPing {args[curArg]} >>>{args[curArg + 1]}<<<\"");
                return false;
            }
            catch (Exception e) {
                Display.Error($"An {e.GetType().ToString().Split('.').Last()} exception occured @ \"PowerPing >>>{args[curArg]}<<<\"");
                return false;
            }
            return true;
        }

        /// <summary>
        /// Search for host can be IPv4 or URL, stores the results in the PingAttributes object
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <param name="attributes">PingAttributes object to store propterties from arguments</param>
        /// <returns>Returns if the address was found or not</returns>
        public static bool FindAddress(string[] args, ref PingAttributes attributes)
        {
            // Look for valid scan address (slightly different format than normal address)
            if (attributes.Operation == PingOperation.Scan) {
                if (Helper.IsValidScanRange(args.First())) {
                    attributes.InputtedAddress = args.First();
                    return true;
                }
                if (Helper.IsValidScanRange(args.Last())) {
                    attributes.InputtedAddress = args.Last();
                    return true;
                }

                // Didn't find one..
                return false;
            }

            // First check first and last arguments for IPv4 address
            if (Helper.IsIPv4Address(args.Last())) {
                attributes.InputtedAddress = args.Last();
                return true;
            }
            if (Helper.IsIPv4Address(args.First())) {
                attributes.InputtedAddress = args.First();
                return true;
            }

            // Then check for URLs
            if (Helper.IsURL(args.Last())) {
                attributes.InputtedAddress = args.Last();
                return true;
            }
            if (Helper.IsURL(args.First())) {
                attributes.InputtedAddress = args.First();
                return true;
            }

            return false;
        }
    }
}
