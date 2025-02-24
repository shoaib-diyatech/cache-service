namespace App.WindowsService;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Processes the <see cref="Request"/> and returns a <see cref="Response"/>. 
/// </summary>
public sealed class RequestHandler
{
    private readonly CommandFactory _commandFactory;

    public RequestHandler(IOptions<CacheSettings> cacheSettings, CacheManager serviceProvider)
    {
        _commandFactory = new CommandFactory(serviceProvider);
    }

    public CommandFactory CommandFactory => _commandFactory;

    public Response ProcessRequest(Request request)
    {
        try
        {
            return request.Command.Execute(request.RequestId, request.Args);
        }
        catch (Exception ex)
        {
            return new Response { RequestId = request.RequestId, Code = Code.InternalServerError, Message = ex.Message };
        }
    }
}
