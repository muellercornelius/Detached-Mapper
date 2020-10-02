﻿using Detached.Mappers.Context;
using Detached.Mappers.Model;
using Detached.Mappers.Patching;
using Detached.Mappers.TypeMaps;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Linq.Expressions;
using Xunit;
using static Detached.Mappers.Expressions.ExtendedExpression;
using static System.Linq.Expressions.Expression;

namespace Detached.Mappers.Tests.Model
{
    public class PropertiesTests
    {
        [Fact]
        public void customize_property()
        {
            MapperOptions modelOptions = new MapperOptions();
            modelOptions.Configure<TargetEntity>()
                        .Member(m => m.Value)
                        .Setter(Lambda(
                                Parameter(typeof(TargetEntity), out Expression entity),
                                Parameter(typeof(int), out Expression value),
                                Parameter(typeof(IMapperContext), out Expression context),
                                Block(
                                    Assign(Property(entity, nameof(TargetEntity.Value)), value),
                                    Call("Add", Field(entity, "_modified"), Constant(nameof(TargetEntity.Value)))
                                )
                            ));

            Mapper mapper = new Mapper(modelOptions);

            var result = mapper.Map<SourceEntity, TargetEntity>(new SourceEntity { Value = 2 });

            bool contains = result.IsSet(nameof(TargetEntity.Value));
            Assert.True(contains);
        }

        public class SourceEntity
        {
            public int Value { get; set; }
        }

        public class TargetEntity : IPatch
        {
            public int Value { get; set; }

            readonly HashSet<string> _modified = new HashSet<string>();

            public bool IsSet(string name)
            {
                return _modified.Contains(name);
            }

            public void Reset()
            {
                _modified.Clear();
            }
        }
    }
}