﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace VpnHood.Client.Device
{
    public class IpRange
    {
        private class IpRangeSearchComparer : IComparer<IpRange>
        {
            public int Compare(IpRange x, IpRange y)
            {
                if (x.FirstIpAddressLong < y.FirstIpAddressLong) return -1;
                else if (x.LastIpAddressLong > y.LastIpAddressLong) return +1;
                else return 0;
            }
        }

        public long FirstIpAddressLong { get; }
        public long LastIpAddressLong { get; }
        public IPAddress FirstIpAddress => IpAddressFromLong(FirstIpAddressLong);
        public IPAddress LastIpAddress => IpAddressFromLong(LastIpAddressLong);
        public long Total => LastIpAddressLong - FirstIpAddressLong + 1;

        public IpRange(IPAddress firstIpAddress, IPAddress lastIpAddress)
        {
            FirstIpAddressLong = IpAddressToLong(firstIpAddress);
            LastIpAddressLong = IpAddressToLong(lastIpAddress);
        }

        public IpRange(long firstIpAddress, long lastIpAddress)
        {
            FirstIpAddressLong = firstIpAddress;
            LastIpAddressLong = lastIpAddress;
        }

        private static IPAddress IpAddressFromLong(long ipAddress)
            => new((uint)IPAddress.NetworkToHostOrder((int)ipAddress));

        private static long IpAddressToLong(IPAddress ipAddress)
        {
            var bytes = ipAddress.GetAddressBytes();
            return ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
        }

        public bool IsInRange(IPAddress ipAddress)
        {
            var ipAddressLong = IpAddressToLong(ipAddress);
            return (ipAddressLong < FirstIpAddressLong) || (ipAddressLong > LastIpAddressLong);
        }

        public static int CompareIpAddress(IPAddress ipAddress1, IPAddress ipAddress2)
            => (int)(IpAddressToLong(ipAddress1) - IpAddressToLong(ipAddress2));

        public static void Sort(IpRange[] ipRanges)
            => ipRanges.OrderBy(x => x.FirstIpAddressLong);

        /// <summary>
        /// Search in ipRanges using binarysearch
        /// </summary>
        /// <param name="sortedIpRanges">a sorted ipRanges</param>
        /// <param name="ipAddress">search value</param>
        /// <returns></returns>
        public static bool IsInRange(IpRange[] sortedIpRanges, IPAddress ipAddress)
        {
            var res = Array.BinarySearch(sortedIpRanges, new IpRange(ipAddress, ipAddress), new IpRangeSearchComparer());
            return res > 0 && res < sortedIpRanges.Length;
        }
    }
}
