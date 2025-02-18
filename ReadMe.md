# Cache Service

This is a worker service project that can be run as a Windows service. The port and the memory size of the cache are configurable from the settings file.

## Configuration

In the `appsettings.json` file, you can configure the cache settings:

```json
"CacheSettings": {
    "Port": 5000,
    "CacheSize": 100
}
```

## Publishing the Project

To publish the project, use the following command:

```sh
dotnet publish -c Release -r win-x64 --self-contained false
```

## Creating the Windows Service

To create the Windows service, use the following command:

```sh
sc.exe create ".net Cache" binpath="E:\cache-service\bin\Release\net9.0\win-x64\publish\cache-service.exe"
```

## Starting the Service

To start the service, use the following command:

```sh
sc.exe start ".net Cache"
```

## Stopping the Service

To stop the service, use the following command:

```sh
sc.exe stop ".net Cache"
```

## Deleting the Service

To delete the service, use the following command:

```sh
sc.exe delete ".net Cache"
```

## Running the Executable

To run the executable and check if it's working, use the following command:

```sh
.\cache-service.exe
```

## Configuring the Log File Location

You can configure the log file location in the `log4net.config` file:

```xml
<file value="cache-service.log" />
```

## Test the server

You can connect to the server via Telenet

```bash
Telnet 127.0.0.1 5000
```
Send the commands, here is the format:
```bash
<RequestId><space><Command><space><args>
```

Expects the following commands:
```
	reqid:001 CREATE key1 123
	
	reqid:001 READ key1
	
	reqid:001 UPDATE key1 456989898
	
	reqid:001 READ key1
	
	reqid:001 DELETE key1
	
	reqid:001 READ key1
	
	reqid:002 MEM ?
	
	reqid:001 FLUSHALL
```

Replies back in a json format