// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using K53Guru.Application.Common.Interfaces;

namespace K53Guru.Infrastructure.Configurations;

/// <summary>
/// AI configuration settings implementation
/// </summary>
public class AISettings : IAISettings
{
    /// <summary>
    /// AI configuration key constraint
    /// </summary>
    public const string Key = nameof(AISettings);

    /// <summary>
    /// Gets or sets the Gemini API key
    /// </summary>
    public string GeminiApiKey { get; set; } = string.Empty;
} 
