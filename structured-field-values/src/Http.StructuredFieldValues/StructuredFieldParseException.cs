// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.

namespace DamianH.Http.StructuredFieldValues;

/// <summary>
/// Exception thrown when parsing of a structured field value fails.
/// </summary>
public class StructuredFieldParseException : FormatException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldParseException"/> class.
    /// </summary>
    public StructuredFieldParseException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldParseException"/> class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public StructuredFieldParseException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldParseException"/> class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public StructuredFieldParseException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="StructuredFieldParseException"/> class with a message and position.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="position">The position in the input where the error occurred.</param>
    public StructuredFieldParseException(string message, int position) : base($"{message} at position {position}") => Position = position;

    /// <summary>Creates a parse error with an input position and an underlying decoding error.</summary>
    public StructuredFieldParseException(string message, int position, Exception innerException)
        : base($"{message} at position {position}", innerException) => Position = position;

    /// <summary>
    /// Gets the position in the input where the error occurred, if known.
    /// </summary>
    public int? Position { get; }
}
