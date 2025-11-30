#if TOOLS
using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Robust.Client.Utility;
using Robust.Shared.Console;
using Robust.Shared.IoC;
using Robust.Shared.Log;
using Robust.Shared.Network;

namespace Robust.Client.Console.Commands
{
    internal sealed class LauncherAuthCommand : LocalizedCommands
    {
        [Dependency] private readonly IAuthManager _auth = default!;
        [Dependency] private readonly IGameControllerInternal _gameController = default!;

        public override string Command => "launchauth";
        public override string Help => "Usage: launchauth <optional username> <optional server id> <optional server url>";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            string? username = null;
            string? serverId = null;
            string? serverUrl = null;
            if (args.Length > 0)
                username = args[0];
            if (args.Length > 1)
                serverId = args[1];
            if (args.Length > 2)
                serverUrl = args[2];

            var basePath = UserDataDir.GetRootUserDataDir(_gameController);
            var launcherDirName = Environment.GetEnvironmentVariable("SS14_LAUNCHER_APPDATA_NAME") ?? "launcher";
            var dbPath = Path.Combine(basePath, launcherDirName, "settings.db");

            #if USE_SYSTEM_SQLITE
                SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_sqlite3());
            #endif
            using var con = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
            con.Open();
            using var cmd = con.CreateCommand();
            cmd.CommandText = "SELECT UserId, UserName, Token, Server, ServerUrl FROM Login WHERE Expires > datetime('NOW')";

            if (username != null)
            {
                cmd.CommandText += " AND UserName = @userName";
                cmd.Parameters.AddWithValue("@userName", username);
            }

            if (serverId != null)
            {
                cmd.CommandText += " AND Server = @serverId";
                cmd.Parameters.AddWithValue("@serverId", serverId);
                if (serverId == IAuthManager.CustomServerId)
                {
                    if (serverUrl == null)
                    {
                        shell.WriteLine("Custom server requires a URL");
                        return;
                    }

                    cmd.CommandText += " AND ServerUrl = @serverUrl";
                    cmd.Parameters.AddWithValue("@serverUrl", serverUrl);
                }
                else if (serverUrl != null)
                {
                    shell.WriteLine("Server URL is only valid for custom servers");
                    return;
                }
            }

            cmd.CommandText += " LIMIT 1;";
            using var reader = cmd.ExecuteReader();

            if (!reader.Read())
            {
                shell.WriteLine("Unable to find a matching login");
                return;
            }

            var userId = Guid.Parse(reader.GetString(0));
            var userName = reader.GetString(1);
            var token = reader.GetString(2);
            serverUrl = reader.GetString(4);

            _auth.Token = token;
            _auth.UserId = new NetUserId(userId);
            _auth.UserServer = new("unset", new(serverUrl));

            shell.WriteLine($"Logged into account {userName}@{reader.GetString(3)} ({serverUrl})");
        }
    }
}

#endif
