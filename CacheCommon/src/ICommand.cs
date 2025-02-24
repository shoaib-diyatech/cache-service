namespace CacheCommon;
public interface ICommand
{
    bool Validate(string[] args);
}