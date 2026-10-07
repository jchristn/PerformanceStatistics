using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using PerformanceStatistics.Monitoring;

namespace TaskManager.Server
{
    /// <summary>
    /// User login and bearer session tokens (in memory; sessions end when the server restarts).
    /// </summary>
    public sealed class SessionService
    {
        #region Private-Members

        private readonly ServerSettings _Settings;
        private readonly ConcurrentDictionary<string, Session> _Sessions = new ConcurrentDictionary<string, Session>(StringComparer.Ordinal);

        #endregion

        #region Constructors-and-Factories

        public SessionService(ServerSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validate credentials and create a session.
        /// </summary>
        /// <returns>Session, or null if the credentials are wrong.</returns>
        public LoginResponse Login(string username, string password)
        {
            bool admin = Matches(username, _Settings.AdminUser) && Matches(password, _Settings.AdminPassword);
            bool viewer = !admin && !String.IsNullOrEmpty(_Settings.ViewerPassword)
                && Matches(username, _Settings.ViewerUser) && Matches(password, _Settings.ViewerPassword);
            if (!admin && !viewer) return null;

            Prune();
            string token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Session session = new Session { Username = username, IsAdmin = admin, ExpiresUtc = DateTime.UtcNow.AddHours(Math.Max(0.1, _Settings.SessionHours)) };
            _Sessions[token] = session;
            return new LoginResponse { Token = token, ExpiresUtc = session.ExpiresUtc, Username = username, IsAdmin = admin };
        }

        /// <summary>
        /// Resolve a token.
        /// </summary>
        /// <returns>Session, or null if missing or expired.</returns>
        public Session Validate(string token)
        {
            if (String.IsNullOrEmpty(token)) return null;
            Session session;
            if (!_Sessions.TryGetValue(token, out session)) return null;
            if (session.ExpiresUtc <= DateTime.UtcNow)
            {
                _Sessions.TryRemove(token, out session);
                return null;
            }

            return session;
        }

        /// <summary>
        /// End a session.
        /// </summary>
        public void Logout(string token)
        {
            Session ignored;
            if (token != null) _Sessions.TryRemove(token, out ignored);
        }

        #endregion

        #region Private-Methods

        private static bool Matches(string supplied, string expected)
        {
            if (String.IsNullOrEmpty(expected) || supplied == null) return false;
            byte[] a = Encoding.UTF8.GetBytes(supplied);
            byte[] b = Encoding.UTF8.GetBytes(expected);
            return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
        }

        private void Prune()
        {
            DateTime now = DateTime.UtcNow;
            foreach (string token in _Sessions.Where(e => e.Value.ExpiresUtc <= now).Select(e => e.Key).ToList())
            {
                Session ignored;
                _Sessions.TryRemove(token, out ignored);
            }
        }

        #endregion

        #region Public-Classes

        /// <summary>
        /// A logged-in user.
        /// </summary>
        public sealed class Session
        {
            public string Username { get; set; }

            public bool IsAdmin { get; set; }

            public DateTime ExpiresUtc { get; set; }
        }

        #endregion
    }
}
