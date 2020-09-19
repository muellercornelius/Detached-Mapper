﻿using Detached.EntityFramework.Queries;
using Detached.Mapping;
using Detached.Mapping.Context;
using Detached.Mapping.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

namespace Detached.EntityFramework.Context
{
    public class EntityFrameworkMapperContext : IMapperContext
    {
        public EntityFrameworkMapperContext(DbContext dbContext, QueryProvider queryProvider, MappingOptions mapperOptions)
        {
            QueryProvider = queryProvider;
            MapperOptions = mapperOptions;
            DbContext = dbContext;
        }

        public MappingOptions MapperOptions { get; }

        public QueryProvider QueryProvider { get; }

        public DbContext DbContext { get; }

        public TTarget OnMapperAction<TTarget, TSource, TKey>(TTarget entity, TSource source, TKey key, MapperActionType actionType)
            where TTarget : class
            where TSource : class
            where TKey : IEntityKey
        {
            if (actionType == MapperActionType.Load)
            {
                TTarget loadedEntity = QueryProvider.Load(DbContext.Set<TTarget>(), source);
                
                if (loadedEntity == null)
                    loadedEntity = GetExistingEntry<TTarget, TKey>(key)?.Entity;

                if (loadedEntity == null && !MapperOptions.RootUpsert)
                    throw new MapperException($"Entity {typeof(TTarget)} with key [{string.Join(", ", key.ToObject())}] does not exist.");
                
                return loadedEntity;
            }
            else
            {
                EntityEntry<TTarget> entry = GetExistingEntry<TTarget, TKey>(key);
                if (entry == null)
                    entry = DbContext.Entry(entity);

                switch (actionType)
                {
                    case MapperActionType.Attach:
                        if (MapperOptions.CreateAggregations && entry.GetDatabaseValues() == null)
                            entry.State = EntityState.Added;
                        else
                            entry.State = EntityState.Unchanged;
                        break;
                    case MapperActionType.Create:
                        entry.State = EntityState.Added;
                        break;
                    case MapperActionType.Delete:
                        entry.State = EntityState.Deleted;
                        break;
                    case MapperActionType.Update:
                        // Do nothing, as change tracking should detect the changes.
                        break;
                }

                return entry.Entity;
            }
        }

        public EntityEntry<TEntity> GetExistingEntry<TEntity, TKey>(TKey key)
            where TEntity : class
            where TKey : IEntityKey
        {
            IStateManager stateManager = DbContext.GetService<IStateManager>();

            IEntityType entityType = DbContext.Model.FindEntityType(typeof(TEntity));
            IKey keyType = entityType.FindPrimaryKey();

            InternalEntityEntry internalEntry = stateManager.TryGetEntry(keyType, key.ToObject());
            if (internalEntry != null)
                return new EntityEntry<TEntity>(internalEntry);
            else
                return null;
        }
    }
}