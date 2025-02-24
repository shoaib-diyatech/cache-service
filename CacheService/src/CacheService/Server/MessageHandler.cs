namespace App.WindowsService;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text.Json;
using System.Text;
using log4net;

/// <summary>
/// Abstract class for handling messages, provides a method to process messages, Messages could be Requests or Events
/// Adds response to the blocking responseQueue
/// </summary>
public abstract class MessageHandler
{
    //Todo: Add this class as super class of EventHandler and RequestHandler
    private static readonly ILog log = LogManager.GetLogger(typeof(MessageHandler));

    private readonly BlockingCollection<(TcpClient, Response)> _responseQueue;

    public MessageHandler(BlockingCollection<(TcpClient, Response)> responseQueue)
    {
        _responseQueue = responseQueue;
    }

    public abstract void Process();
}