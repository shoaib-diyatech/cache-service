namespace CacheCommon;
public class Response
{
    public string RequestId { get; set; }
    public ResponseType Type { get; set; }
    public Code Code { get; set; }
    public string Message { get; set; }
    public object Value {get; set;}
}

public enum ResponseType
{
    Response,
    Event,
    Error
}

public enum Code
{
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