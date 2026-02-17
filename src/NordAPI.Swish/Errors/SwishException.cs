using System;
using System.Net;

namespace NordAPI.Swish.Errors;

/// <summary>
/// Base exception type for Swish SDK errors.
/// </summary>
public class SwishException : Exception
{
    /// <summary>
    /// The HTTP status code returned by the Swish API, if available.
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// The raw response body returned by the Swish API, if available.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    /// Creates a new <see cref="SwishException"/>.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="statusCode">The HTTP status code, if available.</param>
    /// <param name="responseBody">The raw response body, if available.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public SwishException(string message, HttpStatusCode? statusCode = null, string? responseBody = null, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}

/// <summary>
/// Thrown when the Swish API returns an authentication/authorization error.
/// </summary>
public sealed class SwishAuthException : SwishException
{
    /// <summary>
    /// Creates a new <see cref="SwishAuthException"/>.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="responseBody">The raw response body, if available.</param>
    public SwishAuthException(string message, HttpStatusCode statusCode, string? responseBody = null)
        : base(message, statusCode, responseBody)
    {
    }
}

/// <summary>
/// Thrown when the Swish API returns a validation error (HTTP 400).
/// </summary>
public sealed class SwishValidationException : SwishException
{
    /// <summary>
    /// Creates a new <see cref="SwishValidationException"/>.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="responseBody">The raw response body, if available.</param>
    public SwishValidationException(string message, HttpStatusCode statusCode, string? responseBody = null)
        : base(message, statusCode, responseBody)
    {
    }
}

/// <summary>
/// Thrown when the Swish API returns a conflict error (HTTP 409).
/// </summary>
public sealed class SwishConflictException : SwishException
{
    /// <summary>
    /// Creates a new <see cref="SwishConflictException"/>.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="responseBody">The raw response body, if available.</param>
    public SwishConflictException(string message, HttpStatusCode statusCode, string? responseBody = null)
        : base(message, statusCode, responseBody)
    {
    }
}

/// <summary>
/// Thrown for transient errors where retry may be appropriate.
/// </summary>
public sealed class SwishTransientException : SwishException
{
    /// <summary>
    /// Creates a new <see cref="SwishTransientException"/>.
    /// </summary>
    /// <param name="message">A human-readable error message.</param>
    /// <param name="statusCode">The HTTP status code, if available.</param>
    /// <param name="responseBody">The raw response body, if available.</param>
    /// <param name="innerException">The underlying exception, if any.</param>
    public SwishTransientException(string message, HttpStatusCode? statusCode = null, string? responseBody = null, Exception? innerException = null)
        : base(message, statusCode, responseBody, innerException)
    {
    }
}

/// <summary>
/// Thrown when the Swish SDK is misconfigured (e.g., missing required mTLS certificate).
/// </summary>
public sealed class SwishConfigurationException : SwishException
{
    /// <summary>Creates a new SwishConfigurationException.</summary>
    public SwishConfigurationException(string message)
        : base(message)
    {
    }
    /// <summary>Creates a new SwishConfigurationException with an inner exception.</summary>
    public SwishConfigurationException(string message, Exception innerException)
        : base(message, null, null, innerException)
    {
    }
}

