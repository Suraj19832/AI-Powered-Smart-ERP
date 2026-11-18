namespace SmartERP.Application.Common;

public class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.");

/// <summary>A business rule was violated (maps to HTTP 400).</summary>
public class BusinessRuleException(string message) : Exception(message);

public class UnauthorizedException(string message) : Exception(message);
