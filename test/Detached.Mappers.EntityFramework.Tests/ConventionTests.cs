﻿using Detached.Mappers.Annotations;
using Detached.Mappers.EntityFramework.Tests.Model;
using Detached.Mappers.TypeOptions;
using Detached.Mappers.TypeOptions.Class;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using Xunit;

namespace Detached.Mappers.EntityFramework.Tests
{
    public class ConventionTests
    {
        [Fact]
        public async Task apply_conventions_to_fluent()
        {
            TestDbContext context = new TestDbContext(await TestDbContext.CreateOptionsAsync("ConfigDb"));

            MapperOptions mapperOptions = context.GetInfrastructure().GetService<MapperOptions>();

            ITypeOptions typeOptions = mapperOptions.GetTypeOptions(typeof(ConventionTestClass));

            Assert.True(typeOptions.IsEntity());
            Assert.True(typeOptions.GetMember(nameof(ConventionTestClass.CustomizedKey1)).IsKey());
            Assert.True(typeOptions.GetMember(nameof(ConventionTestClass.CustomizedKey1)).IsKey());
            Assert.False(typeOptions.GetMember(nameof(ConventionTestClass.Id)).IsKey());
        }
    }

    public class ConventionTestClass
    {
        public int Id { get; set; }

        public int CustomizedKey1 { get; set; }

        public int CustomizedKey2 { get; set; }

        public string Name { get; set; }

        public static void Configure(MapperOptions mapperOptions)
        {
            mapperOptions.Configure<ConventionTestClass>().Member(c => c.CustomizedKey1).IsKey(true);
        }
    }
}