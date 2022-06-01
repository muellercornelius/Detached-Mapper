﻿using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Detached.Mappers.TypeOptions
{
    public interface IMemberOptions
    {
        Dictionary<string, object> Annotations { get; }
 
        string Name { get; } 

        Type ClrType { get; }

        bool CanRead { get; }

        Expression BuildGetterExpression(Expression instance, Expression context);

        bool CanWrite { get; }

        Expression BuildSetterExpression(Expression instance, Expression value, Expression context);
    }
}