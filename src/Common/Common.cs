namespace App.WindowsService;
public class Request
{
    public string RequestId { get; set; }
    public ICommand Command { get; set; }
    public string[] Args { get; set; }

    /// <summary>
    /// Parse the request string and create a Request object
    /// The request string is of the form <requestId><space><commandType><space><args>
    /// </summary>
    /// <param name="requestString"></param>
    /// <param name="commandFactory"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static Request Parse(string requestString, CommandFactory commandFactory)
    {
        // Split the string by spaces
        string[] parts = requestString.Split(' ', 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 3) throw new ArgumentException("Invalid request format.");

        // Extract the requestId, commandType, and arguments
        // Not using the json parser here since the requestString is not a json string
        // requestString is of the form of <requestId><space><commandType><space><args>
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

public enum Type{
    Response,
    Event,
    Error
}

public enum Code{
    Success = 200,
    Created = 201,
    NoContent = 204,
    BadRequest = 400,
    Unauthorized = 401,
    Forbidden = 403,
    NotFound = 404,
    Conflict = 409,
    InternalServerError = 500
}

public class Response
{
    public string RequestId { get; set; }
    public Type Type { get; set; }
    public Code Code { get; set; }
    public string Message { get; set; }
}