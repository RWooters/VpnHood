﻿namespace VpnHood.Core.Client.Abstractions.Exceptions;

public class NoInternetException()
    : Exception("It looks like your device is not connected to the Internet or the connection is too slow.");