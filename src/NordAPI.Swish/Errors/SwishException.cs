using System;
using System.Net;

namespace NordAPI.Swish.Errors;

public class SwishException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public string? ResponseBody { get; }

    public SwishException(string message, HttpStatusCode? statusCode = null, string? responseBody = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}

public class SwishAuthException : SwishException
{
    public SwishAuthException(string message, HttpStatusCode code, string? body = null)
        : base(message, code, body) { }
}

public class SwishValidationException : SwishException
{
    public SwishValidationException(string message, HttpStatusCode code, string? body = null)
        : base(message, code, body) { }
}

public class SwishConflictException : SwishException
{
    public SwishConflictException(string message, HttpStatusCode code, string? body = null)
        : base(message, code, body) { }
}

public class SwishTransientException : SwishException
{
    public SwishTransientException(string message, HttpStatusCode? code = null, string? body = null, Exception? inner = null)
        : base(message, code, body, inner) { }
}
