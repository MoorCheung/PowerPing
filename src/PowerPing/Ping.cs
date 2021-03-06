﻿/*
MIT License - PowerPing 

Copyright (c) 2019 Matthew Carney

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PowerPing
{
    /// <summary>
    /// Ping Class, used for constructing and sending ICMP packets.
    /// </summary>
    class Ping
    {
        private static readonly ushort m_SessionId = Helper.GenerateSessionId();
        private readonly CancellationToken m_CancellationToken;
        private bool m_Debug = true;

        public Ping(CancellationToken cancellationTkn)
        {
            m_CancellationToken = cancellationTkn;
        }

        /// <summary>
        /// Sends a set of ping packets, results are stores within
        /// Ping.Results of the called object
        ///
        /// Acts as a basic wrapper to SendICMP and feeds it a specific
        /// set of PingAttributes 
        /// </summary>
        public PingResults Send(PingAttributes attrs, Action<PingResults> onResultsUpdate = null)
        {
            // Lookup host if specified
            if (attrs.InputtedAddress != "") {
                attrs.Address = Lookup.QueryDNS(attrs.InputtedAddress, attrs.ForceV4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6);
            }

            Display.PingIntroMsg(attrs);

            if (Display.UseResolvedAddress) {
                try {
                    attrs.InputtedAddress = Helper.RunWithCancellationToken(() => Lookup.QueryHost(attrs.Address), m_CancellationToken);
                } catch (OperationCanceledException) {
                    return new PingResults();
                }
                if (attrs.InputtedAddress == "") {
                    // If reverse lookup fails just display whatever is in the address field
                    attrs.InputtedAddress = attrs.Address; 
                }
            }

            // Perform ping operation and store results
            PingResults results = SendICMP(attrs, onResultsUpdate);

            if (Display.ShowOutput) {
                Display.PingResults(attrs, results);
            }

            return results;
        }
        /// <summary>
        /// Creates a raw socket for ping operations.
        ///
        /// We have to use raw sockets here as we are using our own 
        /// implementation of ICMP and only raw sockets will allow us
        /// to send whatever data we want through it.
        /// 
        /// The downside is this is why we need to run as administrator
        /// but it allows us the greater degree of control over the packets
        /// that we need
        /// </summary>
        /// <param name="family">AddressFamily to use (IP4 or IP6)</param>
        /// <returns>A raw socket</returns>
        private static Socket CreateRawSocket(AddressFamily family)
        {
            Socket s = null;
            try {
                s = new Socket(family, SocketType.Raw, family == AddressFamily.InterNetwork ? ProtocolType.Icmp : ProtocolType.IcmpV6);
            } catch (SocketException) {
                Display.Message("PowerPing uses raw sockets which require Administrative rights to create." + Environment.NewLine +
                                "(You can find more info at https://github.com/Killeroo/PowerPing/issues/110)", ConsoleColor.Cyan);
                Helper.ErrorAndExit("Socket cannot be created, make sure you are running as an Administrator and try again.");
            }
            return s;
        }
        /// <summary>
        /// Core ICMP sending method (used by all other functions)
        /// Takes a set of attributes, performs operation and returns a set of results.
        ///
        /// Works specifically by creating a raw socket, creating a ICMP object and
        /// other socket properties (timeouts, interval etc) using the 
        /// inputted properties (attrs), then performs ICMP operation 
        /// before cleaning up and returning results.
        ///
        /// </summary>
        /// <param name="attrs">Properties of pings to be sent</param>
        /// <param name="resultsUpdateCallback">Method to call after each iteration</param>
        /// <returns>Set of ping results</returns>
        int scale = 10;
        bool inverting = false;
        private PingResults SendICMP(PingAttributes attrs, Action<PingResults> resultsUpdateCallback = null)
        {
            PingResults results = new PingResults();
            ICMP packet = new ICMP();
            byte[] receiveBuffer = new byte[attrs.RecieveBufferSize]; // Ipv4Header.length + IcmpHeader.length + attrs.recievebuffersize
            int bytesRead, packetSize;

            // Convert to IPAddress
            IPAddress ipAddr = IPAddress.Parse(attrs.Address);

            // Setup endpoint
            IPEndPoint iep = new IPEndPoint(ipAddr, 0);

            // Setup raw socket 
            Socket sock = CreateRawSocket(ipAddr.AddressFamily);

            // Helper function to set receive timeout (only if it's changing)
            int appliedReceiveTimeout = 0;
            void SetReceiveTimeout(int receiveTimeout) {
                if (receiveTimeout != appliedReceiveTimeout) {
                    sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, receiveTimeout);
                    appliedReceiveTimeout = receiveTimeout;
                }
            }

            // Set socket options
            sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, attrs.Ttl);
            sock.DontFragment = attrs.DontFragment;
            sock.ReceiveBufferSize = attrs.RecieveBufferSize;

            // Create packet message payload
            byte[] payload;
            if (attrs.Size != -1) {
                payload = Helper.GenerateByteArray(attrs.Size);
            } else {
                payload = Encoding.ASCII.GetBytes(attrs.Message);
            }

            // Construct our ICMP packet
            packet.Type = attrs.Type;
            packet.Code = attrs.Code;
            Buffer.BlockCopy(BitConverter.GetBytes(m_SessionId), 0, packet.Message, 0, 2); // Add identifier to ICMP message
            Buffer.BlockCopy(payload, 0, packet.Message, 4, payload.Length); // Add text into ICMP message
            packet.MessageSize = payload.Length + 4;
            packetSize = packet.MessageSize + 4;

            // Sending loop
            for (int index = 1; attrs.Continous || index <= attrs.Count; index++) {

                if (index != 1) {
                    // Wait for set interval before sending again or cancel if requested
                    if (m_CancellationToken.WaitHandle.WaitOne(attrs.Interval)) {
                        break;
                    }

                    // Generate random interval when RandomTimings flag is set
                    if (attrs.RandomTiming) {
                        attrs.Interval = Helper.RandomInt(5000, 100000);
                    }
                }

                // Include sequence number in ping message
                ushort sequenceNum = (ushort)index;
                Buffer.BlockCopy(BitConverter.GetBytes(sequenceNum), 0, packet.Message, 2, 2);

                // Fill ICMP message field
                if (attrs.RandomMsg) {
                    payload = Encoding.ASCII.GetBytes(Helper.RandomString());
                    Buffer.BlockCopy(payload, 0, packet.Message, 4, payload.Length);
                }

                // Update packet checksum
                packet.Checksum = 0;
                UInt16 chksm = packet.GetChecksum();
                packet.Checksum = chksm;

                try {

                    // Show request packet
                    if (Display.ShowRequests) {
                        Display.RequestPacket(packet, Display.UseInputtedAddress | Display.UseResolvedAddress ? attrs.InputtedAddress : attrs.Address, index);
                    }

                    // If there were extra responses from a prior request, ignore them
                    while (sock.Available != 0) {
                        bytesRead = sock.Receive(receiveBuffer);
                    }

                    // Send ping request
                    sock.SendTo(packet.GetBytes(), packetSize, SocketFlags.None, iep); // Packet size = message field + 4 header bytes
                    long requestTimestamp = Stopwatch.GetTimestamp();
                    try { results.Sent++; }
                    catch (OverflowException) { results.HasOverflowed = true; }
                    
                    if (m_Debug) {
                        // Induce random wait for debugging 
                        Random rnd = new Random();
                        Thread.Sleep(scale);//rnd.Next(scale));//1500));
                        //Thread.Sleep(rnd.Next(100));
                        if (inverting)
                        {
                            scale -= 5;
                        }
                        else
                        {
                            scale += 5;
                        }
                        if (scale > 1100)
                        {
                            inverting = true;
                        }
                        else if (scale == 10)
                        {
                            inverting = false;
                        }
                        //if (rnd.Next(20) == 1) { throw new SocketException(); }
                    }

                    ICMP response;
                    EndPoint responseEP = iep;
                    TimeSpan replyTime = TimeSpan.Zero;
                    do {
                        // Cancel if requested
                        m_CancellationToken.ThrowIfCancellationRequested();

                        // Set receive timeout, limited to 250ms so we don't block very long without checking for
                        // cancellation. If the requested ping timeout is longer, we will wait some more in subsequent
                        // loop iterations until the requested ping timeout is reached.
                        int remainingTimeout = (int)Math.Ceiling(attrs.Timeout - replyTime.TotalMilliseconds);
                        if (remainingTimeout <= 0) {
                            throw new SocketException();
                        }
                        SetReceiveTimeout(Math.Min(remainingTimeout, 250));

                        // Wait for response
                        try {
                            bytesRead = sock.ReceiveFrom(receiveBuffer, ref responseEP);
                        } catch (SocketException) {
                            bytesRead = 0;
                        }
                        replyTime = new TimeSpan(Helper.StopwatchToTimeSpanTicks(Stopwatch.GetTimestamp() - requestTimestamp));

                        if (bytesRead == 0) {
                            response = null;
                        } else {
                            // Store reply packet
                            response = new ICMP(receiveBuffer, bytesRead);

                            // If we sent an echo and receive a response with a different identifier or sequence
                            // number, ignore it (it could correspond to an older request that timed out)
                            if (packet.Type == 8 && response.Type == 0) {
                                ushort responseSessionId = BitConverter.ToUInt16(response.Message, 0);
                                ushort responseSequenceNum = BitConverter.ToUInt16(response.Message, 2);
                                if (responseSessionId != m_SessionId || responseSequenceNum != sequenceNum) {
                                    response = null;
                                }
                            }
                        }
                    } while (response == null);

                    // Display reply packet
                    if (Display.ShowReplies) {
                        Display.ReplyPacket(response, Display.UseInputtedAddress | Display.UseResolvedAddress ? attrs.InputtedAddress : responseEP.ToString(), index, replyTime, bytesRead);
                    }

                    // Store response info
                    try { results.Received++; }
                    catch (OverflowException) { results.HasOverflowed = true; }
                    results.CountPacketType(response.Type);
                    results.SaveResponseTime(replyTime.TotalMilliseconds);
                    
                    if (attrs.BeepLevel == 2) {
                        try { Console.Beep(); }
                        catch (Exception) { } // Silently continue if Console.Beep errors
                    }
                } catch (IOException) {

                    if (Display.ShowOutput) {
                        Display.Error("General transmit error");
                    }
                    results.SaveResponseTime(-1);
                    try { results.Lost++; }
                    catch (OverflowException) { results.HasOverflowed = true; }

                } catch (SocketException) {

                    Display.Timeout(index);
                    if (attrs.BeepLevel == 1) {
                        try { Console.Beep(); }
                        catch (Exception) { results.HasOverflowed = true; }
                    }
                    results.SaveResponseTime(-1);
                    try { results.Lost++; }
                    catch (OverflowException) { results.HasOverflowed = true; }

                } catch (OperationCanceledException) {

                    results.ScanWasCanceled = true;
                    break;

                } catch (Exception) {

                    if (Display.ShowOutput) {
                        Display.Error("General error occured");
                    }
                    results.SaveResponseTime(-1);
                    try { results.Lost++; }
                    catch (OverflowException) { results.HasOverflowed = true; }

                }

                // Run callback (if provided) to notify of updated results
                resultsUpdateCallback?.Invoke(results);
            }

            // Clean up
            sock.Close();

            return results;
        }
    }
}
