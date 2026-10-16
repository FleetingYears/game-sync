if (args.Length == 1 && args[0] == "--network-probe")
    return GameSync.Server.Shared.Network.NetworkProbeServer.Run();

if (args.Length != 0)
{
    Console.Error.WriteLine("用法：dotnet run --project server/server.csproj -- [--network-probe]");
    return 2;
}

Console.WriteLine("Hello, World!");
return 0;
