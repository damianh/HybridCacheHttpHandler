// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

using System.Globalization;

namespace DamianH.Http.StructuredFieldValues;

/// <summary>An immutable RFC 9651 Date, with the full signed structured integer range.</summary>
public sealed class DateItem : BareItem
{
    /// <summary>Creates a date from seconds since the Unix epoch.</summary>
    public DateItem(long unixSeconds)
    {
        if (unixSeconds is < IntegerItem.MinValue or > IntegerItem.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(unixSeconds), unixSeconds, "Date is outside the RFC 9651 range.");
        }
        UnixSeconds = unixSeconds;
    }

    /// <summary>Gets seconds since the Unix epoch, independent of CLR date limits.</summary>
    public long UnixSeconds { get; }

    /// <inheritdoc/>
    public override object Value => UnixSeconds;

    /// <inheritdoc/>
    public override ItemType Type => ItemType.Date;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is DateItem other && UnixSeconds == other.UnixSeconds;

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Type, UnixSeconds);

    /// <inheritdoc/>
    public override string ToString() => $"Date({UnixSeconds.ToString(CultureInfo.InvariantCulture)})";
}
