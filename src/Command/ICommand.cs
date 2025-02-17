namespace App.WindowsService;
public interface ICommand
{
    Response Execute(string requestId, string[] args);
}