// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

internal static class ItemMapperFactory
{
    internal static Func<StructuredFieldItem, T> BuildParseDelegate<T>(
        ValueMapping<T> valueMapping, ParameterMapping<T>[] parameters)
        where T : class, new()
    {
        return item =>
        {
            var instance = new T();
            valueMapping.Setter(instance, ItemTypeResolver.ExtractValue(
                valueMapping.Kind, item.Value, valueMapping.ClrType, "item value"));

            foreach (var parameter in parameters)
            {
                if (item.Parameters.TryGetValue(parameter.Key, out var value))
                {
                    parameter.Setter(instance, ItemTypeResolver.ExtractValue(
                        parameter.Kind, value, parameter.ClrType, $"parameter '{parameter.Key}'"));
                }
                else if (parameter.IsRequired)
                {
                    throw new StructuredFieldParseException($"Missing required parameter '{parameter.Key}'.");
                }
            }

            return instance;
        };
    }

    internal static Func<T, StructuredFieldItem> BuildSerializeDelegate<T>(
        ValueMapping<T> valueMapping, ParameterMapping<T>[] parameters)
        where T : class, new()
    {
        return instance =>
        {
            var rawValue = valueMapping.Getter(instance)
                ?? throw new InvalidOperationException("Item value is required but the property is null.");
            var item = new StructuredFieldItem(ItemTypeResolver.ToItem(valueMapping.Kind, rawValue));

            foreach (var parameter in parameters)
            {
                var value = parameter.Getter(instance);
                if (value is null)
                {
                    if (parameter.IsRequired)
                        throw new InvalidOperationException(
                            $"Parameter '{parameter.Key}' is required but the property is null.");
                    continue;
                }

                item.Parameters.Add(parameter.Key, ItemTypeResolver.ToItem(parameter.Kind, value));
            }

            return item;
        };
    }
}
