﻿using Detached.Mappers.EntityFramework.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;

namespace Detached.Mappers.EntityFramework
{
    public static class Package
    {
        public static DbContextOptionsBuilder UseDetached(this DbContextOptionsBuilder builder, Action<MapperOptions> configure = null)
        {
            MapperOptions mapperOptions = new MapperOptions();
            ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(new DetachedDbContextOptionsExtension(mapperOptions, configure));
            return builder;
        }

        public static DbContextOptionsBuilder<TDbContext> UseDetached<TDbContext>(this DbContextOptionsBuilder<TDbContext> builder, Action<MapperOptions> configure = null)
            where TDbContext : DbContext
        {

            MapperOptions mapperOptions = new MapperOptions();
            ((IDbContextOptionsBuilderInfrastructure)builder).AddOrUpdateExtension(new DetachedDbContextOptionsExtension(mapperOptions, configure));
            return builder;
        }
    }
}