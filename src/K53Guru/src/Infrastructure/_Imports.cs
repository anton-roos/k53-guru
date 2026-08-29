// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

global using System.Security.Claims;
global using AutoMapper;
global using AutoMapper.QueryableExtensions;
global using K53Guru.Application.Common.Interfaces;
global using K53Guru.Application.Common.Interfaces.Identity;
global using K53Guru.Application.Common.Models;
global using K53Guru.Infrastructure.Persistence;
global using K53Guru.Infrastructure.Persistence.Extensions;
global using K53Guru.Infrastructure.Services;
global using K53Guru.Infrastructure.Services.Identity;
global using K53Guru.Domain.Entities;
global using Microsoft.AspNetCore.Components.Authorization;
global using Microsoft.AspNetCore.Identity;
global using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
global using Microsoft.EntityFrameworkCore;
global using Microsoft.Extensions.DependencyInjection;
global using Microsoft.Extensions.Logging;
global using Microsoft.Extensions.Options;
