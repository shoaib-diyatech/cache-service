namespace CacheCommon;

public class CreateCommand : ICommand
{
    public String Key { get; set; }
    public String Value { get; set; }
    public long TTL { get; set; }

    public CreateCommand()
    {
        Key = "";
        Value = "";
        TTL = 0;
    }
    public CreateCommand(String key, String value)
    {
        Key = key;
        Value = value;
        TTL = 0;
    }
    public CreateCommand(String key, String value, long ttl)
    {
        Key = key;
        Value = value;
        TTL = ttl;
    }

    public bool Validate(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("Invalid arguments for CREATE command.");
        return true;
    }

    public override string ToString()
    {
        return $"CREATE {Key} {Value} {TTL}";
    }
}

public class ReadCommand : ICommand
{
    public String Key { get; set; }

    public ReadCommand()
    {
        Key = "";
    }
    public ReadCommand(String key)
    {
        Key = key;
    }
    public bool Validate(string[] args)
    {
        if (args.Length < 1) throw new ArgumentException("Invalid arguments for READ command.");
        return true;
    }

    override public string ToString()
    {
        return $"READ {Key}";
    }
}

public class UpdateCommand : ICommand
{
    public String Key { get; set; }
    public String Value { get; set; }
    public long TTL { get; set; }
    public UpdateCommand()
    {
        Key = "";
        Value = "";
        TTL = 0;
    }
    public UpdateCommand(String key, String value)
    {
        Key = key;
        Value = value;
        TTL = 0;
    }
    public UpdateCommand(String key, String value, long ttl)
    {
        Key = key;
        Value = value;
        TTL = ttl;
    }
    public bool Validate(string[] args)
    {
        if (args.Length < 2) throw new ArgumentException("Invalid arguments for UPDATE command.");
        return true;
    }
    override
    public string ToString()
    {
        return $"UPDATE {Key} {Value} {TTL}";
    }
}

public class DeleteCommand : ICommand
{
    public String Key { get; set; }
    public DeleteCommand()
    {
        Key = "";
    }
    public DeleteCommand(String key)
    {
        Key = key;
    }
    public bool Validate(string[] args)
    {
        if (args.Length < 1) throw new ArgumentException("Invalid arguments for DELETE command.");
        return true;
    }
    override
    public string ToString()
    {
        return $"DELETE {Key}";
    }
}

public class MemCommand : ICommand
{
    public bool Validate(string[] args)
    {
        if (args.Length < 1) throw new ArgumentException("Invalid arguments for MEM command.");
        return true;
    }
}

public class FlushAllCommand : ICommand
{
    public bool Validate(string[] args)
    {
        if (args.Length < 0) throw new ArgumentException("Invalid arguments for FLUSH ALL command.");
        return true;
    }
}

/// <summary>
/// Command to subscribe to events
/// Command Syntax: <requestId><space>SUB<space>eventType
/// </summary>
public class SubCommand : ICommand
{
    public EventName EventType { get; set; }

    public SubCommand()
    {
        EventType = EventName.Unknown;
    }
    public SubCommand(EventName eventType)
    {
        EventType = eventType;
    }
    public bool Validate(string[] args)
    {
        if (args.Length < 1) throw new ArgumentException("Invalid arguments for SUB command.");
        if (!Enum.TryParse<EventName>(args[0], true, out _)) throw new ArgumentException($"Invalid event type: {args[0]}.");
        return true;
    }
    override public string ToString()
    {
        return $"SUB {EventType}";
    }
}

public class UnknownCommand : ICommand
{
    public bool Validate(string[] args)
    {
        // No validation needed for unknown command
        return true;
    }
}