using System;
using System.Reflection;
using System.Runtime.Serialization;
using AutoMapper;
using K53Guru.Application.Common.Interfaces;
using K53Guru.Application.Features.AuditTrails.DTOs;
using K53Guru.Application.Features.Contacts.DTOs;
using K53Guru.Application.Features.Documents.DTOs;
using K53Guru.Application.Features.Identity.DTOs;
using K53Guru.Application.Features.PicklistSets.DTOs;
using K53Guru.Application.Features.Products.DTOs;
using K53Guru.Application.Features.SystemLogs.DTOs;
using K53Guru.Application.Features.Tenants.DTOs;
using K53Guru.Domain.Entities;
using K53Guru.Domain.Identity;
using NUnit.Framework;

namespace K53Guru.Application.UnitTests.Common.Mappings;
public class MappingTests
{
    private readonly IConfigurationProvider _configuration;
    private readonly IMapper _mapper;

    public MappingTests()
    {
        _configuration = new MapperConfiguration(cfg => cfg.AddMaps(Assembly.GetAssembly(typeof(IApplicationDbContext))));
        _mapper = _configuration.CreateMapper();
    }

    [Test]
    public void ShouldHaveValidConfiguration()
    {
        _configuration.AssertConfigurationIsValid();
    }

    [Test]
    [TestCase(typeof(Document), typeof(DocumentDto), true)]
    [TestCase(typeof(Tenant), typeof(TenantDto), true)]
    [TestCase(typeof(Product), typeof(ProductDto), true)]
    [TestCase(typeof(Contact), typeof(ContactDto), true)]
    [TestCase(typeof(PicklistSet), typeof(PicklistSetDto), true)]
    [TestCase(typeof(ApplicationUser), typeof(ApplicationUserDto), false)]
    [TestCase(typeof(ApplicationRole), typeof(ApplicationRoleDto), false)]
    [TestCase(typeof(SystemLog), typeof(SystemLogDto), false)]
    [TestCase(typeof(AuditTrail), typeof(AuditTrailDto), false)]
    public void ShouldSupportMappingFromSourceToDestination(Type source, Type destination, bool inverseMap = false)
    {
        var instance = GetInstanceOf(source);

        _mapper.Map(instance, source, destination);

        if (inverseMap)
        {
            ShouldSupportMappingFromSourceToDestination(destination, source, false);
        }
    }

    private object GetInstanceOf(Type type)
    {
        if (type.GetConstructor(Type.EmptyTypes) != null)
            return Activator.CreateInstance(type);

        throw new InvalidOperationException($"Type {type.FullName} does not have a parameterless constructor.");
    }
}
