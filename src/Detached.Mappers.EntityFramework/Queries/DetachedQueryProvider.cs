﻿using AgileObjects.ReadableExpressions;
using Detached.Mappers.Annotations;
using Detached.Mappers.Exceptions;
using Detached.Mappers.TypeOptions;
using Detached.Mappers.TypeOptions.Class;
using Detached.RuntimeTypes.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using static Detached.RuntimeTypes.Expressions.ExtendedExpression;
using static System.Linq.Expressions.Expression;

namespace Detached.Mappers.EntityFramework.Queries
{
    public class DetachedQueryProvider
    {
        readonly MapperOptions _options;
        IMemoryCache _memoryCache;

        public DetachedQueryProvider(MapperOptions options, IMemoryCache memoryCache)
        {
            _options = options;
            _memoryCache = memoryCache;
        }

        public IQueryable<TProjection> Project<TEntity, TProjection>(IQueryable<TEntity> query)
            where TProjection : class
            where TEntity : class
        {
            var key = new DetachedQueryCacheKey(typeof(TEntity), typeof(TProjection), QueryType.Projection);

            var filter = _memoryCache.GetOrCreate(key, entry =>
            {
                ITypeOptions entityType = _options.GetTypeOptions(typeof(TEntity));
                ITypeOptions projectionType = _options.GetTypeOptions(typeof(TProjection));

                var param = Parameter(entityType.ClrType, "e");
                Expression projection = ToLambda(entityType.ClrType, param, CreateSelectProjection(entityType, projectionType, param));

                entry.SetSize(1);

                return (Expression<Func<TEntity, TProjection>>)projection;
            });

            return query.Select(filter);
        }

        public Task<TTarget> LoadAsync<TSource, TTarget>(IQueryable<TTarget> queryable, TSource source)
            where TSource : class
            where TTarget : class
        {
            DetachedQueryTemplate<TSource, TTarget> queryTemplate = GetTemplate<TSource, TTarget>();
            IQueryable<TTarget> query = queryTemplate.Render(queryable, source);

            return query.FirstOrDefaultAsync();
        }

        public TTarget Load<TSource, TTarget>(IQueryable<TTarget> queryable, TSource source)
            where TSource : class
            where TTarget : class
        {
            DetachedQueryTemplate<TSource, TTarget> queryTemplate = GetTemplate<TSource, TTarget>();
            IQueryable<TTarget> query = queryTemplate.Render(queryable, source);

            return query.FirstOrDefault();
        }

        DetachedQueryTemplate<TSource, TTarget> GetTemplate<TSource, TTarget>()
            where TSource : class
            where TTarget : class
        {
            var key = new DetachedQueryCacheKey(typeof(TSource), typeof(TTarget), QueryType.Load);

            return _memoryCache.GetOrCreate(key, entry =>
            {
                ITypeOptions sourceType = _options.GetTypeOptions(typeof(TSource));
                ITypeOptions targetType = _options.GetTypeOptions(typeof(TTarget));

                DetachedQueryTemplate<TSource, TTarget> queryTemplate = new DetachedQueryTemplate<TSource, TTarget>();

                queryTemplate.SourceConstant = Constant(null, sourceType.ClrType);
                queryTemplate.FilterExpression = CreateFilterExpression<TSource, TTarget>(sourceType, targetType, queryTemplate.SourceConstant);
                queryTemplate.Includes = GetIncludes(sourceType, targetType);

                entry.SetSize(1);

                return queryTemplate;
            });
        }

        Expression<Func<TTarget, bool>> CreateFilterExpression<TSource, TTarget>(ITypeOptions sourceType, ITypeOptions targetType, ConstantExpression sourceConstant)
        {
            var targetParam = Parameter(targetType.ClrType, "e");

            Expression expression = null;

            foreach (string targetMemberName in targetType.MemberNames)
            {
                IMemberOptions targetMember = targetType.GetMember(targetMemberName);
                if (targetMember.IsKey())
                {
                    string sourceMemberName = _options.GetSourcePropertyName(sourceType, targetType, targetMemberName);

                    IMemberOptions sourceMember = sourceType.GetMember(sourceMemberName);
                    if (sourceMember == null)
                    {
                        throw new MapperException($"Can't build query filter, key member {sourceMemberName} not found.");
                    }

                    var targetExpr = targetMember.BuildGetExpression(targetParam, null);
                    var sourceExpr = sourceMember.BuildGetExpression(sourceConstant, null);

                    if (sourceExpr.Type.IsNullable(out _))
                    {
                        sourceExpr = Property(sourceExpr, "Value");
                    }

                    if (expression == null)
                    {
                        expression = Equal(targetExpr, sourceExpr);
                    }
                    else
                    {
                        expression = And(expression, Equal(targetExpr, sourceExpr));
                    }
                }
            }

            return Lambda<Func<TTarget, bool>>(expression, targetParam);
        }

