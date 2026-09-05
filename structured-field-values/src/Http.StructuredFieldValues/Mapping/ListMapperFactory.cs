// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Builds parse and serialize delegates from a <see cref="ListBuilder{T}"/> configuration.
/// </summary>
internal static class ListMapperFactory
{
    /// <summary>
    /// Builds a parse delegate that converts a <see cref="StructuredFieldList"/> into a <typeparamref name="T"/>.
    /// </summary>
    internal static Func<StructuredFieldList, T> BuildParseDelegate<T>(ListElementConfig<T> config)
        where T : class, new()
    {
        return list =>
        {
            var instance = new T();
            var elements = new List<object>();

            foreach (var member in list.Members)
            {
                if (!member.IsItem)
                    throw new StructuredFieldParseException(
                        "List members that are inner lists are not supported by this mapper. " +
                        "Use a dictionary mapper for inner-list structures.");

                var item = member.Item;

                if (config.IsNestedItem)
                {
                    elements.Add(config.NestedItemParseDelegate!(item));
                }
                else
                {
                    elements.Add(ItemTypeResolver.ExtractValue(
                        config.ElementKind,
                        item.Value,
                        config.ElementClrType,
                        "list element"));
                }
            }

            var typedList = CreateTypedReadOnlyList(config.ElementClrType, elements);
            config.Setter(instance, typedList);
            return instance;
        };
    }

    /// <summary>
    /// Builds a serialize delegate that converts a <typeparamref name="T"/> into a <see cref="StructuredFieldList"/>.
    /// </summary>
    internal static Func<T, StructuredFieldList> BuildSerializeDelegate<T>(ListElementConfig<T> config)
        where T : class, new()
    {
        return instance =>
        {
            var list = new StructuredFieldList();
            var rawValue = config.Getter(instance);

            if (rawValue == null)
                return list; // empty list for null collection

            var collection = (System.Collections.IEnumerable)rawValue;

            foreach (var element in collection)
            {
                if (element is null)
                    throw new InvalidOperationException("List contains a null element.");
                list.Add(config.IsNestedItem
                    ? config.NestedItemSerializeDelegate!(element)
                    : new StructuredFieldItem(ItemTypeResolver.ToItem(config.ElementKind, element)));
            }

            return list;
        };
    }

    private static object CreateTypedReadOnlyList(Type elementType, List<object> elements)
    {
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        foreach (var e in elements)
            list.Add(e);
        return list;
    }
}
