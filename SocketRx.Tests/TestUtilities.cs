// Copyright (c) Chris Pulman. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;

namespace CP.Net.Sockets.Tests;

internal static class TestUtilities
{
    private static readonly RandomNumberGenerator RandomNumberGenerator = RandomNumberGenerator.Create();
    private static readonly object Locker = new();

    public static IPEndPoint GetEndPointOnRandomLoopbackPort() =>
        new(IPAddress.Loopback, GetRandomAvailablePort());

    private static int GetRandomAvailablePort()
    {
        lock (Locker)
        {
            while (true)
            {
                // IANA officially recommends 49152 - 65535 for the Ephemeral Ports.
                var port = RandomInt(49152, 65535);
                if (!IsPortUsed(port))
                {
                    return port;
                }
            }
        }
    }

    private static bool IsPortUsed(int port) =>
        IPGlobalProperties
            .GetIPGlobalProperties()
            .GetActiveTcpListeners()
            .Any(ep => ep.Port == port);

    private static int RandomInt(int min, int max)
    {
        var buffer = GetRandomBytes(4);
        var result = BitConverter.ToInt32(buffer, 0);
        return new Random(result).Next(min, max);
    }

    private static byte[] GetRandomBytes(int bytes)
    {
        var buffer = new byte[bytes];
        RandomNumberGenerator.GetBytes(buffer, 0, bytes);
        return buffer;
    }
}
