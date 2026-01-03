namespace ActoEngine.WebApi.Config;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }

    protected DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}

// Domain/Exceptions/NotFoundException.cs
public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message) { }

    public NotFoundException(string name, object key)
        : base($"{name} with key '{key}' was not found") { }
}

// Domain/Exceptions/ValidationException.cs
public class ValidationException : DomainException
{
    public ValidationException(string message) : base(message) { }

    public ValidationException(string message, Exception innerException)
        : base(message, innerException) { }
}

// Domain/Exceptions/BusinessRuleViolationException.cs
public class BusinessRuleViolationException(string message) : DomainException(message) { }

// Domain/Exceptions/DuplicateException.cs
public class DuplicateException : DomainException
{
    public DuplicateException(string message) : base(message) { }

    public DuplicateException(string name, object key)
        : base($"{name} with key '{key}' already exists") { }
}