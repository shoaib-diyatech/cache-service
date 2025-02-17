namespace App.WindowsService;
public class CommandFactory
{
    private readonly CacheManager _cacheManager;

    public CommandFactory(CacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    public ICommand GetCommand(string commandType)
    {
        return commandType.ToUpper() switch
        {
            "CREATE" => new CreateCommand(_cacheManager),
            "READ" => new ReadCommand(_cacheManager),
            "UPDATE" => new UpdateCommand(_cacheManager),
            "DELETE" => new DeleteCommand(_cacheManager),
            "MEM" => new MemCommand(_cacheManager),
            _ => null
        };
    }
}