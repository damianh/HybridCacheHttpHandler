// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Linq.Expressions;
using System.Reflection;

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Compiles property getters and setters from expression trees for efficient reuse.
/// </summary>
internal static class PropertyAccessor
{
    /// <summary>
    /// Compiles a typed getter and a boxed setter from a property-access expression.
    /// </summary>
    /// <typeparam name="T">The declaring type.</typeparam>
    /// <typeparam name="TValue">The property value type.</typeparam>
    /// <param name="expression">A lambda that accesses the property (e.g. <c>x => x.Urgency</c>).</param>
    /// <returns>A compiled getter and a setter that accepts a boxed value.</returns>
    internal static (Func<T, TValue> getter, Action<T, TValue> setter) Compile<T, TValue>(
        Expression<Func<T, TValue>> expression)
    {
        var property = GetProperty(expression);
        var getter = expression.Compile();

        // Build setter: (T instance, TValue value) => instance.Property = value
        var instanceParam = Expression.Parameter(typeof(T), "instance");
        var valueParam = Expression.Parameter(typeof(TValue), "value");
        var setterLambda = Expression.Lambda<Action<T, TValue>>(
            Expression.Assign(
                Expression.Property(instanceParam, property),
                valueParam),
            instanceParam, valueParam);

        var setter = setterLambda.Compile();
        return (getter, setter);
    }

    /// <summary>
    /// Returns the <see cref="PropertyInfo"/> for a property-access expression.
    /// </summary>
    internal static PropertyInfo GetProperty<T, TValue>(Expression<Func<T, TValue>> expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        if (expression.Body is not MemberExpression member ||
            member.Expression != expression.Parameters[0] ||
            member.Member is not PropertyInfo property ||
            property.PropertyType != typeof(TValue))
        {
            throw new ArgumentException(
                "Expression must directly access an instance property of its lambda parameter (x => x.Property).",
                nameof(expression));
        }

        if (property.GetMethod is not { IsStatic: false } ||
            property.SetMethod is not { IsStatic: false } ||
            property.GetIndexParameters().Length != 0)
        {
            throw new ArgumentException(
                $"Property '{property.Name}' must have instance getter and setter accessors.",
                nameof(expression));
        }

        return property;
    }
}
