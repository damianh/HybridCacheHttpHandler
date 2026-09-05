// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues.Mapping;

/// <summary>
/// Builds parse and serialize delegates from a <see cref="DictionaryBuilder{T}"/> configuration.
/// </summary>
internal static class DictionaryMapperFactory
{
    /// <summary>
    /// Builds a parse delegate that converts a <see cref="StructuredFieldDictionary"/> into a <typeparamref name="T"/>.
    /// </summary>
    internal static Func<StructuredFieldDictionary, T> BuildParseDelegate<T>(MemberMapping<T>[] members)
        where T : class, new()
    {
        return dict =>
        {
            var instance = new T();

            foreach (var member in members)
            {
                if (!dict.TryGetValue(member.Key, out var dictMember))
                {
                    if (member.IsRequired)
                        throw new StructuredFieldParseException(
                            $"Missing required dictionary member '{member.Key}'.");
                    continue; // optional member absent — leave property at default
                }

                if (member.IsInnerList)
                {
                    ParseInnerListMember(instance, member, dictMember);
                }
                else
                {
                    if (!dictMember.IsItem)
                        throw new StructuredFieldParseException(
                            $"Dictionary member '{member.Key}' must be an item, not an inner list.");

                    var extracted = ItemTypeResolver.ExtractValue(
                        member.Kind,
                        dictMember.Item.Value,
                        member.ClrType,
                        $"dictionary member '{member.Key}'");
                    member.Setter(instance, extracted);
                }
            }

            return instance;
        };
    }

    /// <summary>
    /// Builds a serialize delegate that converts a <typeparamref name="T"/> into a <see cref="StructuredFieldDictionary"/>.
    /// </summary>
    internal static Func<T, StructuredFieldDictionary> BuildSerializeDelegate<T>(MemberMapping<T>[] members)
        where T : class, new()
    {
        return instance =>
        {
            var dict = new StructuredFieldDictionary();

            foreach (var member in members)
            {
                var rawValue = member.Getter(instance);

                if (rawValue == null)
                {
                    if (member.IsRequired)
                        throw new InvalidOperationException(
                            $"Dictionary member '{member.Key}' is required but the property is null.");
                    continue;
                }

                if (member.IsInnerList)
                {
                    dict.Add(member.Key, SerializeInnerListMember(member, rawValue));
                }
                else
                {
                    var item = ItemTypeResolver.ToItem(member.Kind, rawValue);
                    dict.Add(member.Key, StructuredFieldMember.FromItem(item));
                }
            }

            return dict;
        };
    }

    private static void ParseInnerListMember<T>(T instance, MemberMapping<T> member, StructuredFieldMember dictMember)
    {
        if (!dictMember.IsInnerList)
            throw new StructuredFieldParseException(
                $"Dictionary member '{member.Key}' must be an inner list.");

        var config = member.InnerList!;
        var innerList = dictMember.InnerList;

        if (config.IsNestedItem)
        {
            // Nested structured items: each element mapped by nested item mapper
            var elements = innerList.Items
                .Select(item => config.NestedItemParseDelegate!(item))
                .ToList();

            // Build a typed IReadOnlyList<TElement> by reflection
            var typedList = CreateTypedReadOnlyList(config.ElementClrType, elements);
            member.Setter(instance, typedList);
        }
        else
        {
            // Primitive or token elements
            var elements = innerList.Items
                .Select(item =>
                    ItemTypeResolver.ExtractValue(config.ElementKind, item.Value, config.ElementClrType,
                        $"inner list element in '{member.Key}'"))
                .ToList();

            var typedList = CreateTypedReadOnlyList(config.ElementClrType, elements);
            member.Setter(instance, typedList);
        }
    }

    private static StructuredFieldMember SerializeInnerListMember<T>(MemberMapping<T> member, object rawValue)
    {
        var config = member.InnerList!;
        var collection = (System.Collections.IEnumerable)rawValue;
        var innerList = new InnerList();

        foreach (var element in collection)
        {
            if (element is null)
                throw new InvalidOperationException($"Inner list '{member.Key}' contains a null element.");
            innerList.Add(config.IsNestedItem
                ? config.NestedItemSerializeDelegate!(element)
                : new StructuredFieldItem(ItemTypeResolver.ToItem(config.ElementKind, element)));
        }

        return StructuredFieldMember.FromInnerList(innerList);
    }

    private static object CreateTypedReadOnlyList(Type elementType, System.Collections.IList elements)
    {
        // Build a List<T> and cast to IReadOnlyList<T>
        var listType = typeof(List<>).MakeGenericType(elementType);
        var list = (System.Collections.IList)Activator.CreateInstance(listType)!;
        foreach (var e in elements)
            list.Add(e);
        return list;
    }
}
