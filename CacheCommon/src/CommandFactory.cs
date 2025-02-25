namespace CacheCommon;
public class CommandFactory
{
    // Todo: make GetCommand method static
    public static ICommand GetCommand(string commandType)
    {
        return commandType.ToUpper() switch
        {
            "CREATE" => new CreateCommand(),
            "READ" => new ReadCommand(),
            "UPDATE" => new UpdateCommand(),
            "DELETE" => new DeleteCommand(),
            "MEM" => new MemCommand(),
            "SUB" => new SubCommand(),
            "UNSUB" => new UnsubCommand(),
            "FLUSHALL" => new FlushAllCommand(),
            _ => new UnknownCommand()
        };
    }
}