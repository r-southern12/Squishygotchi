namespace Squishy.Simulation.Game
{
    /// <summary>The player's profile: a display name, an avatar colour and a friend code. Local only.</summary>
    public sealed partial class GameRules
    {
        public const int NameMax = 16;
        // No 0/O, 1/I/L: easy to read out to a friend.
        private const string CodeChars = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        /// <summary>Gives the player a friend code the first time it is needed (format ABCD-2345).</summary>
        public string EnsureFriendCode()
        {
            if (string.IsNullOrEmpty(S.friendCode))
            {
                var c = new char[9];
                for (int i = 0; i < 9; i++) c[i] = i == 4 ? '-' : CodeChars[_rng.NextInt(CodeChars.Length)];
                S.friendCode = new string(c);
            }
            return S.friendCode;
        }

        /// <summary>Saves the profile; a blank name becomes "Keeper". Returns the stored name.</summary>
        public string SetProfile(string name, string avatar)
        {
            name = (name ?? "").Trim();
            if (name.Length > NameMax) name = name.Substring(0, NameMax);
            S.playerName = name.Length == 0 ? "Keeper" : name;
            if (!string.IsNullOrEmpty(avatar)) S.avatar = avatar;
            S.welcomed = true;
            EnsureFriendCode();
            return S.playerName;
        }
    }
}
