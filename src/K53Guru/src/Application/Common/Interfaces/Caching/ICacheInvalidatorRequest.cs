// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace K53Guru.Application.Common.Interfaces.Caching;

public interface ICacheInvalidatorRequest<TResponse> : IRequest<TResponse>
{
    string CacheKey => string.Empty;
    IEnumerable<string>? Tags { get; }
}
