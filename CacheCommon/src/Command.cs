namespace CacheCommon;

public class CreateCommand : ICommand
{
    public String Key { get; set; }
    public String Value { get; set; }
    public long TTL { get; set; }

    /// <summary>
    /// Parse the command string and create a CreateCommand object.
    /// Expects the string to be of the form "CREATE key value [ttl]" or after the CREATE keyword: "key value [ttl]"
    /// </summary>
    /// <param name="commandString"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ICommand Parse(string commandString)
    {
        string[] parts = commandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) throw new ArgumentException("Invalid arguments for CREATE command.");

        int startIndex = parts[0].ToUpper() == "CREATE" ? 1 : 0;
        if (parts.Length - startIndex < 2) throw new ArgumentException("Invalid arguments for CREATE command.");

        string key = parts[startIndex];
        string value = parts[startIndex + 1];
        long ttl = 0;
        if (parts.Length > startIndex + 2) long.TryParse(parts[startIndex + 2], out ttl);

        return new CreateCommand(key, value, ttl);
    }
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

    /// <summary>
    /// Parse the command string and create a ReadCommand object.
    /// Expects the string to be of the form "READ key" or just "key"
    /// </summary>
    /// <param name="commandString"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ICommand Parse(string commandString)
    {
        string[] parts = commandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1) throw new ArgumentException("Invalid arguments for READ command.");
        int startIndex = parts[0].ToUpper() == "READ" ? 1 : 0;
        if (parts.Length - startIndex < 1) throw new ArgumentException("Invalid arguments for READ command.");
        string key = parts[startIndex];
        return new ReadCommand(key);
    }
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

    /// <summary>
    /// Parse the command string and create an UpdateCommand object.
    /// Expects the string to be of the form "UPDATE key value [ttl]" or after the UPDATE keyword: "key value [ttl]"
    /// </summary>
    /// <param name="commandString"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ICommand Parse(string commandString)
    {
        string[] parts = commandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) throw new ArgumentException("Invalid arguments for UPDATE command.");
        int startIndex = parts[0].ToUpper() == "UPDATE" ? 1 : 0;
        if (parts.Length - startIndex < 2) throw new ArgumentException("Invalid arguments for UPDATE command.");
        string key = parts[startIndex];
        string value = parts[startIndex + 1];
        long ttl = 0;
        if (parts.Length > startIndex + 2) long.TryParse(parts[startIndex + 2], out ttl);
        return new UpdateCommand(key, value, ttl);
    }
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

    /// <summary>
    /// Parse the command string and create a DeleteCommand object.
    /// Expects the string to be of the form "DELETE key" or just "key"
    /// </summary>
    /// <param name="commandString"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ICommand Parse(string commandString)
    {
        string[] parts = commandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1) throw new ArgumentException("Invalid arguments for DELETE command.");
        int startIndex = parts[0].ToUpper() == "DELETE" ? 1 : 0;
        if (parts.Length - startIndex < 1) throw new ArgumentException("Invalid arguments for DELETE command.");
        string key = parts[startIndex];
        return new DeleteCommand(key);
    }

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

    public ICommand Parse(string commandString)
    {
        return new MemCommand();
    }
}

public class FlushAllCommand : ICommand
{
    public bool Validate(string[] args)
    {
        if (args.Length < 0) throw new ArgumentException("Invalid arguments for FLUSH ALL command.");
        return true;
    }

    public ICommand Parse(string commandString) { return new FlushAllCommand(); }
}

/// <summary>
/// Command to subscribe to events
/// Command Syntax: <requestId><space>SUB<space>eventType
/// </summary>
public class SubCommand : ICommand
{
    public EventName EventType { get; set; }

    /// <summary>
    /// Parse the command string and create a SubCommand object.
    /// Expects the string to be of the form "SUB eventType" or just "eventType"
    /// </summary>
    /// <param name="commandString"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ICommand Parse(string commandString)
    {
        string[] parts = commandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) throw new ArgumentException("Invalid arguments for SUB command.");
        if (!Enum.TryParse<EventName>(parts[1], true, out EventName eventType)) throw new ArgumentException($"Invalid event type: {parts[1]}.");
        return new SubCommand(eventType);
    }

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

/// <summary>
/// Parse the command string and create a UnsubCommand object.
/// Expects the string to be of the form "UNSUB eventType" or just "eventType"
/// </summary>
/// <param name="commandString"></param>
/// <returns></returns>
/// <exception cref="ArgumentException"></exception>
public class UnsubCommand : ICommand
{
    public EventName EventType { get; set; }
    /// <summary>
    /// Parse the command string and create an UnsubCommand object.
    /// Expects the string to be of the form "UNSUB eventType" or just "eventType"
    /// </summary>
    /// <param name="commandString"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public ICommand Parse(string commandString)
    {
        string[] parts = commandString.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) throw new ArgumentException("Invalid arguments for UNSUB command.");
        if (!Enum.TryParse<EventName>(parts[1], true, out EventName eventType)) throw new ArgumentException($"Invalid event type: {parts[1]}.");
        return new UnsubCommand(eventType);
    }
    public UnsubCommand()
    {
        EventType = EventName.Unknown;
    }
    public UnsubCommand(EventName eventType)
    {
        EventType = eventType;
    }
    public bool Validate(string[] args)
    {
        if (args.Length < 1) throw new ArgumentException("Invalid arguments for UNSUB command.");
        if (!Enum.TryParse<EventName>(args[0], true, out _)) throw new ArgumentException($"Invalid event type: {args[0]}.");
        return true;
    }
    override public string ToString()
    {
        return $"UNSUB {EventType}";
    }
}

public class UnknownCommand : ICommand
{
    public bool Validate(string[] args)
    {
        // No validation needed for unknown command
        return true;
    }

    public ICommand Parse(string commandString)
    {
        return new UnknownCommand();
    }
}