namespace App.WindowsService;
public class RequestHandler
{
    private readonly CacheManager _cacheManager = new CacheManager();

    public string ProcessRequest(string request)
    {
        string[] parts = request.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return "Invalid request format.";

        bool success;
        string key = parts[1];
        string value = parts.Length > 2 ? parts[2] : "";
        CommandType command = Enum.TryParse(parts[0], true, out CommandType parsedCommand) ? parsedCommand : CommandType.UNKNOWN;

        return command switch
        {
            CommandType.CREATE => _cacheManager.Create(key, value) ? $"Created {key}" : "Key already exists.",
            CommandType.READ => _cacheManager.Read(key, out string result) ? result : "Key not found.",
            CommandType.UPDATE => _cacheManager.Update(key, value) ? $"Updated {key}" : "Key not found or modified.",
            CommandType.DELETE => _cacheManager.Delete(key) ? $"Deleted {key}" : "Key not found.",
            _ => "Unknown command."
        };
    }
}
