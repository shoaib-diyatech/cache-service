namespace App.WindowsService;

public class CreateCommand : ICommand
{
    private readonly CacheManager _cacheManager;

    public CreateCommand(CacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    public Response Execute(string requestId, string[] args)
    {
        if (args.Length < 2) return new Response { RequestId = requestId, Code = Code.BadRequest, Type = Type.Error, Message = "Invalid arguments for CREATE command." };
        string key = args[0];
        string value = args[1];
        bool success = _cacheManager.Create(key, value);
        return new Response { RequestId = requestId, Code = success ? Code.Success : Code.Conflict, Type = success ? Type.Response : Type.Error, Message = success ? $"Created {key}" : "Key already exists." };
    }
}

public class AddCommand : ICommand
{
    private readonly CacheManager _cacheManager;

    public AddCommand(CacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    public Response Execute(string requestId, string[] args)
    {
        if (args.Length < 3) return new Response { RequestId = requestId, Code = Code.BadRequest, Type = Type.Error, Message = "Invalid arguments for ADD command." };
        string key = args[0];
        string value = args[1];
        string ttl = args[2];
        long ttlValue = -1;
        long.TryParse(ttl, out ttlValue);
        if (ttlValue < 0) return new Response { RequestId = requestId, Code = Code.BadRequest, Message = "Invalid TTL value." };
        bool success = _cacheManager.Create(key, value, ttlValue);
        return new Response { RequestId = requestId, Code = success ? Code.Success : Code.Conflict, Type = success ? Type.Response : Type.Error, Message = success ? $"Created {key}" : "Key already exists." };
    }
}

public class ReadCommand : ICommand
{
    private readonly CacheManager _cacheManager;

    public ReadCommand(CacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    public Response Execute(string requestId, string[] args)
    {
        if (args.Length < 1) return new Response { RequestId = requestId, Code = Code.BadRequest, Type = Type.Error, Message = "Invalid arguments for READ command." };
        string key = args[0];
        bool success = _cacheManager.Read(key, out string value);
        return new Response { RequestId = requestId, Code = success ? Code.Success : Code.NotFound, Type = success ? Type.Response : Type.Error, Message = value };
    }
}

public class UpdateCommand : ICommand
{
    private readonly CacheManager _cacheManager;

    public UpdateCommand(CacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    public Response Execute(string requestId, string[] args)
    {
        if (args.Length < 2) return new Response { RequestId = requestId, Code = Code.BadRequest, Type = Type.Error, Message = "Invalid arguments for UPDATE command." };
        string key = args[0];
        string value = args[1];
        bool success = _cacheManager.Update(key, value);
        return new Response { RequestId = requestId, Code = success ? Code.Success : Code.NotFound, Type = success ? Type.Response : Type.Error, Message = success ? $"key: {key} Updated successfully" : $"key: {key} not found." };
    }
}

public class DeleteCommand : ICommand
{
    private readonly CacheManager _cacheManager;

    public DeleteCommand(CacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    public Response Execute(string requestId, string[] args)
    {
        if (args.Length < 1) return new Response { RequestId = requestId, Code = Code.BadRequest, Type = Type.Error, Message = "Invalid arguments for DELETE command." };
        string key = args[0];
        bool isDeleted = _cacheManager.Delete(key);
        return new Response { RequestId = requestId, Code = isDeleted ? Code.Success : Code.NotFound, Type = isDeleted ? Type.Response : Type.Error, Message = isDeleted ? "Key Deleted Successfully" : "Key not found." };
    }
}

public class MemCommand : ICommand
{
    private readonly CacheManager _cacheManager;

    public MemCommand(CacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    public Response Execute(string requestId, string[] args)
    {
        if (args.Length < 1) return new Response { RequestId = requestId, Code = Code.BadRequest, Message = "Invalid arguments for MEM command." };
        string memoryUsage = _cacheManager.GetCurrentMemoryUsageInMB().ToString();
        return new Response { RequestId = requestId, Code = Code.Success, Type = Type.Response, Message = memoryUsage };
    }
}

public class FlushAllCommand : ICommand
{
    private readonly CacheManager _cacheManager;

    public FlushAllCommand(CacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    public Response Execute(string requestId, string[] args)
    {
        if (args.Length < 0) return new Response { RequestId = requestId, Code = Code.BadRequest, Type = Type.Error, Message = "Invalid arguments for FLUSH ALL command." };
        bool success = _cacheManager.FlushAll();
        return new Response { RequestId = requestId, Code = success ? Code.Success : Code.NotFound, Type = success ? Type.Response : Type.Error, Message = "Key not found." };
    }
}

/// <summary>
/// Command to subscribe to events
/// Command Syntax: <requestId><space>SUB<space>eventType
/// </summary>
public class SubCommand : ICommand
{
    private readonly CacheManager _cacheManager;

    public SubCommand(CacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    public Response Execute(string requestId, string[] args)
    {
        if (args.Length < 1) return new Response { RequestId = requestId, Code = Code.BadRequest, Type = Type.Error, Message = "Invalid arguments for SUB command." };
        string eventN = args[0];

        if (Enum.TryParse<EventName>(eventN, true, out EventName eventName))
        {
            return new Response { RequestId = requestId, Code = Code.Success, Type = Type.Response, Message = $"Subscribed to {eventName} event successfully." };
        }
        else
        {
            return new Response { RequestId = requestId, Code = Code.BadRequest, Type = Type.Error, Message = $"Invalid event type: {eventN}." };
        }
    }
}

public class UnknownCommand : ICommand
{
    public Response Execute(string requestId, string[] args)
    {
        return new Response { RequestId = requestId, Code = Code.BadRequest, Type = Type.Error, Message = "Unknown command." };
    }
}