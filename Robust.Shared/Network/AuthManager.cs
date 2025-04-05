using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Robust.Shared.Configuration;
using Robust.Shared.Serialization;


namespace Robust.Shared.Network
{
    // Basically turbo-lightweight IConfigurationManager for the purposes of auth var loading from env.

    /// <summary>
    ///     Stores client authentication parameters.
    /// </summary>
    internal interface IAuthManager
    {
        public const string CustomServerId = "Custom";
        public const string DefaultServerUrl = "https://auth.spacestation14.com/";

        NetUserId? UserId { get; set; }
        AuthServer UserServer { get; set; }
        HashSet<AuthServer> Servers { get; set; }
        string? Token { get; set; }
        string? PubKey { get; set; }

        /// <summary>
        /// If true, the user allows HWID information to be provided to servers.
        /// </summary>
        bool AllowHwid { get; set; }

        void LoadFromEnv();
    }

    public sealed class AuthServer(string id, Uri authUrl)
    {
        public string Id { get; } = id;
        public Uri AuthUrl { get; } = authUrl;

        /// Returns a string representation of the auth server
        /// <example>"Space-Wizards@https://auth.spacestation14.com/"</example>
        public override string ToString() => $"{Id}@{AuthUrl}";


        /// Returns a string representation of a list of auth servers
        /// <example>"Space-Wizards@https://auth.spacestation14.com/,SimpleStation@https://auth.simplestation.org/"</example>
        public static string ToStringList(HashSet<AuthServer> servers) => string.Join(',', servers.Select(s => s.ToString()));

        /// Takes a representation of an auth server and returns an AuthServer object
        /// <example>"Space-Wizards@https://auth.spacestation14.com/"</example>
        public static AuthServer FromString(string str)
        {
            var parts = str.Split('@');
            if (parts.Length != 2)
                throw new ArgumentException($"Invalid auth server string: {str}");

            return new(parts[0], new(parts[1]));
        }

        /// Takes a list of auth server representations and returns a HashSet of AuthServer objects
        /// <example>"Space-Wizards@https://auth.spacestation14.com/,SimpleStation@https://auth.simplestation.org/"</example>
        public static HashSet<AuthServer> FromStringList(string str) => new(str.Split(',').Select(FromString));

        public static HashSet<AuthServer> FromCVarList(IConfigurationManager config) => FromStringList(config.GetCVar(CVars.AuthServers));

        public static AuthServer? GetServerFromCVarListById(IConfigurationManager config, string id) => FromCVarList(config).FirstOrDefault(s => s.Id == id);
        public static AuthServer? GetServerFromCVarListByUrl(IConfigurationManager config, Uri url) => FromCVarList(config).FirstOrDefault(s => s.AuthUrl == url);
        public static AuthServer? GetServerFromCVarListByUrl(IConfigurationManager config, string url) => FromCVarList(config).FirstOrDefault(s => s.AuthUrl.ToString() == url);
    }

    internal sealed class AuthManager : IAuthManager
    {
        public static readonly AuthServer FallbackAuthServer = new("Space-Wizards", new("https://auth.spacestation14.com/"));
        public static readonly HashSet<AuthServer> DefaultAuthServers = new()
        {
            FallbackAuthServer,
            new("SimpleStation", new("https://auth.simplestation.org/")),
        };

        public NetUserId? UserId { get; set; }
        public AuthServer UserServer { get; set; } = FallbackAuthServer;
        public HashSet<AuthServer> Servers { get; set; } = DefaultAuthServers;
        public string? Token { get; set; }
        public string? PubKey { get; set; }
        public bool AllowHwid { get; set; } = true;

        public void LoadFromEnv()
        {
            if (TryGetVar("ROBUST_AUTH_SERVERS", out var servers))
                Servers = AuthServer.FromStringList(servers);

            if (TryGetVar("ROBUST_AUTH_USERID", out var userId))
                UserId = new NetUserId(Guid.Parse(userId));

            if (TryGetVar("ROBUST_AUTH_PUBKEY", out var pubKey))
                PubKey = pubKey;

            if (TryGetVar("ROBUST_AUTH_TOKEN", out var token))
                Token = token;

            if (TryGetVar("ROBUST_AUTH_ALLOW_HWID", out var allowHwid))
                AllowHwid = allowHwid.Trim() == "1";

            static bool TryGetVar(string var, [NotNullWhen(true)] out string? val)
            {
                val = Environment.GetEnvironmentVariable(var);
                return val != null;
            }
        }
    }
}