        List<string> GetIncludes(ITypeOptions sourceType, ITypeOptions targetType)
        {
            List<string> result = new List<string>();
            Stack<ITypeOptions> stack = new Stack<ITypeOptions>();

            GetIncludes(sourceType, targetType, stack, null, result);

            for (int i = result.Count - 1; i >= 0; i--)
            {
                string descendantPrefix = result[i] + ".";
                if (result.Any(i => i.StartsWith(descendantPrefix)))
                {
                    result.RemoveAt(i);
                }
            }

            return result;
        }

        void GetIncludes(ITypeOptions sourceType, ITypeOptions targetType, Stack<ITypeOptions> stack, string prefix, List<string> result)
        {
            stack.Push(targetType);

            foreach (string targetMemberName in targetType.MemberNames)
            {
                IMemberOptions targetMember = targetType.GetMember(targetMemberName);
                if (!targetMember.IsParent())
                {
                    string sourceMemberName = _options.GetSourcePropertyName(sourceType, targetType, targetMemberName);

                    IMemberOptions sourceMember = sourceType.GetMember(sourceMemberName);
                    if (sourceMember != null)
                    {
                        ITypeOptions sourceMemberType = _options.GetTypeOptions(sourceMember.ClrType);
                        ITypeOptions targetMemberType = _options.GetTypeOptions(targetMember.ClrType);

                        if (!stack.Contains(targetMemberType))
                        {
                            if (targetMemberType.IsCollection())
                            {
                                string name = prefix + targetMember.Name;
                                result.Add(name);

                                if (targetMember.IsComposition())
                                {
                                    ITypeOptions sourceItemType = _options.GetTypeOptions(sourceMemberType.ItemClrType);
                                    ITypeOptions targetItemType = _options.GetTypeOptions(targetMemberType.ItemClrType);

                                    GetIncludes(sourceItemType, targetItemType, stack, name + ".", result);
                                }

                            }
                            else if (targetMemberType.IsComplexOrEntity())
                            {
                                string name = prefix + targetMember.Name;
                                result.Add(name);

                                if (targetMember.IsComposition())
                                {
                                    GetIncludes(sourceMemberType, targetMemberType, stack, name + ".", result);
                                }
                            }
                        }
                    }
                }
            }

            stack.Pop();
        }

        Expression CreateSelectProjection(ITypeOptions entityType, ITypeOptions projectionType, Expression entityExpr)
        {
            if (entityType.IsCollection())
            {
                ITypeOptions entityItemType = _options.GetTypeOptions(entityType.ItemClrType);
                ITypeOptions projectionItemType = _options.GetTypeOptions(projectionType.ItemClrType);

                var param = Parameter(entityItemType.ClrType, "e");
                var itemMap = CreateSelectProjection(entityItemType, projectionItemType, param);

                LambdaExpression lambda = ToLambda(entityItemType.ClrType, param, itemMap);

                return Call("ToList", typeof(Enumerable), Call("Select", typeof(Enumerable), entityExpr, lambda));
            }
            else if (entityType.IsComplexOrEntity())
            {
                List<MemberBinding> bindings = new List<MemberBinding>();

                foreach (string targetMemberName in entityType.MemberNames)
                {
                    IMemberOptions entityMember = entityType.GetMember(targetMemberName);

                    if (entityMember.CanRead && !entityMember.IsNotMapped())
                    {
                        IMemberOptions projectionMember = projectionType.GetMember(targetMemberName);

                        if (projectionMember != null && projectionMember.CanWrite && !projectionMember.IsNotMapped())
                        {
                            PropertyInfo propInfo = projectionMember.GetPropertyInfo();
                            if (propInfo != null)
                            {
                                ITypeOptions projectionMemberType = _options.GetTypeOptions(projectionMember.ClrType);
                                ITypeOptions entityMemberType = _options.GetTypeOptions(entityMember.ClrType);

                                Expression map = entityMember.BuildGetExpression(entityExpr, null);
                                Expression body = CreateSelectProjection(entityMemberType, projectionMemberType, map);

                                if (entityMemberType.IsComplexOrEntity())
                                {
                                    map = Condition(NotEqual(map, Constant(null, map.Type)), body, Constant(null, body.Type));
                                }
                                else
                                {
                                    map = body;
                                }

                                bindings.Add(Bind(propInfo, map));
                            }
                        }
                    }
                }

                return MemberInit(New(projectionType.ClrType), bindings.ToArray());
            }
            else
            {
                return entityExpr;
            }
        }

        LambdaExpression ToLambda(Type type, ParameterExpression paramExpr, Expression body)
        {
            Type funcType = typeof(Func<,>).MakeGenericType(type, body.Type);
            var lambda = Lambda(funcType, body, new[] { paramExpr });
            return lambda;
        }
    }
}