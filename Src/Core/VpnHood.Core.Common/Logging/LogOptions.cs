﻿using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace VpnHood.Core.Common.Logging;

public class LogOptions
{
    public bool LogToConsole { get; set; } = true;
    public bool LogToFile { get; set; } = true;
    public bool? LogAnonymous { get; set; } 
    public bool AutoFlush { get; set; } = true;
    public string[] LogEventNames { get; set; } = [];
    public bool SingleLineConsole { get; set; } = true;
    public string? GlobalScope { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LogLevel LogLevel { get; set; } = LogLevel.Information;
}