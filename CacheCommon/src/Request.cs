using System.Runtime.CompilerServices;

namespace CacheCommon;
public class Request
{
    public string RequestString { get; private set; }
    public string RequestId { get; set; }

    public RequestType Type { get; set; }
    public ICommand Command { get; set; }
    public string[] Args { get; set; }

    private Request() { }

    public static Request Create()
    {
        String reqId = Guid.NewGuid().ToString(); // Generate a unique request ID.
        return new Request
        {
            RequestId = reqId,
            Type = RequestType.Command,
            Command = new UnknownCommand(),
            Args = new string[] { "" }
        };
    }

    public static Request Create(ICommand command)
    {

        String reqId = Guid.NewGuid().ToString(); // Generate a unique request ID.
        return new Request
        {
            RequestId = reqId,
            Type = RequestType.Command,
            Command = command,
            Args = new string[] { "" }
        }
        ;
    }

    /// <summary>
    /// Parse the request string and create a Request object
    /// The request string is of the form <requestId><space><commandType><space><args>
    /// </summary>
    /// <param name="requestString"></param>
    /// <param name="commandFactory"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static Request Parse(string requestString)
    {
        Request request = new Request();
        request.RequestString = requestString;

        // Split the string by spaces
        string[] parts;
        try
        {
            parts = requestString.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        }
        catch (Exception)
        {
            //throw new ArgumentException("Invalid request format.");
            return new Request
            {
                RequestId = "0",
                Command = new UnknownCommand(),
                Args = new string[] { "" }
            };
        }
        if (parts.Length < 2)
        {
            return new Request
            {
                RequestId = "0",
                Command = new UnknownCommand(),
                Args = new string[] { "" }
            };
        }

        // Extract the requestId, commandType, and arguments
        // Not using the json parser here since the requestString is not a json string
        // requestString is of the form of <requestId><space><commandType><space><args>
        string requestId = parts[0];
        string commandType = parts[1];
        string[] args;
        if (parts.Length < 3)
        {
            args = new string[] { "" };
        }
        else { args = parts[2].Split(' '); }

        // Validate the request ID
        if (string.IsNullOrWhiteSpace(requestId)) throw new ArgumentException("Invalid request ID.");

        // Get the command from the command factory
        ICommand command = CommandFactory.GetCommand(commandType);
        if (command == null) throw new ArgumentException("Invalid command type.");

        try
        {
            // Validate the command arguments
            command.Validate(args);
        }
        catch (Exception)
        {
            throw new ArgumentException($"Invalid arguments for command: {commandType}.");
        }

        // Create and return the Request object
        return new Request
        {
            RequestId = requestId,
            Command = command,
            Type = RequestType.Command,
            Args = args
        };
    }
}

public enum RequestType
{
    Unknown,
    Command,
    Event
}






