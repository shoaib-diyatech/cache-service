namespace App.WindowsService;
public class Request
{

    //  private readonly CommandFactory _commandFactory;

    //      public Request(IOptions<CacheSettings> cacheSettings, CacheManager cacheManager)
    // {
    //     _commandFactory = new CommandFactory(cacheManager);
    // }
    public string RequestId { get; set; }
    public ICommand Command { get; set; }
    public string[] Args { get; set; }

    // Static method to parse the custom string format and create a Request object
    public static Request Parse(string requestString, CommandFactory commandFactory)
    {
        // Split the string by spaces
        string[] parts = requestString.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) throw new ArgumentException("Invalid request format.");

        // Extract the requestId, commandType, and arguments
        string requestId = parts[0];
        string commandType = parts[1];
        string[] args = parts[2].Split(' ');

        // Get the command from the command factory
        ICommand command = commandFactory.GetCommand(commandType);
        if (command == null) throw new ArgumentException("Invalid command type.");

        // Create and return the Request object
        return new Request
        {
            RequestId = requestId,
            Command = command,
            Args = args
        };
    }
}

public class Response
{
    public string RequestId { get; set; }
    public int Code { get; set; }
    public string Message { get; set; }
}