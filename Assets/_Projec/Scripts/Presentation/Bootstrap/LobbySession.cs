using Steamworks;

namespace Game.Presentation.Bootstrap
{
    /// <summary>Guarda el lobby de Steam activo para poder abandonarlo al volver al menú.</summary>
    public static class LobbySession
    {
        private static CSteamID _current = CSteamID.Nil;

        public static CSteamID Current => _current;
        public static void Set(CSteamID id) => _current = id;
        public static void Clear() => _current = CSteamID.Nil;
        public static bool IsValid() => _current != CSteamID.Nil;
    }
}