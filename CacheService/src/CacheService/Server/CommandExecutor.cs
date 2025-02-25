namespace App.WindowsService;

using CacheCommon;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


public class CommandExecutor
{

    private readonly CacheManager _cacheManager;

    public CommandExecutor(CacheManager cacheManager)
    {
        _cacheManager = cacheManager;
    }

    /// <summary>
    /// Executes the command and returns the response.
    /// Implements the execution of all the supported command types
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public Response Execute(String requestId, ICommand command)
    {
        if (command == null) throw new ArgumentNullException(nameof(command));
        else if (command is CreateCommand)
        {
            CreateCommand createCommand = (CreateCommand)command;
            bool success = _cacheManager.Create(createCommand.Key, createCommand.Value);
            return new Response { RequestId = requestId, Code = success ? Code.Success : Code.Conflict, Type = success ? ResponseType.Response : ResponseType.Error, Message = success ? $"Created {createCommand.Key}" : "Key already exists." };
        }
        else if (command is ReadCommand)
        {
            ReadCommand readCommand = (ReadCommand)command;
            object value = _cacheManager.Read(readCommand.Key);
            return new Response { RequestId = requestId, Code = value != null ? Code.Success : Code.NotFound, Type = value != null ? ResponseType.Response : ResponseType.Error, Message = value != null ? $"Read {readCommand.Key}" : "Key not found.", Value = value };
        }
        else if (command is UpdateCommand)
        {
            UpdateCommand updateCommand = (UpdateCommand)command;
            bool success = _cacheManager.Update(updateCommand.Key, updateCommand.Value);
            return new Response { RequestId = requestId, Code = success ? Code.Success : Code.NotFound, Type = success ? ResponseType.Response : ResponseType.Error, Message = success ? $"Updated {updateCommand.Key}" : "Key not found." };
        }
        else if (command is DeleteCommand)
        {
            DeleteCommand deleteCommand = (DeleteCommand)command;
            try
            {
                _cacheManager.Delete(deleteCommand.Key);
                return new Response { RequestId = requestId, Code = Code.Success, Type = ResponseType.Response, Message = $"Deleted {deleteCommand.Key}" };
            }
            catch (Exception ex)
            {
                return new Response { RequestId = requestId, Code = Code.NotFound, Type = ResponseType.Error, Message = ex.Message };

            }
        }
        else if (command is SubCommand)
        {
            SubCommand subCommand = (SubCommand)command;
            return new Response { RequestId = requestId, Code = Code.Success, Type = ResponseType.Event, Message = $"Subscribed to {subCommand.EventType}" };
        }
        else if (command is UnsubCommand)
        {
            UnsubCommand unsubCommand = (UnsubCommand)command;
            return new Response { RequestId = requestId, Code = Code.Success, Type = ResponseType.Event, Message = $"Unsubscribed from {unsubCommand.EventType}" };
        }
        else if (command is FlushAllCommand)
        {
            FlushAllCommand flushAllCommand = (FlushAllCommand)command;
            try
            {
                _cacheManager.Clear();
                return new Response { RequestId = requestId, Code = Code.Success, Type = ResponseType.Response, Message = "Flushed all keys" };
            }
            catch (Exception ex)
            {
                return new Response { RequestId = requestId, Code = Code.InternalServerError, Type = ResponseType.Error, Message = ex.Message };
            }
        }
        else
        {
            return new Response { Code = Code.BadRequest, Message = "Invalid command" };
        }
    }
}

