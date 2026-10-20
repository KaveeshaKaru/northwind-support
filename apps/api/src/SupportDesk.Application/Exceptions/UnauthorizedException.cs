namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when the caller could not be authenticated. Surfaces as HTTP 401; the message is
/// shown to the caller, so it must never say which part of a sign-in was wrong.
/// </summary>
public sealed class UnauthorizedException(string message) : Exception(message);
