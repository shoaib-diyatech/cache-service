namespace CacheCommon;
public class CommandFactory
{

    public ICommand GetCommand(string commandType)
    {
        return commandType.ToUpper() switch
        {
            "CREATE" => new CreateCommand(),
            "READ" => new ReadCommand(),
            "UPDATE" => new UpdateCommand(),
            "DELETE" => new DeleteCommand(),
            "MEM" => new MemCommand(),
            "SUB" => new SubCommand(),
            "FLUSHALL" => new FlushAllCommand(),
            _ => new UnknownCommand()
        };
    }
}