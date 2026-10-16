using GameSync.Server.Shared.Network;

if (args.Length == 1 && args[0] == "--network-probe")
    return NetworkProbeServer.Run();

if (args.Length >= 1 && args[0] == "--network-server" && args.Length <= 2)
{
    int port = 9050;
    if (args.Length == 2 && (!int.TryParse(args[1], out port) || port < 1 || port > 65535))
    {
        Console.Error.WriteLine("端口必须为 1～65535。");
        return 2;
    }
    return SessionServer.Run(port);
}

if (args.Length == 0)
    return SessionServer.Run(9050);

Console.Error.WriteLine("用法：dotnet run --project server/server.csproj -- [--network-probe | --network-server [port]]");
return 2;
