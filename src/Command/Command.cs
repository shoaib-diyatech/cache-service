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
        if (args.Length < 2) return new Response { RequestId = requestId, Code = 400, Message = "Invalid arguments for CREATE command." };
        string key = args[0];
        string value = args[1];
        bool success = _cacheManager.Create(key, value);
        return new Response { RequestId = requestId, Code = success ? 200 : 409, Message = success ? $"Created {key}" : "Key already exists." };
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
        if (args.Length < 1) return new Response { RequestId = requestId, Code = 400, Message = "Invalid arguments for READ command." };
        string key = args[0];
        return new Response { RequestId = requestId, Code = _cacheManager.Read(key, out string value) ? 200 : 404, Message = value };
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
        if (args.Length < 2) return new Response { RequestId = requestId, Code = 400, Message = "Invalid arguments for UPDATE command." };
        string key = args[0];
        string value = args[1];
        return new Response { RequestId = requestId, Code = _cacheManager.Update(key, value) ? 200 : 404, Message = "Key not found." };
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
        if (args.Length < 1) return new Response { RequestId = requestId, Code = 400, Message = "Invalid arguments for DELETE command." };
        string key = args[0];
        bool isDeleted = _cacheManager.Delete(key);
        return new Response { RequestId = requestId, Code = isDeleted ? 200 : 404, Message = isDeleted ? "Key Deleted Successfully" : "Key not found." };
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
        if (args.Length < 1) return new Response { RequestId = requestId, Code = 400, Message = "Invalid arguments for MEM command." };
        string memoryUsage = _cacheManager.getCurrentMemoryUsageInMB().ToString();
        return new Response { RequestId = requestId, Code = (_cacheManager.getCurrentMemoryUsageInMB() > 0) ? 200 : 404, Message = memoryUsage };
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
        if (args.Length < 0) return new Response { RequestId = requestId, Code = 400, Message = "Invalid arguments for FLUSH ALL command." };
        return new Response { RequestId = requestId, Code = _cacheManager.FlushAll() ? 200 : 404, Message = "Key not found." };
    }
}