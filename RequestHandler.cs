namespace App.WindowsService;
using Microsoft.Extensions.Options;
public sealed class RequestHandler
{
    private readonly CacheManager _cacheManager;

    private readonly CacheSettings _cacheSettings;

    public RequestHandler(IOptions<CacheSettings> cacheSettings, CacheManager cacheManager)
    {
        // Injecting depencdenices via DI
        _cacheSettings = cacheSettings.Value;
        _cacheManager = cacheManager;
    }
    public string ProcessRequest(string request)
    {
        try
        {
            string[] parts = request.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return "Invalid request format.";

            bool success;
            string key = parts[1];
            string value = parts.Length > 2 ? parts[2] : "";
            //byte[] valueBytes = Encoding.UTF8.GetBytes(value);
            string ttl = parts.Length > 3 ? parts[3] : ""; // if ttl is present in ADD and UPDATE commands
            CommandType command = Enum.TryParse(parts[0], true, out CommandType parsedCommand) ? parsedCommand : CommandType.UNKNOWN;

            return command switch
            {
                CommandType.CREATE => _cacheManager.Create(key, value) ? $"Created {key}" : "Key already exists.",
                CommandType.READ => _cacheManager.Read(key, out string result) ? result : "Key not found.",
                CommandType.UPDATE => _cacheManager.Update(key, value) ? $"Updated {key}" : "Key not found or modified.",
                CommandType.DELETE => _cacheManager.Delete(key) ? $"Deleted {key}" : "Key not found.",
                CommandType.MEM => $"Memory usage: {_cacheManager.getCurrentMemoryUsageInMB()} MB",
                _ => "Unknown command."
            };
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
